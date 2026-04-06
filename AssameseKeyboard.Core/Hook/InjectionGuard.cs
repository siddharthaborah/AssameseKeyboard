// =============================================================================
// FILE: AssameseKeyboard.Core/Hook/InjectionGuard.cs
//
// INSTRUCTION:
//   Add this file inside the "Hook" folder of AssameseKeyboard.Core.
//   No additional setup needed.
//
// PURPOSE:
//   Every INPUT event we send via SendInput is tagged with OwnInjectionTag
//   in the dwExtraInfo field. The hook callback checks this tag and skips
//   our own injected events, preventing an infinite re-entry loop where:
//     Hook fires → we inject → hook fires again → inject again → ...
// =============================================================================

using System.Runtime.InteropServices;

namespace AssameseKeyboard.Core.Hook;

/// <summary>
/// Provides the mechanism to tag and detect our own injected keystrokes,
/// preventing infinite re-entry in the low-level keyboard hook.
/// </summary>
public static class InjectionGuard
{
    // ── Tag value ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Arbitrary sentinel value written into <c>INPUT.ki.dwExtraInfo</c>
    /// for every keystroke we inject via SendInput.
    ///
    /// Value chosen to be memorable and unlikely to clash with other apps.
    /// 0xA55A_55A5 = alternating bit pattern "As-As" mnemonic for "Assamese".
    /// </summary>
    public const nuint OwnInjectionTag = 0xA55A55A5u;

    // ── Struct mirror ─────────────────────────────────────────────────────────

    /// <summary>
    /// Mirrors the Win32 KBDLLHOOKSTRUCT exactly.
    /// Layout must match: https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-kbdllhookstruct
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        /// <summary>Virtual-key code.</summary>
        public uint vkCode;

        /// <summary>Hardware scan code.</summary>
        public uint scanCode;

        /// <summary>
        /// Flags: bit0=extended, bit4=injected, bit5=injected from lower IL.
        /// </summary>
        public uint flags;

        /// <summary>Timestamp in milliseconds.</summary>
        public uint time;

        /// <summary>
        /// Extra information. We write <see cref="OwnInjectionTag"/> here
        /// for our own injected events.
        /// </summary>
        public nuint dwExtraInfo;
    }

    // ── Detection ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when the hook struct belongs to a keystroke
    /// that was injected by our own <c>InputInjector</c>.
    ///
    /// Checks both the LLKHF_INJECTED flag (bit 4) AND our custom tag,
    /// providing two independent guards against re-entry.
    /// </summary>
    /// <param name="info">
    ///   The KBDLLHOOKSTRUCT received in the hook callback.
    /// </param>
    public static bool IsOwnInjection(KBDLLHOOKSTRUCT info)
    {
        // LLKHF_INJECTED = bit 4 of flags
        const uint LLKHF_INJECTED = 0x10;
        bool isInjected = (info.flags & LLKHF_INJECTED) != 0;
        bool isOurTag = info.dwExtraInfo == OwnInjectionTag;

        // Both conditions must be true:
        // - The OS must have marked it as injected
        // - The extra info must match our tag
        return isInjected && isOurTag;
    }
}