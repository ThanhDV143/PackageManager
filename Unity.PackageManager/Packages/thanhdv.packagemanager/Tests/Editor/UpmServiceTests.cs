using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PackageManager.Shared;

namespace ThanhDV.PackageManager.Tests
{
    public class UpmServiceTests
    {
        private static UpmPackageDetailDto Package(string name, params (string Version, Dictionary<string, string> Dependencies)[] versions)
        {
            var package = new UpmPackageDetailDto { Name = name };
            foreach (var v in versions)
                package.Versions.Add(new UpmVersionDto { Version = v.Version, Dependencies = v.Dependencies });
            return package;
        }

        private static Dictionary<string, string> Deps(params string[] pairs)
        {
            var result = new Dictionary<string, string>();
            for (int i = 0; i < pairs.Length; i += 2) result[pairs[i]] = pairs[i + 1];
            return result;
        }

        // The fakes complete synchronously, so .Result never waits on the editor loop.
        private static List<string> Resolve(UpmPackageDetailDto package, string version, params UpmPackageDetailDto[] registry)
        {
            var byName = new Dictionary<string, UpmPackageDetailDto>();
            foreach (var p in registry) byName[p.Name] = p;
            package.KnownPackages = new List<string>(byName.Keys) { package.Name };
            var requested = new List<string>();
            var result = UpmService.RegistryDependencies(package, version, name =>
            {
                requested.Add(name);
                return Task.FromResult(byName.TryGetValue(name, out var p) ? p : null);
            }).Result;
            CollectionAssert.AllItemsAreUnique(requested);
            return result;
        }

        [Test]
        public void RegistryDependencies_FollowsIndirectOnes_SkippingOthers()
        {
            var bugCatcher = Package("thanhdv.bugcatcher",
                ("1.0.0", Deps("com.tayx.graphy", "4.0.0", "com.yasirkula.ingamedebugconsole", "1.9.0", "com.unity.ugui", "1.0.0")));
            var graphy = Package("com.tayx.graphy", ("4.0.0", Deps("com.tayx.core", "1.0.0", "com.cysharp.unitask", "2.0.0")));
            var console = Package("com.yasirkula.ingamedebugconsole", ("1.9.0", Deps()));
            var core = Package("com.tayx.core", ("1.0.0", Deps()));

            CollectionAssert.AreEquivalent(
                new[] { "com.tayx.graphy", "com.yasirkula.ingamedebugconsole", "com.tayx.core" },
                Resolve(bugCatcher, "1.0.0", graphy, console, core));
        }

        [Test]
        public void RegistryDependencies_UsesTheInstalledVersion()
        {
            var bugCatcher = Package("thanhdv.bugcatcher",
                ("2.0.0", Deps("com.tayx.graphy", "4.0.0")),
                ("1.0.0", Deps("com.yasirkula.ingamedebugconsole", "1.9.0")));
            var graphy = Package("com.tayx.graphy", ("4.0.0", Deps()));
            var console = Package("com.yasirkula.ingamedebugconsole", ("1.9.0", Deps()));

            CollectionAssert.AreEqual(new[] { "com.yasirkula.ingamedebugconsole" }, Resolve(bugCatcher, "1.0.0", graphy, console));
        }

        [Test]
        public void RegistryDependencies_FollowsEachRequestedVersion()
        {
            var bugCatcher = Package("thanhdv.bugcatcher", ("1.0.0", Deps("thanhdv.a", "1.0.0", "thanhdv.b", "1.0.0")));
            var a = Package("thanhdv.a", ("1.0.0", Deps("thanhdv.c", "1.0.0")));
            var b = Package("thanhdv.b", ("1.0.0", Deps("thanhdv.c", "2.0.0")));
            var c = Package("thanhdv.c", ("2.0.0", Deps("com.tayx.graphy", "4.0.0")), ("1.0.0", Deps()));
            var graphy = Package("com.tayx.graphy", ("4.0.0", Deps()));

            CollectionAssert.AreEquivalent(
                new[] { "thanhdv.a", "thanhdv.b", "thanhdv.c", "com.tayx.graphy" },
                Resolve(bugCatcher, "1.0.0", a, b, c, graphy));
        }

        [Test]
        public void RegistryDependencies_StopsOnCycles()
        {
            var a = Package("thanhdv.a", ("1.0.0", Deps("thanhdv.b", "1.0.0")));
            var b = Package("thanhdv.b", ("1.0.0", Deps("thanhdv.a", "1.0.0")));

            CollectionAssert.AreEqual(new[] { "thanhdv.b" }, Resolve(a, "1.0.0", b));
        }
    }
}
