using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PackageManager.Shared;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ThanhDV.PackageManager
{
    /// <summary>
    /// Follows a UPM package's "udependencies" (.unitypackage id → minimum version) in its package.json:
    /// imports the missing ones one by one through Unity's Import dialog when the package is installed, and offers to
    /// remove those nothing needs any more when it is removed. The queue lives in SessionState because importing
    /// scripts reloads the domain between two imports.
    /// </summary>
    [InitializeOnLoad]
    internal static class DependencyImporter
    {
        private const string QueueKey = "ThanhDV.PackageManager.DependencyQueue";
        private const string Field = "udependencies";

        [Serializable]
        private class Job
        {
            public string packageId;
            public string minVersion;
            public string requiredBy;
            /// <summary>The user agreed to replace an older version and its files are already gone.</summary>
            public bool replacing;
        }

        [Serializable]
        private class Queue
        {
            public List<Job> jobs = new List<Job>();
            /// <summary>Jobs taken off the queue since it was last empty, for "2/3".</summary>
            public int started;
        }

        private static bool _running;
        private static double _nextCheck;
        private static double _settledSince = -1;

        /// <summary>Progress and outcomes for the status bar: (message, isError).</summary>
        public static event Action<string, bool> Status;

        static DependencyImporter()
        {
            Events.registeredPackages += OnRegistered;
            if (Load().jobs.Count > 0) Schedule();
        }

        /// <summary>The "udependencies" of a package.json; entries that aren't "id": "version" strings are skipped.</summary>
        public static Dictionary<string, string> Parse(string packageJson)
        {
            var result = new Dictionary<string, string>();
            if (JObject.Parse(packageJson)[Field] is JObject deps)
                foreach (var dep in deps.Properties())
                    if (!string.IsNullOrWhiteSpace(dep.Name) && dep.Value.Type == JTokenType.String)
                        result[dep.Name.Trim()] = ((string)dep.Value).Trim();
            return result;
        }

        /// <summary>An installed package's "udependencies"; empty when it has none or its package.json can't be read.</summary>
        public static Dictionary<string, string> Of(PackageInfo info) => TryRead(info, out _);

        /// <summary>The project has the package in at least the version asked for.</summary>
        public static bool IsMet(ImportedPackage record, string minVersion) => record != null && AtLeast(record.Version, minVersion);

        /// <summary>Some of the package's dependencies are waiting in the queue.</summary>
        public static bool IsQueued(string upmName) => Load().jobs.Any(j => j.requiredBy == upmName);

        /// <summary>Goes through the package's dependencies again, e.g. after a cancelled import or one from before udependencies.</summary>
        public static void Enqueue(PackageInfo info)
        {
            var queue = Load();
            AddJobs(queue, info.name, Read(info));
            Save(queue);
            if (queue.jobs.Count > 0) Schedule();
        }

        private static Dictionary<string, string> Read(PackageInfo info)
        {
            var deps = TryRead(info, out string error);
            if (error != null) Report($"Could not read {Field} of {info.name}: {error}", true);
            return deps;
        }

        private static Dictionary<string, string> TryRead(PackageInfo info, out string error)
        {
            error = null;
            string path = Path.Combine(info.resolvedPath ?? "", "package.json");
            try
            {
                return File.Exists(path) ? Parse(File.ReadAllText(path)) : new Dictionary<string, string>();
            }
            catch (Exception e) when (e is JsonException || e is IOException)
            {
                error = e.Message;
                return new Dictionary<string, string>();
            }
        }

        private static void AddJobs(Queue queue, string upmName, Dictionary<string, string> deps)
        {
            foreach (var dep in deps)
            {
                var queued = queue.jobs.Find(j => j.requiredBy == upmName && j.packageId == dep.Key);
                if (queued != null) queued.minVersion = dep.Value;
                else queue.jobs.Add(new Job { packageId = dep.Key, minVersion = dep.Value, requiredBy = upmName });
            }
        }

        private static void OnRegistered(PackageRegistrationEventArgs args)
        {
            var data = ProjectData.Instance;
            var queue = Load();
            var orphans = new List<ImportedPackage>();
            bool changed = false;

            void Sync(string upmName, Dictionary<string, string> deps)
            {
                changed |= data.Imported.Any(r => r.RequiredBy.Contains(upmName));
                orphans.AddRange(data.DropRequiredBy(upmName, deps.Keys));
                queue.jobs.RemoveAll(j => j.requiredBy == upmName && !deps.ContainsKey(j.packageId));
                AddJobs(queue, upmName, deps);
            }

            foreach (var info in args.removed) Sync(info.name, new Dictionary<string, string>());
            foreach (var info in args.added.Concat(args.changedTo)) Sync(info.name, Read(info));

            if (changed) data.Save();
            Save(queue);
            // Still asked for by a queued job (e.g. it moved from one package to another): not unused.
            orphans.RemoveAll(o => queue.jobs.Any(j => j.packageId == o.PackageId));
            if (orphans.Count > 0) EditorApplication.delayCall += () => AskToRemove(orphans);
            if (queue.jobs.Count > 0) Schedule();
        }

        // ---------- import queue ----------

        private static void Schedule()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        /// <summary>
        /// Starts the next job once the editor has been idle for a second: no import dialog open, nothing compiling
        /// or refreshing. Right after an import, compiling may not have started yet; the wait lets it start.
        /// </summary>
        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_running || now < _nextCheck) return;
            _nextCheck = now + 0.5;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || ImportTracker.ImportingPackageId != null)
            {
                _settledSince = -1;
                return;
            }
            if (_settledSince < 0) _settledSince = now;
            if (now - _settledSince < 1) return;
            EditorApplication.update -= Tick;
            _settledSince = -1;
            _ = Next();
        }

        private static async Task Next()
        {
            var data = ProjectData.Instance;
            var queue = Load();
            // Already there in a new enough version: nothing to import, only note that the package needs it.
            while (queue.jobs.Count > 0 && data.Find(queue.jobs[0].packageId) is { } have && AtLeast(have.Version, queue.jobs[0].minVersion))
            {
                data.AddRequiredBy(have, queue.jobs[0].requiredBy);
                data.Save();
                Finish(queue);
            }
            if (queue.jobs.Count == 0) return;

            _running = true;
            var job = queue.jobs[0];
            string step = $"dependency {queue.started + 1}/{queue.started + queue.jobs.Count} of {job.requiredBy}";
            try
            {
                var record = data.Find(job.packageId);
                Report($"Looking up {job.packageId} ({step})…", false);
                var detail = await ServerClient.GetPackage(job.packageId);
                var latest = detail?.Versions.FirstOrDefault(v => v.IsLatest) ?? detail?.Versions.FirstOrDefault();
                if (latest == null)
                {
                    Fail(queue, $"{job.requiredBy} needs {job.packageId}, but the server doesn't have it.");
                    return;
                }
                if (!AtLeast(latest.Version, job.minVersion))
                {
                    Fail(queue, $"{job.requiredBy} needs {job.packageId} {job.minVersion} or newer, but the server's latest is {latest.Version}.");
                    return;
                }

                if (record != null && !job.replacing)
                {
                    if (!EditorUtility.DisplayDialog("Update dependency",
                            $"{job.requiredBy} needs {record.DisplayName} {job.minVersion} or newer, but the project has {record.Version}.\n\n" +
                            $"Remove {record.Version} and import {latest.Version}?",
                            "Replace", $"Keep {record.Version}"))
                    {
                        data.AddRequiredBy(record, job.requiredBy);
                        data.Save();
                        Finish(queue);
                        return;
                    }
                    // The record stays (with its Explicit / RequiredBy); the import of the new version updates it.
                    var failed = PackageRemover.MoveToTrash(PackageRemover.RemovablePaths(data, record.PackageId));
                    if (failed.Count > 0) Debug.LogWarning("[ThanhDV Package Manager] Could not remove:\n" + string.Join("\n", failed));
                    job.replacing = true;
                    Save(queue);
                    // Removing scripts recompiles; import once that has settled (after a domain reload, if any).
                    return;
                }

                string name = $"{detail.DisplayName} {latest.Version}";
                // Ends in "…" like every in-progress message, so the window doesn't keep it as an outcome.
                string path = await DownloadCache.Get(detail.PackageId, latest, (p, _) =>
                    Report($"Downloading {name} ({step}) {(int)(p * 100)}%…", false));
                queue.jobs.RemoveAt(0);
                queue.started++;
                Save(queue);
                Report($"Importing {name} ({step}), finish in Unity's Import dialog…", false);
                ImportTracker.Import(detail.PackageId, detail.DisplayName, latest.Version, path, job.requiredBy);
            }
            catch (Exception e)
            {
                Fail(queue, $"Could not import {job.packageId} for {job.requiredBy}: {e.Message}");
            }
            finally
            {
                _running = false;
                if (Load().jobs.Count > 0) Schedule();
            }
        }

        private static bool AtLeast(string version, string minimum) => VersionComparer.Instance.Compare(version, minimum) >= 0;

        private static void Finish(Queue queue)
        {
            queue.jobs.RemoveAt(0);
            queue.started++;
            Save(queue);
        }

        /// <summary>Skips the job; the rest of the queue goes on.</summary>
        private static void Fail(Queue queue, string message)
        {
            Finish(queue);
            Report(message, true);
        }

        private static void Report(string message, bool isError)
        {
            // With the window closed nobody shows it; keep problems in the Console.
            if (Status == null && isError) Debug.LogWarning("[ThanhDV Package Manager] " + message);
            Status?.Invoke(message, isError);
        }

        // ---------- removal ----------

        /// <summary>One question per package, so each can be kept or removed. A kept package counts as the user's own.</summary>
        private static void AskToRemove(List<ImportedPackage> orphans)
        {
            var data = ProjectData.Instance;
            foreach (var orphan in orphans)
            {
                // Re-read: the list may have been reloaded, or the package removed, since.
                var record = data.Find(orphan.PackageId);
                if (record == null || record.Explicit || record.RequiredBy.Count > 0) continue;
                if (EditorUtility.DisplayDialog("Remove unused dependency",
                        $"{record.DisplayName} {record.Version} was imported as a dependency, and no installed package needs it any more.\n\n" +
                        "Move its files to the trash? Files other imported packages also use are kept.",
                        "Remove", "Keep"))
                {
                    var failed = PackageRemover.Remove(data, record.PackageId, PackageRemover.RemovablePaths(data, record.PackageId));
                    if (failed.Count > 0) Debug.LogWarning("[ThanhDV Package Manager] Could not remove:\n" + string.Join("\n", failed));
                }
                else
                {
                    record.Explicit = true;
                    data.Save();
                }
            }
        }

        private static Queue Load()
        {
            string json = SessionState.GetString(QueueKey, "");
            return json.Length == 0 ? new Queue() : JsonUtility.FromJson<Queue>(json);
        }

        // An empty queue starts counting from 1 again.
        private static void Save(Queue queue) =>
            SessionState.SetString(QueueKey, queue.jobs.Count == 0 ? "" : JsonUtility.ToJson(queue));
    }
}
