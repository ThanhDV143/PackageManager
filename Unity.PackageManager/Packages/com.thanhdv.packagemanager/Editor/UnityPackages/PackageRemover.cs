using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace ThanhDV.PackageManager
{
    /// <summary>Deletes the assets an imported .unitypackage brought in, keeping those another imported package also owns.</summary>
    internal static class PackageRemover
    {
        /// <summary>The package's GUIDs that no other imported package records.</summary>
        public static List<string> RemovableGuids(ProjectData data, string packageId)
        {
            var record = data.Find(packageId);
            if (record == null) return new List<string>();
            var shared = new HashSet<string>(data.Imported.Where(p => p != record).SelectMany(p => p.Guids));
            return record.Guids.Where(g => !shared.Contains(g)).ToList();
        }

        public static int SharedCount(ProjectData data, string packageId) =>
            (data.Find(packageId)?.Guids.Count ?? 0) - RemovableGuids(data, packageId).Count;

        /// <summary>Existing asset paths (under Assets/) that Remove would delete, sorted so folders precede their contents.</summary>
        public static List<string> RemovablePaths(ProjectData data, string packageId) =>
            RemovableGuids(data, packageId)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && (File.Exists(p) || Directory.Exists(p)))
                .Distinct()
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>True when none of the package's assets are left in the project.</summary>
        public static bool IsMissing(ImportedPackage record) =>
            record.Guids.Count > 0 && record.Guids.All(g =>
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                return path.Length == 0 || (!File.Exists(path) && !Directory.Exists(path));
            });

        /// <summary>
        /// Moves the files to the OS trash, then each selected folder that ended up empty. Folders still holding
        /// other files (the user's, or ones left unselected) are kept. Drops the package's record.
        /// </summary>
        /// <returns>Paths that could not be removed.</returns>
        public static List<string> Remove(ProjectData data, string packageId, IReadOnlyCollection<string> paths)
        {
            var failed = new List<string>();
            var files = paths.Where(p => !AssetDatabase.IsValidFolder(p)).ToArray();
            if (files.Length > 0) AssetDatabase.MoveAssetsToTrash(files, failed);

            // Longest first: a folder's contents always have longer paths than the folder itself.
            foreach (string folder in paths.Where(AssetDatabase.IsValidFolder).OrderByDescending(p => p.Length))
                if (!Directory.EnumerateFileSystemEntries(folder).Any() && !AssetDatabase.MoveAssetToTrash(folder))
                    failed.Add(folder);

            var record = data.Find(packageId);
            if (record != null)
            {
                data.Imported.Remove(record);
                data.Save();
            }

            string name = record?.DisplayName ?? packageId;
            int moved = files.Length - failed.Count(f => !AssetDatabase.IsValidFolder(f));
            Removed?.Invoke(failed.Count == 0
                ? $"Removed {name}: {moved} file(s) moved to the trash."
                : $"Removed {name}, but {failed.Count} item(s) could not be moved to the trash (see the Console).", failed.Count > 0);
            return failed;
        }

        /// <summary>The outcome of a Remove, for the status bar: (message, isError).</summary>
        public static event System.Action<string, bool> Removed;
    }
}
