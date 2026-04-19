using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Meta-test that enforces the "characterization test" convention across
    /// the suite. Session 43 established the pattern via
    /// `feedback_characterization_tests_for_known_bugs.md`:
    ///   1. A latent bug gets a test that asserts the CURRENT (wrong) behavior
    ///   2. The test is named CoverageAudit_Known* or similar ("Known", "Characterization")
    ///   3. The test body contains a comment with "flip", "pin", or "characterization"
    ///      explaining that this assertion inverts when the bug is fixed
    ///
    /// This sentinel pins the CURRENT count of known-characterization tests.
    /// When someone adds a new one without following the convention (no comment
    /// explaining the pattern), the count mismatches and this test fires with
    /// a message pointing to the memory note.
    /// </summary>
    public class CharacterizationTestSentinelTests
    {
        /// <summary>
        /// Walks the Tests/ directory and counts methods whose name matches
        /// the CoverageAudit_Known* / *Characterization* / *KnownUncovered*
        /// patterns. Returns the full list for the sentinel to assert on.
        /// </summary>
        private static List<string> FindCharacterizationTests()
        {
            // Walk up from the test assembly's working directory to find the
            // Tests/ source tree. Falls back gracefully if layout changes.
            var baseDir = Directory.GetCurrentDirectory();
            DirectoryInfo? repoRoot = new DirectoryInfo(baseDir);
            while (repoRoot != null && !Directory.Exists(Path.Combine(repoRoot.FullName, "Tests")))
                repoRoot = repoRoot.Parent;
            if (repoRoot == null) return new List<string>();

            var testsDir = Path.Combine(repoRoot.FullName, "Tests");
            var files = Directory.GetFiles(testsDir, "*.cs", SearchOption.AllDirectories);

            // Match method names that indicate a characterization test:
            //   public void CoverageAudit_KnownUncoveredSkillsets_StillReturnNull()
            //   public void KnownBug_*_Characterization()
            //   public void *_CharacterizesCurrentBehavior()
            // Exclude this file itself (it contains the pattern strings).
            var pattern = new Regex(
                @"public\s+void\s+(\w*(?:CoverageAudit_Known|KnownUncovered|Characteriz|DocumentsCurrentBehavior|CharacterizesCurrent)\w*)\s*\(",
                RegexOptions.IgnoreCase);

            var found = new List<string>();
            foreach (var file in files)
            {
                if (Path.GetFileName(file) == nameof(CharacterizationTestSentinelTests) + ".cs")
                    continue;
                var text = File.ReadAllText(file);
                foreach (Match m in pattern.Matches(text))
                    found.Add($"{Path.GetFileName(file)}::{m.Groups[1].Value}");
            }
            return found;
        }

        [Fact]
        public void CurrentCharacterizationTestCount_MatchesExpected()
        {
            // Pin the count. When a new characterization test is added, update
            // this number AND confirm the new test follows the convention:
            // - Named with CoverageAudit_Known* / *Characterization* /
            //   *DocumentsCurrentBehavior*
            // - Has a body comment explaining WHY the assertion is "wrong"
            // - Has a TODO.md §0 entry referencing the test (optional but
            //   recommended)
            //
            // See memory/feedback_characterization_tests_for_known_bugs.md
            // for the full convention.
            var tests = FindCharacterizationTests();
            const int expected = 1; // Current: AbilityJpCostsTests.CoverageAudit_KnownUncoveredSkillsets_StillReturnNull
            Assert.True(
                tests.Count == expected,
                $"Expected {expected} characterization tests, found {tests.Count}:\n  " +
                string.Join("\n  ", tests) +
                "\n\nIf you added a new characterization test: (a) confirm it follows the naming + comment convention in feedback_characterization_tests_for_known_bugs.md, (b) update `expected` in this test." +
                "\n\nIf you REMOVED one (via a bug fix): (a) confirm the assertion is flipped or the test deleted, (b) decrement `expected`.");
        }

        [Fact]
        public void FindCharacterizationTests_ReturnsNonEmpty_WhenSuiteHasCharacterizations()
        {
            // Smoke test: the scanner should find at least one characterization
            // test in the current suite (CoverageAudit_KnownUncoveredSkillsets).
            // If this fires with zero results, either the suite has lost its
            // characterization tests (suspicious — likely accidental mass
            // deletion) or the scanner regex broke.
            var tests = FindCharacterizationTests();
            Assert.NotEmpty(tests);
        }

        [Fact]
        public void FindCharacterizationTests_Includes_KnownEntry()
        {
            // Concrete assertion: the scanner finds the known characterization
            // test AbilityJpCostsTests.CoverageAudit_KnownUncoveredSkillsets_StillReturnNull.
            // If this fires, either that test was deleted (did the underlying
            // bug get fixed? update expected= in the count test above) or the
            // scanner regex regressed.
            var tests = FindCharacterizationTests();
            Assert.Contains(tests, t => t.Contains("CoverageAudit_KnownUncoveredSkillsets_StillReturnNull"));
        }
    }
}
