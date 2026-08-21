using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.ThemeEditor;
using Xunit;

namespace Tests.Services
{
    /// <summary>
    /// CC-26 guard. UserThemeServiceSingleton, CharacterServiceSingleton and
    /// JobClassServiceSingleton are static, process-wide globals that several test classes
    /// point at their own throwaway temp folder. If a test sets one of these and never puts it
    /// back, the folder can vanish (test teardown deletes it) while some other, unrelated test
    /// is still reading through the global, which is exactly the "file not found" flake this
    /// row fixed. This file enforces the two halves of the contract that prevents it from
    /// coming back:
    ///
    /// A. Reset() must leave nothing behind. Every private static field on the three singleton
    ///    types (other than a lock object) must be null after Reset() runs, once something has
    ///    set it. (This is the exact bug CC-26 found: CharacterServiceSingleton.Reset() nulled
    ///    the cached instance but left the static mod path pointing at the deleted folder.)
    /// B. Every call site that sets one of these globals (Initialize/SetModPath) lives in a
    ///    source file that also calls the matching Reset(), so a new test can't add a setter
    ///    without pairing it.
    /// </summary>
    public class SingletonHygieneGuardTests
    {
        [Fact]
        public void UserThemeServiceSingleton_Reset_Clears_Every_Static_Field()
        {
            UserThemeServiceSingleton.Initialize(Path.Combine(Path.GetTempPath(), "SingletonHygiene_UTS"));
            UserThemeServiceSingleton.Reset();

            AssertNoStaticStateLeft(typeof(UserThemeServiceSingleton));
        }

        [Fact]
        public void CharacterServiceSingleton_Reset_Clears_Every_Static_Field()
        {
            CharacterServiceSingleton.SetModPath(Path.Combine(Path.GetTempPath(), "SingletonHygiene_CSS"));
            CharacterServiceSingleton.Reset();

            AssertNoStaticStateLeft(typeof(CharacterServiceSingleton));
        }

        [Fact]
        public void JobClassServiceSingleton_Reset_Clears_Every_Static_Field()
        {
            JobClassServiceSingleton.Initialize(Path.Combine(Path.GetTempPath(), "SingletonHygiene_JCS"));
            JobClassServiceSingleton.Reset();

            AssertNoStaticStateLeft(typeof(JobClassServiceSingleton));
        }

        /// <summary>
        /// Fails a field left non-null after Reset(). Skips lock objects (readonly, never
        /// meant to be nulled) and value-type fields (nothing to leak).
        /// </summary>
        private static void AssertNoStaticStateLeft(Type singletonType)
        {
            var leftovers = new List<string>();

            foreach (var field in singletonType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.FieldType.IsValueType)
                    continue;
                if (field.IsInitOnly && field.FieldType == typeof(object))
                    continue; // lock objects: readonly, intentionally never null

                var value = field.GetValue(null);
                if (value != null)
                    leftovers.Add($"{singletonType.Name}.{field.Name}");
            }

            Assert.True(leftovers.Count == 0,
                $"Reset() left state behind: [{string.Join(", ", leftovers)}]. A later test that " +
                "reads Instance can be pointed at a temp folder this test already deleted.");
        }

        [Fact]
        public void Every_Initialize_Or_SetModPath_Call_Is_Paired_With_A_Reset_In_The_Same_File()
        {
            var testsRoot = TestsRoot();
            var violations = new List<string>();

            var rules = new (string setter, string resetter)[]
            {
                ("UserThemeServiceSingleton.Initialize(", "UserThemeServiceSingleton.Reset()"),
                ("CharacterServiceSingleton.SetModPath(", "CharacterServiceSingleton.Reset()"),
                ("JobClassServiceSingleton.Initialize(", "JobClassServiceSingleton.Reset()"),
            };

            foreach (var path in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
            {
                // This file legitimately names every setter/resetter pair without pairing them
                // 1:1 per occurrence; it is the contract, not a subject of it.
                if (Path.GetFileName(path) == "SingletonHygieneGuardTests.cs")
                    continue;

                var text = File.ReadAllText(path);

                foreach (var (setter, resetter) in rules)
                {
                    if (text.Contains(setter) && !text.Contains(resetter))
                    {
                        violations.Add($"{RelativePath(testsRoot, path)} calls {setter}...) but never {resetter}");
                    }
                }
            }

            Assert.True(violations.Count == 0,
                "Found a setter with no matching Reset() in the same file:\n" + string.Join("\n", violations));
        }

        private static string TestsRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "docs", "TODO.md")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "Tests")))
                    return Path.Combine(dir.FullName, "Tests");
                dir = dir.Parent;
            }
            throw new FileNotFoundException("repo root (docs/TODO.md + Tests/) not found above the test bin dir");
        }

        private static string RelativePath(string root, string full) =>
            full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar);
    }
}
