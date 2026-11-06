using MordWare.Core.Editions;

namespace MordWare.App.Hosting;

/// <summary>
/// The edition this build is (guardrail 7). <c>MORDWARE_PROD</c> is defined by the project file only for
/// the Release configuration, so a Debug or test build can never be PROD. Nothing at run time is read:
/// no setting, argument or environment value can change the edition.
/// The project file also stamps the same value as <c>[assembly: AssemblyMetadata("MordWare.Edition", ...)]</c>,
/// which the architecture tests check per configuration.
/// </summary>
public static class AppEdition
{
#if MORDWARE_PROD
    public const AppEnvironment Current = AppEnvironment.Prod;
#else
    public const AppEnvironment Current = AppEnvironment.Dev;
#endif

    /// <summary>The names of the current edition (installer identity, data folder).</summary>
    public static EditionProfile Profile => EditionProfile.For(Current);
}
