using MordWare.Core.Editions;

namespace MordWare.Tests.Unit.Editions;

public class EditionProfileTests
{
    [Fact]
    public void Prod_names()
    {
        var prod = EditionProfile.For(AppEnvironment.Prod);
        Assert.Equal(AppEnvironment.Prod, prod.Environment);
        Assert.Equal("MordWare", prod.PackId);
        Assert.Equal("mordWare", prod.InstallerTitle);
        Assert.Equal("mordWare Data", prod.DataFolderName);
    }

    [Fact]
    public void Dev_names()
    {
        var dev = EditionProfile.For(AppEnvironment.Dev);
        Assert.Equal(AppEnvironment.Dev, dev.Environment);
        Assert.Equal("MordWare.Dev", dev.PackId);
        Assert.Equal("mordWare (DEV)", dev.InstallerTitle);
        Assert.Equal("mordWare Data (DEV)", dev.DataFolderName);
    }

    /// <summary>Spec scenario "Editions resolve different data folders": Windows paths ignore case.</summary>
    [Fact]
    public void Data_folders_differ_ignoring_case_and_neither_contains_the_other()
    {
        var prod = EditionProfile.Prod.DataFolderName;
        var dev = EditionProfile.Dev.DataFolderName;
        Assert.False(string.Equals(prod, dev, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(prod + Path.DirectorySeparatorChar, dev, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(dev + Path.DirectorySeparatorChar, prod, StringComparison.OrdinalIgnoreCase);
        Assert.False(dev.Contains(Path.DirectorySeparatorChar) || prod.Contains(Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// The data folders are siblings of the installer's program folders (<c>%LOCALAPPDATA%\&lt;PackId&gt;\</c>),
    /// never the same folder, so an uninstall or reinstall can never delete data.
    /// </summary>
    [Theory]
    [InlineData(AppEnvironment.Prod)]
    [InlineData(AppEnvironment.Dev)]
    public void Data_folder_is_never_an_install_folder(AppEnvironment environment)
    {
        var dataFolder = EditionProfile.For(environment).DataFolderName;
        Assert.DoesNotContain(
            new[] { EditionProfile.Prod.PackId, EditionProfile.Dev.PackId },
            packId => string.Equals(packId, dataFolder, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Pack_ids_and_titles_differ_per_edition()
    {
        Assert.False(string.Equals(EditionProfile.Prod.PackId, EditionProfile.Dev.PackId, StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(EditionProfile.Prod.InstallerTitle, EditionProfile.Dev.InstallerTitle);
    }

    [Fact]
    public void Unknown_edition_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => EditionProfile.For((AppEnvironment)42));
}
