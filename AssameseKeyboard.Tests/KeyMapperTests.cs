// =============================================================================
// FILE: AssameseKeyboard.Tests/KeyMapperTests.cs
// INSTRUCTION:
//   Add to AssameseKeyboard.Tests project.
//   Add project reference to AssameseKeyboard.Core.
//   Uses MSTest (built into WinUI Unit Test App template).
//   NullLogger<T> from Microsoft.Extensions.Logging.Abstractions.
// =============================================================================

using AssameseKeyboard.Core.Mapping;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssameseKeyboard.Tests;

[TestClass]
public sealed class KeyMapperTests
{
    private KeyMapper _mapper = null!;

    [TestInitialize]
    public void Setup()
    {
        _mapper = new KeyMapper(NullLogger<KeyMapper>.Instance);
        _mapper.LoadEmbeddedDefault();
    }

    // ── Layout loaded ─────────────────────────────────────────────────────────

    [TestMethod]
    public void LoadEmbeddedDefault_DoesNotThrow()
    {
        var m = new KeyMapper(NullLogger<KeyMapper>.Instance);
        m.LoadEmbeddedDefault();   // must not throw
    }

    [TestMethod]
    public void LoadEmbeddedDefault_SetsLayoutName()
    {
        Assert.IsNotNull(_mapper.LayoutName);
        Assert.IsFalse(string.IsNullOrWhiteSpace(_mapper.LayoutName));
    }

    [TestMethod]
    public void LoadEmbeddedDefault_LoadsNonZeroMappings()
        => Assert.IsTrue(_mapper.MappingCount > 0,
            "At least one mapping must be loaded.");

    // ── Consonant base layer ──────────────────────────────────────────────────

    [DataTestMethod]
    [DataRow("K", false, "\u0995")]   // ক Ka
    [DataRow("K", true, "\u0996")]   // খ Kha
    [DataRow("I", false, "\u0997")]   // গ Ga
    [DataRow("I", true, "\u0998")]   // ঘ Gha
    [DataRow("U", false, "\u09B9")]   // হ Ha
    [DataRow("U", true, "\u0999")]   // ঙ Nga
    [DataRow("O", false, "\u09A6")]   // দ Da
    [DataRow("O", true, "\u09A7")]   // ধ Dha
    [DataRow("P", false, "\u099C")]   // জ Ja
    [DataRow("P", true, "\u099D")]   // ঝ Jha
    [DataRow("H", false, "\u09AA")]   // প Pa
    [DataRow("H", true, "\u09AB")]   // ফ Pha
    [DataRow("L", false, "\u09A4")]   // ত Ta
    [DataRow("L", true, "\u09A5")]   // থ Tha
    [DataRow("Y", false, "\u09AC")]   // ব Ba
    [DataRow("Y", true, "\u09AD")]   // ভ Bha
    [DataRow("C", false, "\u09AE")]   // ম Ma
    [DataRow("N", false, "\u09B2")]   // ল La
    [DataRow("M", false, "\u09B8")]   // স Sa
    [DataRow("M", true, "\u09B6")]   // শ Sha
    public void Resolve_Consonants(string key, bool shift, string expected)
    {
        var result = _mapper.Resolve(key, shift);
        Assert.AreEqual(expected, result,
            $"Key={key} shift={shift}: expected U+{(int)expected[0]:X4}");
    }

    // ── Assamese-unique letters ───────────────────────────────────────────────

    [TestMethod]
    public void Resolve_J_Base_ReturnsAssameseRa_U09F0()
    {
        var result = _mapper.Resolve("J", shifted: false);
        Assert.AreEqual("\u09F0", result,
            "J (base) must return Assamese Ra U+09F0, not Bengali Ra U+09B0");
    }

    [TestMethod]
    public void Resolve_B_Shifted_ReturnsAssameseWa_U09F1()
    {
        var result = _mapper.Resolve("B", shifted: true);
        Assert.AreEqual("\u09F1", result,
            "B (shifted) must return Assamese Wa U+09F1");
    }

