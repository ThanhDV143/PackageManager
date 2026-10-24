using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PackageManager.Server.Data;
using PackageManager.Shared;

namespace PackageManager.Server.Services;

public partial class PackageService(AppDbContext db, HtmlContent html, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public DateTime Now => clock.GetUtcNow().UtcDateTime;

    public static bool IsValidPackageId(string? id) => id != null && PackageIdRegex().IsMatch(id);

    public static bool IsValidVersion(string? version) =>
        !string.IsNullOrWhiteSpace(version) && version.Length <= 64 && version.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or '+');

    public async Task<PagedResult<PackageSummaryDto>> ListAsync(string? sort, int page, int pageSize, CancellationToken ct)
    {
        // The library is small (tens to hundreds of packages) and version order needs VersionComparer,
        // so sort in memory.
        var all = (await db.Packages.AsNoTracking().Include(p => p.Versions).ToListAsync(ct)).Select(ToSummary);
        all = sort switch
        {
            PackageSort.Name => all.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            PackageSort.Versions => all.OrderByDescending(p => p.VersionCount).ThenByDescending(p => p.UpdatedAt),
            _ => all.OrderByDescending(p => p.UpdatedAt),
        };
        var list = all.ToList();
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);
        return new PagedResult<PackageSummaryDto>
        {
            Items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            Total = list.Count,
            Page = page,
            PageSize = pageSize,
        };
    }

    public Task<Package?> FindAsync(string packageId, CancellationToken ct) =>
        db.Packages.Include(p => p.Versions).FirstOrDefaultAsync(p => p.PackageId == packageId, ct);

    public async Task<List<PackageLookupDto>> LookupAsync(CancellationToken ct) =>
        (await db.Packages.AsNoTracking().Select(p => new PackageLookupDto { PackageId = p.PackageId, DisplayName = p.DisplayName }).ToListAsync(ct))
        .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();

    /// <summary>Finds the package a guessed file name most likely belongs to.</summary>
    public static PackageLookupDto? Match(string guessedName, IReadOnlyList<PackageLookupDto> packages)
    {
        string n = FileNameParser.Normalize(guessedName);
        if (n.Length == 0) return null;

        var exact = packages.FirstOrDefault(p =>
            FileNameParser.Normalize(p.DisplayName) == n ||
            FileNameParser.Normalize(p.PackageId.Split('.').Last()) == n);
        if (exact != null || n.Length < 4) return exact;

        // "Odin Inspector" → "Odin Inspector and Serializer"
        return packages.Where(p => FileNameParser.Normalize(p.DisplayName).StartsWith(n, StringComparison.Ordinal))
            .OrderBy(p => p.DisplayName.Length).FirstOrDefault();
    }

    public static PackageSummaryDto ToSummary(Package p)
    {
        var latest = Latest(p.Versions);
        return new PackageSummaryDto
        {
            PackageId = p.PackageId,
            DisplayName = p.DisplayName,
            Publisher = p.Publisher,
            ShortDescription = p.ShortDescription,
            MainImageUrl = p.MainImageUrl,
            LatestVersion = latest?.Version,
            LatestVersionId = latest?.Id,
            LatestSize = latest?.Size,
            VersionCount = p.Versions.Count,
            UpdatedAt = AsUtc(p.Versions.Count > 0 ? p.Versions.Max(v => v.UploadedAt) : p.CreatedAt),
        };
    }

    public static PackageDetailDto ToDetail(Package p)
    {
        var latest = Latest(p.Versions);
        var dto = new PackageDetailDto
        {
            AssetStoreSlug = p.AssetStoreUrl != null && AssetStoreClient.TryNormalizeUrl(p.AssetStoreUrl, out _, out string slug) ? slug : null,
            UpdatedAt = AsUtc(p.UpdatedAt),
            Versions = p.Versions
                .OrderByDescending(v => v.Version, VersionComparer.Instance).ThenByDescending(v => v.UploadedAt)
                .Select(v => ToVersion(v, v == latest)).ToList(),
        };
        CopyEditable(p, dto);
        return dto;
    }

    public static PackageVersionDto ToVersion(PackageVersion v, bool isLatest) => new()
    {
        Id = v.Id,
        Version = v.Version,
        FileName = v.FileName,
        Size = v.Size,
        UnityVersion = v.UnityVersion,
        ReleaseNotes = v.ReleaseNotes,
        UploadedAt = AsUtc(v.UploadedAt),
        IsLatest = isLatest,
    };

    public static PackageVersion? Latest(IEnumerable<PackageVersion> versions) =>
        versions.OrderByDescending(v => v.Version, VersionComparer.Instance).ThenByDescending(v => v.UploadedAt).FirstOrDefault();

    /// <summary>Validates and copies editable fields. Returns an error message, or null on success.</summary>
    public async Task<string?> ApplyAsync(Package p, PackageEditDto dto, CancellationToken ct)
    {
        string id = dto.PackageId.Trim().ToLowerInvariant();
        if (!IsValidPackageId(id)) return "ID chỉ gồm chữ thường, số, dấu chấm, gạch ngang (vd. com.moremountains.feel).";
        if (string.IsNullOrWhiteSpace(dto.DisplayName)) return "Tên hiển thị không được để trống.";
        if (!string.Equals(id, p.PackageId, StringComparison.OrdinalIgnoreCase) &&
            await db.Packages.AnyAsync(x => x.PackageId == id && x.Id != p.Id, ct))
            return $"ID \"{id}\" đã tồn tại.";
        if (!string.IsNullOrWhiteSpace(dto.AssetStoreUrl) && !AssetStoreClient.TryNormalizeUrl(dto.AssetStoreUrl, out _, out _))
            return "Link Asset Store không hợp lệ.";

        p.PackageId = id;
        p.DisplayName = Limit(dto.DisplayName, 200)!;
        p.Publisher = Limit(dto.Publisher, 200);
        p.Category = Limit(dto.Category, 200);
        p.UnityVersion = Limit(dto.UnityVersion, 50);
        p.StoreVersion = Limit(dto.StoreVersion, 50);
        p.ShortDescription = Limit(dto.ShortDescription, 1000);
        p.DescriptionHtml = html.Sanitize(dto.DescriptionHtml);
        p.AssetStoreUrl = string.IsNullOrWhiteSpace(dto.AssetStoreUrl) ? null : dto.AssetStoreUrl.Trim();
        p.MainImageUrl = SafeUrl(dto.MainImageUrl);
        var media = dto.Media.Where(m => SafeUrl(m.Url) != null)
            .Select(m => new MediaItemDto { Type = m.Type == "youtube" ? "youtube" : "image", Url = m.Url, ThumbnailUrl = SafeUrl(m.ThumbnailUrl) })
            .Take(50).ToList();
        p.MediaJson = media.Count > 0 ? JsonSerializer.Serialize(media, Json) : null;
        p.MetadataFetchedAt = dto.MetadataFetchedAt;
        p.UpdatedAt = Now;
        return null;
    }

    private static void CopyEditable(Package p, PackageEditDto dto)
    {
        dto.PackageId = p.PackageId;
        dto.DisplayName = p.DisplayName;
        dto.Publisher = p.Publisher;
        dto.Category = p.Category;
        dto.UnityVersion = p.UnityVersion;
        dto.StoreVersion = p.StoreVersion;
        dto.ShortDescription = p.ShortDescription;
        dto.DescriptionHtml = p.DescriptionHtml;
        dto.AssetStoreUrl = p.AssetStoreUrl;
        dto.MainImageUrl = p.MainImageUrl;
        dto.Media = string.IsNullOrEmpty(p.MediaJson) ? [] : JsonSerializer.Deserialize<List<MediaItemDto>>(p.MediaJson, Json) ?? [];
        dto.MetadataFetchedAt = p.MetadataFetchedAt is { } f ? AsUtc(f) : null;
    }

    private static string? SafeUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp) ? u.ToString() : null;

    private static string? Limit(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }

    /// <summary>SQLite returns DateTime with Kind=Unspecified; everything is stored as UTC.</summary>
    public static DateTime AsUtc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,199}$")]
    private static partial Regex PackageIdRegex();
}
