using PackageManager.Shared;

namespace PackageManager.Tests;

public class FileNameParserTests
{
    [Theory]
    [InlineData("Feel v5.8.unitypackage", "Feel", "5.8")]
    [InlineData("Feel_5.5.unitypackage", "Feel", "5.5")]
    [InlineData("FMOD for Unity 2.02.30.unitypackage", "FMOD for Unity", "2.02.30")]
    [InlineData("Odin Inspector 3.3.1.11.unitypackage", "Odin Inspector", "3.3.1.11")]
    [InlineData("Rewired_1.1.49.unitypackage", "Rewired", "1.1.49")]
    [InlineData("NewTool_v1.0.unitypackage", "NewTool", "1.0")]
    [InlineData("Tool-v2.unitypackage", "Tool", "2")]
    [InlineData("backup (1).unitypackage", "backup", null)]
    [InlineData("backup_old.unitypackage", "backup old", null)]
    [InlineData("FMOD for Unity 202.unitypackage", "FMOD for Unity 202", null)]
    [InlineData("1.2.3.unitypackage", "1.2.3", "1.2.3")]
    public void Parse_GuessesNameAndVersion(string file, string name, string? version)
    {
        var parsed = FileNameParser.Parse(file);
        Assert.Equal(name, parsed.Name);
        Assert.Equal(version, parsed.Version);
    }

    [Fact]
    public void Normalize_KeepsLettersAndDigitsOnly() =>
        Assert.Equal("hotreloadeditcode", FileNameParser.Normalize("Hot Reload | Edit_Code"));
}

public class VersionComparerTests
{
    [Theory]
    [InlineData("2.02.30", "2.02.4")]
    [InlineData("1.10", "1.9")]
    [InlineData("5.8", "5.7.9")]
    [InlineData("1.0.1", "1.0-beta")]
    [InlineData("3.3.1.11", "3.3.1.2")]
    public void Compare_OrdersNumericClusters(string higher, string lower)
    {
        Assert.True(VersionComparer.Instance.Compare(higher, lower) > 0);
        Assert.True(VersionComparer.Instance.Compare(lower, higher) < 0);
    }

    [Fact]
    public void Compare_EqualForLeadingZeros() => Assert.Equal(0, VersionComparer.Instance.Compare("1.02", "1.2"));
}

public class PackageIdsTests
{
    [Theory]
    [InlineData("More Mountains", "Feel", "com.moremountains.feel")]
    [InlineData(null, "NewTool", "local.newtool")]
    [InlineData("Sirenix", "Odin Inspector and Serializer", "com.sirenix.odininspectorandserializer")]
    public void Suggest_BuildsReverseDomainId(string? publisher, string name, string expected) =>
        Assert.Equal(expected, PackageIds.Suggest(publisher, name));
}
