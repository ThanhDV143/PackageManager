using System;
using System.Collections.Generic;

namespace PackageManager.Shared
{
    public static class PackageSort
    {
        public const string Updated = "updated";
        public const string Name = "name";
        public const string Versions = "versions";
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    // ---------- .unitypackage ----------

    public class PackageSummaryDto
    {
        public string PackageId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Publisher { get; set; }
        public string? ShortDescription { get; set; }
        public string? MainImageUrl { get; set; }
        public string? LatestVersion { get; set; }
        public int? LatestVersionId { get; set; }
        public long? LatestSize { get; set; }
        public int VersionCount { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class MediaItemDto
    {
        /// <summary>"image" or "youtube".</summary>
        public string Type { get; set; } = "image";
        public string Url { get; set; } = "";
        public string? ThumbnailUrl { get; set; }
    }

    public class PackageVersionDto
    {
        public int Id { get; set; }
        public string Version { get; set; } = "";
        public string FileName { get; set; } = "";
        public long Size { get; set; }
        public string? UnityVersion { get; set; }
        public string? ReleaseNotes { get; set; }
        public DateTime UploadedAt { get; set; }
        public bool IsLatest { get; set; }
    }

    /// <summary>Editable package metadata (create / edit dialog, Asset Store fill).</summary>
    public class PackageEditDto
    {
        public string PackageId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Publisher { get; set; }
        public string? Category { get; set; }
        public string? UnityVersion { get; set; }
        public string? StoreVersion { get; set; }
        public string? ShortDescription { get; set; }
        public string? DescriptionHtml { get; set; }
        public string? AssetStoreUrl { get; set; }
        public string? MainImageUrl { get; set; }
        public List<MediaItemDto> Media { get; set; } = new List<MediaItemDto>();
        public DateTime? MetadataFetchedAt { get; set; }
    }

    public class PackageDetailDto : PackageEditDto
    {
        public string? AssetStoreSlug { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<PackageVersionDto> Versions { get; set; } = new List<PackageVersionDto>();
    }

    public class PackageLookupDto
    {
        public string PackageId { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }

    // ---------- Upload ----------

    public class UploadResolveRequest
    {
        public List<string> FileNames { get; set; } = new List<string>();
    }

    public class UploadResolveItem
    {
        public string FileName { get; set; } = "";
        public string GuessedName { get; set; } = "";
        public string? GuessedVersion { get; set; }
        public string? MatchPackageId { get; set; }
        public string? MatchDisplayName { get; set; }
        public bool VersionExists { get; set; }
    }

    // ---------- Unmanaged files ----------

    public class UnmanagedFileDto
    {
        public string RelativePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string? GuessedVersion { get; set; }
        public long Size { get; set; }
        public DateTime ModifiedAt { get; set; }
    }

    public class UnmanagedGroupDto
    {
        public string Name { get; set; } = "";
        public string? MatchPackageId { get; set; }
        public string? MatchDisplayName { get; set; }
        public List<UnmanagedFileDto> Files { get; set; } = new List<UnmanagedFileDto>();
    }

    public class UnmanagedScanDto
    {
        public string Folder { get; set; } = "";
        public DateTime ScannedAt { get; set; }
        public int TotalFiles { get; set; }
        public List<UnmanagedGroupDto> Groups { get; set; } = new List<UnmanagedGroupDto>();
    }

    public class AssignFileItem
    {
        public string RelativePath { get; set; } = "";
        public string Version { get; set; } = "";
    }

    public class AssignFilesRequest
    {
        public string PackageId { get; set; } = "";
        public List<AssignFileItem> Files { get; set; } = new List<AssignFileItem>();
    }

    public class IgnoreFilesRequest
    {
        public List<string> RelativePaths { get; set; } = new List<string>();
    }

    // ---------- UPM (Verdaccio, read-only) ----------

    public class UpmPackageSummaryDto
    {
        public string Name { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public string Version { get; set; } = "";
        public string? Author { get; set; }
        public List<string> Keywords { get; set; } = new List<string>();
        public DateTime? UpdatedAt { get; set; }
    }

    public class UpmListDto
    {
        public string Registry { get; set; } = "";
        public bool Available { get; set; }
        public List<UpmPackageSummaryDto> Packages { get; set; } = new List<UpmPackageSummaryDto>();
    }

    public class UpmVersionDto
    {
        public string Version { get; set; } = "";
        public DateTime? PublishedAt { get; set; }
        public string? Unity { get; set; }
        public Dictionary<string, string> Dependencies { get; set; } = new Dictionary<string, string>();
    }

    public class UpmPackageDetailDto
    {
        public string Registry { get; set; } = "";
        public string RegistryName { get; set; } = "";
        /// <summary>The package's page in the Verdaccio web UI.</summary>
        public string RegistryPageUrl { get; set; } = "";
        /// <summary>Browsable source repository (from package.json "repository"), when there is one.</summary>
        public string? RepositoryUrl { get; set; }
        public string Name { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public string LatestVersion { get; set; } = "";
        public string? Author { get; set; }
        public string? License { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<string> Keywords { get; set; } = new List<string>();
        public string? ReadmeHtml { get; set; }
        /// <summary>Newest first.</summary>
        public List<UpmVersionDto> Versions { get; set; } = new List<UpmVersionDto>();
        /// <summary>Package names that exist on the registry, for linking dependencies.</summary>
        public List<string> KnownPackages { get; set; } = new List<string>();
    }

    // ---------- Search ----------

    public static class PackageSource
    {
        public const string Unity = "unity";
        public const string Upm = "upm";
    }

    public class SearchResultDto
    {
        public string Source { get; set; } = PackageSource.Unity;
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? By { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        /// <summary>Human-readable list of matched fields ("tên, mô tả").</summary>
        public string MatchedFields { get; set; } = "";
        public int HighlightStart { get; set; } = -1;
        public int HighlightLength { get; set; }
    }

    public class SearchResponseDto
    {
        public string Query { get; set; } = "";
        public bool UpmAvailable { get; set; } = true;
        public List<SearchResultDto> Results { get; set; } = new List<SearchResultDto>();
    }

    // ---------- Auth ----------

    public class AuthStateDto
    {
        public bool IsAdmin { get; set; }
        public string? UserName { get; set; }
    }

    public class LoginRequest
    {
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class ApiError
    {
        public string Message { get; set; } = "";
    }
}
