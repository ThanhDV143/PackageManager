using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PackageManager.Shared;

namespace ThanhDV.PackageManager
{
    /// <summary>Downloaded .unitypackage files, shared by all projects on this machine (like Unity's Asset Store cache).</summary>
    internal static class DownloadCache
    {
        private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

        public static string Root => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThanhDV.PackageManager", "Downloads");

        public static string PathFor(string packageId, PackageVersionDto version) =>
            Path.Combine(Root, Segment(packageId), Segment(version.Version), Segment(version.FileName));

        public static bool Has(string packageId, PackageVersionDto version)
        {
            var file = new FileInfo(PathFor(packageId, version));
            return file.Exists && file.Length == version.Size;
        }

        /// <returns>The local path, downloading the file first unless it is already cached.</returns>
        public static async Task<string> Get(string packageId, PackageVersionDto version, Action<float, ulong> progress)
        {
            string path = PathFor(packageId, version);
            if (Has(packageId, version)) return path;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            await ServerClient.Download(version.Id, path, progress);
            return path;
        }

        /// <summary>Server values become one safe path segment (no separators, no "." / "..").</summary>
        private static string Segment(string value)
        {
            string s = new string(value.Select(c => InvalidChars.Contains(c) || c == '/' || c == '\\' ? '_' : c).ToArray()).Trim().Trim('.');
            return s.Length == 0 ? "_" : s;
        }
    }
}
