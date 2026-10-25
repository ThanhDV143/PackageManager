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

        public static async Task Install(UpmPackageDetailDto package, string version)
        {
            var manifest = JObject.Parse(File.ReadAllText(ManifestPath));
            if (ManifestRegistry.Ensure(manifest, package.RegistryName, package.Registry, package.Name))
                File.WriteAllText(ManifestPath, manifest.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n");
            await Wait(Client.Add($"{package.Name}@{version}"));
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
