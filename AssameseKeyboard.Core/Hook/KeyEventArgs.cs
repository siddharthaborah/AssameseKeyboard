// =============================================================================
// FILE: AssameseKeyboard.Core/Hook/KeyEventArgs.cs
//
// INSTRUCTION:
//   Create folder "Hook" inside AssameseKeyboard.Core project.
//   Add this file inside it.
//   No additional setup needed — pure C# class, no NuGet dependencies.
// =============================================================================

namespace AssameseKeyboard.Core.Hook;

/// <summary>
/// Carries information about a single low-level keyboard event received
/// from the WH_KEYBOARD_LL hook.
///
/// Set <see cref="Handled"/> = <c>true</c> inside a handler to suppress
/// the original keystroke so the target application never sees it.
/// </summary>
public sealed class KeyEventArgs : EventArgs
{
    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>
    /// Windows virtual-key code (e.g. 0x41 = VK 'A', 0x10 = VK_SHIFT).
    /// Full list: https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes
    /// </summary>
    public uint VirtualKey { get; }

    /// <summary>
    /// Hardware scan code as reported by the keyboard driver.
    /// Used for disambiguation of extended keys.
    /// </summary>
    public uint ScanCode { get; }

    /// <summary>
    /// Raw flags from KBDLLHOOKSTRUCT.flags.
    /// Bit 0 = extended key, Bit 4 = injected, Bit 5 = injected from lower IL.
    /// </summary>
    public uint Flags { get; }

    /// <summary>
    /// Set to <c>true</c> by a handler to swallow the original keystroke.
    /// When true the hook callback returns 1, preventing the key from
    /// reaching the active window.
    /// </summary>
    public bool Handled { get; set; }

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <param name="virtualKey">Virtual-key code from KBDLLHOOKSTRUCT.vkCode.</param>
    /// <param name="scanCode">Hardware scan code from KBDLLHOOKSTRUCT.scanCode.</param>
    /// <param name="flags">Flags field from KBDLLHOOKSTRUCT.flags.</param>
    public KeyEventArgs(uint virtualKey, uint scanCode, uint flags = 0)
    {
        VirtualKey = virtualKey;
        ScanCode = scanCode;
        Flags = flags;
    }

    // ── Convenience helpers ───────────────────────────────────────────────────

    /// <summary>True when the LLKHF_EXTENDED flag (bit 0) is set.</summary>
    public bool IsExtendedKey => (Flags & 0x01) != 0;

    /// <summary>True when the LLKHF_INJECTED flag (bit 4) is set.</summary>
    public bool IsInjected => (Flags & 0x10) != 0;

    /// <inheritdoc/>
    public override string ToString()
        => $"VK=0x{VirtualKey:X2} SC=0x{ScanCode:X2} " +
           $"Flags=0x{Flags:X2} Handled={Handled}";
}