using System.Net;
using System.Text;
using System.Text.Json;
using PackageManager.Shared;

namespace PackageManager.Server.Services;

public class AssetStoreException(string message) : Exception(message);

/// <summary>
/// Reads package metadata from a public Asset Store page. The page embeds a normalized cache
/// (data.ENTITY.Product / ProductVersion / ProductPublisher / Category) in its ReactDOMrender(...) call.
/// This is scraping: when Unity changes the page, <see cref="AssetStoreException"/> tells the admin to fill fields by hand.
/// </summary>
public class AssetStoreClient(HttpClient http, HtmlContent html, TimeProvider clock, ILogger<AssetStoreClient> log)
{
    public const string Host = "assetstore.unity.com";

    public static bool TryNormalizeUrl(string? url, out Uri uri, out string slug)
    {
        slug = "";
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out uri!) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !(uri.Host.Equals(Host, StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("www." + Host, StringComparison.OrdinalIgnoreCase)))
            return false;

        slug = uri.AbsolutePath.TrimEnd('/').Split('/').LastOrDefault() ?? "";
        if (!slug.Split('-').Last().All(char.IsDigit) || slug.Length == 0) return false;
        uri = new Uri($"https://{Host}{uri.AbsolutePath.TrimEnd('/')}");
        return true;
    }

    public async Task<PackageEditDto> FetchAsync(string url, CancellationToken ct)
    {
        if (!TryNormalizeUrl(url, out var uri, out string slug))
            throw new AssetStoreException("Link không hợp lệ. Dùng link dạng https://assetstore.unity.com/packages/…/ten-123456.");

        string page;
        try
        {
            using var res = await http.GetAsync(uri, ct);
            if (res.StatusCode == HttpStatusCode.NotFound) throw new AssetStoreException("Asset Store trả về 404 — kiểm tra lại link.");
            res.EnsureSuccessStatusCode();
            page = await res.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            throw new AssetStoreException($"Không tải được trang Asset Store ({ex.StatusCode?.ToString() ?? ex.Message}).");
        }

        try
        {
            return Parse(page, uri, slug);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            log.LogWarning(ex, "Could not parse Asset Store page {Url}", uri);
            throw new AssetStoreException("Không đọc được dữ liệu, hãy nhập tay. Trang Asset Store có thể đã đổi cấu trúc.");
        }
    }

    public PackageEditDto Parse(string page, Uri uri, string slug)
    {
        using var doc = ExtractState(page);
        var entity = doc.RootElement.GetProperty("data").GetProperty("ENTITY");
        string productId = slug.Split('-').Last();

        var products = entity.GetProperty("Product");
        var product = products.TryGetProperty(productId, out var p) ? p : products.EnumerateObject().First().Value;

        string name = Str(product, "name") ?? throw new InvalidOperationException("no name");
        string? publisher = Ref(entity, product, "publisher") is { } pub ? Str(pub, "name") : null;
        string? storeVersion = Ref(entity, product, "currentVersion") is { } ver ? Str(ver, "name") : null;
        string? category = Ref(entity, product, "category") is { } cat ? (Str(cat, "longName") ?? Str(cat, "name")) : null;

        var media = new List<MediaItemDto>();
        if (product.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array)
        {
            foreach (var img in images.EnumerateArray())
            {
                string? type = Str(img, "type"), imageUrl = Https(Str(img, "imageUrl"));
                if (imageUrl == null) continue;
                media.Add(new MediaItemDto
                {
                    Type = type == "youtube" ? "youtube" : "image",
                    Url = imageUrl,
                    ThumbnailUrl = Https(Str(img, "thumbnailUrl")),
                });
            }
        }

        string? mainImage = product.TryGetProperty("mainImage", out var mi) ? Https(Str(mi, "big") ?? Str(mi, "small")) : null;

        return new PackageEditDto
        {
            PackageId = PackageIds.Suggest(publisher, name),
            DisplayName = WebUtility.HtmlDecode(name),
            Publisher = publisher,
            Category = category?.Replace("/", " / "),
            UnityVersion = MinUnity(product),
            StoreVersion = storeVersion,
            ShortDescription = HtmlContent.ToPlainText(Str(product, "elevatorPitch")) is { Length: > 0 } pitch ? pitch : null,
            DescriptionHtml = html.Sanitize(Str(product, "description")),
            AssetStoreUrl = uri.ToString(),
            MainImageUrl = mainImage,
            Media = media,
            MetadataFetchedAt = clock.GetUtcNow().UtcDateTime,
        };
    }

    private static JsonDocument ExtractState(string page)
    {
        const string marker = "ReactDOMrender(";
        int entityAt = page.IndexOf("\"ENTITY\"", StringComparison.Ordinal);
        if (entityAt < 0) throw new InvalidOperationException("no ENTITY");
        int start = page.LastIndexOf(marker, entityAt, StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException("no render call");
        start += marker.Length;

        // Parse exactly one JSON value; the rest of the script follows it.
        var bytes = Encoding.UTF8.GetBytes(page.Substring(start));
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 256 });
        return JsonDocument.ParseValue(ref reader);
    }

    /// <summary>Follows a {"type":"id","id":["Kind","123"]} reference into data.ENTITY.</summary>
    private static JsonElement? Ref(JsonElement entity, JsonElement owner, string property)
    {
        if (!owner.TryGetProperty(property, out var r) || r.ValueKind != JsonValueKind.Object ||
            !r.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Array || id.GetArrayLength() != 2)
            return null;
        string? kind = id[0].GetString(), key = id[1].GetString();
        return kind != null && key != null && entity.TryGetProperty(kind, out var table) && table.TryGetProperty(key, out var value)
            ? value : null;
    }

    private static string? MinUnity(JsonElement product)
    {
        if (!product.TryGetProperty("supportedUnityVersions", out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        string? min = arr.EnumerateArray().Select(v => v.GetString()).Where(v => !string.IsNullOrEmpty(v))
            .OrderBy(v => v, VersionComparer.Instance).FirstOrDefault();
        if (min == null) return null;
        var parts = min.Split('.');
        return (parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : parts[0]) + "+";
    }

    private static string? Str(JsonElement e, string property) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static string? Https(string? url) =>
        string.IsNullOrWhiteSpace(url) ? null : url.StartsWith("//", StringComparison.Ordinal) ? "https:" + url : url;
}
