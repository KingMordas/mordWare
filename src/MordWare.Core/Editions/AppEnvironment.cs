namespace MordWare.Core.Editions;

/// <summary>The edition of mordWare that is running. See docs/architecture.md, "Editions".</summary>
public enum AppEnvironment
{
    /// <summary>The development edition (package <c>MordWare.Dev</c>, shown as "mordWare (DEV)").</summary>
    Dev,

    /// <summary>The real edition the owner uses (package <c>MordWare</c>, shown as "mordWare").</summary>
    Prod,
}
