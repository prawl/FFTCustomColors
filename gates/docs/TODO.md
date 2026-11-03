# TODO

STATUS: CONTRACT (machine-checked by TodoContractTests; format grammar at the bottom of this file)

The work ledger, the same system as the mods with the MG id prefix. "Now" holds what is actively
being worked for the current release (hard cap 5, each entry carries Done means + Verify).
"Backlog" captures everything else at the cheapest possible entry cost. Items EXIT this file only
through docs/CHANGELOG.md, moved there in the commit that ships or kills them. The release ship
gate lives in docs/RELEASE_SCOPE.md.

## Now (release: 0.1)

- **[MG-14] Let the pre-commit hook write the file rows, so nobody runs floor --write by hand** (opened 2026-11-02) [QUEUED]
  - Done means: when a commit touches production code in a repo that has rules.json, the
    shared pre-commit hook runs health.py floor --write and stages docs/health/files.json and
    floor.json, so a shrink, a new file or a gone file never reaches the ratchet unrecorded. A
    worse floor still refuses, with the confession row, since widening stays the owner's call.
    (Tech: the pre-commit in the setup repo; floor --write exits 1 on a worse floor, which
    refuses the commit; the two files are added to the index after a successful write.)
  - Verify: in a scratch repo on the rulebook, a commit that shrinks a file lands with the
    updated rows staged and the ratchet green right after; a commit that widens a row is
    refused with the row to paste; Check.ps1 green here.
- **[MG-19] Hold a mod's warning count to a record while it pays its warnings down** (opened 2026-11-03) [BUILDING]
  - Done means: a mod that turns the shared warnings as errors switch off gets its compiler
    warnings counted on every build and held to docs/health/warnings.json, so the count can
    only fall; the first run writes the record, a lower count moves it, and a higher count is
    refused with the confession row. Nothing changes for a mod on warnings as errors. (Tech:
    Invoke-WarningRatchet inside Invoke-HealthGate, armed by ModGatesWarningsAsErrors false in
    the root Directory.Build.targets; a full rebuild of every non test project in rules.json,
    distinct warning lines counted; the record holds since, warnings and an optional card.)
  - Verify: Color Customizer's first deploy writes the record at its 264 warnings; a planted
    unused variable is refused with the row to paste and passes again once removed; Check.ps1
    green here.
- **[MG-20] Let the image literal lists work when two production files share a name** (opened 2026-11-03) [BUILDING]
  - Done means: the allow list and pin table checks no longer crash when two production files
    share a file name, which Color Customizer has twice; a name counts as still needing its
    entry when any file of that name does. (Tech: ToLookup by file name instead of
    ToDictionary in ImageLiteralContractTests.)
  - Verify: the shared tests pass in Color Customizer with its two StoryCharacterRegistry.cs
    and two ModInitializer.cs files; Check.ps1 green here.

## Backlog

- [MG-5] 2026-11-02: Let the style contract's em dash ban take a per-repo scope list, since data
  files hold design notes with em dashes that are not game text.
- [MG-6] 2026-11-02: Have the shared setup's /gates status print the rulebook version a repo is
  on, so one screen shows both the session gates and the build gates.
- [MG-17] 2026-11-03: A mod written in Python gets only the ledger, style and comment contracts
  from the rulebook, because the health tape measure and the comment budget read C# alone and
  the health ratchet refuses a repo with zero production rows; decide whether health.py and the
  comment tests should measure Python under tools, including tools/lib, which the comment scan
  skips today. Found hooking up Offensive Chemist, which links seven test files by name instead
  of Tests/** and leaves the health gate out of its pipeline.
- [MG-18] 2026-11-03: Decide whether the ledger may let Now go empty, since the owner finds the
  refusal at every last exit annoying and the reason behind it is not written down anywhere; the
  rule has been in the ledger test since its first day (Living Weapons f338eb84, 2026-07-05) and
  reads as a forcing function, so each exit also names what comes next. (Tech: the NotEmpty
  assertion in TodoContractTests and the "Now never goes empty" line in the global CLAUDE.md; a
  change here lands in every repo on the rulebook at its next subtree pull.)

## Walled (blocked by engine / external)

- Nothing yet.

## Format (enforced by TodoContractTests)

- Sections, in this order and no others: Now (with the release name in the header), Backlog,
  Walled, Format.
- Now: at most 5 entries. Entry first line: `- **[MG-<n>] <title>** (opened YYYY-MM-DD) [STATUS]`
  where STATUS is QUEUED, BUILDING, AWAITING-LIVE, or BLOCKED(reason). Every entry carries a
  `- Done means:` and a `- Verify:` sub-bullet. Promote from Backlog by filling those in; if Now
  is at cap, demote something first.
- Backlog: entry first line `- [MG-<n>] YYYY-MM-DD: <one sentence>`; indented continuation lines
  are free. Capture new items here in the session they surface.
- ELI5-first prose: the first sentence of every entry, and the opening of every Done means /
  Verify, is plain language a non-programmer follows. Technical detail comes AFTER, in
  continuation lines or a "(Tech: ...)" tail, never instead of it.
- IDs are unique across this file and docs/CHANGELOG.md; never reuse a retired ID.
- Items exit ONLY by moving to docs/CHANGELOG.md when they ship or die: in the shipping commit
  itself, or in the immediately following commit when the exit row cites that commit's own hash.
- No em dashes and no double-dash separators anywhere in this file or the changelog.
- AWAITING-LIVE resolutions (flipping a row out of AWAITING-LIVE) are owner-only.
