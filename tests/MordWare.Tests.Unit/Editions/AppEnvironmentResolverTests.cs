using MordWare.Core.Editions;

namespace MordWare.Tests.Unit.Editions;

public class AppEnvironmentResolverTests
{
    [Fact]
    public void Prod_package_name_resolves_to_Prod() =>
        Assert.Equal(AppEnvironment.Prod, AppEnvironmentResolver.FromPackageName("MordWare"));

    [Fact]
    public void Dev_package_name_resolves_to_Dev() =>
        Assert.Equal(AppEnvironment.Dev, AppEnvironmentResolver.FromPackageName("MordWare.Dev"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("mordware")]          // identity names are case-sensitive: never guess PROD
    [InlineData("MordWare ")]
    [InlineData("Something.Else")]
    public void Unknown_or_missing_package_name_resolves_to_Dev(string? packageName) =>
        Assert.Equal(AppEnvironment.Dev, AppEnvironmentResolver.FromPackageName(packageName));
}
