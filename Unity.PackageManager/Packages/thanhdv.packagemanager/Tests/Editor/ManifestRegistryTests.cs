using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ThanhDV.PackageManager.Tests
{
    public class ManifestRegistryTests
    {
        private const string Url = "https://upm.thanhdv.com";

        [TestCase("com.thanhdv.utils", "com.thanhdv")]
        [TestCase("com.thanhdv.lib.utilities", "com.thanhdv")]
        [TestCase("thanhdv.utilities", "thanhdv")]
        [TestCase("com.thanhdv", "com.thanhdv")]
        [TestCase("tool", "tool")]
        [TestCase("com.unity.textmeshpro", null)]
        [TestCase("com.unity", null)]
        public void ScopeFor_NeverCoversUnityPackages(string name, string scope) =>
            Assert.AreEqual(scope, ManifestRegistry.ScopeFor(name));

        [Test]
        public void Ensure_AddsRegistry_KeepingDependencies()
        {
            var manifest = JObject.Parse("{\"dependencies\":{\"com.unity.ugui\":\"1.0.0\"}}");
            Assert.IsTrue(ManifestRegistry.Ensure(manifest, "ThanhDV", Url, "com.thanhdv.utils"));
            Assert.AreEqual("1.0.0", (string)manifest["dependencies"]["com.unity.ugui"]);
            var registry = manifest["scopedRegistries"][0];
            Assert.AreEqual("ThanhDV", (string)registry["name"]);
            Assert.AreEqual(Url, (string)registry["url"]);
            CollectionAssert.AreEqual(new[] { "com.thanhdv" }, registry["scopes"].ToObject<string[]>());
        }

        [Test]
        public void Ensure_AddsScopeToTheSameUrl_IgnoringTrailingSlash()
        {
            var manifest = JObject.Parse("{\"scopedRegistries\":[{\"name\":\"Mine\",\"url\":\"https://upm.thanhdv.com/\",\"scopes\":[\"com.other\"]}]}");
            Assert.IsTrue(ManifestRegistry.Ensure(manifest, "ThanhDV", Url, "com.thanhdv.utils"));
            var registries = (JArray)manifest["scopedRegistries"];
            Assert.AreEqual(1, registries.Count);
            Assert.AreEqual("Mine", (string)registries[0]["name"]);
            CollectionAssert.AreEqual(new[] { "com.other", "com.thanhdv" }, registries[0]["scopes"].ToObject<string[]>());
        }

        [Test]
        public void Ensure_AddsScopeToTheSameHost_IgnoringScheme()
        {
            var manifest = JObject.Parse("{\"scopedRegistries\":[{\"name\":\"ThanhDV\",\"url\":\"http://upm.thanhdv.com\",\"scopes\":[\"thanhdv\"]}]}");
            Assert.IsTrue(ManifestRegistry.Ensure(manifest, "ThanhDV", Url, "com.thanhdv.utils"));
            var registries = (JArray)manifest["scopedRegistries"];
            Assert.AreEqual(1, registries.Count);
            Assert.AreEqual("http://upm.thanhdv.com", (string)registries[0]["url"]);
            CollectionAssert.AreEqual(new[] { "thanhdv", "com.thanhdv" }, registries[0]["scopes"].ToObject<string[]>());
        }

        [Test]
        public void Ensure_LeavesOtherRegistriesAlone()
        {
            var manifest = JObject.Parse("{\"scopedRegistries\":[{\"name\":\"OpenUPM\",\"url\":\"https://package.openupm.com\",\"scopes\":[\"com.cysharp\"]}]}");
            Assert.IsTrue(ManifestRegistry.Ensure(manifest, "ThanhDV", Url, "com.thanhdv.utils"));
            var registries = (JArray)manifest["scopedRegistries"];
            Assert.AreEqual(2, registries.Count);
            CollectionAssert.AreEqual(new[] { "com.cysharp" }, registries[0]["scopes"].ToObject<string[]>());
        }

        [Test]
        public void Ensure_NoChangeWhenAlreadyCovered()
        {
            var manifest = JObject.Parse("{\"scopedRegistries\":[{\"name\":\"ThanhDV\",\"url\":\"https://upm.thanhdv.com\",\"scopes\":[\"com.thanhdv\"]}]}");
            string before = manifest.ToString();
            Assert.IsFalse(ManifestRegistry.Ensure(manifest, "ThanhDV", Url, "com.thanhdv.utils"));
            Assert.AreEqual(before, manifest.ToString());
        }

        [Test]
        public void Ensure_RefusesScopeAnotherRegistryServes()
        {
            var manifest = JObject.Parse("{\"scopedRegistries\":[{\"name\":\"OpenUPM\",\"url\":\"https://package.openupm.com\",\"scopes\":[\"com.cysharp\"]}]}");
            string before = manifest.ToString();
            Assert.Throws<InvalidOperationException>(() => ManifestRegistry.Ensure(manifest, "ThanhDV", Url, "com.cysharp.unitask"));
            Assert.AreEqual(before, manifest.ToString());
        }

        [Test]
        public void Ensure_RefusesSameNameAtAnotherUrl()
        {
            var manifest = JObject.Parse("{\"scopedRegistries\":[{\"name\":\"ThanhDV\",\"url\":\"http://192.168.1.10:4873\",\"scopes\":[\"thanhdv\"]}]}");
            string before = manifest.ToString();
            Assert.Throws<InvalidOperationException>(() => ManifestRegistry.Ensure(manifest, "ThanhDV", Url, "com.thanhdv.utils"));
            Assert.AreEqual(before, manifest.ToString());
        }

        [Test]
        public void EnsureDependency_RefusesSameNameAtAnotherUrl()
        {
            var manifest = JObject.Parse("{\"scopedRegistries\":[{\"name\":\"ThanhDV\",\"url\":\"http://192.168.1.10:4873\",\"scopes\":[\"thanhdv\"]}]}");
            Assert.Throws<InvalidOperationException>(() => ManifestRegistry.EnsureDependency(manifest, "ThanhDV", Url, "com.tayx.graphy", out _));
        }

        [Test]
        public void Ensure_RefusesUnityScope() =>
            Assert.Throws<InvalidOperationException>(() => ManifestRegistry.Ensure(new JObject(), "ThanhDV", Url, "com.unity.forked"));

        [Test]
        public void EnsureDependency_AddsItsScopeToTheRegistry()
        {
            var manifest = JObject.Parse("{\"scopedRegistries\":[{\"name\":\"ThanhDV\",\"url\":\"https://upm.thanhdv.com/\",\"scopes\":[\"thanhdv\"]}]}");
            Assert.IsTrue(ManifestRegistry.EnsureDependency(manifest, "ThanhDV", Url, "com.tayx.graphy", out string servedBy));
            Assert.IsNull(servedBy);
            CollectionAssert.AreEqual(new[] { "thanhdv", "com.tayx" }, manifest["scopedRegistries"][0]["scopes"].ToObject<string[]>());
        }

        [Test]
        public void EnsureDependency_AddsRegistryWhenMissing()
        {
            var manifest = new JObject();
            Assert.IsTrue(ManifestRegistry.EnsureDependency(manifest, "ThanhDV", Url, "com.tayx.graphy", out _));
            CollectionAssert.AreEqual(new[] { "com.tayx" }, manifest["scopedRegistries"][0]["scopes"].ToObject<string[]>());
        }

        [Test]
        public void EnsureDependency_NoChangeWhenAlreadyCovered()
        {
            var manifest = JObject.Parse("{\"scopedRegistries\":[{\"name\":\"ThanhDV\",\"url\":\"https://upm.thanhdv.com\",\"scopes\":[\"com.tayx\"]}]}");
            string before = manifest.ToString();
            Assert.IsFalse(ManifestRegistry.EnsureDependency(manifest, "ThanhDV", Url, "com.tayx.graphy", out string servedBy));
            Assert.IsNull(servedBy);
            Assert.AreEqual(before, manifest.ToString());
        }

        [TestCase("com.tayx")]
        [TestCase("com.tayx.graphy")]
        public void EnsureDependency_LeavesItWithTheRegistryServingIt(string openUpmScope)
        {
            var manifest = JObject.Parse("{\"scopedRegistries\":[{\"name\":\"OpenUPM\",\"url\":\"https://package.openupm.com\",\"scopes\":[\"" + openUpmScope + "\"]}]}");
            string before = manifest.ToString();
            Assert.IsFalse(ManifestRegistry.EnsureDependency(manifest, "ThanhDV", Url, "com.tayx.graphy", out string servedBy));
            Assert.AreEqual("OpenUPM", servedBy);
            Assert.AreEqual(before, manifest.ToString());
        }

        [Test]
        public void EnsureDependency_LongestScopeWins()
        {
            var manifest = JObject.Parse("{\"scopedRegistries\":[" +
                "{\"name\":\"ThanhDV\",\"url\":\"https://upm.thanhdv.com\",\"scopes\":[\"com.tayx\"]}," +
                "{\"name\":\"OpenUPM\",\"url\":\"https://package.openupm.com\",\"scopes\":[\"com.tayx.graphy\"]}]}");
            Assert.IsFalse(ManifestRegistry.EnsureDependency(manifest, "ThanhDV", Url, "com.tayx.graphy", out string servedBy));
            Assert.AreEqual("OpenUPM", servedBy);
        }

        [Test]
        public void EnsureDependency_IgnoresNarrowerScopesOfOtherRegistries()
        {
            var manifest = JObject.Parse("{\"scopedRegistries\":[{\"name\":\"OpenUPM\",\"url\":\"https://package.openupm.com\",\"scopes\":[\"com.tayx.other\"]}]}");
            Assert.IsTrue(ManifestRegistry.EnsureDependency(manifest, "ThanhDV", Url, "com.tayx.graphy", out string servedBy));
            Assert.IsNull(servedBy);
            CollectionAssert.AreEqual(new[] { "com.tayx" }, manifest["scopedRegistries"][1]["scopes"].ToObject<string[]>());
        }

        [Test]
        public void EnsureDependency_SkipsUnityPackages()
        {
            var manifest = new JObject();
            Assert.IsFalse(ManifestRegistry.EnsureDependency(manifest, "ThanhDV", Url, "com.unity.textmeshpro", out string servedBy));
            Assert.IsNull(servedBy);
            Assert.AreEqual("{}", manifest.ToString(Newtonsoft.Json.Formatting.None));
        }

        [Test]
        public void SharedScopes_ListsScopesAnotherRegistryHasToo()
        {
            var manifest = JObject.Parse("{\"scopedRegistries\":[" +
                "{\"name\":\"ThanhDV\",\"url\":\"https://upm.thanhdv.com/\",\"scopes\":[\"thanhdv\",\"com.tayx\"]}," +
                "{\"name\":\"OpenUPM\",\"url\":\"https://package.openupm.com\",\"scopes\":[\"com.tayx\",\"com.tayx.graphy\"]}]}");
            CollectionAssert.AreEqual(new[] { "com.tayx" }, ManifestRegistry.SharedScopes(manifest, Url));
        }
    }
}
