// =============================================================================
// FILE: AssameseKeyboard.Core/Injection/InputInjector.cs
//
// INSTRUCTION:
//   Create folder "Injection" inside AssameseKeyboard.Core.
//   Add this file inside it.
//   No additional NuGet packages needed.
//
// PURPOSE:
//   Injects Unicode text into the currently focused window using the
//   Win32 SendInput API with KEYEVENTF_UNICODE.
//
// JUKTAKKHOR (CONJUNCT) HANDLING:
//   Assamese conjuncts are formed by: Consonant + Hasanta(U+09CD) + Consonant
//   e.g. ক(U+0995) + ্(U+09CD) + ষ(U+09B7) = ক্ষ
//
//   We inject one Unicode codepoint at a time. The target application's
//   text engine (Uniscribe, DirectWrite, HarfBuzz) handles the shaping.
//   We must NOT pre-compose conjuncts ourselves — let the engine do it.
//
// NFC NORMALISATION POLICY:
//   - Multi-character sequences (pre-built conjuncts from the JSON like
//     "\u0995\u09CD\u09B7") are NFC-normalised before injection.
//   - Single combining marks (hasanta, matras, nukta, chandrabindu) are
//     injected raw without normalisation because they need to attach to
//     whatever consonant was typed just before them.
// =============================================================================

using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using static AssameseKeyboard.Core.Hook.InjectionGuard;

namespace AssameseKeyboard.Core.Injection;