    [TestMethod]
    public void Resolve_W_Base_ReturnsAssameseWa_U09F1()
    {
        var result = _mapper.Resolve("W", shifted: false);
        Assert.AreEqual("\u09F1", result,
            "W (base) must return Assamese Wa U+09F1");
    }

    // ── HASANTA — the most critical mapping ────────────────────────────────────

    [TestMethod]
    public void Resolve_D_Base_ReturnsHasanta_U09CD()
    {
        var result = _mapper.Resolve("D", shifted: false);
        Assert.AreEqual("\u09CD", result,
            "D (base) MUST return Hasanta U+09CD. This is the conjunct key.");
    }

    [TestMethod]
    public void Resolve_D_Shifted_ReturnsShortA_U0985()
    {
        var result = _mapper.Resolve("D", shifted: true);
        Assert.AreEqual("\u0985", result,
            "D (shifted) must return short-A vowel U+0985.");
    }

    [TestMethod]
    public void Hasanta_IsExactlyOneCodepoint()
    {
        var result = _mapper.Resolve("D", shifted: false)!;
        Assert.AreEqual(1, result.Length, "Hasanta must be a single codepoint.");
        Assert.AreEqual(0x09CD, (int)result[0],
            $"Expected U+09CD, got U+{(int)result[0]:X4}.");
    }

    // ── Vowels (standalone) ───────────────────────────────────────────────────

    [DataTestMethod]
    [DataRow("E", false, "\u0985")]   // অ Short-A
    [DataRow("E", true, "\u0986")]   // আ Long-Aa
    [DataRow("R", false, "\u0987")]   // ই Short-I
    [DataRow("R", true, "\u0988")]   // ঈ Long-Ii
    [DataRow("T", false, "\u0989")]   // উ Short-U
    [DataRow("T", true, "\u098A")]   // ঊ Long-Uu
    [DataRow("Q", false, "\u0993")]   // ও O-vowel
    [DataRow("Q", true, "\u0994")]   // ঔ Au-vowel
    [DataRow("S", true, "\u098F")]   // এ E-vowel
    public void Resolve_StandaloneVowels(string key, bool shift, string expected)
    {
        var result = _mapper.Resolve(key, shift);
        Assert.AreEqual(expected, result,
            $"Key={key} shift={shift}: expected vowel U+{(int)expected[0]:X4}");
    }

    // ── Vowel matras (signs) ──────────────────────────────────────────────────

    [DataTestMethod]
    [DataRow("A", false, "\u09BE")]   // া Aa-matra
    [DataRow("A", true, "\u09CB")]   // ো O-matra
    [DataRow("S", false, "\u09C7")]   // ে E-matra
    [DataRow("F", false, "\u09BF")]   // ি I-matra
    [DataRow("G", false, "\u09C1")]   // ু U-matra
    public void Resolve_VowelMatras(string key, bool shift, string expected)
    {
        var result = _mapper.Resolve(key, shift);
        Assert.AreEqual(expected, result,
            $"Matra key={key} shift={shift}: expected U+{(int)expected[0]:X4}");
    }

    // ── AltGr layer ───────────────────────────────────────────────────────────

    [DataTestMethod]
    [DataRow("I", "\u09C0")]   // ী Long-Ii matra
    [DataRow("U", "\u09C2")]   // ূ Long-Uu matra
    [DataRow("F", "\u09C3")]   // ৃ Ri matra
    [DataRow("S", "\u09C8")]   // ৈ Ai matra
    [DataRow("A", "\u09CC")]   // ৌ Au matra
    [DataRow("Q", "\u0990")]   // ঐ Ai vowel standalone
    [DataRow("W", "\u0982")]   // ং Anusvara
    public void Resolve_AltGrLayer(string key, string expected)
    {
        var result = _mapper.Resolve(key, shifted: false, altGr: true);
        Assert.AreEqual(expected, result,
            $"AltGr key={key}: expected U+{(int)expected[0]:X4}");
    }

