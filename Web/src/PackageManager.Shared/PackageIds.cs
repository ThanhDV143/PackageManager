using System.Linq;

namespace PackageManager.Shared
{
    public static class PackageIds
    {
        /// <summary>"More Mountains" + "Feel" → "com.moremountains.feel"; without a publisher → "local.feel".</summary>
        public static string Suggest(string? publisher, string? name)
        {
            string pub = Part(publisher), pkg = Part(name);
            if (pkg.Length == 0) pkg = "package";
            return pub.Length > 0 ? $"com.{pub}.{pkg}" : $"local.{pkg}";
        }

        private static string Part(string? value) =>
            new string(FileNameParser.Normalize(value).Where(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')).ToArray());
    }
}
