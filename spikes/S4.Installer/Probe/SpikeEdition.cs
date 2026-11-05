namespace S4.Installer.Probe;

/// <summary>
/// Design D4: the edition is a build constant (Release = PROD-like, every other configuration =
/// DEV-like), never a runtime switch. Spike names only, never MordWare / MordWare.Dev.
/// </summary>
public static class SpikeEdition
{
#if MORDWARE_PROD
    public const bool IsProd = true;
    public const string Name = "PROD";
    public const string PackId = "MordWare.Spike.S4";
    public const string Title = "mordWare S4 spike";
#else
    public const bool IsProd = false;
    public const string Name = "DEV";
    public const string PackId = "MordWare.Spike.S4.Dev";
    public const string Title = "mordWare S4 spike (DEV)";
#endif
}
