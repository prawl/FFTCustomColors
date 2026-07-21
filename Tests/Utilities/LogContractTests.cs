using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FFTColorCustomizer.Core;
using Xunit;

namespace FFTColorCustomizer.Tests.Utilities
{
    /// <summary>
    /// The logging contract's enforcement gate (docs/LOGGING.md), ported from the sibling FFT
    /// mods (CC-2 stage 2). Source-scan checks over ColorMod/**/*.cs:
    ///
    /// 1. The LogVerb enum matches docs/LOGGING.md's committed verb table one-for-one: the doc
    ///    and the code cannot drift.
    /// 2. Raw Console writes live only inside the facade plumbing, plus a SHRINKING per-file
    ///    allowlist of pre-existing stragglers: the conversion sweeps remove entries, and a
    ///    cleaned file can never regress (the allowlist itself is checked for rot).
    /// 3. No string literal passed to a ModLogger call contains a double-dash separator or an
    ///    em dash.
    /// 4. Typed console-eligible messages (ModLogger.Log/LogWarning/LogError with a LogVerb)
    ///    pass the subject-first lexical fence: open with an uppercase letter or an
    ///    interpolation hole, never a bare "Word:" leader.
    /// </summary>
    public class LogContractTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "docs", "LOGGING.md")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "ColorMod")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new FileNotFoundException("repo root (docs/LOGGING.md + ColorMod/) not found above the test bin dir");
        }

        private static IEnumerable<string> SourceFiles(string repoRoot)
        {
            string root = Path.Combine(repoRoot, "ColorMod");
            return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.Combine("obj", "")) && !f.Contains(Path.Combine("bin", "")));
        }

        /// <summary>The facade's own plumbing: the only files allowed to touch raw sinks
        /// permanently.</summary>
        private static readonly HashSet<string> PermanentAllowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ModLogger.cs", "FileConsoleLogger.cs", "ConsoleLogger.cs", "NullLogger.cs",
        };

        /// <summary>Pre-existing raw-Console stragglers (census 2026-07-21), retired sweep by
        /// sweep. REMOVE a file from this list in the sweep that cleans it; never add one.
        /// When this list is empty, stage 6 deletes it and the contract turns fully strict.</summary>
        private static readonly HashSet<string> ShrinkingConsoleAllowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ConfigurationForm.cs", "DebugResources.cs", "Mod.cs", "RamzaTexThemeService.cs",
        };

        // --- 1. LogVerb <-> docs/LOGGING.md lockstep ---

        private static readonly Regex VerbTableRowRegex = new Regex(@"^\|\s*`([a-z][a-z-]*)`\s*\|", RegexOptions.Compiled);

        private static List<string> ParseVerbTokensFromLoggingMd(string repoRoot)
        {
            string path = Path.Combine(repoRoot, "docs", "LOGGING.md");
            var verbs = new List<string>();
            bool inTable = false;
            foreach (var raw in File.ReadAllLines(path))
            {
                if (!inTable)
                {
                    if (raw.StartsWith("| Verb |")) inTable = true;
                    continue;
                }
                if (raw.StartsWith("|---") || raw.StartsWith("|-")) continue;
                var m = VerbTableRowRegex.Match(raw);
                if (m.Success) verbs.Add(m.Groups[1].Value);
                else if (!raw.StartsWith("|")) break;
            }
            return verbs;
        }

        [Fact]
        public void LogVerb_enum_matches_the_committed_LOGGING_md_verb_table_one_for_one()
        {
            var docVerbs = ParseVerbTokensFromLoggingMd(RepoRoot());
            Assert.NotEmpty(docVerbs);
            var enumVerbs = Enum.GetValues<LogVerb>().Select(v => v.Token()).ToList();

            Assert.Equal(docVerbs.Distinct().Count(), docVerbs.Count);
            Assert.Equal(enumVerbs.Distinct().Count(), enumVerbs.Count);

            var docSet = new HashSet<string>(docVerbs);
            var enumSet = new HashSet<string>(enumVerbs);
            Assert.True(docSet.SetEquals(enumSet),
                $"docs/LOGGING.md verb table and LogVerb are out of lockstep. " +
                $"In doc but not enum: [{string.Join(", ", docSet.Except(enumSet))}]. " +
                $"In enum but not doc: [{string.Join(", ", enumSet.Except(docSet))}].");
        }

        // --- 2. Raw Console writes: facade plumbing plus the shrinking allowlist only ---

        private static readonly Regex ConsoleWriteRegex = new Regex(@"\bConsole\.(WriteLine|Write|Error)\b", RegexOptions.Compiled);

        [Fact]
        public void Console_writes_live_only_inside_the_facade_or_the_shrinking_allowlist()
        {
            var offenders = new List<string>();
            foreach (var path in SourceFiles(RepoRoot()))
            {
                string name = Path.GetFileName(path);
                if (PermanentAllowList.Contains(name) || ShrinkingConsoleAllowList.Contains(name)) continue;
                if (ConsoleWriteRegex.IsMatch(File.ReadAllText(path))) offenders.Add(name);
            }
            Assert.True(offenders.Count == 0,
                "Files writing to the console outside the facade plumbing and the shrinking allowlist "
                + "(route through ModLogger; never ADD to the allowlist):\n" + string.Join("\n", offenders));
        }

        [Fact]
        public void The_shrinking_allowlist_does_not_rot()
        {
            // Every allowlisted file must still exist and still contain a raw console write;
            // a sweep that cleans a file must also delete its allowlist entry, so the list
            // only ever shrinks and reflects reality.
            var stale = new List<string>();
            var byName = SourceFiles(RepoRoot()).ToLookup(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
            foreach (var name in ShrinkingConsoleAllowList)
            {
                var matches = byName[name].ToList();
                if (matches.Count == 0) { stale.Add($"{name}: no such production file"); continue; }
                if (!matches.Any(p => ConsoleWriteRegex.IsMatch(File.ReadAllText(p))))
                    stale.Add($"{name}: contains no raw Console write anymore; remove it from the allowlist");
            }
            Assert.True(stale.Count == 0, "Stale allowlist entries:\n" + string.Join("\n", stale));
        }

        // --- 3. No double-dash / em dash inside a ModLogger call's string literals ---

        private static readonly Regex StringLiteralRegex = new Regex(@"\$?@?""(?:[^""\\]|\\.)*""", RegexOptions.Compiled);
        private const char EmDash = '—';  // by escape: log text bans the literal character

        /// <summary>Finds every ModLogger call and returns the string-literal contents inside
        /// each call's argument list. Balances parens and skips string bodies so interpolation
        /// holes cannot desync the scan.</summary>
        internal static List<string> FacadeCallStringLiterals(string source)
        {
            var results = new List<string>();
            var callStart = new Regex(@"\bModLogger\.(Log|LogWarning|LogError|LogDebug|LogException|LogSuccess|LogSection)\s*\(");
            foreach (Match m in callStart.Matches(source))
            {
                string args = ExtractBalancedArgs(source, m.Index);
                if (args == null) continue;
                foreach (Match lit in StringLiteralRegex.Matches(args))
                    results.Add(lit.Value);
            }
            return results;
        }

        [Fact]
        public void FacadeCallStringLiterals_detects_a_double_dash_separator()
        {
            var literals = FacadeCallStringLiterals("ModLogger.Log(LogVerb.Theme, \"applied -- 2 files\");");
            Assert.Contains(literals, l => l.Contains(" -- "));
        }

        [Fact]
        public void FacadeCallStringLiterals_detects_an_em_dash()
        {
            var literals = FacadeCallStringLiterals($"ModLogger.LogWarning(LogVerb.Ramza, \"fallback{EmDash}previous look stays\");");
            Assert.Contains(literals, l => l.Contains(EmDash));
        }

        [Fact]
        public void FacadeCallStringLiterals_passes_a_clean_call()
        {
            var literals = FacadeCallStringLiterals("ModLogger.Log(LogVerb.Theme, \"The theme was applied: 2 files swapped.\");");
            Assert.DoesNotContain(literals, l => l.Contains(" -- ") || l.Contains(EmDash));
        }

        [Fact]
        public void No_ModLogger_call_in_the_repo_passes_a_string_literal_with_a_double_dash_or_em_dash()
        {
            var violations = new List<string>();
            foreach (var path in SourceFiles(RepoRoot()))
            {
                string name = Path.GetFileName(path);
                if (PermanentAllowList.Contains(name)) continue;
                foreach (var lit in FacadeCallStringLiterals(File.ReadAllText(path)))
                    if (lit.Contains(" -- ") || lit.Contains(EmDash))
                        violations.Add($"{name}: {lit}");
            }
            Assert.True(violations.Count == 0,
                "ModLogger calls with a disallowed separator:\n" + string.Join("\n", violations));
        }

        // --- 4. Subject-first lexical fence (typed console-eligible calls only) ---

        private static readonly Regex LeaderPrefixRegex = new Regex(@"^[A-Za-z][A-Za-z-]*:", RegexOptions.Compiled);

        /// <summary>Extracts the raw MESSAGE argument of every typed console-eligible facade
        /// call: ModLogger.Log/LogWarning/LogError whose FIRST argument is a LogVerb member
        /// (message is argument 1). LogDebug and the trace verb are diagnostic tiers, not
        /// curated narrative, so they are not fenced. Only literal arguments are returned; a
        /// variable cannot be lexically assessed.</summary>
        internal static List<string> TypedConsoleEligibleMessageLiterals(string source)
        {
            var results = new List<string>();
            var callStart = new Regex(@"\bModLogger\.(Log|LogWarning|LogError)\s*\(");
            foreach (Match m in callStart.Matches(source))
            {
                string args = ExtractBalancedArgs(source, m.Index);
                if (args == null) continue;
                var parts = SplitTopLevelArgs(args);
                if (parts.Count < 2) continue;
                string verbArg = parts[0].Trim();
                if (!Regex.IsMatch(verbArg, @"(^|\.)LogVerb\.\w+$")) continue;
                if (verbArg.EndsWith("LogVerb.Trace")) continue;
                string arg = parts[1].Trim();
                if (arg.StartsWith("$\"") || arg.StartsWith("\""))
                    results.Add(arg);
            }
            return results;
        }

        private static string ExtractBalancedArgs(string source, int matchIndex)
        {
            int openParen = source.IndexOf('(', matchIndex);
            if (openParen < 0) return null;
            int depth = 1;
            int i = openParen + 1;
            int argsStart = i;
            for (; i < source.Length && depth > 0; i++)
            {
                char c = source[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == '"')
                {
                    i++;
                    while (i < source.Length && source[i] != '"')
                    {
                        if (source[i] == '\\') i++;
                        i++;
                    }
                }
            }
            if (depth != 0) return null;
            return source.Substring(argsStart, i - argsStart - 1);
        }

        private static List<string> SplitTopLevelArgs(string args)
        {
            var parts = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < args.Length; i++)
            {
                char c = args[i];
                if (c == '(' || c == '{' || c == '[') depth++;
                else if (c == ')' || c == '}' || c == ']') depth--;
                else if (c == '"')
                {
                    i++;
                    while (i < args.Length && args[i] != '"')
                    {
                        if (args[i] == '\\') i++;
                        i++;
                    }
                }
                else if (c == ',' && depth == 0)
                {
                    parts.Add(args.Substring(start, i - start));
                    start = i + 1;
                }
            }
            parts.Add(args.Substring(start));
            return parts;
        }

        /// <summary>The lexical fence: after stripping the literal markers the message must
        /// open with an uppercase letter or an interpolation hole, and must not open with a
        /// bare "Word:" leader (the old prefix style: "[THEME CHANGE]", "LoadRegistry:").</summary>
        internal static bool PassesSubjectFirstFence(string literal)
        {
            string body = literal.StartsWith("$\"") ? literal.Substring(2)
                : literal.StartsWith("\"") ? literal.Substring(1)
                : literal;
            if (body.Length == 0) return false;
            char first = body[0];
            if (first == '{') return true;
            if (!char.IsUpper(first)) return false;
            return !LeaderPrefixRegex.IsMatch(body);
        }

        [Theory]
        [InlineData("\"The theme corvid is now applied to Knight: 2 files swapped.\"", true)]
        [InlineData("$\"{count} selections were saved.\"", true)]
        [InlineData("\"theme: corvid applied\"", false)]
        [InlineData("\"Applied: corvid\"", false)]
        [InlineData("\"applied corvid to knight\"", false)]
        public void PassesSubjectFirstFence_lexical_cases(string literal, bool expected)
            => Assert.Equal(expected, PassesSubjectFirstFence(literal));

        [Fact]
        public void No_typed_console_eligible_call_in_the_repo_opens_with_a_bare_leader_word()
        {
            var violations = new List<string>();
            foreach (var path in SourceFiles(RepoRoot()))
            {
                string name = Path.GetFileName(path);
                if (PermanentAllowList.Contains(name)) continue;
                foreach (var lit in TypedConsoleEligibleMessageLiterals(File.ReadAllText(path)))
                    if (!PassesSubjectFirstFence(lit))
                        violations.Add($"{name}: {lit}");
            }
            Assert.True(violations.Count == 0,
                "Typed console-eligible calls failing the subject-first lexical fence:\n" + string.Join("\n", violations));
        }
    }
}
