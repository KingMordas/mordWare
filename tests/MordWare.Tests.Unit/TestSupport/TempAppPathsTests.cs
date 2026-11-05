namespace MordWare.Tests.Unit.TestSupport;

public class TempAppPathsTests
{
    private static readonly string PackagesFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");

    [Fact]
    public void Folders_live_under_the_temp_root_and_never_under_the_packages_folder()
    {
        using var paths = new TempAppPaths();

        foreach (var folder in new[] { paths.DataFolder, paths.LogsFolder })
        {
            Assert.StartsWith(paths.Root, folder, StringComparison.OrdinalIgnoreCase);
            Assert.False(
                folder.StartsWith(PackagesFolder, StringComparison.OrdinalIgnoreCase),
                $"Test folder '{folder}' resolved under an installed package's storage.");
            Assert.True(Directory.Exists(folder));
        }
    }

    [Fact]
    public void Each_instance_gets_its_own_root_and_dispose_removes_it()
    {
        var first = new TempAppPaths();
        using var second = new TempAppPaths();
        Assert.NotEqual(first.Root, second.Root);

        first.Dispose();

        Assert.False(Directory.Exists(first.Root));
        Assert.True(Directory.Exists(second.Root));
    }

    [Fact]
    public void Tests_always_run_as_Dev()
    {
        using var paths = new TempAppPaths();
        Assert.Equal(MordWare.Core.Editions.AppEnvironment.Dev, paths.Environment);
    }
}
