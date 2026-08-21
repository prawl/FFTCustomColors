# Changelog (work-ledger exits)

STATUS: CONTRACT (machine-checked by TodoContractTests)

Where docs/TODO.md items land when they ship, die, or retract; newest first within a cycle.
Entry first line: `- [CC-<n>] SHIPPED <hash> YYYY-MM-DD: <summary>`, or WONTFIX / RETRACTED
with a date and no hash.

## 3.2.0 cycle

- [CC-27] SHIPPED 157bb593 2026-08-21: every rank of every monster can now be recoloured in the
  theme editor, not just the first one. Sixteen families ship with three ranks each, so forty
  eight monsters appear in the config dropdowns, but the editor listed one entry per family and
  always opened the first rank's colours, leaving thirty two of them impossible to recolour by
  hand. A player designing a Black Chocobo look was quietly shown the yellow one instead, so the
  shades they adjusted belonged to a different creature and whatever they saved was built on the
  wrong starting palette. Nothing errored; it edited the wrong thing. Each rank is now its own
  entry carrying the creature's real name and a rank number, and picking one opens that
  creature's colours in the sliders, the preview and the comparison pane, with per section reset
  and reset all both staying on the chosen rank. Saved themes are untouched and need no
  migration: a theme still belongs to the family, so anything saved before this loads and applies
  exactly as before and is still offered on all three ranks. The save confirmation used to name
  the family and point at the wrong section, telling someone who saved a Bonesnatch theme to look
  for Skeleton under Generic Characters; it now names the selection, points at Monsters, and says
  the theme covers every rank of that family. Owner live verified in game. (Tech: PaletteModifier
  gains PaletteIndex with PaletteOffset of PaletteIndex times 32, threaded through SetPaletteColor,
  GetColorFromData, CopyPaletteIndex and all three preview paths, the HD BMP swap now receiving
  GetModifiedPalette so the selected rank sits at offset 0; default 0 leaves every single palette
  caller byte identical. New pure MonsterEditorEntries expands MonsterThemeRegistry.Families into
  one entry per family and rank, reading family.PaletteIndices rather than tier position.
  ThemeEditorPanel maps each rank display name to the FAMILY name so the save key and mapping
  lookups are unchanged, and funnels every modifier construction through LoadRankAwareModifier and
  ReloadModifiersFromCurrentSprite; OnResetAllClick gained an internal ConfirmResetAll seam so both
  the confirm and decline paths are testable. ThemeSavedEventArgs carries DisplayName and
  ConfigurationForm.Data exposes BuildThemeSavedMessage. Seven adversarial verify rounds, final
  score 9 of 10, every load bearing line proven by mutation with the catching test named.
  Suite 1355 of 1355.)

- [CC-22] SHIPPED 2ce9cf70 2026-08-13: a color theme you save for one of the twelve NPCs now
  shows up in that NPC's little preview picture when you pick it, instead of stubbornly
  showing the plain vanilla colors and looking like it did nothing. The theme was always
  saved and always reached the game; only the preview lied about it, which is impossible to
  tell apart from a theme that never applied. Owner confirmed live. (Tech: the preview
  resolved a sprite file through a hand-written display-name table in CharacterRowBuilder
  that stopped at Construct8, so every NPC fell through to battle_<displayname>_spr.bin,
  which does not exist; the user-theme loader bailed there and the caller silently fell back
  to the vanilla sheet, with Alma and Simon working only because their display name happens
  to be their sprite name. Sprite names now come from Data/StoryCharacters.json through the
  new InternalSpriteNameResolver, the same source the apply path already read, so preview and
  apply cannot disagree and a new roster entry needs no code change; the old table survives
  as an alias fallback for names with no registry row. The HD sheet plus palette render moved
  ahead of the bin lookup so a missing bin can no longer abort a preview, and Meliadoul's
  preview now follows the registry to h80 like the game does. NpcThemePipelineTests pins all
  three links of the chain and was built red first, which split the diagnosis exactly:
  preview failed, apply passed; suite 1329 green.)
- [CC-20] SHIPPED 128fff2e 2026-08-13: twelve non-player characters (Alma, Argath, Celia,
  Elmdore, Gaffgarion, Isilud, Lettie, Orran, Ovelia, Simon, Valmafra, Zalmour) can now be
  picked in the config window and recolored in the theme editor, each with an HD preview
  picture and a handful of named color sliders like "Cape" or "Surcoat". They live in their
  own NPCs section so they never crowd the Story Characters list, and they ship with no
  built-in color themes on purpose; authoring themes for them is CC-21. Owner confirmed all
  twelve in-game. (Tech: CharacterDefinition.Category drives a new collapsible section and a
  matching theme editor dropdown group backed by Data/SectionMappings/NPC; sprite bins
  verified against the vanilla files by exact palette match, which also found duplicate
  sprites now all registered for Alma, Gaffgarion, and both twins; Celia's preview is
  Lettie's sheet re-indexed with Celia's palette since no toolkit sheet exists, and the twins
  reuse the Dancer_Female layout because their indices 7 to 15 are byte identical; slider
  anchors sit at each group's pixel-weighted lightness center, with shadeMode uniformHue
  wherever a group is achromatic or a large single-hue ramp; roster pinned by
  NpcRosterContractTests, suite 1309 green; infrastructure landed in 2e72f17e.)
