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
  - Owner re-raised 2026-08-24: the console still prints far too many unnecessary info lines.
    The two sink core already shipped (stages 1 and 2, v3.1.2); the quieting he is asking for
    is exactly the remaining conversion sweeps plus the strictness flip, so finishing stages
    3 to 6 is the fix and the console noise is the acceptance test.
- **[CC-21] Give the twelve story NPCs colour themes of their own** (opened 2026-08-13) [BUILDING]
  - Done means: every one of the twelve NPCs offers more than "original" in its theme dropdown,
    the colours are ones the owner has approved on screen, and choosing a theme actually recolours
    that character everywhere the game draws them. (Tech: one hand-edited recipe per character in
    ColorMod/Data/BuiltInThemes/NPC/, baked by tools/ThemeBaker into sprites_<char>_<theme>/ plus
    an availableThemes entry in StoryCharacters.json, covering every bin in mapping.Sprites.)
  - Verify: the owner picks each new theme in game and sees the intended colours on the unit, and
    NpcBuiltInThemeContractTests re-bakes every recipe with the production code and byte-compares
    the result against the shipped sprite.
  - Landed 2026-08-21 and owner live-verified batch by batch: 22 themes across 10 of the 12 NPCs
    (Alma 5, Isilud 3, Ovelia 2, Argath 2, Zalmour 2, Gaffgarion 2, Orran 2, Simon 2, Elmdore 1,
    Valmafra 1). The colours were hand picked by a friend of the owner on the in-game sliders and
    delivered as screenshots, then transcribed into recipes. Remaining: Celia and Lettie are still
    original-only.
  - Two defects were found and fixed on the way. BuildLinked.ps1 carried a hardcoded ten-character
    allowlist, so no NPC theme folder ever deployed: the registry advertised the theme, the folder
    never shipped, the player saw vanilla, and nothing logged a word. That list is now derived from
    StoryCharacters.json. Separately the baker and the contract test only handled a character's
    FIRST sprite, so Alma (three bins: aruma, gyumu, h82) would have reverted to vanilla in two of
    her three appearances.
- **[CC-35] Add Delita to the NPC roster** (opened 2026-08-24) [BUILDING]
  - Done means: Delita can be recoloured like any other NPC. He shows up in the NPCs section of
    the config window as three entries, one per story look (chapter 1, chapters 2 and 3, chapter
    4), each with a working preview picture and a theme editor entry whose sliders the owner has
    calibrated from one-slider-per-index scaffolding into real gear sections. (Tech: his sprites
    are dily, dily2 and dily3, not the guessed dirita names; dily already shipped in
    sprites_original and the other two bins plus HD BMPs 836 to 841 were imported from the
    owner's extracted game files, so no pac extraction was needed.)
  - Verify: NpcRosterContractTests covers all three Delita entries (registry, config property,
    mapping, preview) and the owner opens the config window, sees the three Delita rows with
    correct previews, and confirms in the theme editor that each calibrated slider group
    recolours the intended gear.
  - Scaffolding landed 2026-08-24 on branch cc35-delita-npc: bins, preview BMPs, registry
    entries, config wiring, display names, and per-index mappings, suite 1689 green. Waiting on
    the owner's in-game index to gear calibration pass before the mappings become real sections.
  - Chapter 1 finished 2026-08-24: the owner calibrated the sliders in game and the mapping now
    has five real gear sections. One palette colour painted both his face and his arm trim, so
    recolouring the arms used to tint his temple and jaw; those face pixels were moved onto the
    skin colour at the pixel level in the battle texture and the preview sheet (910 pixels over
    four passes, portrait untouched, owner verified on screen). Chapters 2 and 3 and 4 still
    expose one slider per index and wait on the owner's calibration.
  - Chapters 2 and 3 finished 2026-08-24, owner verified on screen: four gear sections (Armor,
    Under Armor, Hair, Skin Color). One palette colour painted both his hair and a scatter of
    shadow specks on his red tunic, so the tunic specks were moved onto the under armour's own
    dark red at the pixel level (284 pixels across the texture pair and the preview sheets,
    portrait untouched). Only chapter 4 remains.
  - Chapter 4 finished 2026-08-24: three sections (Armor, Cape, Skin Color) after the most
    tangled calibration of the three. Three pixel level splits untangled shared colours: the
    cape's stipple left the hair colour, the armour's shading left the skin colours, and the
    whole head left the armour colours, about ten thousand pixels across the texture pair and
    preview sheets, portrait untouched every time. Hair on this chapter deliberately stays
    original by owner choice. Owner live pass underway.

## Backlog

- [CC-28] 2026-08-21: The config window has a thin light colored strip running along the very
  top edge, above the red title bar, left over from window creation. A first attempt made it
  worse instead of better: intercepting WM_NCCALCSIZE to claim the whole window as client area
  removed the strip on first paint, but after the user dragged the title bar to move the window
  the whole strip turned solid white and never repainted again, a bigger and more visible bug
  than the original. That change was reverted; ConfigurationForm.cs's WndProc is back to its
  original state, confirmed byte identical by diff. (Tech: the strip is OS drawn non client
  area reserved by the WS_THICKFRAME window style even though FormBorderStyle is None, so no
  client area control such as a docked panel can cover it. Two untested leads for whoever picks
  this up: proper WM_NCCALCSIZE handling that also repaints correctly across WM_MOVE and
  WM_NCPAINT, or dropping WS_THICKFRAME entirely and driving resize purely from the existing
  WM_NCHITTEST logic. Neither has been tried; the WM_NCCALCSIZE only attempt above is what
  broke on drag.)

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
  mechanism row is [wep-spr-palette-block] in the FFTLivingWeapons LIVE_LEDGER. ANSWERED
  2026-08-21 by the sibling session, owner live run: NO RESTART is needed. The game re reads
  file 71 on every battle load and does not keep a decoded copy in video memory, proven by a hot
  swap between two battles in one launch, three reads all served from the override and zero from
  the game's own copy. So the slider applies at the next battle. UI wording must say takes effect
  NEXT BATTLE and not immediately, because it does not change the battle already on screen. This
  covers file 71 only; menu icon art comes from the g2d container which is read once per launch,
  so if the slider ever touches menu art that half is untested and may still need a relaunch.)
