// =============================================================================
// FILE: AssameseKeyboard.Core/Mapping/KeyMapping.cs
//
// INSTRUCTION:
//   Create folder "Mapping" inside AssameseKeyboard.Core.
//   Add this file inside it.
//   No additional setup needed — pure data model.
// =============================================================================

using System.Text.Json.Serialization;

namespace AssameseKeyboard.Core.Mapping;

/// <summary>
/// Represents the Assamese output for a single physical key across
/// all modifier layers. Deserialised from the JSON layout file.
///
/// ALL string values use Unicode escape sequences in the JSON source
/// (e.g. "\u0995" instead of "ক") so the file is encoding-agnostic.
/// At runtime the values are normal C# strings in memory.
/// </summary>
public sealed record KeyMapping
{
    // ── JSON properties ───────────────────────────────────────────────────────

    /// <summary>
    /// Key name as returned by <c>((System.Windows.Forms.Keys)vkCode).ToString()</c>.
    /// Examples: "A", "D1", "OemSemicolon", "OemPeriod".
    /// Always uppercase for letter keys.
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Assamese string to inject when no modifier (Shift / Caps) is active.
    /// May be multiple Unicode codepoints for pre-built conjuncts.
    /// May be an ASCII passthrough like "," or "/".
    /// Stored internally as the actual Unicode characters (not escape sequences).
    /// </summary>
    [JsonPropertyName("base")]
    public string Base { get; init; } = string.Empty;

    /// <summary>
    /// Assamese string to inject when Shift is logically active
    /// (Shift XOR CapsLock).
    /// </summary>
    [JsonPropertyName("shifted")]
    public string Shifted { get; init; } = string.Empty;

    /// <summary>
    /// Optional AltGr (Right-Alt) layer output.
    /// <c>null</c> means this key has no AltGr binding and passes through.
    /// </summary>
    [JsonPropertyName("altGr")]
    public string? AltGr { get; init; }

    // ── Metadata (for tooling / UI) ───────────────────────────────────────────

    /// <summary>Human-readable description of the base character. Optional.</summary>
    [JsonPropertyName("_base_desc")]
    public string? BaseDescription { get; init; }

    /// <summary>Human-readable description of the shifted character. Optional.</summary>
    [JsonPropertyName("_shifted_desc")]
    public string? ShiftedDescription { get; init; }

    // ── Validation ────────────────────────────────────────────────────────────

    /// <summary>Returns true if this mapping has the minimum required data.</summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Key) &&
        Base is not null &&
        Shifted is not null;

    /// <summary>True when Base contains a hasanta (U+09CD) character.</summary>
    public bool BaseIsHasanta => Base.Contains('\u09CD');

    /// <summary>True when Base is a single Assamese combining mark.</summary>
    public bool BaseIsCombiningMark =>
        Base.Length == 1 &&
        System.Globalization.CharUnicodeInfo.GetUnicodeCategory(Base[0])
            is System.Globalization.UnicodeCategory.NonSpacingMark
            or System.Globalization.UnicodeCategory.SpacingCombiningMark;
}