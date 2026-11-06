namespace MordWare.Core.Editions;

/// <summary>
/// The edition of mordWare that is running, fixed by the build: Release builds are PROD, every other
/// configuration is DEV. Nothing at run time can change it. Its names come from
/// <see cref="EditionProfile.For"/>. See docs/architecture.md, "Editions".
/// </summary>
public enum AppEnvironment
{
    /// <summary>The development edition, shown as "mordWare (DEV)", with data in "mordWare Data (DEV)".</summary>
    Dev,

    /// <summary>The real edition users install, shown as "mordWare", with data in "mordWare Data".</summary>
    Prod,
}
