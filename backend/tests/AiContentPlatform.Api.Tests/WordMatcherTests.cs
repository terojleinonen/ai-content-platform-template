using AiContentPlatform.Api.Services;

namespace AiContentPlatform.Api.Tests;

public class WordMatcherTests
{
    [Theory]
    [InlineData("pienyrittäjä", "pienyrittäjän")]
    [InlineData("pienyrittäjä", "pienyrittäjille")]
    [InlineData("helsinki", "helsingissä")]
    [InlineData("helsinki", "helsinkiin")]
    [InlineData("kauppa", "kaupassa")]
    [InlineData("kauppa", "kauppojen")]
    [InlineData("blogi", "blogia")]
    [InlineData("blogi", "blogeissa")]
    [InlineData("ihminen", "ihmisen")]
    [InlineData("kokemus", "kokemuksen")]
    [InlineData("tunne", "tunteen")]
    [InlineData("paahdettu", "paahdetun")]
    [InlineData("kahvi", "kahvia")]
    public void Finnish_MatchesInflectedForms(string keyword, string word) =>
        Assert.True(WordMatcher.For(keyword, TextLanguage.Finnish)(word));

    [Theory]
    [InlineData("kauppa", "kausi")]
    [InlineData("helsinki", "helmi")]
    [InlineData("blogi", "blokki")]
    [InlineData("tee", "teemme")]          // short words only match exactly
    [InlineData("kahvi", "kahvinkeittimellämme")] // suffix too long to be an inflection
    public void Finnish_DoesNotMatchUnrelatedWords(string keyword, string word) =>
        Assert.False(WordMatcher.For(keyword, TextLanguage.Finnish)(word));

    [Theory]
    [InlineData("bag", "bags", true)]
    [InlineData("box", "boxes", true)]
    [InlineData("story", "stories", true)]
    [InlineData("coffee", "coffee", true)]
    [InlineData("coffee", "coffeehouse", false)]
    [InlineData("roast", "roasted", false)]
    public void English_MatchesExactAndPlural(string keyword, string word, bool expected) =>
        Assert.Equal(expected, WordMatcher.For(keyword, TextLanguage.English)(word));
}

public class LanguagesTests
{
    [Theory]
    [InlineData("Pienyrittäjän arki on täynnä kiireitä, ja blogi jää usein hännille.", TextLanguage.Finnish)]
    [InlineData("Tuoreeltaan paahdettua kahvia suoraan kotiovellesi.", TextLanguage.Finnish)]
    [InlineData("Our coffee is roasted to order and shipped within 48 hours.", TextLanguage.English)]
    [InlineData("12345 !!!", TextLanguage.Other)]
    public void Detect_IdentifiesLanguage(string text, TextLanguage expected) =>
        Assert.Equal(expected, Languages.Detect(text));

    [Theory]
    [InlineData("Finnish", TextLanguage.Finnish)]
    [InlineData("suomi", TextLanguage.Finnish)]
    [InlineData("fi", TextLanguage.Finnish)]
    [InlineData("English", TextLanguage.English)]
    [InlineData("Swedish", TextLanguage.Other)]
    [InlineData(null, TextLanguage.Auto)]
    public void Parse_NormalizesNames(string? name, TextLanguage expected) =>
        Assert.Equal(expected, Languages.Parse(name));
}
