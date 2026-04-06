// =============================================================================
// FILE: AssameseKeyboard.Core/Mapping/KeyboardLayout.cs
// INSTRUCTION: Add to the "Mapping" folder in AssameseKeyboard.Core.
// =============================================================================

using System.Text.Json.Serialization;

namespace AssameseKeyboard.Core.Mapping;

/// <summary>
/// The full serialisable definition of a keyboard layout.
/// Loaded from an embedded or external JSON file at runtime.
/// </summary>
public sealed class KeyboardLayout
{
    /// <summary>Human-readable name shown in the UI (e.g. "Assamese Default").</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Semantic version string (e.g. "1.2.0").</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>Author or organisation name.</summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// All key mappings in this layout.
    /// Keys not listed here pass through unmodified to the target application.
    /// </summary>
    [JsonPropertyName("mappings")]
    public List<KeyMapping> Mappings { get; set; } = new();

    /// <summary>
    /// Optional comments array — ignored by the engine, used for documentation.
    /// </summary>
    [JsonPropertyName("_comment")]
    public List<string>? Comment { get; set; }
}