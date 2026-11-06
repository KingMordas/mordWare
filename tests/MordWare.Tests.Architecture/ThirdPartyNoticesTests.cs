using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MordWare.Tests.Architecture;

/// <summary>
/// Guardrail 1 and spec "Legal texts ship with every release": every package whose files the App ships
/// has an entry ("Package: &lt;id&gt;") in THIRD-PARTY-NOTICES.txt, so a new dependency cannot ship unlisted.
/// Checked against the App's PackageReferences, the known transitive redistributables, and every package
/// the App's restore resolved (obj/project.assets.json).
/// </summary>
public partial class ThirdPartyNoticesTests
{
    /// <summary>Packages used only at build time: nothing of them is redistributed.</summary>
    private static readonly string[] BuildOnly =
    [
        "Microsoft.Windows.SDK.BuildTools",
        "Microsoft.Windows.SDK.BuildTools.MSIX",
    ];

    /// <summary>
    /// Redistributables the App ships without a PackageReference of its own: the .NET runtime pack, the
    /// Windows SDK projection (and the C#/WinRT runtime in it), and WebView2 (a WinUI dependency).
    /// </summary>
    private static readonly string[] KnownTransitiveRedistributables =
    [
        "Microsoft.NETCore.App.Runtime.win-x64",
        "Microsoft.Windows.SDK.NET.Ref",
        "Microsoft.Web.WebView2",
    ];

    private static readonly string Root = FindRepositoryRoot();

    private static readonly string AppFolder = Path.Combine(Root, "src", "MordWare.App");

    [Fact]
    public void Every_App_package_reference_is_listed()
    {
        var references = XDocument.Load(Path.Combine(AppFolder, "MordWare.App.csproj"))
            .Descendants("PackageReference")
            .Select(e => (string?)e.Attribute("Include"))
            .OfType<string>()
            .ToArray();

        Assert.NotEmpty(references);
        AssertListed(references.Except(BuildOnly, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Every_known_transitive_redistributable_is_listed() =>
        AssertListed(KnownTransitiveRedistributables);

    [Fact]
    public void Every_package_the_App_restores_is_listed()
    {
        var assets = Path.Combine(AppFolder, "obj", "project.assets.json");
        Assert.True(File.Exists(assets), "Restore the App before running the architecture tests: " + assets);

        using var json = JsonDocument.Parse(File.ReadAllText(assets));
        var packages = json.RootElement.GetProperty("libraries").EnumerateObject()
            .Where(l => l.Value.GetProperty("type").GetString() == "package")
            .Select(l => l.Name.Split('/')[0])
            .Except(BuildOnly, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(packages);
        AssertListed(packages);
    }

    private static void AssertListed(IEnumerable<string> packages)
    {
        var listed = ListedPackages();
        var missing = packages.Where(p => !listed.Contains(p)).ToArray();
        Assert.True(missing.Length == 0, "Missing from THIRD-PARTY-NOTICES.txt (add a 'Package:' entry with its license): " + string.Join(", ", missing));
    }

    /// <summary>The package ids named on "Package: &lt;id&gt;" lines (anything after the id is a comment).</summary>
    private static HashSet<string> ListedPackages()
    {
        var text = File.ReadAllText(Path.Combine(Root, "THIRD-PARTY-NOTICES.txt"));
        return PackageLine().Matches(text).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^\s*Package:\s*([A-Za-z0-9_.-]+)", RegexOptions.Multiline)]
    private static partial Regex PackageLine();

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MordWare.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (MordWare.slnx) not found.");
    }
}