- [CC-9] SHIPPED d3b10f3b 2026-08-12: the config window is readable at any Windows display
  scale, on both ways of opening it, ending the Nexus clipped-slider report and the owner's
  overlap sightings. Text used to outgrow its fixed pixel boxes above 100 percent scaling;
  now a per-thread DPI scope has Windows scale the finished window (UnawareGdiScaled with
  fallbacks), and three always-broken layout bugs are fixed: the title bar covering the
  first content row, section gaps that never rendered (WinForms ignores Margin on docked
  controls), and a section header clipped by its fixed height. Red first tests cover all
  four fixes; two adversarial verify rounds ran (the first proved the key DPI test vacuous
  in the DPI unaware test host and blocked at 7/10; the fixed test pins its thread to
  PerMonitorV2 and the rerun shipped at 9/10), suite 1253 green, owner live verified at 150
  and 120 percent on both entry paths with the log showing UnawareGdiScaled applied. The
  durable font measured layout conversion is CC-19.
- [CC-16] SHIPPED 08daee09 2026-07-21: local builds now stop with a clear red message when
  the work ledger or the logging contract is malformed, instead of quietly deploying or
  packaging while only CI would have noticed. (Tech: Invoke-UnitTestGate in
  tools/pipeline.ps1, filtered to TodoContractTests plus LogContractTests, called by
  BuildLinked.ps1 and Publish.ps1; proven by a corrupted Backlog row refusing to deploy and
  a clean end-to-end BuildLinked run.)
- [CC-15] SHIPPED da66c27a 2026-07-21: the dev deploy and the release zip no longer carry
  duplicate copies of the build, staging, and verification code that could drift apart (the
  drift class behind the v3.0.x broken zips). A shared dot-sourced tools/pipeline.ps1 feeds
  both scripts, and deploy verification runs off the same tools/package_manifest.json as the
  analyze.py zip gate. Two long-broken blocks are fixed: the User-config seed that never ran
  (undefined variables) and the Publish dev-install cleanup that deleted nothing. Also fixed
  in the pass: a red Publish gate could exit 0 because an exit from inside the try was
  overridden by the finally with an unset exit code; gates now throw and the exit code
  defaults to failure. Deployed and zipped file sets verified identical to pre-refactor
  captures.

- [CC-11] SHIPPED 7c4d5b8d 2026-07-21: the ConfigOverwriteOnStartupTest CI flake is fixed at
  the root. Constructing Mod writes Config.json into its own folder, the shared test bin dir,
  and xUnit class parallelism let two such classes race on that one file (the "being used by
  another process" IOException, twice on CI the same day). All ten Mod-constructing test
  classes now share the serialized ModBinDir collection; temp-dir tests stay parallel and the
  suite runs in unchanged time.

- [CC-13] SHIPPED 0a34e6ac 2026-07-21: silent-failure hardening in the theme-apply path.
  Locked and access-denied sprite copies now warn honestly that the change was NOT applied
  (the old DEBUG lines promised a "path redirection" that never runs in production); apply
  failures no longer masquerade as "Failed to open configuration UI"; and ConfigPathResolver
  unifies Config.json on the Reloaded User path with one-time migration of legacy mod-folder
  configs, ending the split-brain that could silently revert F1 selections. 9 tests. Owner
  live-verified the unified config path in the live log the same day.

- [CC-12] SHIPPED 853f9090 2026-07-21: the Reloaded-II Configure Mod button now copies sprites
  on release installs. The flow hard-coded the sprite target as Mods\FFTColorCustomizer, a
  folder that only exists on the dev machine, so every release user who configured via the
  launcher button got a saved config, a success report, and zero sprite changes (top suspect
  for the Nexus "no modification on any character" report). ModInstallPathResolver now trusts
  the executing DLL's directory with ModFolder fallback; 7 tests including a source-scan pin.
  Owner live-verified same day on a paxtrick.fft.colorcustomizer install via the button.

- [CC-1] SHIPPED 3b82b132 2026-07-21: the work-ledger system, adopted from FFTLivingWeapons:
  docs/TODO.md and this changelog under TodoContractTests enforcement (35 tests, proven
  non-vacuous by three deliberate sabotages), a draft RELEASE_SCOPE for 3.2.0 in lockstep with
  the Now header, and the ledger seeded with the QoL backlog. The shipping commit also carried
  the working/** test-compile exclusion (the suite was unbuildable against stray scratch
  sources) and the CLAUDE.md test-count correction. Owner gave the commit go-ahead in-session.

## Pre-ledger baseline

- [CC-0] SHIPPED e370efbe 2026-07-21: the 3.1.0 release (16 themable monster families including
  Hydra/Tiamat, three Construct 8 presets, and the zip-packaging fix for strict extractors)
  shipped before this ledger existed. Recorded retroactively as the baseline row so the changelog
  format has a real anchor; everything before 3.1.0 lives in git history only.
