using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ThanhDV.PackageManager
{
    /// <summary>
    /// Imports a downloaded .unitypackage and, only once Unity reports success, records its version and the
    /// GUIDs actually imported in <see cref="ProjectData"/>. The pending import lives in SessionState because a
    /// package with scripts triggers a domain reload in the middle.
    /// </summary>
    [InitializeOnLoad]
    internal static class ImportTracker
    {
        private const string PendingKey = "ThanhDV.PackageManager.PendingImport";

        [Serializable]
        private class Pending
        {
            public string packageId;
            public string displayName;
            public string version;
            public string fileStem;
            public long startedTicks;
            /// <summary>The UPM package this is a dependency of; empty for the user's own import.</summary>
            public string requiredBy;
        }

        static ImportTracker()
        {
            AssetDatabase.onImportPackageItemsCompleted += OnItems;
            AssetDatabase.importPackageCancelled += name => OnCancelledOrFailed(name, null);
            AssetDatabase.importPackageFailed += OnCancelledOrFailed;
        }

        /// <summary>
        /// Package id of the import waiting for Unity's dialog / import to finish, or null. A pending import whose
        /// dialog is gone and that started over a minute ago is treated as abandoned, in case Unity never reported it.
        /// </summary>
        public static string ImportingPackageId
        {
            get
            {
                var pending = Load();
                if (pending == null) return null;
                bool recent = DateTime.UtcNow.Ticks - pending.startedTicks < TimeSpan.FromMinutes(1).Ticks;
                return recent || IsImportDialogOpen() ? pending.packageId : null;
            }
        }

        /// <summary>Raised whenever <see cref="ImportingPackageId"/> may have changed.</summary>
        public static event Action Changed;

        /// <summary>The outcome of a tracked import, for the status bar: (message, isError).</summary>
        public static event Action<string, bool> Finished;

        /// <param name="requiredBy">The UPM package whose "udependencies" asks for this one; null for the user's own import.</param>
        public static void Import(string packageId, string displayName, string version, string path, string requiredBy = null)
        {
            Save(new Pending
            {
                packageId = packageId,
                displayName = displayName,
                version = version,
                fileStem = Path.GetFileNameWithoutExtension(path),
                startedTicks = DateTime.UtcNow.Ticks,
                requiredBy = requiredBy,
            });
            Changed?.Invoke();
            // Interactive: Unity's own dialog lets the user pick which items to import.
            AssetDatabase.ImportPackage(path, true);
        }

        // "Items completed" is the success signal: it carries what was imported and comes for imports confirmed in
        // the dialog. (importPackageCompleted only came for non-interactive imports when this was checked on 2022.3.)
        private static void OnItems(string[] items)
        {
            var pending = Load();
            if (pending == null) return;
            var guids = items.Select(AssetDatabase.AssetPathToGUID).Where(g => !string.IsNullOrEmpty(g));
            // JsonUtility gives back a null string as "".
            string requiredBy = string.IsNullOrEmpty(pending.requiredBy) ? null : pending.requiredBy;
            ProjectData.Instance.RecordImport(pending.packageId, pending.displayName, pending.version, guids, DateTime.UtcNow, requiredBy);
            Save(null);
            ProjectData.Instance.Save();
            Changed?.Invoke();
            Finished?.Invoke($"Imported {pending.displayName} {pending.version}.", false);
        }

        /// <param name="error">Unity's message when the import failed; null when the user cancelled it.</param>
        private static void OnCancelledOrFailed(string packageName, string error)
        {
            var pending = Load();
            if (pending == null || !Matches(pending, packageName)) return;
            Save(null);
            Changed?.Invoke();
            string name = $"{pending.displayName} {pending.version}";
            Finished?.Invoke(error == null ? $"Import of {name} cancelled." : $"Import of {name} failed: {error}", error != null);
        }

        private static bool IsImportDialogOpen() =>
            Resources.FindObjectsOfTypeAll<EditorWindow>().Any(w => w.GetType().FullName == "UnityEditor.PackageImport");

        // Unity passes the name with or without its folder and extension. GetFileNameWithoutExtension can't be used on it:
        // "PMTest_1.0.0" would lose ".0".
        private static bool Matches(Pending pending, string packageName)
        {
            const string extension = ".unitypackage";
            string name = Path.GetFileName(packageName);
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - extension.Length);
            return string.Equals(name, pending.fileStem, StringComparison.OrdinalIgnoreCase);
        }

        private static Pending Load()
        {
            string json = SessionState.GetString(PendingKey, "");
            return json.Length == 0 ? null : JsonUtility.FromJson<Pending>(json);
        }

        private static void Save(Pending pending) =>
            SessionState.SetString(PendingKey, pending == null ? "" : JsonUtility.ToJson(pending));
    }
}
