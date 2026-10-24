namespace PackageManager.Server.Data;

public class Package
{
    public int Id { get; set; }

    /// <summary>Public identifier such as "com.moremountains.feel".</summary>
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

    /// <summary>Screenshots / videos as JSON (List&lt;MediaItemDto&gt;).</summary>
    public string? MediaJson { get; set; }
    public DateTime? MetadataFetchedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<PackageVersion> Versions { get; set; } = [];
}

public class PackageVersion
{
    public int Id { get; set; }
    public int PackageDbId { get; set; }
    public Package Package { get; set; } = null!;

    public string Version { get; set; } = "";
    public string FileName { get; set; } = "";

    /// <summary>Path relative to the packages folder, always with forward slashes.</summary>
    public string RelativePath { get; set; } = "";
    public long Size { get; set; }
    public string? UnityVersion { get; set; }
    public string? ReleaseNotes { get; set; }
    public DateTime UploadedAt { get; set; }
}

/// <summary>A file in the packages folder the admin chose to hide from "File chưa quản lý".</summary>
public class IgnoredFile
{
    public int Id { get; set; }
    public string RelativePath { get; set; } = "";
}