/// <summary>
/// Injects Unicode text as keyboard events into the active window.
/// Each character becomes a pair of KEYDOWN + KEYUP SendInput events
/// tagged with <see cref="OwnInjectionTag"/> to prevent hook re-entry.
/// </summary>
public sealed class InputInjector
{
    // ── Win32 structs ─────────────────────────────────────────────────────────

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;          // Must be 0 for Unicode injection
        public ushort wScan;        // Unicode codepoint
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;  // We write OwnInjectionTag here
    }

    // INPUT union: we only use the keyboard variant
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct INPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public KEYBDINPUT ki;  // Offset 8 on 64-bit; 4 on 32-bit
    }

    // Correct 32-bit INPUT layout
    [StructLayout(LayoutKind.Explicit, Size = 28)]
    private struct INPUT32
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(4)] public KEYBDINPUT ki;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint nInputs,
        [MarshalAs(UnmanagedType.LPArray), In] byte[] pInputs,
        int cbSize);

    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly ILogger<InputInjector> _logger;

    // Cache the INPUT struct size — differs between 32-bit and 64-bit processes
    private static readonly bool s_is64bit = IntPtr.Size == 8;
    private static readonly int s_inputSize = IntPtr.Size == 8
        ? Marshal.SizeOf<INPUT>()
        : Marshal.SizeOf<INPUT32>();

    // Assamese Unicode combining mark ranges — these must not be NFC-normalised
    // when injected alone because they need to attach to a preceding base char.
    private static readonly HashSet<int> s_combiningCodepoints = new()
    {
        0x09CD,  // Hasanta / Virama — THE conjunct former
        0x09BC,  // Nukta
        0x0981,  // Chandrabindu
        0x0982,  // Anusvara
        0x0983,  // Visarga
        0x09BE,  // Aa matra (aa vowel sign)
        0x09BF,  // I matra
        0x09C0,  // Ii matra
        0x09C1,  // U matra
        0x09C2,  // Uu matra
        0x09C3,  // Ri matra
        0x09C4,  // Rii matra
        0x09C7,  // E matra
        0x09C8,  // Ai matra
        0x09CB,  // O matra
        0x09CC,  // Au matra
    };

    // ── Constructor ───────────────────────────────────────────────────────────

    public InputInjector(ILogger<InputInjector> logger)
        => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Injects <paramref name="text"/> as Unicode key events into the
    /// currently active window.
    ///
    /// CONJUNCT HANDLING:
    ///   To type a conjunct, inject the sequence one character at a time:
    ///   1. Base consonant  (e.g. Ka U+0995)
    ///   2. Hasanta         (U+09CD) — this call, single char
    ///   3. Second consonant (e.g. Ssa U+09B7)
    ///   The text engine in the target app builds the visual conjunct.
    ///
    ///   For pre-built multi-char strings like "\u0995\u09CD\u09B7" (from
    ///   the JSON shift layer for Ksha), we inject all three characters
    ///   in sequence — same result.
    /// </summary>
    /// <param name="text">
    ///   One or more Unicode characters to inject.
    ///   Must not be null or empty.
    /// </param>
    /// <exception cref="ArgumentNullException"/>
    public void SendUnicodeString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0) return;

        // Decide whether to NFC-normalise
        // Single combining marks: inject raw (they will attach to whatever
        // consonant was just typed, which may be in a different injection call)
        // Multi-char sequences: NFC-normalise for canonical composition
        string toInject = ShouldSkipNormalisation(text)
            ? text
            : text.Normalize(NormalizationForm.FormC);

        // Build the raw byte array for SendInput
        byte[] inputBytes = BuildInputBytes(toInject);

        if (inputBytes.Length == 0) return;

        uint eventCount = (uint)(inputBytes.Length / s_inputSize);
        uint sent = SendInput(eventCount, inputBytes, s_inputSize);

        if (sent != eventCount)
        {
            int err = Marshal.GetLastWin32Error();
            _logger.LogWarning(
                "[InputInjector] SendInput delivered {Sent}/{Total} events " +
                "for text U+{Codepoints}. Win32 error: {Err}.",
                sent, eventCount,
                string.Join(" U+", toInject.Select(c => $"{(int)c:X4}")),
                err);
        }
        else
        {
            _logger.LogDebug(
                "[InputInjector] Injected {Count} char(s): U+{Codepoints}",
                toInject.Length,
                string.Join(" U+", toInject.Select(c => $"{(int)c:X4}")));
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the text should be injected without NFC normalisation.
    /// This applies to single combining marks that must attach to a base
    /// character that was injected in a previous call.
    /// </summary>
    private static bool ShouldSkipNormalisation(string text)
    {
        if (text.Length != 1) return false;
        int cp = text[0];
        // Explicit combining set
        if (s_combiningCodepoints.Contains(cp)) return true;
        // Unicode category fallback
        var cat = CharUnicodeInfo.GetUnicodeCategory(text[0]);
        return cat is UnicodeCategory.NonSpacingMark
                   or UnicodeCategory.SpacingCombiningMark
                   or UnicodeCategory.EnclosingMark;
    }

    /// <summary>
    /// Converts a Unicode string into a flat byte array ready for SendInput.
    /// Each character produces two INPUT structs (key-down + key-up).
    /// Surrogate pairs (codepoints > U+FFFF) are handled correctly.
    /// </summary>
    private static byte[] BuildInputBytes(string text)
    {
        // Enumerate actual Unicode scalar values (handles surrogates)
        var codepoints = new List<int>();
        for (int i = 0; i < text.Length;)
        {
            int cp = char.ConvertToUtf32(text, i);
            codepoints.Add(cp);
            i += cp > 0xFFFF ? 2 : 1;
        }

        // Each codepoint = 2 events (down + up)
        // Each event = s_inputSize bytes
        int totalEvents = codepoints.Count * 2;
        byte[] buffer = new byte[totalEvents * s_inputSize];
        int offset = 0;

        foreach (int cp in codepoints)
        {
            if (cp <= 0xFFFF)
            {
                // BMP character — single INPUT pair
                WriteInput(buffer, ref offset, (ushort)cp, keyUp: false);
                WriteInput(buffer, ref offset, (ushort)cp, keyUp: true);
            }
            else
            {
                // Supplementary character — use surrogate pair
                // (very rare in Assamese but handled for correctness)
                char high = (char)((cp - 0x10000) / 0x400 + 0xD800);
                char low = (char)((cp - 0x10000) % 0x400 + 0xDC00);
                WriteInput(buffer, ref offset, high, keyUp: false);
                WriteInput(buffer, ref offset, high, keyUp: true);
                WriteInput(buffer, ref offset, low, keyUp: false);
                WriteInput(buffer, ref offset, low, keyUp: true);
            }
        }

        return buffer[..offset];  // trim to actual used size
    }

    /// <summary>
    /// Writes one INPUT struct (keyboard, Unicode) into <paramref name="buffer"/>
    /// at <paramref name="offset"/>, then advances offset by s_inputSize.
    /// </summary>
    private static void WriteInput(
        byte[] buffer, ref int offset, ushort wScan, bool keyUp)
    {
        uint flags = KEYEVENTF_UNICODE;
        if (keyUp) flags |= KEYEVENTF_KEYUP;

        if (s_is64bit)
        {
            var inp = new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = wScan,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = OwnInjectionTag
                }
            };
            // Marshal to bytes
            var bytes = new byte[s_inputSize];
            var handle = GCHandle.Alloc(inp, GCHandleType.Pinned);
            try
            {
                Marshal.Copy(handle.AddrOfPinnedObject(), bytes, 0, s_inputSize);
            }
            finally { handle.Free(); }
            Buffer.BlockCopy(bytes, 0, buffer, offset, s_inputSize);
        }
        else
        {
            var inp = new INPUT32
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = wScan,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = OwnInjectionTag
                }
            };
            var bytes = new byte[s_inputSize];
            var handle = GCHandle.Alloc(inp, GCHandleType.Pinned);
            try
            {
                Marshal.Copy(handle.AddrOfPinnedObject(), bytes, 0, s_inputSize);
            }
            finally { handle.Free(); }
            Buffer.BlockCopy(bytes, 0, buffer, offset, s_inputSize);
        }

        offset += s_inputSize;
    }
}
