// =============================================================================
// FILE: AssameseKeyboard.App/ViewModels/KeyboardPreviewViewModel.cs
// INSTRUCTION: Add to the "ViewModels" folder in AssameseKeyboard.App.
//   All Assamese characters stored as Unicode escape sequences (\uXXXX)
//   so the source file is encoding-safe.
// =============================================================================

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AssameseKeyboard.App.ViewModels;

// ── Key-level ViewModel ───────────────────────────────────────────────────────

/// <summary>
/// Represents one physical key on the visual keyboard preview.
/// All Assamese strings use Unicode codepoints internally.
/// </summary>
public sealed class KeyViewModel
{
    /// <summary>Latin key label (e.g. "A", "D1", "Tab").</summary>
    public string Latin { get; init; } = string.Empty;

    /// <summary>
    /// Assamese base-layer character (shown bottom-right, gold).
    /// Unicode escape in source; rendered as glyph at runtime.
    /// </summary>
    public string AssBase { get; init; } = string.Empty;

    /// <summary>Assamese shift-layer character (shown top-right, blue).</summary>
    public string AssShift { get; init; } = string.Empty;

    /// <summary>Assamese AltGr-layer character (shown top-left, green). Optional.</summary>
    public string AssAltGr { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable Unicode codepoint label shown in tooltip.
    /// e.g. "U+09CD  HASANTA"
    /// </summary>
    public string Tooltip { get; init; } = string.Empty;

    /// <summary>Pixel width of this key on the preview grid.</summary>
    public double Width { get; init; } = 58;

    /// <summary>True for special function keys (Tab, Caps, Shift, Enter, Backspace).</summary>
    public bool IsSpecialKey { get; init; }

    /// <summary>True when this key carries a hasanta — highlighted differently.</summary>
    public bool IsHasantaKey { get; init; }
}

/// <summary>One horizontal row of keys on the preview grid.</summary>
public sealed class KeyRowViewModel
{
    public ObservableCollection<KeyViewModel> Keys { get; } = new();
}

// ── Character reference entry ─────────────────────────────────────────────────

/// <summary>A single row in the Unicode character reference table.</summary>
public sealed class CharRefEntry
{
    public string Codepoint { get; init; } = string.Empty;   // "U+09CD"
    public string Glyph { get; init; } = string.Empty;   // actual char
    public string Name { get; init; } = string.Empty;   // "Hasanta / Virama"
    public string Category { get; init; } = string.Empty;   // "Diacritic"
    public string Key { get; init; } = string.Empty;   // "D"
    public string Layer { get; init; } = string.Empty;   // "Base"
}

// ── Main ViewModel ────────────────────────────────────────────────────────────

/// <summary>
/// ViewModel for the Keyboard Preview page.
/// Builds the visual keyboard grid and the character reference table
/// from hardcoded layout data (matching assamese_default.json).
/// </summary>
public sealed partial class KeyboardPreviewViewModel : BaseViewModel
{
    // ── Observable collections ────────────────────────────────────────────────

    public ObservableCollection<KeyRowViewModel> Rows { get; } = new();
    public ObservableCollection<CharRefEntry> CharTable { get; } = new();

    // ── Observable properties ─────────────────────────────────────────────────

    [ObservableProperty] private bool _showAssamese = true;
    [ObservableProperty] private bool _showShifted = true;
    [ObservableProperty] private bool _showAltGr = false;

    // ── Juktakkhor hint ───────────────────────────────────────────────────────

