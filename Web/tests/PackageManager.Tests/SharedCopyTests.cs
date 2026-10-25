namespace PackageManager.Tests;

/// <summary>The Unity package compiles its own copy of these Shared files; they must not drift apart.</summary>
public class SharedCopyTests
{
    private const string SharedDir = "Web/src/PackageManager.Shared";
    private const string UnityCopyDir = "Unity.PackageManager/Packages/com.thanhdv.packagemanager/Editor/Shared";

    [Theory]
    [InlineData("Dtos.cs")]
    [InlineData("VersionComparer.cs")]
    public void UnityCopy_MatchesShared(string file)
    {
        string root = RepoRoot();
        string shared = Read(Path.Combine(root, SharedDir, file));
        string copy = Read(Path.Combine(root, UnityCopyDir, file));
        Assert.True(shared == copy, $"{UnityCopyDir}/{file} differs from {SharedDir}/{file} — copy the Shared file over it.");
    }

    // Line endings depend on git's autocrlf on each machine, so compare without them.
    private static string Read(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, SharedDir)) && Directory.Exists(Path.Combine(dir.FullName, UnityCopyDir)))
                return dir.FullName;
        throw new DirectoryNotFoundException("Repository root not found above " + AppContext.BaseDirectory);
    }
}
