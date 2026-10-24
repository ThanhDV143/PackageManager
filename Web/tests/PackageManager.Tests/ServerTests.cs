using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PackageManager.Server;
using PackageManager.Server.Services;
using PackageManager.Shared;

namespace PackageManager.Tests;

public class AssetStoreParseTests
{
    // Shape of the state the Asset Store page passes to ReactDOMrender(...), trimmed to what the parser reads.
    private const string Page = """
        <html><script>document.addEventListener("DOMContentLoaded", function() {var c = window["X"];
        c.ReactDOMrender({"data":{"header":{"x":1},"ENTITY":{
          "Product":{"183370":{"id":"183370","name":"Feel","slug":"feel-183370",
            "description":"<p>&#128293; <strong>Juicy</strong></p><script>alert(1)</script><a href=\"//example.com/x\">link</a>",
            "elevatorPitch":"Feel is the best way to improve your game feel.",
            "supportedUnityVersions":["2022.3.23","2019.4.3","6000.0.23"],
            "mainImage":{"big":"//cdn.example.com/key.jpg"},
            "images":[{"type":"youtube","imageUrl":"https://www.youtube.com/embed/abc","thumbnailUrl":"//cdn.example.com/t.png"},
                      {"type":"screenshot","imageUrl":"//cdn.example.com/s1.jpg","thumbnailUrl":"//cdn.example.com/s1_t.jpg"}],
            "currentVersion":{"type":"id","id":["ProductVersion","1523792"]},
            "publisher":{"type":"id","id":["ProductPublisher","10305"]},
            "category":{"type":"id","id":["Category","108"]}}},
          "ProductVersion":{"1523792":{"name":"6.3"}},
          "ProductPublisher":{"10305":{"name":"More Mountains"}},
          "Category":{"108":{"name":"Particles & Effects","longName":"Tools/Particles & Effects"}}
        }}}, document.getElementById("app"));});</script></html>
        """;

    [Fact]
    public void Parse_ReadsMetadataFromEmbeddedState()
    {
        var client = new AssetStoreClient(new HttpClient(), new HtmlContent(), TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<AssetStoreClient>.Instance);
        Assert.True(AssetStoreClient.TryNormalizeUrl("https://assetstore.unity.com/packages/tools/particles-effects/feel-183370?x=1", out var uri, out var slug));

        var dto = client.Parse(Page, uri, slug);

        Assert.Equal("com.moremountains.feel", dto.PackageId);
        Assert.Equal("Feel", dto.DisplayName);
        Assert.Equal("More Mountains", dto.Publisher);
        Assert.Equal("Tools / Particles & Effects", dto.Category);
        Assert.Equal("6.3", dto.StoreVersion);
        Assert.Equal("2019.4+", dto.UnityVersion);
        Assert.Equal("https://cdn.example.com/key.jpg", dto.MainImageUrl);
        Assert.Equal("https://assetstore.unity.com/packages/tools/particles-effects/feel-183370", dto.AssetStoreUrl);
        Assert.Equal(2, dto.Media.Count);
        Assert.Equal("youtube", dto.Media[0].Type);
        Assert.Equal("https://cdn.example.com/s1.jpg", dto.Media[1].Url);
        Assert.DoesNotContain("<script", dto.DescriptionHtml);
        Assert.Contains("target=\"_blank\"", dto.DescriptionHtml);
    }

    [Theory]
    [InlineData("http://assetstore.unity.com/packages/tools/feel-183370")]
    [InlineData("https://evil.example.com/packages/tools/feel-183370")]
    [InlineData("https://assetstore.unity.com/packages/tools/feel")]
    [InlineData("not a url")]
    public void TryNormalizeUrl_RejectsOtherHostsAndShapes(string url) =>
        Assert.False(AssetStoreClient.TryNormalizeUrl(url, out _, out _));
}

public class FileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pm-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FileStore _store;

    public FileStoreTests()
    {
        _store = new FileStore(Options.Create(new StorageOptions { PackagesPath = _root }), new FakeEnv(Path.GetTempPath()));
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("sub/../../secret.txt")]
    [InlineData("")]
    public void Resolve_RejectsPathsOutsideRoot(string path) => Assert.Null(_store.Resolve(path));

    [Fact]
    public void Resolve_AcceptsNestedPath() => Assert.NotNull(_store.Resolve("sub/Feel v5.8.unitypackage"));

    [Fact]
    public async Task SaveAsync_SanitizesNameAndAvoidsOverwrite()
    {
        var first = await _store.SaveAsync("../../Feel v5.8.unitypackage", new MemoryStream([1, 2, 3]), default);
        var second = await _store.SaveAsync("Feel v5.8.unitypackage", new MemoryStream([4]), default);

        Assert.Equal(_root, first.DirectoryName);
        Assert.Equal("Feel v5.8.unitypackage", first.Name);
        Assert.Equal("Feel v5.8 (1).unitypackage", second.Name);
        Assert.Equal(3, first.Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeEnv(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

public class PackageMatchTests
{
    private static readonly List<PackageLookupDto> Packages =
    [
        new() { PackageId = "com.moremountains.feel", DisplayName = "Feel" },
        new() { PackageId = "com.sirenix.odin", DisplayName = "Odin Inspector and Serializer" },
        new() { PackageId = "com.firelight.fmod", DisplayName = "FMOD for Unity" },
    ];

    [Theory]
    [InlineData("Feel", "com.moremountains.feel")]
    [InlineData("feel", "com.moremountains.feel")]
    [InlineData("Odin Inspector", "com.sirenix.odin")]
    [InlineData("fmod", "com.firelight.fmod")]
    [InlineData("Rewired", null)]
    [InlineData("Fe", null)]
    public void Match_FindsBestPackage(string name, string? expected) =>
        Assert.Equal(expected, PackageService.Match(name, Packages)?.PackageId);
}
