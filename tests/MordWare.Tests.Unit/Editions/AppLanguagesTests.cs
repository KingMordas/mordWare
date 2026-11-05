using MordWare.Core.Editions;

namespace MordWare.Tests.Unit.Editions;

public class AppLanguagesTests
{
    [Fact]
    public void Default_is_US_English() => Assert.Equal("en-US", AppLanguages.Default);

    [Theory]
    [InlineData("it-IT", "it-IT")]
    [InlineData("IT-it", "it-IT")]
    [InlineData("en-US", "en-US")]
    [InlineData("en-GB", "en-US")]
    [InlineData("fr-FR", "en-US")]
    [InlineData(null, "en-US")]
    public void Normalize_maps_unsupported_tags_to_the_default(string? tag, string expected) =>
        Assert.Equal(expected, AppLanguages.Normalize(tag));

    [Fact]
    public void Italian_culture_formats_numbers_the_Italian_way() =>
        Assert.Equal("1.234,5", 1234.5m.ToString("#,##0.0", AppLanguages.Culture(AppLanguages.Italian)));
}
