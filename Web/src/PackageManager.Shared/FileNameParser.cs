using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PackageManager.Shared
{
    public readonly struct ParsedFileName
    {
        public ParsedFileName(string name, string? version)
        {
            Name = name;
            Version = version;
        }

        public string Name { get; }
        public string? Version { get; }
    }

    /// <summary>Guesses a package name and version from a file name such as "Feel v5.8.unitypackage".</summary>
    public static class FileNameParser
    {
        // A version is either dotted ("2.02.30") or explicitly prefixed with v ("v5"), and must stand alone.
        private static readonly Regex VersionPattern = new Regex(
            @"(?<=^|[\s_\-(\[])(?:[vV](?<v>\d+(?:\.\d+)*)|(?<v>\d+(?:\.\d+)+))(?=$|[\s_\-)\]])",
            RegexOptions.Compiled);

        private static readonly Regex CopySuffix = new Regex(@"\s*\(\d+\)$", RegexOptions.Compiled);
        private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.Compiled);

        public static ParsedFileName Parse(string fileName)
        {
            string stem = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
            Match? last = VersionPattern.Matches(stem).Cast<Match>().LastOrDefault();

            string? version = null;
            string name = stem;
            if (last != null)
            {
                version = last.Groups["v"].Value;
                string before = stem.Substring(0, last.Index);
                if (Clean(before).Length > 0) name = before;
            }

            name = Clean(name);
            if (version == null) name = Clean(CopySuffix.Replace(name, string.Empty));
            if (name.Length == 0) name = stem;
            return new ParsedFileName(name, version);
        }

        /// <summary>Lower-case letters and digits only, for fuzzy name matching.</summary>
        public static string Normalize(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var sb = new StringBuilder(value!.Length);
            foreach (char c in value)
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        private static string Clean(string value)
        {
            value = value.Replace('_', ' ');
            value = Spaces.Replace(value, " ");
            return value.Trim(' ', '-', '.', '(', '[', ',');
        }
    }
}
