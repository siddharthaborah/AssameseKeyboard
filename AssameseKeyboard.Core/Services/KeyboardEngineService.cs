// =============================================================================
// FILE: AssameseKeyboard.Core/Services/KeyboardEngineService.cs
// INSTRUCTION: Create folder "Services" inside AssameseKeyboard.Core.
//              Add this file inside it.
//
// PURPOSE:
//   Top-level orchestrator. Wires:
//     KeyboardHook → ShiftStateTracker → KeyMapper → JuktakkhorEngine
//                                                  → InputInjector
//
// FLOW for each keystroke:
//   1. Hook fires KeyIntercepted with the virtual-key code
//   2. ShiftStateTracker checks if we should pass through (Ctrl/Win/Alt)
//   3. KeyMapper resolves vk + modifier state → Assamese string (or null)
//   4. JuktakkhorEngine processes the string (may buffer hasanta)
//   5. InputInjector sends each output string via SendInput
//   6. KeyEventArgs.Handled = true suppresses the original keystroke
//
// SPECIAL KEYS handled:
//   - Backspace: flush any pending hasanta, then pass through
//   - Space/Enter: flush any pending hasanta, then pass through
//   - All Ctrl/Win/Alt combos: always pass through
// =============================================================================

using AssameseKeyboard.Core.Hook;
using AssameseKeyboard.Core.Injection;
using AssameseKeyboard.Core.Mapping;
using Microsoft.Extensions.Logging;

namespace AssameseKeyboard.Core.Services;

/// <summary>
/// The top-level keyboard engine. Manages the full lifecycle of
/// Assamese input interception, conjunct formation, and Unicode injection.
///
/// Lifecycle: new → Start() → [events] → Stop() → Dispose()
/// </summary>
public sealed class KeyboardEngineService : IDisposable
{
    // ── Win32 virtual-key codes for special handling ───────────────────────────
    private const uint VK_BACK = 0x08;   // Backspace
    private const uint VK_RETURN = 0x0D;   // Enter
    private const uint VK_SPACE = 0x20;   // Space
    private const uint VK_ESCAPE = 0x1B;   // Escape
    private const uint VK_DELETE = 0x2E;   // Delete
    private const uint VK_TAB = 0x09;   // Tab

    // ── Dependencies ─────────────────────────────────────────────────────────

    private readonly KeyboardHook _hook;
    private readonly InputInjector _injector;
    private readonly KeyMapper _mapper;
    private readonly ShiftStateTracker _shift;
    private readonly JuktakkhorEngine _juktakkhor;
    private readonly ILogger<KeyboardEngineService> _logger;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool _disposed;

