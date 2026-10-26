using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace ThanhDV.PackageManager
{
    /// <summary>
    /// Per-project state kept in ProjectSettings/ so it is committed and the whole team shares it:
    /// the server URL and which .unitypackage versions were imported.
    /// </summary>
    internal class ProjectData
    {
        public const string FilePath = "ProjectSettings/ThanhDV.PackageManager.json";

        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
        };

        private static ProjectData _instance;

        public string ServerUrl { get; set; } = "";
        public List<ImportedPackage> Imported { get; set; } = new List<ImportedPackage>();

        public static ProjectData Instance => _instance ??= Load();

        /// <summary>Raised after every <see cref="Save"/> and <see cref="Reload"/>.</summary>
        public static event Action Changed;

        /// <summary>Re-reads the file, e.g. after a git pull changed it.</summary>
        public static void Reload()
        {
            _instance = Load();
            Changed?.Invoke();
        }

        public void Save()
        {
            Imported.Sort((a, b) => string.CompareOrdinal(a.PackageId, b.PackageId));
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Json));
            Changed?.Invoke();
        }

        public ImportedPackage Find(string packageId) => Imported.Find(p => p.PackageId == packageId);

        /// <summary>
        /// Records a (re-)import: the version is overwritten, GUIDs are merged with the earlier ones so
        /// Remove still finds files that only an older version had.
        /// </summary>
        /// <param name="requiredBy">The UPM package whose "udependencies" pulled this in; null when the user imported it.</param>
        public void RecordImport(string packageId, string displayName, string version, IEnumerable<string> guids, DateTime now,
            string requiredBy = null)
        {
            var record = Find(packageId);
            if (record == null)
            {
                record = new ImportedPackage { PackageId = packageId, Explicit = requiredBy == null };
                Imported.Add(record);
            }
            else if (requiredBy == null) record.Explicit = true;
            if (requiredBy != null) AddRequiredBy(record, requiredBy);
            record.DisplayName = displayName;
            record.Version = version;
            record.ImportedAt = now;

            var known = new HashSet<string>(record.Guids);
            foreach (string guid in guids)
                if (known.Add(guid)) record.Guids.Add(guid);
            // Sorted so re-imports give small, stable diffs in git.
            record.Guids.Sort(StringComparer.Ordinal);
        }

        public void AddRequiredBy(ImportedPackage record, string upmName)
        {
            if (record.RequiredBy.Contains(upmName)) return;
            record.RequiredBy.Add(upmName);
            record.RequiredBy.Sort(StringComparer.Ordinal);
        }

        /// <summary>Takes <paramref name="upmName"/> off every package except those it still needs.</summary>
        /// <returns>Packages that nothing needs any more and that the user didn't import themselves.</returns>
        public List<ImportedPackage> DropRequiredBy(string upmName, ICollection<string> stillNeeded)
        {
            var orphans = new List<ImportedPackage>();
            foreach (var record in Imported)
                if (!stillNeeded.Contains(record.PackageId) && record.RequiredBy.Remove(upmName) &&
                    record.RequiredBy.Count == 0 && !record.Explicit)
                    orphans.Add(record);
            return orphans;
        }

        private static ProjectData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonConvert.DeserializeObject<ProjectData>(File.ReadAllText(FilePath), Json) ?? new ProjectData();
            }
            catch (Exception e) when (e is JsonException || e is IOException)
            {
                Debug.LogWarning($"[ThanhDV Package Manager] Could not read {FilePath}: {e.Message}");
            }
            return new ProjectData();
        }
    }

    internal class ImportedPackage
    {
        public string PackageId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Version { get; set; } = "";
        public DateTime ImportedAt { get; set; }
        public List<string> Guids { get; set; } = new List<string>();
        /// <summary>Imported by the user, not only pulled in by "udependencies". Records from before udependencies default to true.</summary>
        public bool Explicit { get; set; } = true;
        /// <summary>Installed UPM packages whose "udependencies" need this package.</summary>
        public List<string> RequiredBy { get; set; } = new List<string>();
    }
}
