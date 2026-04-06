// =============================================================================
// FILE: AssameseKeyboard.Core/Mapping/ShiftStateTracker.cs
// INSTRUCTION: Add to the "Mapping" folder in AssameseKeyboard.Core.
//
// PURPOSE:
//   Queries the live Win32 keyboard state to determine:
//   - Is either Shift key physically held?
//   - Is Caps Lock toggled on?
//   - Logical shift state = Shift XOR CapsLock (standard typewriter rule)
//   - Is AltGr (Right Alt) held? — used for the AltGr layer
//   - Is Ctrl held? — Ctrl combos must always pass through
//   - Is Win key held? — Win combos must always pass through
//
// WHY NOT TRACK VIA HOOK EVENTS?
//   Reading GetKeyState() at injection time is more reliable than tracking
//   key-down/up transitions in the hook, because:
//   1. The hook can miss paired up-events (app losing focus mid-key)
//   2. GetKeyState() returns the instantaneous physical state
//   3. No state synchronisation needed across threads
// =============================================================================

using System.Runtime.InteropServices;

namespace AssameseKeyboard.Core.Mapping;

/// <summary>
/// Provides real-time query of keyboard modifier state via Win32 GetKeyState.
/// All properties read live Win32 state — no caching, always current.
/// </summary>
public sealed class ShiftStateTracker
{
    // ── Virtual-key codes ─────────────────────────────────────────────────────

    // Modifier keys
    private const int VK_SHIFT = 0x10;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_CONTROL = 0x11;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_MENU = 0x12;   // Alt
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;   // Right Alt = AltGr on many layouts
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_CAPITAL = 0x14;   // Caps Lock toggle

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the state of the virtual key at the time the thread last
    /// processed a message. High-order bit = key down, low-order bit = toggled.
    /// </summary>
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    // ── Helpers ───────────────────────────────────────────────────────────────

    // High-order bit set = key is physically pressed (held down)
    private static bool IsDown(int vk) => (GetKeyState(vk) & 0x8000) != 0;

    // Low-order bit set = toggle is on (Caps Lock, Scroll Lock, Num Lock)
    private static bool IsToggled(int vk) => (GetKeyState(vk) & 0x0001) != 0;

    // ── Public state properties ───────────────────────────────────────────────

    /// <summary>True when either physical Shift key is held.</summary>
    public bool IsShiftDown => IsDown(VK_LSHIFT) || IsDown(VK_RSHIFT);

    /// <summary>True when Caps Lock is toggled ON.</summary>
    public bool IsCapsLockOn => IsToggled(VK_CAPITAL);

    /// <summary>
    /// The logical shift state used to choose between the base and shifted
    /// layers of the keyboard layout.
    ///
    /// Follows the standard typewriter rule:
    ///   Shift held + Caps OFF = shifted layer
    ///   Shift held + Caps ON  = base layer (Shift cancels Caps)
    ///   Shift NOT held + Caps ON  = shifted layer
    ///   Shift NOT held + Caps OFF = base layer
    ///
    /// In short: IsShiftActive = IsShiftDown XOR IsCapsLockOn
    /// </summary>
    public bool IsShiftActive => IsShiftDown ^ IsCapsLockOn;

    /// <summary>
    /// True when Right-Alt is physically held.
    ///
    /// On Windows, pressing Right-Alt also sets Left-Ctrl internally
    /// for legacy reasons. We test Right-Alt directly (VK_RMENU) to
    /// avoid false positives from Ctrl+Alt combos.
    /// </summary>
    public bool IsAltGrDown => IsDown(VK_RMENU);

    /// <summary>
    /// True when either Ctrl key is held.
    ///
    /// All Ctrl+key combos (Ctrl+C, Ctrl+V, Ctrl+Z, etc.) must be
    /// passed through unmodified — we never intercept Ctrl combos.
    /// </summary>
    public bool IsCtrlDown => IsDown(VK_LCONTROL) || IsDown(VK_RCONTROL);

    /// <summary>
    /// True when either Windows key is held.
    /// Win+key combos (Win+D, Win+L, etc.) pass through unmodified.
    /// </summary>
    public bool IsWinDown => IsDown(VK_LWIN) || IsDown(VK_RWIN);

    /// <summary>
    /// True when Left-Alt is held (without Right-Alt).
    /// Alt+key combos in most apps are menu accelerators and must pass through.
    /// </summary>
    public bool IsAltDown => IsDown(VK_LMENU) && !IsDown(VK_RMENU);

    /// <summary>
    /// True when ANY modifier that requires pass-through is active.
    /// Use this in the engine to quickly skip interception.
    ///
    /// We intercept ONLY when: no Ctrl, no Win, no Left-Alt.
    /// We DO intercept with Shift (normal) and with AltGr (third layer).
    /// </summary>
    public bool ShouldPassThrough => IsCtrlDown || IsWinDown || IsAltDown;
}
