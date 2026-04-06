// =============================================================================
// FILE: AssameseKeyboard.Core/Hook/KeyboardHook.cs
//
// INSTRUCTION:
//   Add this file inside the "Hook" folder of AssameseKeyboard.Core.
//   No additional NuGet packages needed — uses P/Invoke directly.
//
// CRITICAL REQUIREMENTS:
//   1. Install() MUST be called on the same STA thread that pumps a
//      Win32 message loop. The WinUI 3 UI thread qualifies automatically.
//   2. The app must declare <rescap:Capability Name="runFullTrust"/>
//      in Package.appxmanifest, otherwise the hook silently fails on
//      elevated target processes (UAC-elevated apps, admin terminals).
//   3. Keep the KeyboardHook instance alive for the lifetime of the app.
//      Disposing it uninstalls the hook.
// =============================================================================

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace AssameseKeyboard.Core.Hook;

/// <summary>
/// Installs a system-wide low-level keyboard hook using the Win32
/// <c>WH_KEYBOARD_LL</c> mechanism via <c>SetWindowsHookEx</c>.
///
/// For every WM_KEYDOWN / WM_SYSKEYDOWN event that is NOT our own
/// injection, raises <see cref="KeyIntercepted"/>.
///
/// Set <see cref="KeyEventArgs.Handled"/> = <c>true</c> in the handler
/// to suppress the original keystroke from reaching the target window.
///
/// Lifecycle:  new → Install() → [events] → Dispose()
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    // ── Win32 API signatures ──────────────────────────────────────────────────

    private delegate IntPtr LowLevelKeyboardProc(
        int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    // ── Win32 constants ───────────────────────────────────────────────────────

    private const int WH_KEYBOARD_LL = 13;
    private const int HC_ACTION = 0;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    // ── Fields ────────────────────────────────────────────────────────────────

    private IntPtr _hookHandle = IntPtr.Zero;

    // CRITICAL: Store the delegate in a field.
    // If stored only as a local variable the GC may collect it
    // while the native hook still holds a function pointer to it,
    // causing an AccessViolationException (crash with no useful error).
    private readonly LowLevelKeyboardProc _procDelegate;

    private readonly ILogger<KeyboardHook> _logger;
    private bool _disposed;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// When <c>false</c>, all keystrokes pass through unmodified.
    /// Toggle this to pause / resume Assamese input without uninstalling
    /// the hook (which would require a new Install() call).
    /// Thread-safe: the hook callback reads this on the hook thread.
    /// </summary>
    public volatile bool IsEnabled = true;

    /// <summary>
    /// Raised for each WM_KEYDOWN / WM_SYSKEYDOWN event that originates
    /// from the physical keyboard (not our own injection).
    ///
    /// THREADING: Raised on the thread that called Install().
    /// Since that is the WinUI UI thread, handlers may safely touch UI.
    /// However keep handlers fast — the hook callback has a strict
    /// Win32 time budget (typically ~200 ms before the OS kills the hook).
    /// </summary>
    public event EventHandler<KeyEventArgs>? KeyIntercepted;

    /// <summary>True after Install() and before Dispose().</summary>
    public bool IsInstalled => _hookHandle != IntPtr.Zero;

    // ── Constructor ───────────────────────────────────────────────────────────

    public KeyboardHook(ILogger<KeyboardHook> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _procDelegate = HookCallback;   // pin the delegate — must outlive the hook
    }

    // ── Install / Uninstall ───────────────────────────────────────────────────

    /// <summary>
    /// Installs the low-level keyboard hook.
    ///
    /// MUST be called on an STA thread with a message loop (WinUI UI thread).
    /// Safe to call only once; subsequent calls are no-ops.
    /// </summary>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when SetWindowsHookEx fails (check Win32 error for details).
    ///   Common causes: insufficient privileges, no message loop on calling thread.
    /// </exception>
    public void Install()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hookHandle != IntPtr.Zero)
        {
            _logger.LogWarning(
                "[KeyboardHook] Already installed (handle=0x{Handle:X}). " +
                "Ignoring duplicate Install() call.", _hookHandle);
            return;
        }

        // Obtain the module handle of our own process — required by
        // SetWindowsHookEx for global (cross-process) hooks.
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule
            ?? throw new InvalidOperationException(
                "[KeyboardHook] Cannot obtain MainModule. " +
                "Ensure the app is not running in a stripped/AOT context.");

        var hMod = GetModuleHandle(module.ModuleName);
        if (hMod == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"[KeyboardHook] GetModuleHandle failed with Win32 error {err}.");
        }

        _hookHandle = SetWindowsHookEx(
            WH_KEYBOARD_LL,
            _procDelegate,
            hMod,
            dwThreadId: 0);   // 0 = global hook across all threads

        if (_hookHandle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"[KeyboardHook] SetWindowsHookEx failed with Win32 error {err}. " +
                "Ensure Package.appxmanifest declares runFullTrust capability " +
                "and the app runs with sufficient privileges.");
        }

        _logger.LogInformation(
            "[KeyboardHook] Installed successfully (handle=0x{Handle:X}).",
            _hookHandle);
    }

    /// <summary>
    /// Removes the hook without disposing the object.
    /// After Uninstall() you may call Install() again to reinstall.
    /// </summary>
    public void Uninstall()
    {
        if (_hookHandle == IntPtr.Zero) return;

        if (!UnhookWindowsHookEx(_hookHandle))
        {
            int err = Marshal.GetLastWin32Error();
            _logger.LogWarning(
                "[KeyboardHook] UnhookWindowsHookEx returned false " +
                "(Win32 error {Err}). Hook may already have been removed.", err);
        }
        else
        {
            _logger.LogInformation("[KeyboardHook] Uninstalled successfully.");
        }

        _hookHandle = IntPtr.Zero;
    }

    // ── Hook callback ─────────────────────────────────────────────────────────

    /// <summary>
    /// Called by Windows for every low-level keyboard event system-wide.
    /// Must return quickly — Windows kills slow hooks.
    /// Must never throw — exceptions here crash the process.
    /// </summary>
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // nCode < 0: must pass on without processing (Win32 contract)
        if (nCode != HC_ACTION)
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        // Only process if the engine is enabled
        if (!IsEnabled)
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        // We only care about key-down events
        int msg = wParam.ToInt32();
        bool isKeyDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
        if (!isKeyDown)
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        // Unmarshal the KBDLLHOOKSTRUCT from the lParam pointer
        var info = Marshal.PtrToStructure<InjectionGuard.KBDLLHOOKSTRUCT>(lParam);

        // Skip keystrokes injected by our own InputInjector — prevents
        // the infinite re-entry loop described in InjectionGuard.cs
        if (InjectionGuard.IsOwnInjection(info))
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        // Build the event args and fire the event
        var args = new KeyEventArgs(info.vkCode, info.scanCode, info.flags);

        try
        {
            KeyIntercepted?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            // Never let subscriber exceptions escape a Win32 callback.
            // Log and continue — swallowing is intentional here.
            _logger.LogError(ex,
                "[KeyboardHook] Unhandled exception in KeyIntercepted " +
                "subscriber for VK=0x{VK:X2}. The keystroke will pass through.",
                info.vkCode);
        }

        // Handled = true → return 1 to suppress the original keystroke
        // Handled = false → pass to next hook / target window
        return args.Handled
            ? new IntPtr(1)
            : CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        Uninstall();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer safety net — if the caller forgets Dispose()
    /// we still attempt to uninstall to avoid a dangling hook.
    /// </summary>
    ~KeyboardHook()
    {
        if (!_disposed)
        {
            // Cannot log here (logger may be gone), but we must unhook
            if (_hookHandle != IntPtr.Zero)
                UnhookWindowsHookEx(_hookHandle);
        }
    }
}
