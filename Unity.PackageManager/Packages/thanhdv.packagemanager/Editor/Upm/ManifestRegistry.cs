using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ThanhDV.PackageManager
{
    /// <summary>Adds the Verdaccio scoped registry to Packages/manifest.json without touching other registries.</summary>
    internal static class ManifestRegistry
    {
        /// <summary>
        /// "com.thanhdv.utils" → "com.thanhdv", "thanhdv.utilities" → "thanhdv" (as the portal's install snippet).
        /// Never a scope that also covers Unity's packages: Verdaccio proxies com.unity.*, so they would all go
        /// through the NAS. Null for com.unity.* names themselves.
        /// </summary>
        public static string ScopeFor(string packageName)
        {
            if (Covers("com.unity", packageName)) return null;
            var parts = packageName.Split('.');
            string scope = parts.Length >= 3 ? parts[0] + "." + parts[1] : parts[0];
            return Covers(scope, "com.unity.x") ? packageName : scope;
        }

        /// <summary>Unity routes a package to a registry when its name equals a scope or starts with "scope.".</summary>
        public static bool Covers(string scope, string packageName) =>
            packageName == scope || packageName.StartsWith(scope + ".", StringComparison.Ordinal);

        /// <summary>Makes <paramref name="packageName"/> resolve from <paramref name="url"/>.</summary>
        /// <returns>True when the manifest changed.</returns>
        public static bool Ensure(JObject manifest, string registryName, string url, string packageName)
        {
            string scope = ScopeFor(packageName)
                ?? throw new InvalidOperationException($"Can't install {packageName} from a custom registry: its scope overlaps Unity's own packages (com.unity).");

            if (!(manifest["scopedRegistries"] is JArray registries))
            {
                registries = new JArray();
                manifest["scopedRegistries"] = registries;
            }

            var entry = registries.OfType<JObject>().FirstOrDefault(r => SameUrl((string)r["url"], url));
            // Two registries claiming the same scope confuse Unity; let the user decide which one serves it.
            var other = registries.OfType<JObject>().FirstOrDefault(r => r != entry &&
                r["scopes"] is JArray s && s.Any(x => Covers((string)x, packageName) || Covers(scope, (string)x)));
            if (other != null)
                throw new InvalidOperationException(
                    $"Registry \"{(string)other["name"]}\" in manifest.json already serves the scope of {packageName}. Edit manifest.json before installing it from {registryName}.");

            if (entry == null)
            {
                AddRegistry(registries, registryName, url, scope, packageName);
                return true;
            }

            if (!(entry["scopes"] is JArray scopes))
            {
                scopes = new JArray();
                entry["scopes"] = scopes;
            }
            if (scopes.Any(s => Covers((string)s, packageName))) return false;
            scopes.Add(scope);
            return true;
        }

        /// <summary>
        /// Makes <paramref name="packageName"/>, a dependency of a package from <paramref name="url"/>, resolve from
        /// there too. Unity takes the registry with the longest scope covering a name, so a dependency another
        /// registry already wins stays with it (<paramref name="servedBy"/> names that registry), while narrower
        /// scopes of other registries ("com.tayx.other" under an added "com.tayx") keep winning their packages.
        /// </summary>
        /// <returns>True when the manifest changed.</returns>
        public static bool EnsureDependency(JObject manifest, string registryName, string url, string packageName, out string servedBy)
        {
            servedBy = null;
            string scope = ScopeFor(packageName);
            if (scope == null) return false;

            if (!(manifest["scopedRegistries"] is JArray registries))
            {
                registries = new JArray();
                manifest["scopedRegistries"] = registries;
            }
            var winner = registries.OfType<JObject>()
                .SelectMany(r => Scopes(r).Select(s => (Registry: r, Scope: s)))
                .Where(x => Covers(x.Scope, packageName))
                .OrderByDescending(x => x.Scope.Length)
                .Select(x => x.Registry)
                .FirstOrDefault();
            if (winner != null)
            {
                if (!SameUrl((string)winner["url"], url)) servedBy = (string)winner["name"];
                return false;
            }

            var entry = registries.OfType<JObject>().FirstOrDefault(r => SameUrl((string)r["url"], url));
            if (entry == null)
            {
                AddRegistry(registries, registryName, url, scope, packageName);
                return true;
            }
            if (!(entry["scopes"] is JArray scopes))
            {
                scopes = new JArray();
                entry["scopes"] = scopes;
            }
            scopes.Add(scope);
            return true;
        }

        /// <summary>Scopes of the registry at <paramref name="url"/> that another registry lists too.</summary>
        public static List<string> SharedScopes(JObject manifest, string url)
        {
            var registries = (manifest["scopedRegistries"] as JArray ?? new JArray()).OfType<JObject>().ToList();
            var mine = registries.Where(r => SameUrl((string)r["url"], url)).SelectMany(Scopes).ToList();
            var others = new HashSet<string>(registries.Where(r => !SameUrl((string)r["url"], url)).SelectMany(Scopes));
            return mine.Where(others.Contains).Distinct().ToList();
        }

        /// <summary>Unity rejects two registries with the same name, so one already at another URL is the user's to fix.</summary>
        private static void AddRegistry(JArray registries, string registryName, string url, string scope, string packageName)
        {
            var sameName = registries.OfType<JObject>().FirstOrDefault(r => (string)r["name"] == registryName);
            if (sameName != null)
                throw new InvalidOperationException(
                    $"Registry \"{registryName}\" in manifest.json points to {(string)sameName["url"]}, not {url}. Edit manifest.json before installing {packageName}.");
            registries.Add(new JObject
            {
                ["name"] = registryName,
                ["url"] = url,
                ["scopes"] = new JArray(scope),
            });
        }

        private static IEnumerable<string> Scopes(JObject registry) =>
            (registry["scopes"] as JArray ?? new JArray()).Select(s => (string)s).Where(s => s != null);

        public static bool SameUrl(string a, string b) =>
            a != null && b != null && string.Equals(WithoutScheme(a), WithoutScheme(b), StringComparison.OrdinalIgnoreCase);

        private static string WithoutScheme(string url)
        {
            url = url.Trim().TrimEnd('/');
            int i = url.IndexOf("://", StringComparison.Ordinal);
            return i < 0 ? url : url.Substring(i + 3);
        }
    }
}