- [CC-24] 2026-08-19: Make the weapon recolour behave when Living Weapons is installed too. Both
  mods write the same sprite sheet and there is no way to merge one, so whichever loads last
  silently erases the other and the player gets no warning. Instead of competing, read whichever
  sheet the other mod deployed and shift its colours rather than replacing them, which leaves each
  mod doing the half it is good at. (Tech: detect the file owner via IFFTOModPackManager and take
  their baked battle_wep_spr.bin as the rebuild base in place of sprites_original when they own it,
  else vanilla. Depends on CC-23. Prior art for the detect half is GenericJobsDetector.)

- [CC-29] 2026-08-21: When a player recolours a unit, the face shown in menus and dialogue keeps
  the original colours, so a themed unit reads as two different people. Recolour the portrait to
  match the body. (Tech: the portrait lives in the SAME sprite bin the mod already ships, at
  palette slots 8 to 15, with slots 0 to 7 holding the unit; documented at scripts/README.md:954
  and consistent with a census of 38 generic job bins where slots 8 to 12 carry data whose
  trailing skin colours are identical across slots. So no new file channel is needed. The catch
  is that the index to body part mapping differs between the unit half and the portrait half, so
  the existing Data/SectionMappings entries cannot be reused as is and a portrait side mapping is
  needed per character. Measured 2026-08-21, the game derives that mapping for us: its own faction
  portrait palettes sit in slots 8 to 12, and comparing them shows indices 1 to 4 vary on 36 to 38
  of 38 generic job bins while index 15 never varies. So 1 to 4 are the team colour clothing block,
  15 is skin, and 5 to 6 are secondary clothing varying on about 25 of 38. The indices the artists
  recoloured per faction are exactly the ones a player theme should recolour.)
  - Verify: the owner opens the formation and a dialogue scene on a themed unit and the face
    matches the body.

