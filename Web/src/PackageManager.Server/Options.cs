namespace PackageManager.Server;

public class StorageOptions
{
    /// <summary>Folder holding the .unitypackage files (the old site's unitypkgs/).</summary>
    public string PackagesPath { get; set; } = "App_Data/unitypkgs";
    public string DatabasePath { get; set; } = "App_Data/app.db";
}

public class AdminOptions
{
    public string UserName { get; set; } = "admin";

    /// <summary>Plain password from env/secret (Admin__Password). Login is disabled while empty.</summary>
    public string? Password { get; set; }
}

public class VerdaccioOptions
{
    public string Url { get; set; } = "https://upm.thanhdv.com";

    /// <summary>Name used for the scopedRegistries entry in the install snippet.</summary>
    public string RegistryName { get; set; } = "ThanhDV";
}
