# TODO

STATUS: CONTRACT (machine-checked by TodoContractTests; format grammar at the bottom of this file)

The work ledger, ported from the FFTLivingWeapons system with the CC id prefix. "Now" holds what
is actively being worked for the current release (hard cap 5, each entry carries Done means +
Verify). "Backlog" captures everything else at the cheapest possible entry cost. Items EXIT this
file only through docs/CHANGELOG.md, moved there in the commit that ships or kills them. The
release ship gate lives in docs/RELEASE_SCOPE.md; Now is the in-flight subset, not a mirror of
that checklist.

## Now (release: 3.2.0)

- **[CC-10] Investigate the mod-broken-after-game-update reports** (opened 2026-07-21) [BUILDING]
  - Done means: the failure is reproduced or ruled out on the latest game patch, the root cause
    is identified (prior art: the earlier "broken on 1.5" report was a zip-packaging defect, not
    code; triage doc docs/PORT_1.5.md), and a fix or a pinned compatibility note ships to Nexus.
  - Verify: the owner, or a reporting user, confirms themes load and apply in-game on the latest
    game version.
  - Findings 2026-07-21 (three-agent investigation): the SQLite report is SOLVED as a packaging
    defect. The zip ships native e_sqlite3.dll only under runtimes/win-x64/native/, but
    Publish.ps1 deletes FFTColorCustomizer.deps.json (Clean-BuildOutput, line 297) and never
    mirrors BuildLinked.ps1's root-level e_sqlite3.dll copy (lines 92-98, added in c686d852
    "for Reloaded-II compatibility"), so SqliteConnection's type initializer throws for every
    zip user while the dev deploy carries both resolution paths. Consequence: Ramza NXD
    theming has been silently dead for ALL zip users since v3.0.0 (the startup apply swallows
    the same exception; the theme editor is just where it becomes visible). Also proven:
    Publish.ps1 line 180 strips the six hair-highlight-fix g2d tex bins from every release,
    and neither Verify-Package nor the CI artifact check can catch any of this. Fix batch:
    keep deps.json, copy e_sqlite3.dll to the package root, stop deleting the g2d hair-fix
    texes, add all three to the Verify-Package required list. The no-colors report has no
    single proven cause; ranked suspects captured as CC-12 and CC-13.
  - Packaging fix BUILT and OWNER LIVE-VERIFIED 2026-07-21: Publish.ps1 keeps deps.json,
    copies e_sqlite3.dll to the mod root, ships the six g2d hair-fix texes; new hard gates
    (tools/analyze.py against tools/package_manifest.json, tools/SqliteSmoke functional open)
    run inside Publish and CI, both proven red on the old v3.1.0 zip (8 violations plus the
    exact DllNotFoundException users hit) and green on the fixed zip; owner installed the
    fixed zip as a user and saved a new Ramza theme with no SQLite error. Remaining on this
    row: CC-12/CC-13 fixes and the Nexus outreach plus release.
- **[CC-2] Rework logging to the sibling mods' model** (opened 2026-07-21) [BUILDING]
  - Done means: every line the mod prints says what kind of event it is and when it happened,
    the log file keeps the previous run instead of erasing it at launch, and the console stays
    quiet unless the player caused something or something went wrong. (Tech: typed ModLogger
    facade over a two-sink FileConsoleLogger, closed 9-verb glossary, live_log.txt with .prev
    rotation, docs/LOGGING.md pinned by LogContractTests, staged commits per the proposal
    artifact: facade core, contract tests, three conversion sweeps, strictness flip, flight
    recorder riders CC-3 and CC-14.)
  - Verify: the full suite stays green at every stage; LogContractTests goes red on a glossary
    drift or a raw Console.WriteLine; the owner eyeballs a live launch showing the new line
    shape with live_log.prev.txt preserved from the prior run.
  - Stages 1 and 2 live-verified 2026-07-21 on an owner-driven double launch: the new line
    shape (timestamps, levels, tag) confirmed in-game, and the second launch preserved the
    whole first session in live_log.prev.txt with a fresh live_log.txt. Riders found and
    fixed along the way: BuildLinked's clean was deleting logs/ on every deploy (9a16b092).
    Remaining: conversion sweeps (stages 3 to 5), strictness flip (6), flight recorder (7).