- [CC-30] 2026-08-21: Rework the older built in themes, which the owner rates as sloppy beside the
  ones authored on 2026-08-21. Measurement says the cause is mostly colour choice rather than a
  bug in the mod: across 353 recoloured sections only 24 lose real shading, and the median shading
  lost is the same for old and new themes. (Tech: the audit measures ABSOLUTE lightness range lost
  per section. An earlier ratio based version of the same metric was wrong and ranked Orlandeau
  thunder_god the worst theme in the library when the owner rates it his favourite, because
  dividing by a near zero original ramp turns a trivial change into a dramatic score; that theme
  ranks 50th of 353 once measured absolutely, and its Armor section GAINS 0.23 contrast. Real
  offenders worth fixing: Reis midnight_black Dress 0.32 to 0.05, Meliadoul plague_knight
  SkinColor 0.39 to 0.13, Reis platinum Shirt 0.23 to 0.02. Decide separately whether to import
  the FFTLivingWeapons docs/ICON_STYLE_BIBLE.md, noting it is written about pixel artwork such as
  grain and engraving while this repo does indexed palette swaps, so only part of it can transfer.)

- [CC-32] 2026-08-21: Ramza's little figure that walks around the world map keeps his original
  colours when a theme is applied, so a themed Ramza looks right in battle and wrong on the map.
  This was built once and never landed, and the half that works is further along than "we tried
  and failed" suggests. (Tech: the overworld sprite is a UI texture, not the battle sprite:
  ui/ffto/worldmap/texture/ramza_{a,b,c}_uitx.tex plus matching textureparts .utexpt, in extracted
  pac 0008, a separate channel from the battle g2d tex_830 to 835. Letter is by chapter, a is ch1
  blue, b is ch2 and ch3 purple, c is ch4 teal. The work sits on branch worldmap-custom-theme-wip
  at commit c17bd924, 1335 lines over 37 files: WorldMapTexSwapper, WorldMapTexCodec,
  WorldMapCustomThemeApplier, 223 lines of tests, pre baked assets for all three built in themes,
  and a post mortem at docs/WORLDMAP_CUSTOM_THEME_WIP.md. BUILT IN PRESETS WERE CONFIRMED WORKING
  IN GAME by the owner; only CUSTOM user themes fail, because their texture must be generated at
  runtime and that path was never bisected. Four suspects are recorded and none were isolated: the
  game may reject the uncompressed R8G8B8A8 tex we write where vanilla ships BC7; the hue gate may
  miss the armour pixels; the custom theme branch may not fire or may write the wrong override
  path; a relaunch may be needed since UI textures map at launch. Merge cost measured 2026-08-21:
  the branch is one commit off a base that main has since passed by 40, and a read only merge probe
  conflicts ONLY in BuildLinked.ps1 and Publish.ps1, both rewritten wholesale by CC-10, while the
  C# and the csproj auto merge clean. So the landing plan is to re apply the worldmap copy and
  verify steps onto the current scripts rather than merge the old ones, and to bisect the four
  suspects with the game closed.)

- [CC-36] 2026-08-24: The theme editor preview only shows a frozen pose, so you cannot tell how a
  theme reads in motion until you launch the game. Add an Animate checkbox beside the existing
  Compare one that plays the sprite's walk cycle the way the game does. The owner wants this
  cracked before the next release ships; it is the last story before that release. (Tech: the
  preview already crops standing frames from the HD BMP sheet; animating means cycling the walk
  frames for the current facing on a timer, sheet layouts per character come from the same
  FrameLayout/crop machinery the carousel uses.)

- [CC-37] 2026-08-24: Pie in the sky. Let a player upload their own sprite sheet for a specific
  character and have the customizer validate it and cut it properly, so the upload effectively
  becomes a new selectable theme that swaps in a different sprite, not just different colours.
  (Tech: needs a validator for sheet size, indexed format and frame layout against that
  character's expected geometry, a converter from the uploaded sheet into a battle_<sprite>_spr
  bin (and possibly HD tex), and a user theme registration path alongside the existing palette
  based user themes.)

- [CC-38] 2026-08-24: In the theme editor you can only start from a character's built in template;
  your own saved themes are not offered as a starting point, so editing an existing theme means
  rebuilding it from scratch. List the user's custom themes at the bottom of the Templates dropdown
  so picking one loads it for editing. (Tech: user themes live per character via UserThemeService
  and are browsable in MyThemesPanel; the ask is a group at the bottom of ThemeEditorPanel's
  template dropdown that resolves to the saved palette instead of the vanilla template.)

- [CC-39] 2026-08-24: NPCs should come before Monsters wherever the two groups are listed, and
  today Monsters sit first. Swap the two groups and their items in the theme editor template
  dropdown and any other surface that orders them, such as the config window sections. (Tech:
  reorder the group blocks in ThemeEditorPanel's dropdown construction and check
  ConfigurationForm's section loading order.)

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
