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
using UnityEditor.PackageManager.Requests;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ThanhDV.PackageManager
{
    /// <summary>Installs / removes Verdaccio packages through Unity's Package Manager, which also resolves their dependencies.</summary>
    internal static class UpmService
    {
        private const string ManifestPath = "Packages/manifest.json";

        public static async Task<Dictionary<string, PackageInfo>> ListInstalled()
        {
            // Indirect dependencies too: a registry package another package pulled in must show as installed.
            var request = Client.List(true, true);
            await Wait(request);
            return request.Result.ToDictionary(p => p.name);
        }

        /// <summary>
        /// Routes the package and its dependencies published on the same registry to that registry, then adds it.
        /// Unity alone would look for those dependencies on its own registry, unless the manifest has their scopes.
        /// </summary>
        /// <returns>Notes for the user: dependencies another registry keeps serving, scopes two registries share.</returns>
        public static async Task<List<string>> Install(UpmPackageDetailDto package, string version)
        {
            var dependencies = await RegistryDependencies(package, version, ServerClient.GetUpm);
            var manifest = JObject.Parse(File.ReadAllText(ManifestPath));
            bool changed = ManifestRegistry.Ensure(manifest, package.RegistryName, package.Registry, package.Name);
            var notes = new List<string>();
            foreach (string dependency in dependencies)
            {
                changed |= ManifestRegistry.EnsureDependency(manifest, package.RegistryName, package.Registry, dependency, out string servedBy);
                if (servedBy != null) notes.Add($"{dependency} stays with registry \"{servedBy}\".");
            }
            if (changed)
                File.WriteAllText(ManifestPath, manifest.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n");
            if (SharedScopesWarning(manifest, package.Registry) is string warning) notes.Add(warning);
            await Wait(Client.Add($"{package.Name}@{version}"));
            return notes;
        }

        /// <summary>
        /// The package's dependencies, direct and indirect, that are published on its registry. Each version a
        /// package is asked for is followed, since Unity resolves to the highest of them.
        /// </summary>
        internal static async Task<List<string>> RegistryDependencies(UpmPackageDetailDto package, string version,
            Func<string, Task<UpmPackageDetailDto>> get)
        {
            var published = new HashSet<string>(package.KnownPackages);
            var details = new Dictionary<string, UpmPackageDetailDto> { [package.Name] = package };
            var visited = new HashSet<string> { $"{package.Name}@{version}" };
            var pending = new Queue<(UpmPackageDetailDto Detail, string Version)>();
            pending.Enqueue((package, version));
            var found = new List<string>();

            while (pending.Count > 0)
            {
                var (detail, v) = pending.Dequeue();
                var dependencies = detail.Versions.Find(x => x.Version == v)?.Dependencies;
                if (dependencies == null) continue;
                foreach (var dependency in dependencies)
                {
                    // Not published there (com.unity.*, OpenUPM's, …): Unity finds it as it would without us.
                    if (!published.Contains(dependency.Key) || !visited.Add($"{dependency.Key}@{dependency.Value}")) continue;
                    if (!found.Contains(dependency.Key)) found.Add(dependency.Key);
                    if (!details.TryGetValue(dependency.Key, out var dependencyDetail))
                        details[dependency.Key] = dependencyDetail = await get(dependency.Key);
                    if (dependencyDetail != null) pending.Enqueue((dependencyDetail, dependency.Value));
                }
            }
            return found;
        }

        /// <summary>A warning when a scope of the registry at <paramref name="url"/> is in another registry too; null otherwise.</summary>
        public static string SharedScopesWarning(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || !File.Exists(ManifestPath)) return null;
            try
            {
                return SharedScopesWarning(JObject.Parse(File.ReadAllText(ManifestPath)), url);
            }
            catch (Exception e) when (e is JsonException || e is IOException)
            {
                return null;
            }
        }

        private static string SharedScopesWarning(JObject manifest, string url)
        {
            var shared = ManifestRegistry.SharedScopes(manifest, url);
            return shared.Count == 0
                ? null
                : $"manifest.json lists {string.Join(", ", shared)} under more than one registry; Unity can't tell which one serves it.";
        }

        public static Task Remove(string name) => Wait(Client.Remove(name));

        private static Task Wait(Request request)
        {
            var done = new TaskCompletionSource<bool>();
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= tick;
                if (request.Status == StatusCode.Success) done.SetResult(true);
                else done.SetException(new InvalidOperationException(request.Error?.message ?? "Package Manager reported an error."));
            };
            EditorApplication.update += tick;
            return done.Task;
        }
    }
}