    /// <summary>
    /// Tip shown on the preview page explaining conjunct typing.
    /// Uses Unicode escapes for all Assamese text.
    /// </summary>
    public string JuktakkhorHint =>
        // "Tip: Press D for hasanta (্). Type consonant + D + consonant."
        // Examples: ক + ্ + ষ = ক্ষ  |  ত + ্ + ৰ = ত্ৰ  |  ন + ্ + ত = ন্ত
        "Tip \u2014 Press \u2018D\u2019 to insert Hasanta (\u09CD U+09CD) between " +
        "two consonants to form conjunct letters (\u09AF\u09C1\u0995\u09CD\u09A4\u09BE\u0995\u09CD\u09B7\u09F0):\n" +
        "\u0995 + \u09CD + \u09B7 = \u0995\u09CD\u09B7   " +
        "\u09A4 + \u09CD + \u09F0 = \u09A4\u09CD\u09F0   " +
        "\u09A8 + \u09CD + \u09A4 = \u09A8\u09CD\u09A4   " +
        "\u09AE + \u09CD + \u09AA = \u09AE\u09CD\u09AA   " +
        "\u09B8 + \u09CD + \u0995 = \u09B8\u09CD\u0995";

    // ── Constructor ───────────────────────────────────────────────────────────

    public KeyboardPreviewViewModel()
    {
        BuildKeyboardRows();
        BuildCharTable();
    }

    // ── Keyboard grid builder ─────────────────────────────────────────────────