    // ── Assamese digits ───────────────────────────────────────────────────────

    [DataTestMethod]
    [DataRow("D0", "\u09E6")]   // ০
    [DataRow("D1", "\u09E7")]   // ১
    [DataRow("D2", "\u09E8")]   // ২
    [DataRow("D3", "\u09E9")]   // ৩
    [DataRow("D4", "\u09EA")]   // ৪
    [DataRow("D5", "\u09EB")]   // ৫
    [DataRow("D6", "\u09EC")]   // ৬
    [DataRow("D7", "\u09ED")]   // ৭
    [DataRow("D8", "\u09EE")]   // ৮
    [DataRow("D9", "\u09EF")]   // ৯
    public void Resolve_AssameseDigits(string key, string expected)
    {
        var result = _mapper.Resolve(key, shifted: false);
        Assert.AreEqual(expected, result,
            $"Digit key={key}: expected U+{(int)expected[0]:X4}");
    }

    // ── Pre-built conjuncts ───────────────────────────────────────────────────

    [TestMethod]
    public void Resolve_D7_Shifted_ReturnsKsha_Conjunct()
    {
        // Shift+7 → ক্ষ = U+0995 + U+09CD + U+09B7
        var result = _mapper.Resolve("D7", shifted: true);
        Assert.AreEqual("\u0995\u09CD\u09B7", result,
            "Shift+7 must return Ksha conjunct (Ka+Hasanta+Ssa).");
        Assert.AreEqual(3, result!.Length, "Ksha conjunct must be 3 codepoints.");
        Assert.AreEqual('\u09CD', result[1],
            "Middle codepoint of Ksha must be Hasanta U+09CD.");
    }

    [TestMethod]
    public void Resolve_D6_Shifted_ReturnsTra_Conjunct()
    {
        // Shift+6 → ত্ৰ = U+09A4 + U+09CD + U+09F0
        var result = _mapper.Resolve("D6", shifted: true);
        Assert.AreEqual("\u09A4\u09CD\u09F0", result,
            "Shift+6 must return Tra conjunct (Ta+Hasanta+Assamese-Ra).");
        Assert.AreEqual('\u09F0', result![2],
            "Third codepoint of Tra must be Assamese Ra U+09F0.");
    }

    // ── Punctuation ───────────────────────────────────────────────────────────

    [TestMethod]
    public void Resolve_Period_Base_ReturnsDanda_U0964()
    {
        var result = _mapper.Resolve("OemPeriod", shifted: false);
        Assert.AreEqual("\u0964", result,
            "Period (base) must return Danda U+0964 (Assamese full stop).");
    }

    [TestMethod]
    public void Resolve_Period_Shifted_ReturnsDoubleDanda_U0965()
    {
        var result = _mapper.Resolve("OemPeriod", shifted: true);
        Assert.AreEqual("\u0965", result,
            "Period (shifted) must return Double Danda U+0965.");
    }

    // ── Unknown / passthrough keys ────────────────────────────────────────────

    [TestMethod]
    public void Resolve_F12_ReturnsNull()
        => Assert.IsNull(_mapper.Resolve("F12", shifted: false));

    [TestMethod]
    public void Resolve_EmptyKey_ReturnsNull()
        => Assert.IsNull(_mapper.Resolve(string.Empty, shifted: false));

    [TestMethod]
    public void Resolve_NullKey_ReturnsNull()
        => Assert.IsNull(_mapper.Resolve(null!, shifted: false));

    [TestMethod]
    public void LoadFromFile_NonExistentPath_FallsBackToDefault()
    {
        var m = new KeyMapper(NullLogger<KeyMapper>.Instance);
        m.LoadFromFile(@"C:\this\does\not\exist.json");
        // After fallback the mapper must still work
        Assert.IsNotNull(m.Resolve("K", shifted: false));
    }
}