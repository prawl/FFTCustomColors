# Changelog (work-ledger exits)

STATUS: CONTRACT (machine-checked by TodoContractTests)

Where docs/TODO.md items land when they ship, die, or retract; newest first within a cycle.
Entry first line: `- [CC-<n>] SHIPPED <hash> YYYY-MM-DD: <summary>`, or WONTFIX / RETRACTED
with a date and no hash.

## 3.2.0 cycle

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
