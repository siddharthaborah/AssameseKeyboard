// =============================================================================
// FILE: AssameseKeyboard.Core/Mapping/JuktakkhorEngine.cs
// INSTRUCTION: Add to the "Mapping" folder in AssameseKeyboard.Core.
//
// PURPOSE — JUKTAKKHOR (যুক্তাক্ষৰ) CONJUNCT ENGINE
// ====================================================
// Assamese conjuncts are formed by joining consonants with Hasanta (্ U+09CD).
//
// TYPING SEQUENCE for ক্ষ (ksha):
//   1. User presses K  → injects ক (U+0995)
//   2. User presses D  → injects ্ (U+09CD, hasanta)    ← engine intercepts
//   3. User presses &  → injects ষ (U+09B7)
//   Result in the target app: ক + ্ + ষ = ক্ষ  (shaped by the text engine)
//
// This engine tracks the "pending hasanta" state:
//   - After a consonant is injected, the engine watches for hasanta (D key)
//   - While hasanta is "pending", it is NOT yet injected
//   - If the next key is a consonant: inject hasanta + consonant (conjunct formed)
//   - If the next key is NOT a consonant: inject the pending hasanta alone,
//     then process the new key normally (conjunct cancelled)
//   - If the user pauses (timer expires): inject the pending hasanta alone
//
// This "lazy hasanta" approach gives correct visual feedback:
//   The hasanta appears immediately in most OpenType text engines anyway
//   because they shape on-the-fly, but this engine ensures correct semantics.
//
// UNICODE RANGES for Assamese consonants (U+0995–U+09B9 + Assamese-specific):
//   U+0995–U+09A8  Ka–Na (main series)
//   U+09AA–U+09B0  Pa–Ra
//   U+09B2         La
//   U+09B6–U+09B9  Sha–Ha
//   U+09F0         Assamese Ra (ৰ)
//   U+09F1         Assamese Wa (ৱ)
//   U+09CE         Khanda Ta (ৎ) — does NOT take hasanta (it IS the final form)
// =============================================================================

namespace AssameseKeyboard.Core.Mapping;

/// <summary>
/// Manages the pending-hasanta state for Assamese conjunct formation.
///
/// Call <see cref="ProcessOutput"/> with every string about to be injected.
/// It returns the actual string(s) to inject, which may differ from the input
/// if hasanta buffering or flushing is needed.
/// </summary>
public sealed class JuktakkhorEngine
{
    // ── Unicode constants ─────────────────────────────────────────────────────

    /// <summary>Hasanta / Virama U+09CD — the conjunct former.</summary>
    public const char Hasanta = '\u09CD';

