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

        [Test]
        public void RecordImport_AsDependency_IsNotExplicit_UntilTheUserImportsIt()
        {
            var data = new ProjectData();
            data.RecordImport("com.a.dep", "Dep", "1", new[] { "a" }, Now, "com.x.tool");
            var record = data.Find("com.a.dep");
            Assert.IsFalse(record.Explicit);
            CollectionAssert.AreEqual(new[] { "com.x.tool" }, record.RequiredBy);

            data.RecordImport("com.a.dep", "Dep", "2", new[] { "a" }, Now, "com.b.other");
            Assert.IsFalse(record.Explicit);
            CollectionAssert.AreEqual(new[] { "com.b.other", "com.x.tool" }, record.RequiredBy);

            data.RecordImport("com.a.dep", "Dep", "2", new[] { "a" }, Now);
            Assert.IsTrue(record.Explicit);
        }

        [Test]
        public void DropRequiredBy_ReturnsOnlyPackagesNothingElseNeeds()
        {
            var data = new ProjectData();
            data.RecordImport("com.a.only", "Only", "1", new[] { "a" }, Now, "com.x.tool");
            data.RecordImport("com.a.shared", "Shared", "1", new[] { "b" }, Now, "com.x.tool");
            data.AddRequiredBy(data.Find("com.a.shared"), "com.y.tool");
            data.RecordImport("com.a.mine", "Mine", "1", new[] { "c" }, Now);
            data.AddRequiredBy(data.Find("com.a.mine"), "com.x.tool");
            data.RecordImport("com.a.kept", "Kept", "1", new[] { "d" }, Now, "com.x.tool");

            var orphans = data.DropRequiredBy("com.x.tool", new[] { "com.a.kept" });

            CollectionAssert.AreEqual(new[] { "com.a.only" }, orphans.ConvertAll(o => o.PackageId));
            CollectionAssert.AreEqual(new[] { "com.y.tool" }, data.Find("com.a.shared").RequiredBy);
            CollectionAssert.IsEmpty(data.Find("com.a.mine").RequiredBy);
            CollectionAssert.AreEqual(new[] { "com.x.tool" }, data.Find("com.a.kept").RequiredBy);
        }

        [Test]
        public void ExplicitDefaultsToTrue_ForRecordsWrittenBeforeUDependencies()
        {
            var record = Newtonsoft.Json.JsonConvert.DeserializeObject<ImportedPackage>("{\"packageId\":\"com.a.old\",\"version\":\"1\"}");
            Assert.IsTrue(record.Explicit);
            CollectionAssert.IsEmpty(record.RequiredBy);
        }

        [Test]
        public void Parse_ReadsUDependencies_AndSkipsBadEntries()
        {
            var deps = DependencyImporter.Parse(
                "{\"name\":\"com.x.tool\",\"udependencies\":{\"com.a.feel\":\"5.5\",\" com.a.any \":\"\",\"com.a.bad\":5}}");
            CollectionAssert.AreEquivalent(new[] { "com.a.feel", "com.a.any" }, deps.Keys);
            Assert.AreEqual("5.5", deps["com.a.feel"]);
            Assert.AreEqual("", deps["com.a.any"]);
            CollectionAssert.IsEmpty(DependencyImporter.Parse("{\"name\":\"com.x.tool\"}"));
        }
    }
}
