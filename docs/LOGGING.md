# Logging (the runtime contract)

STATUS: CONTRACT (machine-checked by Tests/Utilities/LogContractTests.cs; the verb table below
and the LogVerb enum are pinned to each other, so changing the glossary means changing both
together)

The logging model ported from the sibling FFT mods (CC-2), adapted to this mod's domain: theme
verbs instead of battle verbs, apply passes instead of battle edges. The goal is that a Nexus
bug report (a console paste) and the log file a user attaches are readable evidence, and that
the run being reported on still exists on disk after a relaunch.

## The line shape

Every line is one of:

    [Color Customizer] [HH:mm:ss.fff] [LEVEL] [verb] message     (the FILE, always)
    [Color Customizer] [HH:mm:ss.fff] [LEVEL] message            (the CONSOLE at Info)

- **Two-sink rule:** the file (logs/live_log.txt) gets every line, Debug included,
  unconditionally; the console shows a line only at or above the configured level. The
  evidence chain is never thinner than the console.
- **Rotation, not truncation:** live_log.txt rotates to live_log.prev.txt at launch, so the
  previous run survives. The name stays live_log.txt: the fft-dev helpers and every existing
  habit keep working.
- **Verb rendering split:** the file line always carries the verb bracket. The console drops
  it at Info tier only (subject-first prose a player reads); Warning and Error console lines
  keep it, and so does a Debug line on a console raised to Debug.
- **Console dedup:** a repeated (level, verb, message) identity prints once per apply pass;
  ModLogger.NoteApplyPassEdge() resets the seen-set at apply-pass edges. The file is never
  deduped.
- **Two-line id pattern:** file paths, byte counts, and ids ride a [trace] Debug companion
  line, never the console sentence.
- **User-initiated vs background:** work the player caused speaks at Info; background
  re-application of unchanged selections stays at Debug. This is the relevance gate (the
  sibling mods' "armed" analog): the console only narrates what the player caused.
- **WPF thread rule:** the configuration form logs from its UI thread; sink writes are one
  cheap append under a lock and every failure is swallowed. Logging must never block or take
  down the form or the game.

## The verb glossary (closed set, 9 verbs)

Pinned one-for-one against the LogVerb enum (ColorMod/Core/LogVerb.cs) by LogContractTests.
The set is CLOSED: a new subsystem reuses one of these verbs, or this table is amended
deliberately together with the enum. The sibling mods' battle verbs do not exist here.

| Verb | Covers | Level discipline |
|------|--------|------------------|
| `startup` | Launch header, hotkey hook, version line | Info, once per launch |
| `config` | Config.json load, merge, save | Info per user-driven change; Warning on read failure |
| `theme` | Theme discovery and apply orchestration | Info for apply results a user asked for; Debug for startup re-applies |
| `sprite` | Sprite file copy and swap mechanics | Debug per file; Warning on a missing source or failed copy |
| `ramza` | The Ramza TEX pipeline plus NXD patching | Info for end results; Warning for generation fallbacks; Debug for stages |
| `monster` | Monster family registry and recolor | Same discipline as `sprite` |
| `worldmap` | World-map Ramza texture swaps | Debug; Warning on a miss |
| `ui` | Configuration form lifecycle, previews, theme editor | Info open, save, close; Debug preview churn; Error for editor failures |
| `trace` | File-only evidence: paths, ids, byte counts | Debug, file-only |

## Text rules

- Subject-first plain sentences on every console-eligible line: open with an uppercase letter
  or an interpolation hole, never a bare "Word:" leader (the old prefix style). Enforced
  lexically by LogContractTests on typed facade calls.
- No em dashes and no double-dash separators in log text (source-scanned on facade call
  literals); use colons, commas, and parentheses.
- PASS/FAIL style capitalized tokens are not used in runtime lines; they belong to the build
  pipeline's voice.

## Migration state (the staged plan from the CC-2 proposal)

The legacy untyped ModLogger entry points (Log/LogWarning/LogError/LogDebug without a verb)
stay alive while the call sites migrate sweep by sweep; legacy lines render with timestamp
and level but no verb bracket. Raw Console.WriteLine stragglers are pinned by a shrinking
per-file allowlist in LogContractTests: a file cleaned in a sweep leaves the allowlist and
can never regress. When the allowlist hits zero the legacy entry points go private and the
contract turns fully strict (stage 6), and the flight recorder port follows (stage 7, the
CC-3 decision: flush on apply-pass edges and on the first Error of a launch).
