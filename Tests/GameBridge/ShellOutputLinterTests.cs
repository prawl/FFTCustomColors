using System.IO;
using System.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Static linter for fft.sh — catches output-rendering regressions that
    /// the xUnit tests can't otherwise reach (shell output isn't exercised
    /// by the C# test suite).
    ///
    /// Session 31 battle-render audit caught: literal "undefined" in verbose
    /// unit output when stat fields (pa/ma/brave/faith) weren't populated.
    /// Fixed by filtering nulls. This linter pins that fix so future edits
    /// to the node embed don't silently reintroduce the pattern.
    ///
    /// Additions welcome — any "never do X in the compact render" rule that
    /// survives a pattern match belongs here.
    /// </summary>
    public class ShellOutputLinterTests
    {
        private static string FindFftSh()
        {
            // RunTests.sh runs from repo root. Test binary lives in
            // bin/Debug/net8.0-windows/ so walk up to find fft.sh.
            var dir = System.AppContext.BaseDirectory;
            for (int i = 0; i < 8; i++)
            {
                var candidate = Path.Combine(dir, "fft.sh");
                if (File.Exists(candidate)) return candidate;
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            throw new FileNotFoundException("Could not locate fft.sh relative to test base directory.");
        }

        [Fact]
        public void NoLiteralUndefinedInConsoleLog()
        {
            // Anti-pattern: node embeds that concatenate a maybe-null JS value
            // into a string produce literal "undefined". Example fixed in
            // session 31 batch-3:
            //   extra=' PA='+u.pa     → prints "PA=undefined" when u.pa is null
            //   extra=' Br='+u.brave
            //
            // This test scans fft.sh for any line that concatenates a bare
            // property access without a null guard like (u.pa??'?') or an
            // explicit if-check. Matches are flagged; new occurrences need
            // a guard or an allowlist entry.
            var fftPath = FindFftSh();
            var lines = File.ReadAllLines(fftPath);

            // Pattern: quoted-string + '+' + identifier.property where the
            // concatenation result lands in a console.log or string template
            // AND the property access has no ?? fallback and no preceding
            // `if (foo !=` guard on the same line.
            var offenders = new System.Collections.Generic.List<(int lineNo, string content)>();
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // Rough heuristic: line mentions "console.log" or "extra=" or
                // "+' " and contains a .pa|.ma|.brave|.faith|.hp|.maxHp style
                // access without a `??` fallback adjacent to that access.
                if (!line.Contains("console.log") && !line.Contains("extra=") && !line.Contains("msg+=") && !line.Contains("LINE=")) continue;

                // Known-null-susceptible fields from the verbose Units block.
                // Use regex word-boundary so "u.ma" doesn't false-match "u.maxHp".
                string[] riskyAccessors = { "u.pa", "u.ma", "u.brave", "u.faith" };
                foreach (var access in riskyAccessors)
                {
                    // Pattern: the accessor concatenated via '+ or +'...'+ AND
                    // not followed by an identifier character (so u.ma isn't
                    // matched inside u.maxHp).
                    var concatPattern = new System.Text.RegularExpressions.Regex(
                        @"'\+" + System.Text.RegularExpressions.Regex.Escape(access) + @"(?![A-Za-z0-9_])"
                        + @"|\+" + System.Text.RegularExpressions.Regex.Escape(access) + @"\+");
                    if (!concatPattern.IsMatch(line)) continue;
                    // Guarded via null-check or fallback? skip.
                    if (line.Contains(access + "!=null") || line.Contains(access + " != null")) continue;
                    if (line.Contains(access + "??") || line.Contains(access + " ??")) continue;
                    if (line.Contains(access + "||'") || line.Contains(access + " || '")) continue;
                    offenders.Add((i + 1, line.Trim()));
                    break;
                }
            }

            Assert.True(offenders.Count == 0,
                "fft.sh has line(s) that concatenate u.pa/u.ma/u.brave/u.faith into strings " +
                "without a null-guard. This reintroduces the 'undefined' literal in verbose " +
                "unit output. Wrap the access in an if-check or use ??fallback:\n  " +
                string.Join("\n  ", offenders.Select(o => $"L{o.lineNo}: {o.content}")));
        }

        [Fact]
        public void BattleRender_PrintsFacingForAllTeams()
        {
            // 2026-04-24 policy reversal (commit 9d0a515): player/ally facing
            // (f=X) is useful as a verification aid after the live-observed
            // player-facing-byte memory bug (Ramza reads facing=East while
            // visually facing West). The prior enemies-only rule assumed
            // player facing was always accurate + auto-managed; that's not
            // true. Show f=X for all teams.
            //
            // Current correct pattern:
            //   const face=u.facing?' f='+u.facing[0]:'';
            // Regression to guard against:
            //   const face=(u.facing&&u.team===1)?' f='+u.facing[0]:'';
            var fftPath = FindFftSh();
            var content = File.ReadAllText(fftPath);

            // Positive check: the unconditional pattern is present.
            Assert.Contains("const face=u.facing?' f='+u.facing[0]:''", content);
            // Negative check: the old team===1 ternary for facing is gone.
            Assert.DoesNotContain("(u.facing&&u.team===1)", content);
        }

        [Fact]
        public void BattleRender_MoveTileHeightsAreRounded()
        {
            // Session 31 polish: half-step heights (h=4.5) don't change
            // decisions. Pattern check: Math.round(t.h) appears where move
            // tile height is rendered.
            var fftPath = FindFftSh();
            var content = File.ReadAllText(fftPath);
            Assert.Contains("Math.round(t.h)", content);
        }

        [Fact]
        public void BattleRender_AttackTilesHideWhenAllEmpty()
        {
            // Session 31 audit: Attack tiles line pure noise when all 4
            // cardinals are empty. Guard: we now filter to occupiedAtk
            // before rendering. Pattern: `occupiedAtk` filter + length check.
            var fftPath = FindFftSh();
            var content = File.ReadAllText(fftPath);
            Assert.Contains("occupiedAtk", content);
            // Length check may carry additional guards (e.g. acted-this-turn
            // suppression). Match the prefix `if(occupiedAtk.length` so an
            // added `&&!_acted` clause doesn't break the test.
            Assert.Contains("if(occupiedAtk.length", content);
        }
    }
}
