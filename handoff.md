# Session handoff (2026-07-21: CC work ledger ADOPTED and SHIPPED; disposable scratch, bank nothing here)

## State: the ledger is live on main and CI is green

- Commit train: 3b82b132 (ledger system + TodoContractTests + seeded rows + working/** csproj
  exclusion + CLAUDE.md count fix) -> 2741aa70 (CC-1 exit to changelog citing 3b82b132). Both
  PUSHED to main (owner-directed). No tag; pushing main does not release (only vX.Y.Z tags do).
- CI went RED on first exercise, then green: ConfigOverwriteOnStartupTest flaked (run
  29805522663, IOException on the shared bin-output Config.json, xUnit parallelism race),
  passed untouched on rerun. Captured as CC-11. Not caused by the ledger commits.
- The gate: Tests/TodoContractTests.cs, 35 tests, proven non-vacuous by three deliberate
  sabotages (bad status token, duplicate id in Backlog, em dash) that each turned it red.
- Ledger state: Now = CC-10 only (game-update breakage reports, QUEUED). Backlog CC-2..CC-9 +
  CC-11. CC-0 (retroactive 3.1.0 baseline) and CC-1 are the changelog's first rows.
- UNCOMMITTED in the working tree right now: the CC-11 flake row (docs/TODO.md), the new
  Work Ledger section in CLAUDE.md, and this handoff. Contract tests green against them
  (35/35). Awaiting the owner's commit go-ahead.

## What remains (owner decisions, in rough priority)

1. CC-10 triage: users report the mod broken after the latest game update (Nexus). Prior art:
   the "broken on 1.5" scare was the Publish.ps1 backslash-zip packaging defect, not code
   (docs/PORT_1.5.md); start by identifying the new game patch version and re-running that
   triage shape before suspecting code.
2. Triage the QoL ports into 3.2.0 scope: CC-2 logging rework (scouting banked: roughly 590
   call sites behind the static ModLogger facade, about 40 raw Console.WriteLine stragglers,
   two wiring points; see the cc-ledger-and-qol-port memory), CC-3 flight recorder (core is
   domain-neutral; flush on theme-apply edge + first error), CC-4 fingerprint guard (lean
   WONTFIX: this mod touches no memory).
3. CC-7: the Knight Male hair-highlight review artifact (working/kn_changemap.png) is GONE
   from disk along with the uncommitted Type B output; regenerate per
   docs/HAIR_HIGHLIGHT_FIX_PROCESS.md before any owner review can happen.
4. The Nexus theme-editor slider report (CC-9) reads like a DPI/scaling layout bug; reproduce
   at non-100-percent Windows scaling first.

## Traps for the next session (each verified this session)

- TodoContractTests requires a NON-EMPTY Now section: exiting the last Now item means
  promoting its successor in the same commit (CC-10 sits there now).
- No em dashes and no " -- " in docs/TODO.md or docs/CHANGELOG.md; the gate scans every line.
- The test csproj compiles EVERY .cs under the repo root except ColorMod/, working/, and
  Program.cs. Stray .cs files anywhere else break the entire suite build (bit this session:
  working/equipmod decompiles made RunTests.sh unbuildable until the exclusion landed).
  Scratch goes in working/ or the session scratchpad, never a new root dir.
- git commit -a skips untracked files: the four ledger docs were untracked at first landing;
  stage explicitly or a partial commit turns CI red (RepoRoot throws without docs/TODO.md).
- A red CI in an area the commit never touched may be CC-11 (bin-output Config.json race);
  rerun before diagnosing. Same family: several suites early-return on missing environment
  and report PASS (CC-8), so "Skipped: 0" does not mean everything ran.
- The id-uniqueness gate only sees ids in Now/Backlog/changelog ENTRY positions; an id in
  Format or Walled prose is invisible to it by design (a misplaced sabotage proved this).
- Two Claude sessions may share this handoff: append, never overwrite.
