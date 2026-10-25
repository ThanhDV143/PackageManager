using System;
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
                registries.Add(new JObject
                {
                    ["name"] = registryName,
                    ["url"] = url,
                    ["scopes"] = new JArray(scope),
                });
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

        private static bool SameUrl(string a, string b) =>
            a != null && string.Equals(a.Trim().TrimEnd('/'), b.Trim().TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }
}
