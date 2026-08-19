# TODO

STATUS: CONTRACT (machine-checked by TodoContractTests; format grammar at the bottom of this file)

The work ledger, ported from the FFTLivingWeapons system with the CC id prefix. "Now" holds what
is actively being worked for the current release (hard cap 5, each entry carries Done means +
Verify). "Backlog" captures everything else at the cheapest possible entry cost. Items EXIT this
file only through docs/CHANGELOG.md, moved there in the commit that ships or kills them. The
release ship gate lives in docs/RELEASE_SCOPE.md; Now is the in-flight subset, not a mirror of
that checklist.

## Now (release: 3.2.0)

- **[CC-27] Two thirds of the monsters cannot be recoloured in the theme editor** (opened 2026-08-19) [QUEUED]
  - Done means: every rank of every monster family can be opened, previewed and recoloured in the
    theme editor, not just the first one. Sixteen families shipped with three ranks each, which is
    forty eight themable monsters in the config dropdowns, but the editor lists one entry per
    family and always opens the first rank's colours. So a player who wants to design a look for a
    Black Chocobo or a Red Chocobo cannot see its colours at all: the editor shows them the yellow
    one, and any theme they save is built against the wrong starting palette. Thirty two of the
    forty eight are unreachable. The built-in presets are fine and already per rank; this is
    specifically the custom theme editor. (Tech: PaletteModifier hardcodes palette 0 throughout,
    SetPaletteColor writes at index*2 with no paletteIndex*32 offset and both preview extractors
    pass paletteIndex: 0, while ThemeEditorPanel enumerates one entry per Data/SectionMappings/
    Monster/<Family>.json. Needs a rank selector in the editor plus a palette index threaded
    through PaletteModifier, the preview extractors and UserThemeService's save key, which is
    currently the tier-agnostic EditorKey.)
  - Verify: open the theme editor, pick a family, switch to rank II and then rank III, and see the
    preview and the sliders change to that rank's own colours; save a theme on rank II, apply it in
    game, and confirm rank II changed while ranks I and III kept their own looks.
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

## Backlog

- [CC-21] 2026-08-13: Author built-in color themes for the twelve NPCs shipped in CC-20. They
  currently offer only "original" in their dropdowns, which was deliberate: the plumbing and
  the per-part color sliders landed first so themes could be designed against working
  sliders. Each theme is a palette saved from the theme editor into a sprites_<name>_<theme>
  directory plus an availableThemes entry in StoryCharacters.json. Open question worth
  deciding first: one generic color set for everyone (the Reis pattern, ~25 named colors,
  scriptable) versus hand-designed signature themes per character (the Agrias pattern).

- [CC-25] 2026-08-17: Give the Nexus banner a fresh face the way Living Weapons got one: fewer,
  bigger themed sprites arranged as a left-to-right rainbow, so a visitor instantly reads "this
  mod recolors everything" instead of the current wall of hundreds of tiny sprites that blur
  into brown mush. (Tech: port the FFTLivingWeapons recipe from tools/make_banner.py and
  tools/lib/plate.py in that repo: exact-fit grid for the 1300x372 Nexus box rendered at 2x,
  hue-sorted column-major fill, a colourfulness score picking the boldest themed sprite per
  tile, the shared brushed slate-steel plate with cast shadows, and a mock pass overlaying
  Nexus's scrim plus title to check legibility before upload. Tile source: standing frames
  decoded from the sprites_* BINs via the renderer logic in scripts/render_sprite_preview.py,
  integer NEAREST upscale to keep pixels crisp.)
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

- [CC-23] 2026-08-19: Let players recolour the weapons their units swing in battle, with the same
  kind of sliders the jobs and monsters already have. The colour of a swung weapon lives in a small
  colour table at the front of one sprite sheet, and shipping our own copy of that file changes it,
  which is proven and owner live verified. Thirteen colour sets serve all 127 weapons, so a slider
  moves a GROUP of weapons and not a single one, and which weapons sit in which group cannot be
  changed by any file we ship. The control must therefore rotate the hue of whatever colours it
  finds rather than set an absolute colour, so it composes with the Living Weapons bake instead of
  fighting it and never needs the weapon to palette map at all. (Tech: FFTPack file 71
  unit/battle_wep_spr.bin, 512 byte palette block of 16 palettes x 16 BGR555, deploy at
  FFTIVC/data/enhanced/fftpack/unit/. Weapons draw from palettes 3 to 15 and effects from 0 to 2
  with zero overlap across all 127 weapons, so a recolour can never retint a swing arc. Rides the
  monster path almost verbatim: MonsterThemeCoordinator rebuild from original, MonsterRecolor
  section apply, RelativeShadeGenerator, one Data/SectionMappings entry for the zone shape
  {1-4}{5-7}{8-10}{11-14}{15}, and previews decoded from the sheet's own 4bpp pixels rather than a
  staged HD BMP. Evidence, coverage table and controls in docs/WEAPON_COLOR_CC_FINDINGS.md; the
  mechanism row is [wep-spr-palette-block] in the FFTLivingWeapons LIVE_LEDGER.)
- [CC-24] 2026-08-19: Make the weapon recolour behave when Living Weapons is installed too. Both
  mods write the same sprite sheet and there is no way to merge one, so whichever loads last
  silently erases the other and the player gets no warning. Instead of competing, read whichever
  sheet the other mod deployed and shift its colours rather than replacing them, which leaves each
  mod doing the half it is good at. (Tech: detect the file owner via IFFTOModPackManager and take
  their baked battle_wep_spr.bin as the rebuild base in place of sprites_original when they own it,
  else vanilla. Depends on CC-23. Prior art for the detect half is GenericJobsDetector.)

- [CC-26] 2026-08-19: Chase down a test that fails at random. During the CC-23 ledger work the
  suite failed once on ApplyConfiguration_UsesCorrectModPath, then passed twice on re-run with not
  a single character changed in between. A test that only sometimes passes is worse than one that
  always fails, because it trains everyone to shrug and re-run, and the next real breakage gets
  shrugged at too. Find out whether it is the test or the code that is unreliable. (Tech:
  Tests/Core/ModComponents/ConfigurationCoordinatorPathTests.cs:171, seen 2026-08-19 on a
  docs-only working tree, 1 fail then 1328 pass, then 1329 pass twice. Prime suspects are shared
  temp-directory state or path casing between tests rather than a production defect, but that is a
  guess and needs proving. If it turns out to be a real race in the coordinator's path resolution
  it is a user-facing bug, not a test bug.)

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
