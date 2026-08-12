# Changelog (work-ledger exits)

STATUS: CONTRACT (machine-checked by TodoContractTests)

Where docs/TODO.md items land when they ship, die, or retract; newest first within a cycle.
Entry first line: `- [CC-<n>] SHIPPED <hash> YYYY-MM-DD: <summary>`, or WONTFIX / RETRACTED
with a date and no hash.

## 3.2.0 cycle

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