- **[CC-9] Fix the config window clipping and overlap on scaled and unusual displays** (opened 2026-07-21) [AWAITING-LIVE]
  - Done means: a player at any Windows display scale sees every label, slider, and button in
    the config window fully readable and clickable, on both ways of opening it (F1 in-game and
    the Reloaded launcher Configure button), and the overlaps the owner sees even without
    scaling are gone. (Tech: stage 1 per-thread DPI scope, UNAWARE_GDISCALED with UNAWARE
    fallback, wrapped around both entry points; stage 2 dock order fix, rendered spacer gaps
    between theme editor sections, font-derived header height. The durable font-measured
    layout conversion is CC-19.)
  - Verify: full suite green with the new red-first layout tests; the owner live-checks both
    entry paths above 100 percent scaling, and the 5120x1440 monitor at 100 percent, and sees
    no clipped labels and no overlaps.
  - History: Nexus report and screenshot 2026-07-21 (docs/reports/cc9_theme_editor_clipped_ui.png),
    second user screenshot 2026-08-12 (docs/reports/cc9_config_form_clipped_ui_2.png), owner
    overlap on 5120x1440 at 100 percent. Audit 2026-08-12 confirmed two defect classes: text
    scales with display scaling while every box is a fixed pixel count (no AutoScaleMode or DPI
    handling anywhere, both entry paths affected), plus three 100 percent bugs: title bar
    paints over the first content row (ConfigurationForm.Layout.cs:61), section gaps never
    render because WinForms ignores Margin on docked controls (ThemeEditorPanel.cs:560,
    HslColorPicker.cs:49), and the 13pt section header sits in a 25px label
    (HslColorPicker.cs:70). Implementer traps: tests blessing broken pixels must be relaxed in
    the same commit (ThemeEditorSectionTests.cs:1682, 1698 assert the never-rendered Margin);
    the F1 dialog runs on an MTA thread pool thread, so the DPI scope wraps the existing call
    site rather than moving to a new thread.
  - Built and adversarially verified 2026-08-12: every fix landed behind a red first test
    (title bar overlap, missing section gaps, clipped header, DPI scope), two independent
    verify rounds ran (first found the key DPI test was vacuous in the DPI unaware test host
    and blocked at 7/10; after the test gained real teeth by pinning the thread to
    PerMonitorV2 first, a fresh round proved it by sabotage and shipped at 9/10), full suite
    1253 green. Uncommitted, awaiting the owner live pass per the Verify bullet.
  - Owner live pass PASSED 2026-08-12: both entry points tested (F1 in game and the Reloaded
    Configure button) at 150 and 120 percent display scale, everything renders clean and
    crisp, and the live log shows the scope applying its best variant (UnawareGdiScaled) on
    both dialog opens. The 5120x1440 native check is moot: under the scope the window always
    lays out internally at 100 percent, so the scaled views exercised all three overlap fixes.
    Exit to the changelog rides the shipping commits.

## Backlog

- [CC-3] 2026-07-21: Add a flight recorder if it makes sense. The FFTLivingWeapons core (bounded
  ring, jsonl flush files, first-error FlushOnce trigger, retention prune) is domain-neutral and
  portable; the battle-edge flush triggers are not, so the port would flush on theme-apply edges
  and first error instead, making "my theme did not apply" reports diagnosable after the fact.
  Decide alongside CC-2 since the error trigger hooks the logger's Error path.
- [CC-4] 2026-07-21: Evaluate the fingerprint guard (the FFTLivingWeapons LaunchGuard pattern).
  Early lean is WONTFIX: that guard protects pinned memory offsets against game patches, and this
  mod is a pure file-override mod with no memory reads or writes, so there may be nothing for it
  to guard. Cheap evaluation, then a deliberate keep-or-kill decision on the record.
- [CC-5] 2026-07-21: Chin-strap pixels follow the Hair color slider on most generic sprites (the
  mirror of the hair-highlight bug). Real and confirmed in-game; deliberately deferred until the
  hair-highlight project completes.
- [CC-6] 2026-07-21: Finish WotL Dark Knight and Onion Knight support. Registry, config, and
  sprite-manager phases are complete; still open per docs/TODO_WOTL_JOBS.md: original sprite
  extraction from unit_psp, at least one alternate theme, UI previews, section mappings, the
  GenericJobs-detection niceties, and the whole integration-testing and edge-case phases (all
  in-game verification with the GenericJobs mod installed).
- [CC-7] 2026-07-21: Knight Male hair-highlight fix, Type B surgical pixel remap. The process
  doc (docs/HAIR_HIGHLIGHT_FIX_PROCESS.md) says it is built and awaiting visual review at
  working/kn_changemap.png, but that artifact no longer exists anywhere on disk (the uncommitted
  Type B output in working/ was cleaned out; verified 2026-07-21), so the remap must be
  regenerated per the doc's process before the owner review can happen.
- [CC-8] 2026-07-21: Remove or fix the suite's silently-skipped tests. The runner reports 0
  skipped because the skips are silent early returns: environment-gated guards (mappings dir
  missing, base SQLite missing, nxd files missing, dropdown entry absent) return from the test
  body and count as PASSED while asserting nothing (ThemeEditorRamzaTests, RamzaThemeSaverTests,
  NxdPatcherTests, CharacterDefinitionServiceTests). Convert them to reported skips or hermetic
  tests, or delete them.
