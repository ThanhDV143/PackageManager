using Microsoft.Extensions.Options;

namespace PackageManager.Server.Services;

/// <summary>All disk access to the packages folder. Every path from a client goes through <see cref="Resolve"/>.</summary>
public class FileStore
{
    public static readonly string[] AllowedExtensions = [".unitypackage", ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz"];

    private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars()
        .Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).Distinct().ToArray();

    public FileStore(IOptions<StorageOptions> options, IHostEnvironment env)
    {
        Root = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.PackagesPath));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public static bool IsAllowed(string fileName) =>
        AllowedExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);

    /// <summary>Absolute path for a stored relative path, or null when it escapes the root.</summary>
    public string? Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        string full = Path.GetFullPath(Path.Combine(Root, relativePath.Replace('\\', '/').TrimStart('/')));
        string rootWithSep = Root.EndsWith(Path.DirectorySeparatorChar) ? Root : Root + Path.DirectorySeparatorChar;
        return full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    public string ToRelative(string fullPath) => Path.GetRelativePath(Root, fullPath).Replace('\\', '/');

    public IEnumerable<FileInfo> EnumerateFiles() =>
        new DirectoryInfo(Root)
            .EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
            .Where(f => IsAllowed(f.Name) && !f.Name.StartsWith(".upload-", StringComparison.Ordinal));

    /// <summary>Streams <paramref name="body"/> to a temp file, then moves it to a free name in the root.</summary>
    public async Task<FileInfo> SaveAsync(string fileName, Stream body, CancellationToken ct)
    {
        string safeName = SanitizeFileName(fileName);
        string temp = Path.Combine(Root, $".upload-{Guid.NewGuid():N}");
        try
        {
            await using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
                await body.CopyToAsync(fs, 1 << 20, ct);

            for (int attempt = 0; ; attempt++)
            {
                string target = FreePath(safeName);
                try
                {
                    File.Move(temp, target);
                    return new FileInfo(target);
                }
                catch (IOException) when (attempt < 5 && File.Exists(target))
                {
                    // Another upload took the same name in the meantime; pick the next free one.
                }
            }
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    public static string SanitizeFileName(string fileName)
    {
        string name = Path.GetFileName(fileName.Replace('\\', '/'));
        name = string.Concat(name.Select(c => InvalidNameChars.Contains(c) || char.IsControl(c) ? '_' : c)).Trim().TrimStart('.');
        if (name.Length == 0) name = "package.unitypackage";
        if (name.Length > 200) name = name[..(200 - Path.GetExtension(name).Length)] + Path.GetExtension(name);
        return name;
    }

    public static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string FreePath(string fileName)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName), ext = Path.GetExtension(fileName);
        string path = Path.Combine(Root, fileName);
        for (int i = 1; File.Exists(path); i++)
            path = Path.Combine(Root, $"{stem} ({i}){ext}");
        return path;
    }
}
