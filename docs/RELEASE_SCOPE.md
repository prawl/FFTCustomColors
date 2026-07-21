# Release Scope: next release (QoL + infrastructure)

STATUS: DRAFT (scope not yet locked by the owner)

Current shipped version 3.1.0; proposed next **3.2.0** (owner confirms the bump). This doc is the
ship gate for the release named in docs/TODO.md's Now header; TodoContractTests keeps the two in
lockstep (the release name must appear here). The heavier per-box enforcement the sibling repo
runs (ReleaseScopeContractTests) is deliberately not ported yet; it comes in when this doc grows
a real box inventory.

**Identity: "Adopt the FFTLivingWeapons engineering QoL systems."** Infrastructure first, then
whatever player-facing QoL the owner locks in.

## IN (ship gate; every box green = ship)

### 1. Work-ledger system (CC-1)
- [x] docs/TODO.md + docs/CHANGELOG.md + TodoContractTests enforce the ledger contract; suite
      green; owner gave the go-ahead (shipped 3b82b132, 2026-07-21).

### 2. Game-update compatibility (CC-10)
- [ ] Users report the mod does not work after the latest game update: investigate, fix or
      document, and confirm live before shipping anything else.

### 3. Candidates pending owner triage (not yet committed to this release)
- Logging rework to the LivingWeapons model (CC-2).
- Flight recorder, if it makes sense here (CC-3).
- Fingerprint guard evaluation, likely WONTFIX on a pure file-override mod (CC-4).

## OUT (deferred, tracked in the ledger)

- Chin-strap color bug (CC-5), WotL job completion (CC-6), Knight Male hair-highlight review
  (CC-7): all ride docs/TODO.md's Backlog until promoted.
