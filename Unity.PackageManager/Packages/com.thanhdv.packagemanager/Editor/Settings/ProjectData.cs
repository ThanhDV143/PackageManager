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
        public void RecordImport(string packageId, string displayName, string version, IEnumerable<string> guids, DateTime now)
        {
            var record = Find(packageId);
            if (record == null)
            {
                record = new ImportedPackage { PackageId = packageId };
                Imported.Add(record);
            }
            record.DisplayName = displayName;
            record.Version = version;
            record.ImportedAt = now;

            var known = new HashSet<string>(record.Guids);
            foreach (string guid in guids)
                if (known.Add(guid)) record.Guids.Add(guid);
            // Sorted so re-imports give small, stable diffs in git.
            record.Guids.Sort(StringComparer.Ordinal);
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
    }
}
