using System;
using NUnit.Framework;

namespace ThanhDV.PackageManager.Tests
{
    public class ImportRecordTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void RecordImport_OverwritesVersion_AndMergesGuids()
        {
            var data = new ProjectData();
            data.RecordImport("com.a.feel", "Feel", "5.5", new[] { "b", "a" }, Now);
            data.RecordImport("com.a.feel", "Feel", "5.8", new[] { "c", "a" }, Now.AddDays(1));

            var record = data.Find("com.a.feel");
            Assert.AreEqual(1, data.Imported.Count);
            Assert.AreEqual("5.8", record.Version);
            Assert.AreEqual(Now.AddDays(1), record.ImportedAt);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, record.Guids);
        }

        [Test]
        public void RemovableGuids_SkipsAssetsAnotherPackageOwns()
        {
            var data = new ProjectData();
            data.RecordImport("com.a.one", "One", "1", new[] { "own1", "shared", "own2" }, Now);
            data.RecordImport("com.a.two", "Two", "1", new[] { "shared", "other" }, Now);

            CollectionAssert.AreEquivalent(new[] { "own1", "own2" }, PackageRemover.RemovableGuids(data, "com.a.one"));
            Assert.AreEqual(1, PackageRemover.SharedCount(data, "com.a.one"));
            CollectionAssert.IsEmpty(PackageRemover.RemovableGuids(data, "com.a.missing"));
        }
    }
}
