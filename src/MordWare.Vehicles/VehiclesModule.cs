namespace MordWare.Vehicles;

/// <summary>
/// Entry point of the Vehicles module (morVehicles). The module is a skeleton until roadmap item [2]:
/// <list type="bullet">
/// <item><c>Domain</c>: pure C# (no EF Core, no WinUI), unit-tested without a database.</item>
/// <item><c>UseCases</c>: application logic the UI calls.</item>
/// <item><c>Data</c>: the module's own DbContext and mappings (from foundation onward).</item>
/// </list>
/// See docs/architecture.md.
/// </summary>
public static class VehiclesModule
{
    /// <summary>The module's stable name, used in the architecture-test module list.</summary>
    public const string Name = "Vehicles";
}
