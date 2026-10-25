// Copied as-is into the Unity package (Editor/Shared); SharedCopyTests keeps the two in sync.
// Unity doesn't enable nullable annotations project-wide, hence the directive.
#nullable enable
using System;
using System.Collections.Generic;

namespace PackageManager.Shared
{
    /// <summary>
    /// Compares version strings cluster by cluster: numeric runs numerically, other runs ordinally
    /// ("2.02.30" &gt; "2.02.4", "1.10" &gt; "1.9").
    /// </summary>
    public sealed class VersionComparer : IComparer<string?>
    {
        public static readonly VersionComparer Instance = new VersionComparer();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                bool dx = char.IsDigit(x[i]), dy = char.IsDigit(y[j]);
                int si = i, sj = j;
                if (dx && dy)
                {
                    while (i < x.Length && char.IsDigit(x[i])) i++;
                    while (j < y.Length && char.IsDigit(y[j])) j++;
                    int c = CompareNumeric(x.Substring(si, i - si), y.Substring(sj, j - sj));
                    if (c != 0) return c;
                }
                else if (!dx && !dy)
                {
                    while (i < x.Length && !char.IsDigit(x[i])) i++;
                    while (j < y.Length && !char.IsDigit(y[j])) j++;
                    int c = string.Compare(x.Substring(si, i - si), y.Substring(sj, j - sj), StringComparison.OrdinalIgnoreCase);
                    if (c != 0) return c;
                }
                else
                {
                    // A number ranks above text at the same position ("1.0.1" > "1.0-beta").
                    return dx ? 1 : -1;
                }
            }
            return (x.Length - i).CompareTo(y.Length - j);
        }

        private static int CompareNumeric(string a, string b)
        {
            a = a.TrimStart('0');
            b = b.TrimStart('0');
            if (a.Length != b.Length) return a.Length.CompareTo(b.Length);
            return string.CompareOrdinal(a, b);
        }
    }
}
