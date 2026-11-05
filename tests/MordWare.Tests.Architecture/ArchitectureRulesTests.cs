using ArchUnitNET.Fluent;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static MordWare.Tests.Architecture.MordWareArchitecture;

namespace MordWare.Tests.Architecture;

/// <summary>The module-boundary rules of design D7 (spec: development-workflow). See docs/architecture.md.</summary>
public class ArchitectureRulesTests
{
    private static readonly string[] AllModuleAssemblies = Modules.Select(ModuleAssembly).ToArray();

    public static TheoryData<string> ModuleNames => new(Modules);

    /// <summary>Regex matching exactly the given assembly names (ArchUnitNET matches the full assembly name).</summary>
    private static string Exactly(params IEnumerable<string> names) =>
        "^(" + string.Join("|", names.Select(System.Text.RegularExpressions.Regex.Escape)) + ")(,.*)?$";

    [Fact]
    public void Rule1_Core_depends_on_no_other_mordWare_assembly()
    {
        IArchRule rule = Types().That().ResideInAssemblyMatching(Exactly("MordWare.Core"))
            .Should().NotDependOnAnyTypesThat().ResideInAssemblyMatching(@"^MordWare\.(?!Core(,|$))[^,]+(,.*)?$")
            .Because("Core is the bottom layer shared by every module");
        ArchRuleAssert.HasNoViolations(rule);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Rule2_a_module_depends_only_on_shared_libraries(string module)
    {
        var self = ModuleAssembly(module);
        var forbidden = AllModuleAssemblies.Where(a => a != self).Append(App).ToArray();
        IArchRule rule = Types().That().ResideInAssemblyMatching(Exactly(self))
            .Should().NotDependOnAnyTypesThat().ResideInAssemblyMatching(Exactly(forbidden))
            .Because("modules talk to each other only through contracts owned by the providing module, and never reach into the app");
        ArchRuleAssert.HasNoViolations(rule);
    }

    [Fact]
    public void Rule3_domain_code_uses_neither_the_database_nor_the_UI_framework()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^MordWare\.\w+\.Domain(\..+)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^(Microsoft\.EntityFrameworkCore|Microsoft\.UI|Windows)(\..+)?$")
            .Because("domain logic is pure C#, unit-tested without a database or a window");
        ArchRuleAssert.HasNoViolations(rule);
    }

    [Fact]
    public void Rule4_tests_never_resolve_the_real_data_location()
    {
        IArchRule rule = Types().That().ResideInAssemblyMatching(Exactly(TestAssemblies))
            .Should().NotDependOnAnyTypesThat().HaveFullName("MordWare.App.Hosting.PackagedAppPaths")
            .AndShould().NotDependOnAnyTypesThat().HaveFullName("MordWare.App.Hosting.AppHost")
            .Because("tests use a temporary folder and must never touch an installed edition's data");
        ArchRuleAssert.HasNoViolations(rule);
    }

    [Fact]
    public void Rule5_shared_infrastructure_depends_on_no_module()
    {
        IArchRule rule = Types().That().ResideInAssemblyMatching(Exactly("MordWare.Persistence", "MordWare.Integrations"))
            .Should().NotDependOnAnyTypesThat().ResideInAssemblyMatching(Exactly(AllModuleAssemblies.Append(App)))
            .Because("shared infrastructure sits below the modules")
            .WithoutRequiringPositiveResults(); // Persistence and Integrations are still empty skeletons
        ArchRuleAssert.HasNoViolations(rule);
    }
}