    private void BuildKeyboardRows()
    {
        // Helper: create a normal key
        static KeyViewModel K(
            string latin,
            string assBase,
            string assShift,
            string assAltGr = "",
            string tooltip = "",
            double width = 58)
            => new()
            {
                Latin = latin,
                AssBase = assBase,
                AssShift = assShift,
                AssAltGr = assAltGr,
                Tooltip = tooltip,
                Width = width,
                IsHasantaKey = assBase == "\u09CD",
            };

        // Helper: create a special/wide key
        static KeyViewModel W(string label, double width)
            => new()
            {
                Latin = label,
                Width = width,
                IsSpecialKey = true,
            };

        // ── Number row ────────────────────────────────────────────────────────
        var row0 = new KeyRowViewModel();
        row0.Keys.Add(W("`", 46));
        row0.Keys.Add(K("1", "\u09E7", "\u0964", "", "U+09E7 \u09E7 ONE  |  U+0964 \u0964 DANDA"));
        row0.Keys.Add(K("2", "\u09E8", "@", "", "U+09E8 \u09E8 TWO"));
        row0.Keys.Add(K("3", "\u09E9", "#", "", "U+09E9 \u09E9 THREE"));
        row0.Keys.Add(K("4", "\u09EA", "$", "", "U+09EA \u09EA FOUR"));
        row0.Keys.Add(K("5", "\u09EB", "%", "", "U+09EB \u09EB FIVE"));
        row0.Keys.Add(K("6", "\u09EC", "\u09A4\u09CD\u09F0", "", "U+09EC \u09EC SIX  |  Shift: \u09A4\u09CD\u09F0 (Tra)"));
        row0.Keys.Add(K("7", "\u09ED", "\u0995\u09CD\u09B7", "", "U+09ED \u09ED SEVEN  |  Shift: \u0995\u09CD\u09B7 (Ksha)"));
        row0.Keys.Add(K("8", "\u09EE", "*", "", "U+09EE \u09EE EIGHT"));
        row0.Keys.Add(K("9", "\u09EF", "(", "", "U+09EF \u09EF NINE"));
        row0.Keys.Add(K("0", "\u09E6", ")", "", "U+09E6 \u09E6 ZERO"));
        row0.Keys.Add(K("-", "\u0983", "_", "", "U+0983 VISARGA \u0983"));
        row0.Keys.Add(K("=", "\u09CE", "\u0965", "", "U+09CE KHANDA-TA \u09CE  |  U+0965 DOUBLE-DANDA \u0965"));
        row0.Keys.Add(W("\u232B Backspace", 98));   // ⌫
        Rows.Add(row0);

        // ── Q row ─────────────────────────────────────────────────────────────
        var row1 = new KeyRowViewModel();
        row1.Keys.Add(W("Tab \u21E5", 74));
        row1.Keys.Add(K("Q", "\u0993", "\u0994", "\u0990", "U+0993 \u0993 O-VOWEL  |  Shift: U+0994 \u0994 AU  |  AltGr: U+0990 \u0990 AI"));
        row1.Keys.Add(K("W", "\u09F1", "\u09F1", "\u0982", "U+09F1 \u09F1 ASSAMESE-WA (unique)  |  AltGr: U+0982 \u0982 ANUSVARA"));
        row1.Keys.Add(K("E", "\u0985", "\u0986", "\u098B", "U+0985 \u0985 SHORT-A  |  Shift: U+0986 \u0986 LONG-AA  |  AltGr: U+098B \u098B RI-VOWEL"));
        row1.Keys.Add(K("R", "\u0987", "\u0988", "\u098B", "U+0987 \u0987 SHORT-I  |  Shift: U+0988 \u0988 LONG-II  |  AltGr: RI-vowel"));
        row1.Keys.Add(K("T", "\u0989", "\u098A", "\u098C", "U+0989 \u0989 SHORT-U  |  Shift: U+098A \u098A LONG-UU  |  AltGr: U+098C \u098C VOCALIC-L"));
        row1.Keys.Add(K("Y", "\u09AC", "\u09AD", "", "U+09AC \u09AC BA  |  Shift: U+09AD \u09AD BHA"));
        row1.Keys.Add(K("U", "\u09B9", "\u0999", "\u09C2", "U+09B9 \u09B9 HA  |  Shift: U+0999 \u0999 NGA  |  AltGr: U+09C2 \u09C2 UU-MATRA"));
        row1.Keys.Add(K("I", "\u0997", "\u0998", "\u09C0", "U+0997 \u0997 GA  |  Shift: U+0998 \u0998 GHA  |  AltGr: U+09C0 \u09C0 II-MATRA"));
        row1.Keys.Add(K("O", "\u09A6", "\u09A7", "", "U+09A6 \u09A6 DA  |  Shift: U+09A7 \u09A7 DHA"));
        row1.Keys.Add(K("P", "\u099C", "\u099D", "\u09B7", "U+099C \u099C JA  |  Shift: U+099D \u099D JHA  |  AltGr: U+09B7 \u09B7 SSA"));
        row1.Keys.Add(K("[", "\u09A1", "\u09A2", "", "U+09A1 \u09A1 DDA  |  Shift: U+09A2 \u09A2 DDHA"));
        row1.Keys.Add(K("]", "\u09BC", "\u099E", "", "U+09BC NUKTA  |  Shift: U+099E \u099E NYA"));
        row1.Keys.Add(K("\\", "\u09AF", "|", "", "U+09AF \u09AF YA"));
        Rows.Add(row1);

        // ── Home row ──────────────────────────────────────────────────────────
        var row2 = new KeyRowViewModel();
        row2.Keys.Add(W("Caps", 84));
        row2.Keys.Add(K("A", "\u09BE", "\u09CB", "\u09CC", "U+09BE \u09BE AA-MATRA  |  Shift: U+09CB \u09CB O-MATRA  |  AltGr: U+09CC \u09CC AU-MATRA"));
        row2.Keys.Add(K("S", "\u09C7", "\u098F", "\u09C8", "U+09C7 \u09C7 E-MATRA  |  Shift: U+098F \u098F E-VOWEL  |  AltGr: U+09C8 \u09C8 AI-MATRA"));
        // HASANTA key — highlighted specially
        row2.Keys.Add(new KeyViewModel
        {
            Latin = "D",
            AssBase = "\u09CD",   // ্  U+09CD HASANTA
            AssShift = "\u0985",   // অ  U+0985 SHORT-A
            Tooltip = "U+09CD \u09CD HASANTA (Virama) \u2014 conjunct former\nShift: U+0985 \u0985 SHORT-A",
            Width = 58,
            IsHasantaKey = true,
        });
        row2.Keys.Add(K("F", "\u09BF", "\u0987", "\u09C3", "U+09BF \u09BF I-MATRA  |  Shift: U+0987 \u0987 I-VOWEL  |  AltGr: U+09C3 \u09C3 RI-MATRA"));
        row2.Keys.Add(K("G", "\u09C1", "\u0989", "\u09C2", "U+09C1 \u09C1 U-MATRA  |  Shift: U+0989 \u0989 U-VOWEL  |  AltGr: U+09C2 \u09C2 UU-MATRA"));
        row2.Keys.Add(K("H", "\u09AA", "\u09AB", "", "U+09AA \u09AA PA  |  Shift: U+09AB \u09AB PHA"));
        row2.Keys.Add(K("J", "\u09F0", "\u09F0", "", "U+09F0 \u09F0 ASSAMESE-RA (unique) both layers"));
        row2.Keys.Add(K("K", "\u0995", "\u0996", "", "U+0995 \u0995 KA  |  Shift: U+0996 \u0996 KHA"));
        row2.Keys.Add(K("L", "\u09A4", "\u09A5", "", "U+09A4 \u09A4 TA  |  Shift: U+09A5 \u09A5 THA"));
        row2.Keys.Add(K(";", "\u099A", "\u099B", "\u09B0", "U+099A \u099A CA  |  Shift: U+099B \u099B CHA  |  AltGr: U+09B0 \u09B0 BENGALI-RA"));
        row2.Keys.Add(K("'", "\u099F", "\u09A0", "", "U+099F \u099F TTA  |  Shift: U+09A0 \u09A0 TTHA"));
        row2.Keys.Add(W("Enter \u21B5", 104));
        Rows.Add(row2);

        // ── Bottom row ────────────────────────────────────────────────────────
        var row3 = new KeyRowViewModel();
        row3.Keys.Add(W("\u21E7 Shift", 114));
        row3.Keys.Add(K("Z", "\u09CE", "\u09A3", "", "U+09CE \u09CE KHANDA-TA  |  Shift: U+09A3 \u09A3 NNA"));
        row3.Keys.Add(K("X", "\u09BC", "\u0981", "\u0983", "NUKTA \u09BC  |  Shift: U+0981 \u0981 CHANDRABINDU  |  AltGr: U+0983 \u0983 VISARGA"));
        row3.Keys.Add(K("C", "\u09AE", "\u09A3", "", "U+09AE \u09AE MA  |  Shift: U+09A3 \u09A3 NNA"));
        row3.Keys.Add(K("V", "\u09A8", "\u09A8", "", "U+09A8 \u09A8 NA (both layers)"));
        row3.Keys.Add(K("B", "\u09F0", "\u09F1", "", "U+09F0 \u09F0 ASSAMESE-RA  |  Shift: U+09F1 \u09F1 ASSAMESE-WA"));
        row3.Keys.Add(K("N", "\u09B2", "\u09A8", "", "U+09B2 \u09B2 LA  |  Shift: U+09A8 \u09A8 NA"));
        row3.Keys.Add(K("M", "\u09B8", "\u09B6", "", "U+09B8 \u09B8 SA  |  Shift: U+09B6 \u09B6 SHA"));
        row3.Keys.Add(K(",", ",", "\u09AF", "", "Comma  |  Shift: U+09AF \u09AF YA"));
        row3.Keys.Add(K(".", "\u0964", "\u0965", "", "U+0964 \u0964 DANDA  |  Shift: U+0965 \u0965 DOUBLE-DANDA"));
        row3.Keys.Add(K("/", "/", "?", "", "Slash  |  Shift: Question mark"));
        row3.Keys.Add(W("Shift \u21E7", 114));
        Rows.Add(row3);

        // ── Space bar row ─────────────────────────────────────────────────────
        var row4 = new KeyRowViewModel();
        row4.Keys.Add(W("Ctrl", 64));
        row4.Keys.Add(W("Win", 52));
        row4.Keys.Add(W("Alt", 58));
        row4.Keys.Add(new KeyViewModel
        {
            Latin = "\u0985\u09B8\u09AE\u09C0\u09AF\u09BC\u09BE  Space",   // অসমীয়া  Space
            Width = 360,
            IsSpecialKey = true,
        });
        row4.Keys.Add(W("AltGr", 64));
        row4.Keys.Add(W("Menu", 52));
        row4.Keys.Add(W("Ctrl", 64));
        Rows.Add(row4);
    }

