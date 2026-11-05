namespace S1.Encryption;

public class TempFolderTests
{
    [Fact]
    public void TempFolder_is_created_and_deleted()
    {
        string path;
        using (var temp = new TempFolder())
        {
            path = temp.Path;
            Assert.True(Directory.Exists(path));
        }
        Assert.False(Directory.Exists(path));
    }
}
