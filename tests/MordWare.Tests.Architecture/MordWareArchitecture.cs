using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using Assembly = System.Reflection.Assembly;

namespace MordWare.Tests.Architecture;

/// <summary>
/// Loads every mordWare assembly once for all architecture rules. Libraries come from project references;
/// the WinUI App and the unit-test assembly are read from their build output by path.
/// </summary>
public static class MordWareArchitecture
{
    /// <summary>
    /// Every feature module. Adding a module means adding its name here (CLAUDE.md definition of done):
    /// the rules then apply to it automatically.
    /// </summary>
    public static readonly IReadOnlyList<string> Modules = ["Vehicles"];

    /// <summary>Shared assemblies a module may depend on.</summary>
    public static readonly IReadOnlyList<string> SharedLibraries = ["MordWare.Core", "MordWare.Persistence", "MordWare.Integrations"];

    public const string App = "MordWare.App";

    /// <summary>Test assemblies whose dependencies are checked (rule 4).</summary>
    public static readonly IReadOnlyList<string> TestAssemblies = ["MordWare.Tests.Unit", "MordWare.Tests.Architecture"];

    public static string ModuleAssembly(string module) => $"MordWare.{module}";

    public static readonly ArchUnitNET.Domain.Architecture Instance = Load();

    private static ArchUnitNET.Domain.Architecture Load()
    {
        var libraries = new[]
        {
            typeof(MordWare.Core.Editions.AppEnvironment).Assembly,
            typeof(MordWare.Vehicles.VehiclesModule).Assembly,
            Assembly.Load("MordWare.Persistence"),
            Assembly.Load("MordWare.Integrations"),
            typeof(MordWareArchitecture).Assembly,
        };

        var byPath = new[]
        {
            FindBuildOutput(Path.Combine("src", "MordWare.App"), "MordWare.App.dll"),
            FindBuildOutput(Path.Combine("tests", "MordWare.Tests.Unit"), "MordWare.Tests.Unit.dll"),
        };

        var loader = new ArchLoader().LoadAssemblies(libraries);

        // By file path, never Assembly.LoadFile: ArchUnitNET reads metadata only, so the WinUI App
        // (self-contained, x64) is read without loading or running any of its code.
        foreach (var path in byPath)
        {
            loader = loader.LoadFilteredDirectory(Path.GetDirectoryName(path)!, Path.GetFileName(path));
        }

        return loader.Build();
    }

    /// <summary>
    /// Finds <paramref name="fileName"/> under the project's bin folder for the same configuration
    /// (Debug/Release) this test assembly was built in: either <c>bin/&lt;Configuration&gt;/</c> or,
    /// when the solution builds the project for a platform (the App is mapped to x64),
    /// <c>bin/&lt;Platform&gt;/&lt;Configuration&gt;/</c>. The newest match wins.
    /// </summary>
    internal static string FindBuildOutput(string projectFolder, string fileName)
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);   // .../bin/<Configuration>/<tfm>/
        var configuration = here.Parent!.Name;
        var root = here;
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "MordWare.slnx")))
        {
            root = root.Parent;
        }

        if (root is null)
        {
            throw new InvalidOperationException("Repository root (MordWare.slnx) not found above " + here.FullName);
        }

        var bin = Path.Combine(root.FullName, projectFolder, "bin");
        var configurationFolders = new List<string> { Path.Combine(bin, configuration) };
        if (Directory.Exists(bin))
        {
            configurationFolders.AddRange(Directory.EnumerateDirectories(bin)
                .Select(platform => Path.Combine(platform, configuration)));
        }

        var match = configurationFolders
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, fileName, SearchOption.AllDirectories))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return match ?? throw new InvalidOperationException(
            $"{fileName} not found under {Path.Combine(bin, configuration)} or {Path.Combine(bin, "<platform>", configuration)}. " +
            $"Build the solution ({configuration}) before running the architecture tests.");
    }
}