- [CC-19] 2026-08-12: Convert the config window layout to measure its own text so any display
  scale or font size lays out correctly by construction (AutoSize labels and buttons, table and
  flow layout rows, theme editor first) behind a new FontScaleInvariantTests sweep, then retire
  the CC-9 stage 1 DPI scope so the window renders crisp at native scale instead of OS-stretched.
  Traps mapped by the CC-9 audit: relax each area's pixel-equality tests in the same commit
  (ThemeEditorSectionTests.cs:1938, 1952, 2591 are true equalities); create handles before
  PerformLayout so ComboBox and TrackBar realize sizes in tests; pin the inner panel width
  against the AutoScroll feedback loop; retiring the scope makes saved window sizes jump once
  (WindowStateService stores raw pixels with no scale stamp, worth stamping while in there).
- [CC-17] 2026-07-21: A ledger row accidentally pasted after the Format section escapes every
  grammar and id-uniqueness scan, because the contract tests only read entries out of the Now,
  Backlog, and changelog sections (found by a sabotage that landed after Format and stayed
  green); decide whether entry-shaped lines in Walled or Format should fail the contract. The
  sibling repos share the same blind spot.
  Fixed in the TreasureMaster sibling 2026-07-21 (its TM-6, commit 5569e8e) and the decision
  there was yes, they fail the contract: an entry-shape regex swept over the non-entry
  sections, proven by a planted stray going red and the revert going green. The port is that
  one test with the id prefix swapped to CC.
- [CC-18] 2026-08-12: Resizing the config window by its edges misbehaves for anyone whose
  monitor sits left of or above their main monitor, because the window decodes the mouse
  position without sign extension and negative screen coordinates read as huge positive
  numbers. (Tech: WM_NCHITTEST lParam unpacked with unsigned 16-bit masks,
  ConfigurationForm.cs:411; surfaced by the CC-9 audit; fold into whichever CC-9 stage
  touches that handler.)
- [CC-14] 2026-07-21: Cleanup riders from the CC-10/CC-13 verify passes: remove the dead
  InterceptFilePath plumbing entirely (nothing calls it in production, but 7 test files
  exercise it, so it is its own refactor), and triage the pre-existing log noise seen in the
  owner's live log (three "missing theme files" warnings for RamzaChapter dirs that never
  exist under unit/, and an empty-name "Original sprite not found for reis:" warning). Both
  fold naturally into the CC-2 logging rework if that lands first.

## Walled (blocked by engine / external)

- Some story characters (Mustadio, Rapha, Meliadoul, Construct 8 observed) need a game restart
  before a theme change renders: the game caches those sprites and never re-reads them at
  runtime. The mod writes the files correctly; the wall is engine-side.
- Vortex shows local-built zips with a warning icon and no version: Vortex hash-matches against
  the Nexus database, so a locally built zip can never pass. Unfixable from the zip side; the
  workaround is right-click, Set Source, Other.

## Format (enforced by TodoContractTests)

- Sections, in this order and no others: Now (with the release name in the header), Backlog,
  Walled, Format.
- Now: at most 5 entries. Entry first line: `- **[CC-<n>] <title>** (opened YYYY-MM-DD) [STATUS]`
  where STATUS is QUEUED, BUILDING, AWAITING-LIVE, or BLOCKED(reason). Every entry carries a
  `- Done means:` and a `- Verify:` sub-bullet. Promote from Backlog by filling those in; if Now
  is at cap, demote something first.
- Backlog: entry first line `- [CC-<n>] YYYY-MM-DD: <one sentence>`; indented continuation lines
  are free. Capture new items here in the session they surface.
- ELI5-first prose (owner rule, 2026-07-21): the first sentence of every entry, and the opening
  of every Done means / Verify, is plain language a non-programmer follows: what is broken or
  wanted, for whom, what done looks like. Technical detail (file names, ids, gate numbers)
  comes AFTER that opening, in continuation lines or a "(Tech: ...)" tail, never instead of it.
- IDs are unique across this file and docs/CHANGELOG.md; never reuse a retired ID.
- Items exit ONLY by moving to docs/CHANGELOG.md when they ship or die: in the shipping commit
  itself, or in the immediately following commit when the exit row cites that commit's own hash.
  The Now section must stay non-empty: exiting the last Now item means promoting a successor in
  the same commit.
- No em dashes and no double-dash separators anywhere in this file or the changelog.
- AWAITING-LIVE resolutions (flipping a row out of AWAITING-LIVE) are owner-only.