    // All Assamese/Bengali consonants that can participate in conjuncts
    private static readonly HashSet<char> s_consonants = new()
    {
        '\u0995', // ক Ka
        '\u0996', // খ Kha
        '\u0997', // গ Ga
        '\u0998', // ঘ Gha
        '\u0999', // ঙ Nga
        '\u099A', // চ Ca
        '\u099B', // ছ Cha
        '\u099C', // জ Ja
        '\u099D', // ঝ Jha
        '\u099E', // ঞ Nya
        '\u099F', // ট Tta
        '\u09A0', // ঠ Ttha
        '\u09A1', // ড Dda
        '\u09A2', // ঢ Ddha
        '\u09A3', // ণ Nna
        '\u09A4', // ত Ta
        '\u09A5', // থ Tha
        '\u09A6', // দ Da
        '\u09A7', // ধ Dha
        '\u09A8', // ন Na
        '\u09AA', // প Pa
        '\u09AB', // ফ Pha
        '\u09AC', // ব Ba
        '\u09AD', // ভ Bha
        '\u09AE', // ম Ma
        '\u09AF', // য Ya
        '\u09B0', // র Ra (Bengali)
        '\u09B2', // ল La
        '\u09B6', // শ Sha
        '\u09B7', // ষ Ssa
        '\u09B8', // স Sa
        '\u09B9', // হ Ha
        '\u09F0', // ৰ Assamese Ra (unique)
        '\u09F1', // ৱ Assamese Wa (unique)
        // Note: U+09CE (ৎ Khanda Ta) intentionally excluded —
        // it is the final/isolated form and does not take hasanta
    };

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// True when we have seen a consonant followed by hasanta and are
    /// waiting to see if the next character is also a consonant.
    /// </summary>
    public bool HasPendingHasanta { get; private set; }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Processes the next string to be injected, applying juktakkhor logic.
    ///
    /// Returns an <see cref="EngineResult"/> containing:
    /// - <see cref="EngineResult.ToInjectNow"/>: inject this immediately
    /// - <see cref="EngineResult.ConsumedInput"/>: true if input was handled
    ///
    /// CALL PATTERN in KeyboardEngineService:
    /// <code>
    ///   var result = _juktakkhor.ProcessOutput(assamese);
    ///   foreach (var s in result.ToInjectNow)
    ///       _injector.SendUnicodeString(s);
    /// </code>
    /// </summary>
    /// <param name="text">
    ///   The Assamese string the KeyMapper resolved for the current keystroke.
    /// </param>
    public EngineResult ProcessOutput(string text)
    {
        if (string.IsNullOrEmpty(text))
            return EngineResult.Empty;

        bool inputIsHasanta = text == "\u09CD";
        bool inputIsConsonant = text.Length == 1 && IsConsonant(text[0]);
        bool inputIsMultiChar = text.Length > 1;   // pre-built conjunct from JSON

        // ── Case 1: Input is hasanta ──────────────────────────────────────────
        if (inputIsHasanta)
        {
            if (HasPendingHasanta)
            {
                // Double hasanta: flush the first one, buffer the second
                // (rare but possible: ক্ + ্ = ক্্ which is visually odd but valid)
                HasPendingHasanta = true;
                return new EngineResult(new[] { "\u09CD" }, consumed: true);
            }

            // Buffer the hasanta — wait to see the next character
            HasPendingHasanta = true;
            return EngineResult.Buffered;
        }

        // ── Case 2: Pending hasanta + consonant → conjunct ────────────────────
        if (HasPendingHasanta && inputIsConsonant)
        {
            HasPendingHasanta = false;
            // Inject hasanta + consonant together — the text engine shapes them
            return new EngineResult(
                new[] { "\u09CD", text },
                consumed: true);
        }

        // ── Case 3: Pending hasanta + non-consonant → cancel conjunct ─────────
        if (HasPendingHasanta)
        {
            HasPendingHasanta = false;
            // Flush the buffered hasanta, then inject the new character
            return new EngineResult(
                new[] { "\u09CD", text },
                consumed: true);
        }

        // ── Case 4: Multi-char string (pre-built conjunct from JSON) ──────────
        // e.g. Shift+7 → "\u0995\u09CD\u09B7" (ক্ষ ksha)
        // Inject each character individually so the text engine shapes correctly
        if (inputIsMultiChar)
        {
            var parts = text.Select(c => c.ToString()).ToArray();
            return new EngineResult(parts, consumed: true);
        }

        // ── Case 5: Normal character — no pending hasanta ─────────────────────
        return new EngineResult(new[] { text }, consumed: true);
    }

    /// <summary>
    /// Flushes any pending hasanta immediately.
    /// Call this when the user switches app focus, presses Enter, Space,
    /// Backspace, or any non-Assamese key — situations where a dangling
    /// hasanta should be committed to the text stream as-is.
    /// </summary>
    /// <returns>
    ///   The hasanta string to inject, or null if nothing was pending.
    /// </returns>
    public string? FlushPending()
    {
        if (!HasPendingHasanta) return null;
        HasPendingHasanta = false;
        return "\u09CD";
    }

    /// <summary>Cancels any pending hasanta without injecting it.</summary>
    public void CancelPending() => HasPendingHasanta = false;

    /// <summary>Returns true if the character is an Assamese consonant.</summary>
    public static bool IsConsonant(char c) => s_consonants.Contains(c);

    /// <summary>Returns true if the string is a single Assamese consonant.</summary>
    public static bool IsSingleConsonant(string s)
        => s.Length == 1 && IsConsonant(s[0]);

    // ── Result type ───────────────────────────────────────────────────────────

    /// <summary>
    /// Result returned by <see cref="ProcessOutput"/>.
    /// Contains the strings to inject and a flag indicating disposition.
    /// </summary>
    public sealed class EngineResult
    {
        /// <summary>Singleton empty result (nothing to inject).</summary>
        public static readonly EngineResult Empty =
            new(Array.Empty<string>(), consumed: false);

        /// <summary>Singleton buffered result (hasanta held, nothing injected yet).</summary>
        public static readonly EngineResult Buffered =
            new(Array.Empty<string>(), consumed: true);

        /// <summary>
        /// Ordered list of strings to inject, one at a time.
        /// May be empty if the input was buffered.
        /// </summary>
        public IReadOnlyList<string> ToInjectNow { get; }

        /// <summary>
        /// True if the engine handled (consumed) the input.
        /// The caller should suppress the original keystroke.
        /// False only for <see cref="Empty"/> — input was not handled.
        /// </summary>
        public bool ConsumedInput { get; }

        public EngineResult(string[] toInject, bool consumed)
        {
            ToInjectNow = toInject;
            ConsumedInput = consumed;
        }
    }
}