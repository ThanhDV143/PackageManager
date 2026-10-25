using System;

namespace ThanhDV.PackageManager
{
    internal enum SourceView
    {
        InProject,
        UnityPackages,
        Upm,
    }

    /// <summary>One row of the list: a .unitypackage from the server (or one only known from the import record), or a UPM package.</summary>
    internal class PackageItem
    {
        public bool IsUpm;
        /// <summary>.unitypackage package id, or UPM package name.</summary>
        public string Id = "";
        public string DisplayName = "";
        public string Subtitle;
        public string LatestVersion;
        /// <summary>Imported (.unitypackage) or installed (UPM) version; null when not in the project.</summary>
        public string InstalledVersion;
        public string Description;
        /// <summary>Imported, but none of its files are left in the project.</summary>
        public bool Missing;
        /// <summary>In the project, but the server / registry no longer lists it.</summary>
        public bool Gone;

        public string Key => (IsUpm ? "upm:" : "pkg:") + Id;

        public string Tooltip
        {
            get
            {
                if (Gone) return IsUpm ? "The registry no longer has this package." : "The server no longer has this package.";
                if (Missing) return $"Imported {InstalledVersion}, but its files are no longer in the project";
                if (InstalledVersion != null && LatestVersion != null && InstalledVersion != LatestVersion)
                    return $"Using {InstalledVersion} · latest is {LatestVersion}";
                return null;
            }
        }

        public bool Matches(string query) =>
            Contains(DisplayName, query) || Contains(Id, query) || Contains(Subtitle, query) || Contains(Description, query);

        private static bool Contains(string value, string query) =>
            value != null && value.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }
}
