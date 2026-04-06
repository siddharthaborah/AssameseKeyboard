// =============================================================================
// FILE: AssameseKeyboard.Core/Mapping/KeyMapper.cs
// INSTRUCTION: Add to the "Mapping" folder in AssameseKeyboard.Core.
//
// DEPENDENCIES: System.Text.Json (NuGet), System.Windows.Forms.Keys (framework ref)
//
// PURPOSE:
//   Loads a KeyboardLayout from JSON and resolves virtual-key codes
//   to their Assamese Unicode output strings.
//
// KEY NAME RESOLUTION:
//   Virtual-key code → System.Windows.Forms.Keys enum → string name
//   Examples:
//     0x41 → Keys.A         → "A"
//     0x31 → Keys.D1        → "D1"
//     0xBA → Keys.OemSemicolon → "OemSemicolon"
//   The JSON layout uses these exact string names as the "key" field.
//
// THREAD SAFETY:
//   LoadEmbeddedDefault() and LoadFromFile() must not be called concurrently.
//   Resolve() is read-only and thread-safe after loading is complete.
// =============================================================================

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AssameseKeyboard.Core.Mapping;

/// <summary>
/// Resolves a virtual-key code + modifier state to the Assamese Unicode
/// string that should be injected into the active window.
///
/// Loaded from an embedded or external JSON layout file.
/// After <see cref="LoadEmbeddedDefault"/> or <see cref="LoadFromFile"/>,
/// call <see cref="Resolve"/> for each intercepted keystroke.
/// </summary>
public sealed class KeyMapper
{
    // ── Lookup tables ─────────────────────────────────────────────────────────

    // Primary map: (keyName, isShifted) → Assamese string
    private readonly Dictionary<(string key, bool shifted), string> _map
        = new(StringComparer.OrdinalIgnoreCase.ToKeyedComparer());

    // AltGr layer: keyName → Assamese string
    private readonly Dictionary<string, string> _altGrMap
        = new(StringComparer.OrdinalIgnoreCase);

    // ── State ─────────────────────────────────────────────────────────────────

    private KeyboardLayout? _layout;
    private readonly ILogger<KeyMapper> _logger;

    // ── JSON options ──────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Constructor ───────────────────────────────────────────────────────────

    public KeyMapper(ILogger<KeyMapper> logger)
        => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Name of the currently loaded layout.</summary>
    public string? LayoutName => _layout?.Name;

    /// <summary>Version string of the currently loaded layout.</summary>
    public string? LayoutVersion => _layout?.Version;

    /// <summary>Total number of key mappings loaded.</summary>
    public int MappingCount => _layout?.Mappings.Count ?? 0;

    /// <summary>
    /// Loads the default Assamese layout that is compiled into the assembly
    /// as an embedded resource (Layouts/assamese_default.json).
    ///
    /// MUST be called before any call to <see cref="Resolve"/>.
    /// Safe to call multiple times — reloads and replaces current layout.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   Thrown if the embedded resource is missing (misconfigured .csproj).
    /// </exception>
    public void LoadEmbeddedDefault()
    {
        const string ResourceName =
            "AssameseKeyboard.Core.Layouts.assamese_default.json";

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"[KeyMapper] Embedded resource '{ResourceName}' not found. " +
                "Ensure Build Action is set to 'EmbeddedResource' for " +
                "Layouts/assamese_default.json in AssameseKeyboard.Core.");

