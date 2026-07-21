# Changelog (work-ledger exits)

STATUS: CONTRACT (machine-checked by TodoContractTests)

Where docs/TODO.md items land when they ship, die, or retract; newest first within a cycle.
Entry first line: `- [CC-<n>] SHIPPED <hash> YYYY-MM-DD: <summary>`, or WONTFIX / RETRACTED
with a date and no hash.

## 3.2.0 cycle

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