    // ── Character reference table ─────────────────────────────────────────────

    private void BuildCharTable()
    {
        // Standalone vowels
        Add("U+0985", "\u0985", "Short-A vowel", "Standalone Vowel", "E", "Base");
        Add("U+0986", "\u0986", "Long-Aa vowel", "Standalone Vowel", "E", "Shift");
        Add("U+0987", "\u0987", "Short-I vowel", "Standalone Vowel", "R", "Base");
        Add("U+0988", "\u0988", "Long-Ii vowel", "Standalone Vowel", "R", "Shift");
        Add("U+0989", "\u0989", "Short-U vowel", "Standalone Vowel", "T", "Base");
        Add("U+098A", "\u098A", "Long-Uu vowel", "Standalone Vowel", "T", "Shift");
        Add("U+098B", "\u098B", "Ri vowel (Vocalic R)", "Standalone Vowel", "E", "AltGr");
        Add("U+098F", "\u098F", "E vowel", "Standalone Vowel", "S", "Shift");
        Add("U+0990", "\u0990", "Ai vowel", "Standalone Vowel", "Q", "AltGr");
        Add("U+0993", "\u0993", "O vowel", "Standalone Vowel", "Q", "Base");
        Add("U+0994", "\u0994", "Au vowel", "Standalone Vowel", "Q", "Shift");

        // Vowel signs (matras)
        Add("U+09BE", "\u09BE", "Aa-matra (vowel sign)", "Vowel Sign", "A", "Base");
        Add("U+09BF", "\u09BF", "I-matra (vowel sign)", "Vowel Sign", "F", "Base");
        Add("U+09C0", "\u09C0", "Ii-matra (vowel sign)", "Vowel Sign", "I", "AltGr");
        Add("U+09C1", "\u09C1", "U-matra (vowel sign)", "Vowel Sign", "G", "Base");
        Add("U+09C2", "\u09C2", "Uu-matra (vowel sign)", "Vowel Sign", "U", "AltGr");
        Add("U+09C3", "\u09C3", "Ri-matra (vowel sign)", "Vowel Sign", "F", "AltGr");
        Add("U+09C7", "\u09C7", "E-matra (vowel sign)", "Vowel Sign", "S", "Base");
        Add("U+09C8", "\u09C8", "Ai-matra (vowel sign)", "Vowel Sign", "S", "AltGr");
        Add("U+09CB", "\u09CB", "O-matra (vowel sign)", "Vowel Sign", "A", "Shift");
        Add("U+09CC", "\u09CC", "Au-matra (vowel sign)", "Vowel Sign", "A", "AltGr");

        // Consonants
        Add("U+0995", "\u0995", "Ka", "Consonant", "K", "Base");
        Add("U+0996", "\u0996", "Kha", "Consonant", "K", "Shift");
        Add("U+0997", "\u0997", "Ga", "Consonant", "I", "Base");
        Add("U+0998", "\u0998", "Gha", "Consonant", "I", "Shift");
        Add("U+0999", "\u0999", "Nga", "Consonant", "U", "Shift");
        Add("U+099A", "\u099A", "Ca", "Consonant", ";", "Base");
        Add("U+099B", "\u099B", "Cha", "Consonant", ";", "Shift");
        Add("U+099C", "\u099C", "Ja", "Consonant", "P", "Base");
        Add("U+099D", "\u099D", "Jha", "Consonant", "P", "Shift");
        Add("U+099E", "\u099E", "Nya", "Consonant", "]", "Shift");
        Add("U+099F", "\u099F", "Tta", "Consonant", "'", "Base");
        Add("U+09A0", "\u09A0", "Ttha", "Consonant", "'", "Shift");
        Add("U+09A1", "\u09A1", "Dda", "Consonant", "[", "Base");
        Add("U+09A2", "\u09A2", "Ddha", "Consonant", "[", "Shift");
        Add("U+09A3", "\u09A3", "Nna", "Consonant", "Z", "Shift");
        Add("U+09A4", "\u09A4", "Ta", "Consonant", "L", "Base");
        Add("U+09A5", "\u09A5", "Tha", "Consonant", "L", "Shift");
        Add("U+09A6", "\u09A6", "Da", "Consonant", "O", "Base");
        Add("U+09A7", "\u09A7", "Dha", "Consonant", "O", "Shift");
        Add("U+09A8", "\u09A8", "Na", "Consonant", "V", "Base");
        Add("U+09AA", "\u09AA", "Pa", "Consonant", "H", "Base");
        Add("U+09AB", "\u09AB", "Pha", "Consonant", "H", "Shift");
        Add("U+09AC", "\u09AC", "Ba", "Consonant", "Y", "Base");
        Add("U+09AD", "\u09AD", "Bha", "Consonant", "Y", "Shift");
        Add("U+09AE", "\u09AE", "Ma", "Consonant", "C", "Base");
        Add("U+09AF", "\u09AF", "Ya", "Consonant", "\\", "Base");
        Add("U+09B0", "\u09B0", "Bengali Ra", "Consonant", ";", "AltGr");
        Add("U+09B2", "\u09B2", "La", "Consonant", "N", "Base");
        Add("U+09B6", "\u09B6", "Sha", "Consonant", "M", "Shift");
        Add("U+09B7", "\u09B7", "Ssa", "Consonant", "P", "AltGr");
        Add("U+09B8", "\u09B8", "Sa", "Consonant", "M", "Base");
        Add("U+09B9", "\u09B9", "Ha", "Consonant", "U", "Base");

        // Assamese unique
        Add("U+09F0", "\u09F0", "Assamese Ra (unique)", "Assamese Unique", "J", "Base");
        Add("U+09F1", "\u09F1", "Assamese Wa (unique)", "Assamese Unique", "W", "Base");
        Add("U+09CE", "\u09CE", "Khanda Ta (final)", "Assamese Unique", "Z", "Base");

        // Diacritics
        Add("U+09CD", "\u09CD", "Hasanta / Virama (conjunct former)", "Diacritic", "D", "Base");
        Add("U+09BC", "\u09BC", "Nukta", "Diacritic", "X", "Base");
        Add("U+0981", "\u0981", "Chandrabindu", "Diacritic", "X", "Shift");
        Add("U+0982", "\u0982", "Anusvara", "Diacritic", "W", "AltGr");
        Add("U+0983", "\u0983", "Visarga", "Diacritic", "-", "Base");

        // Punctuation
        Add("U+0964", "\u0964", "Danda (Assamese full stop)", "Punctuation", ".", "Base");
        Add("U+0965", "\u0965", "Double Danda", "Punctuation", ".", "Shift");

        // Digits
        for (int i = 0; i <= 9; i++)
        {
            char digit = (char)(0x09E6 + i);
            Add($"U+{0x09E6 + i:X4}", digit.ToString(),
                $"Assamese digit {i}", "Digit",
                i.ToString(), "Base");
        }
    }

    private void Add(
        string codepoint, string glyph, string name,
        string category, string key, string layer)
        => CharTable.Add(new CharRefEntry
        {
            Codepoint = codepoint,
            Glyph = glyph,
            Name = name,
            Category = category,
            Key = key,
            Layer = layer,
        });
}