    /// <summary>True after Start(), false after Stop() or before Start().</summary>
    public bool IsRunning { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised on the UI thread each time an Assamese character string
    /// is successfully injected. Useful for a status overlay or log.
    /// The string argument is the injected Assamese text (Unicode).
    /// </summary>
    public event EventHandler<string>? CharacterInjected;

    /// <summary>
    /// Raised when the engine state changes (started/stopped).
    /// </summary>
    public event EventHandler<bool>? RunningStateChanged;

    // ── Constructor ───────────────────────────────────────────────────────────

    public KeyboardEngineService(
        KeyboardHook hook,
        InputInjector injector,
        KeyMapper mapper,
        ShiftStateTracker shift,
        JuktakkhorEngine juktakkhor,
        ILogger<KeyboardEngineService> logger)
    {
        _hook = hook ?? throw new ArgumentNullException(nameof(hook));
        _injector = injector ?? throw new ArgumentNullException(nameof(injector));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _shift = shift ?? throw new ArgumentNullException(nameof(shift));
        _juktakkhor = juktakkhor ?? throw new ArgumentNullException(nameof(juktakkhor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _hook.KeyIntercepted += OnKeyIntercepted;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Installs the hook and begins intercepting keystrokes.
    /// Safe to call multiple times — subsequent calls are no-ops.
    /// </summary>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="InvalidOperationException">
    ///   If the hook fails to install (see KeyboardHook.Install for causes).
    /// </exception>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning) return;

        _hook.Install();
        _hook.IsEnabled = true;
        IsRunning = true;

        _logger.LogInformation("[KeyboardEngine] Started.");
        RunningStateChanged?.Invoke(this, true);
    }

    /// <summary>
    /// Pauses interception without uninstalling the hook.
    /// Pending hasanta is flushed before pausing.
    /// Call Start() to resume.
    /// </summary>
    public void Stop()
    {
        if (!IsRunning) return;

        // Flush any buffered hasanta before going silent
        FlushHasanta();

        _hook.IsEnabled = false;
        IsRunning = false;

        _logger.LogInformation("[KeyboardEngine] Stopped.");
        RunningStateChanged?.Invoke(this, false);
    }

    /// <summary>
    /// Permanently disposes the engine. Do not call Start() after this.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _hook.KeyIntercepted -= OnKeyIntercepted;
        FlushHasanta();
        _hook.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    // ── Core handler ─────────────────────────────────────────────────────────

    private void OnKeyIntercepted(object? sender, KeyEventArgs e)
    {
        // ── Guard: skip modifier-combo keys ──────────────────────────────────
        // Ctrl+anything, Win+anything, Left-Alt+anything must pass through.
        // (AltGr = Right-Alt is allowed — it activates the third layer.)
        if (_shift.ShouldPassThrough)
            return;

        // ── Guard: special keys that need hasanta flush ───────────────────────
        if (IsFlushTrigger(e.VirtualKey))
        {
            FlushHasanta();
            return;   // pass through (do not suppress)
        }

        // ── Resolve the key via the layout ───────────────────────────────────
        string? assamese = _mapper.Resolve(
            e.VirtualKey,
            _shift.IsShiftActive,
            _shift.IsAltGrDown);

        if (assamese is null)
        {
            // Key not mapped in this layout.
            // Flush any pending hasanta (user typed a non-Assamese key).
            FlushHasanta();
            return;
        }

        // ── Run through the juktakkhor engine ────────────────────────────────
        var result = _juktakkhor.ProcessOutput(assamese);

        if (!result.ConsumedInput)
        {
            // Engine chose not to consume — pass through
            return;
        }

        // Suppress the original keystroke
        e.Handled = true;

        // Inject all output strings in order
        string allInjected = string.Concat(result.ToInjectNow);

        foreach (var segment in result.ToInjectNow)
        {
            if (!string.IsNullOrEmpty(segment))
                _injector.SendUnicodeString(segment);
        }

        if (result.ToInjectNow.Count > 0)
        {
            _logger.LogDebug(
                "[KeyboardEngine] VK=0x{VK:X2} shift={Shift} → injected U+{CP}",
                e.VirtualKey,
                _shift.IsShiftActive,
                string.Join(" U+",
                    allInjected.Select(c => $"{(int)c:X4}")));

            CharacterInjected?.Invoke(this, allInjected);
        }
        else
        {
            // Buffered (hasanta pending) — nothing injected yet, that is fine
            _logger.LogDebug(
                "[KeyboardEngine] VK=0x{VK:X2} → hasanta buffered (pending conjunct).",
                e.VirtualKey);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Injects any pending hasanta immediately.
    /// Called when a key is pressed that cannot form a conjunct
    /// (e.g. a vowel, a space, Enter, Backspace, or switching apps).
    /// </summary>
    private void FlushHasanta()
    {
        var pending = _juktakkhor.FlushPending();
        if (pending is not null)
        {
            _injector.SendUnicodeString(pending);
            _logger.LogDebug(
                "[KeyboardEngine] Flushed pending hasanta (U+09CD).");
        }
    }

    /// <summary>
    /// Returns true for keys that should cause a pending hasanta to be
    /// flushed without the key itself being suppressed.
    /// </summary>
    private static bool IsFlushTrigger(uint vk)
        => vk is VK_BACK or VK_RETURN or VK_SPACE
              or VK_ESCAPE or VK_DELETE or VK_TAB;
}