        LoadFromStream(stream, sourceName: "embedded default");
    }

    /// <summary>
    /// Loads a custom layout from an external JSON file.
    /// Falls back to the embedded default if the file is missing or corrupt.
    /// </summary>
    /// <param name="filePath">Absolute path to a JSON layout file.</param>
    public void LoadFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning(
                "[KeyMapper] Layout file not found at '{Path}'. " +
                "Falling back to embedded default.", filePath);
            LoadEmbeddedDefault();
            return;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            LoadFromStream(stream, sourceName: filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[KeyMapper] Failed to load layout from '{Path}'. " +
                "Falling back to embedded default.", filePath);
            LoadEmbeddedDefault();
        }
    }

    /// <summary>
    /// Resolves a virtual-key code to its Assamese Unicode output.
    ///
    /// Returns <c>null</c> when:
    ///   - The virtual key is not mapped in the current layout
    ///   - The virtual key cannot be converted to a key name
    ///   - No layout has been loaded yet
    ///
    /// The caller should inject the returned string via InputInjector
    /// and suppress the original keystroke.
    /// If null is returned, the original keystroke passes through unchanged.
    /// </summary>
    /// <param name="virtualKey">Virtual-key code from KBDLLHOOKSTRUCT.</param>
    /// <param name="shifted">Logical shift state (Shift XOR CapsLock).</param>
    /// <param name="altGr">True when Right-Alt (AltGr) is held.</param>
    public string? Resolve(uint virtualKey, bool shifted, bool altGr = false)
    {
        var keyName = VkToKeyName(virtualKey);
        if (keyName is null) return null;

        return Resolve(keyName, shifted, altGr);
    }

    /// <summary>
    /// Resolves a key name string to its Assamese Unicode output.
    /// Overload used directly in unit tests.
    /// </summary>
    /// <param name="keyName">Key name e.g. "A", "D1", "OemSemicolon".</param>
    /// <param name="shifted">Logical shift state.</param>
    /// <param name="altGr">AltGr state.</param>
    public string? Resolve(string keyName, bool shifted, bool altGr = false)
    {
        if (string.IsNullOrWhiteSpace(keyName)) return null;

        // AltGr layer takes precedence over shift layer
        if (altGr && _altGrMap.TryGetValue(keyName, out var altGrResult))
            return altGrResult;

        // Normal base / shift layer
        return _map.TryGetValue((keyName, shifted), out var result)
            ? result
            : null;
    }

    // ── Virtual-key → name ────────────────────────────────────────────────────

    // Virtual keys we never intercept — always pass through
    private static readonly HashSet<uint> s_passthroughKeys =
    [
        0x10, // ShiftKey
        0xA0, // LShiftKey
        0xA1, // RShiftKey
        0x11, // ControlKey
        0xA2, // LControlKey
        0xA3, // RControlKey
        0x12, // Menu
        0xA4, // LMenu
        0xA5, // RMenu
        0x14, // Capital
        0x09, // Tab
        0x0D, // Return
        0x08, // Back
        0x2E, // Delete
        0x1B, // Escape
        0x20, // Space
        0x25, // Left
        0x27, // Right
        0x26, // Up
        0x28, // Down
        0x24, // Home
        0x23, // End
        0x21, // PageUp
        0x22, // PageDown
        0x2D, // Insert
        0x2C, // PrintScreen
        0x13, // Pause
        0x91, // Scroll
        0x70, // F1
        0x71, // F2
        0x72, // F3
        0x73, // F4
        0x74, // F5
        0x75, // F6
        0x76, // F7
        0x77, // F8
        0x78, // F9
        0x79, // F10
        0x7A, // F11
        0x7B, // F12
        0x5B, // LWin
        0x5C, // RWin
        0x5D, // Apps
        0x90, // NumLock
        0x60, // NumPad0
        0x61, // NumPad1
        0x62, // NumPad2
        0x63, // NumPad3
        0x64, // NumPad4
        0x65, // NumPad5
        0x66, // NumPad6
        0x67, // NumPad7
        0x68, // NumPad8
        0x69, // NumPad9
        0x6A, // Multiply
        0x6B, // Add
        0x6D, // Subtract
        0x6E, // Decimal
        0x6F, // Divide
    ];

    /// <summary>
    /// Converts a virtual-key code to the key name used in the JSON layout.
    /// Returns null for keys that should never be intercepted.
    /// </summary>
    public static string? VkToKeyName(uint vk)
    {
        if (s_passthroughKeys.Contains(vk))
            return null;

        if (vk is >= 0x41 and <= 0x5A)
            return ((char)vk).ToString();

        if (vk is >= 0x30 and <= 0x39)
            return $"D{vk - 0x30}";

        return vk switch
        {
            0xBD => "OemMinus",
            0xBB => "Oemplus",
            0xDB => "OemOpenBrackets",
            0xDD => "OemCloseBrackets",
            0xDC => "OemBackslash",
            0xBA => "OemSemicolon",
            0xDE => "OemQuotes",
            0xBC => "Oemcomma",
            0xBE => "OemPeriod",
            0xBF => "OemQuestion",
            _ => null,
        };
    }

    // ── Private loader ────────────────────────────────────────────────────────

    private void LoadFromStream(Stream stream, string sourceName)
    {
        KeyboardLayout layout;
        try
        {
            layout = JsonSerializer.Deserialize<KeyboardLayout>(stream, s_jsonOptions)
                ?? throw new InvalidDataException("Layout JSON deserialised to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"[KeyMapper] JSON parse error in '{sourceName}': {ex.Message}", ex);
        }

        // Validate and populate lookup tables
        _map.Clear();
        _altGrMap.Clear();

        int loaded = 0;
        int skipped = 0;

        foreach (var mapping in layout.Mappings)
        {
            if (!mapping.IsValid)
            {
                _logger.LogWarning(
                    "[KeyMapper] Skipping invalid mapping: key='{Key}' " +
                    "base='{Base}' shifted='{Shifted}'.",
                    mapping.Key, mapping.Base, mapping.Shifted);
                skipped++;
                continue;
            }

            _map[(mapping.Key, false)] = mapping.Base;
            _map[(mapping.Key, true)] = mapping.Shifted;

            if (mapping.AltGr is not null)
                _altGrMap[mapping.Key] = mapping.AltGr;

            loaded++;
        }

        _layout = layout;

        _logger.LogInformation(
            "[KeyMapper] Layout '{Name}' v{Version} loaded from {Source}. " +
            "{Loaded} mappings, {Skipped} skipped, {AltGr} AltGr bindings.",
            layout.Name, layout.Version, sourceName,
            loaded, skipped, _altGrMap.Count);
    }
}

// ── Extension helper ──────────────────────────────────────────────────────────

file static class DictionaryExtensions
{
    /// <summary>
    /// Returns an IEqualityComparer that wraps a StringComparer
    /// for use as a tuple key comparer for (string, bool) tuples.
    /// The string component uses the provided StringComparer;
    /// the bool component uses default equality.
    /// </summary>
    public static IEqualityComparer<(string, bool)> ToKeyedComparer(
        this StringComparer stringComparer)
        => new TupleComparer(stringComparer);

    private sealed class TupleComparer : IEqualityComparer<(string, bool)>
    {
        private readonly StringComparer _sc;
        public TupleComparer(StringComparer sc) => _sc = sc;

        public bool Equals((string, bool) x, (string, bool) y)
            => _sc.Equals(x.Item1, y.Item1) && x.Item2 == y.Item2;

        public int GetHashCode((string, bool) obj)
            => HashCode.Combine(_sc.GetHashCode(obj.Item1), obj.Item2);
    }
}
