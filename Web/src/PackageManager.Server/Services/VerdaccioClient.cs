using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PackageManager.Shared;

namespace PackageManager.Server.Services;

/// <summary>Read-only access to the Verdaccio registry, cached briefly so the UPM tab and search stay fast.</summary>
public class VerdaccioClient(HttpClient http, IMemoryCache cache, IOptions<VerdaccioOptions> options, HtmlContent html, ILogger<VerdaccioClient> log)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(2);

    public string Registry => options.Value.Url.TrimEnd('/');
    public string RegistryName => options.Value.RegistryName;

    public async Task<UpmListDto> ListAsync(CancellationToken ct)
    {
        if (cache.TryGetValue("upm:list", out UpmListDto? cached) && cached != null) return cached;
        try
        {
            // The web UI's list holds only packages published to this Verdaccio. /-/v1/search would also return
            // copies cached from an uplink (e.g. com.unity.* proxied from Unity's registry), which we don't show.
            using var doc = await GetJsonAsync("/-/verdaccio/data/packages", ct);
            var packages = doc.RootElement.EnumerateArray()
                .Where(p => Str(p, "name") != null)
                .Select(p => new UpmPackageSummaryDto
                {
                    Name = Str(p, "name")!,
                    DisplayName = Str(p, "displayName"),
                    Description = Str(p, "description"),
                    Version = Str(p, "version") ?? "",
                    Author = Person(p, "author"),
                    Keywords = p.TryGetProperty("keywords", out var kw) && kw.ValueKind == JsonValueKind.Array
                        ? kw.EnumerateArray().Select(k => k.GetString()).OfType<string>().ToList()
                        : [],
                    UpdatedAt = p.TryGetProperty("time", out var t) && t.ValueKind == JsonValueKind.String && t.TryGetDateTime(out var d)
                        ? d.ToUniversalTime() : null,
                })
                .ToList();

            var result = new UpmListDto { Registry = Registry, Available = true, Packages = packages };
            cache.Set("upm:list", result, CacheFor);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException)
        {
            log.LogWarning(ex, "Verdaccio list failed");
            return new UpmListDto { Registry = Registry, Available = false };
        }
    }

    /// <returns>null when the package does not exist.</returns>
    public async Task<UpmPackageDetailDto?> GetDetailAsync(string name, CancellationToken ct)
    {
        string key = "upm:pkg:" + name;
        if (cache.TryGetValue(key, out UpmPackageDetailDto? cached)) return cached;

        using var res = await http.GetAsync($"{Registry}/{Uri.EscapeDataString(name)}", ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = doc.RootElement;

        var times = root.TryGetProperty("time", out var t) && t.ValueKind == JsonValueKind.Object
            ? t.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(p => p.Name, p => p.Value.TryGetDateTime(out var d) ? d.ToUniversalTime() : (DateTime?)null)
            : [];

        var versions = new List<UpmVersionDto>();
        if (root.TryGetProperty("versions", out var vs) && vs.ValueKind == JsonValueKind.Object)
        {
            foreach (var v in vs.EnumerateObject())
            {
                versions.Add(new UpmVersionDto
                {
                    Version = v.Name,
                    PublishedAt = times.GetValueOrDefault(v.Name),
                    Unity = Str(v.Value, "unity"),
                    Dependencies = v.Value.TryGetProperty("dependencies", out var deps) && deps.ValueKind == JsonValueKind.Object
                        ? deps.EnumerateObject().ToDictionary(d => d.Name, d => d.Value.GetString() ?? "")
                        : [],
                });
            }
        }
        versions.Sort((a, b) => VersionComparer.Instance.Compare(b.Version, a.Version));

        string latest = root.TryGetProperty("dist-tags", out var tags) && Str(tags, "latest") is { } l ? l : versions.FirstOrDefault()?.Version ?? "";
        var latestJson = vs.ValueKind == JsonValueKind.Object && vs.TryGetProperty(latest, out var lj) ? lj : default;

        string? readme = Str(root, "readme");
        if (string.IsNullOrWhiteSpace(readme) && latestJson.ValueKind == JsonValueKind.Object) readme = Str(latestJson, "readme");
        // npm/Verdaccio placeholder for packages published without a README.
        if (readme?.Trim().StartsWith("ERROR: No README", StringComparison.OrdinalIgnoreCase) == true) readme = null;

        var detail = new UpmPackageDetailDto
        {
            Registry = Registry,
            RegistryName = RegistryName,
            RegistryPageUrl = $"{Registry}/-/web/detail/{Uri.EscapeDataString(Str(root, "name") ?? name)}",
            RepositoryUrl = RepositoryUrl(latestJson),
            Name = Str(root, "name") ?? name,
            DisplayName = Str(latestJson, "displayName"),
            Description = Str(latestJson, "description") ?? Str(root, "description"),
            LatestVersion = latest,
            Author = Person(latestJson, "author"),
            License = Str(latestJson, "license"),
            UpdatedAt = times.GetValueOrDefault("modified") ?? times.GetValueOrDefault(latest),
            Keywords = latestJson.ValueKind == JsonValueKind.Object && latestJson.TryGetProperty("keywords", out var kw) && kw.ValueKind == JsonValueKind.Array
                ? kw.EnumerateArray().Select(k => k.GetString()).OfType<string>().ToList()
                : [],
            ReadmeHtml = html.Markdown(readme),
            Versions = versions,
        };
        cache.Set(key, detail, CacheFor);
        return detail;
    }

    private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken ct)
    {
        using var res = await http.GetAsync(Registry + path, ct);
        res.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    /// <summary>package.json "repository" (string or { url }), else a GitHub "homepage", as a browsable https link.</summary>
    private static string? RepositoryUrl(JsonElement version)
    {
        if (version.ValueKind != JsonValueKind.Object) return null;
        string? raw = version.TryGetProperty("repository", out var repo)
            ? repo.ValueKind == JsonValueKind.String ? repo.GetString() : Str(repo, "url")
            : null;
        return ToBrowsableRepoUrl(raw)
            ?? (ToBrowsableRepoUrl(Str(version, "homepage")) is { } home && home.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ? home : null);
    }

    /// <summary>
    /// "git+https://github.com/a/b.git", "git@github.com:a/b.git", "github:a/b" or "a/b" → "https://github.com/a/b".
    /// Returns null for anything that does not end up as an http(s) URL.
    /// </summary>
    public static string? ToBrowsableRepoUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string url = raw.Trim();

        if (url.StartsWith("github:", StringComparison.OrdinalIgnoreCase)) url = "https://github.com/" + url[7..];
        else if (System.Text.RegularExpressions.Regex.IsMatch(url, @"^[\w.-]+/[\w.-]+$")) url = "https://github.com/" + url;
        else if (url.StartsWith("git@", StringComparison.Ordinal) && url.IndexOf(':') is > 4 and var colon)
            url = $"https://{url[4..colon]}/{url[(colon + 1)..]}";

        if (url.StartsWith("git+", StringComparison.Ordinal)) url = url[4..];
        if (url.StartsWith("git://", StringComparison.Ordinal)) url = "https://" + url[6..];
        if (url.StartsWith("ssh://git@", StringComparison.Ordinal)) url = "https://" + url[10..];

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return null;
        string path = uri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) path = path[..^4];
        return $"{uri.Scheme}://{uri.Host}{path}";
    }

    private static string? Person(JsonElement e, string property)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(property, out var p)) return null;
        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Object => Str(p, "name"),
            _ => null,
        };
    }

    private static string? Str(JsonElement e, string property) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
}
