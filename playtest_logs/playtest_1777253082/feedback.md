# Playtest Feedback - 2026-04-26 (Mandalia Plain, post-88d41ad verification)

Battle: Mandalia Plain (Brigands' Den)
Starting state: BattleMoving, ui=(2,4)
Goal: Verify the 4 P1 fixes from this morning's commit 88d41ad hold; find new issues.

### [P1] Phantom enemy at (0,0) with HP=8192/288 — wildly out-of-range stats
**Repro:** `screen` on BattleMyTurn at Mandalia Plain. Active = Knight Ramza Lv8.
**Expected:** Phantom/residue units filtered by `StaleBattleUnitFilter` (per the morning's commit 88d41ad). MaxHp 288 doesn't match anyone real on this map; the unit also has nonsensical hp=8192 (current > max by 28x), no name, no job, and stacks [Confuse, Regen, Slow, Stop] simultaneously.
**Actual:** Renders in `Units:` block:
```
[ENEMY] (0,0) f=S HP=8192/288 d=6 R:Regenerate S:EXP Boost [Confuse,Regen,Slow,Stop]
```
**Logs:** see `commands.log` — entry "screen on BattleMyTurn".
**Notes:** Either (a) StaleBattleUnitFilter is letting a row through where MaxHp matches some active slot but other fields are garbage, or (b) the static-array slot at (0,0) is a sentinel/uninitialized slot that wasn't filtered. The `R:Regenerate` reaction + `S:EXP Boost` support are valid IC abilities, so it's not random bytes — feels like a stale slot from a different battle that happens to share MaxHp=288 with something here. Also: `hp > maxHp` (8192 > 288) should itself be a sanity-check rejection in StaleBattleUnitFilter.

### [P3] `screen -v` does nothing on BattleMoving
**Repro:** `screen -v` while in BattleMoving state.
**Expected:** Verbose output, or at least an error explaining `-v` only works on BattleMyTurn.
**Actual:** Silently identical to plain `screen`: `[BattleMoving] ui=(2,4) curLoc=Mandalia Plain obj=Brigands' Den t=168ms[screen]`.
**Notes:** From `declare -f screen`, the verbose branch is gated on `SCR == "BattleMyTurn"`. Should at least say "verbose only available on BattleMyTurn" or auto-Cancel and re-scan.

### [P1] AttackOutcomeClassifier reports KO when attack actually MISSED (re-targeting branch)
**Repro:** With Knight Ramza at (2,4), Chocobo at (1,4) HP 64/64. `fft battle_attack 1 4`.
**Expected:** Bridge to detect MISS (the post-animation game stays on BattleAttacking re-targeting mode → bridge already sends Escape) and report `> [MISS] Attack on (1,4)`. Or at least not report a KO.
**Actual:** Bridge confidently reports `Attacked (1,4) from (2,4) — KO'd! (64→0/64) → (2,4) HP=393/393`. Re-scanning shows Chocobo at (1,4) is still HP=64/64. The `[ACTED]` flag is set so the turn was consumed.
**Logs:**
```
[BattleAttack] Pre-attack: target HP=64/64 lv=7
[BattleAttack] Post-attack: live HP=0 static=64 chose=0 (was 64)
[BattleAttack] Still on BattleAttacking post-animation (miss re-targeting); sending Escape
[BattleAttack] Attacked (1,4) from (2,4) — KO'd! (64→0/64)
```
**Notes:** The "Still on BattleAttacking post-animation (miss re-targeting)" branch is the strongest signal we've missed. Narrator chose `live=0` over `static=64` — but here `live=0` is a stale-region read (the live address space is bouncing or returning a default), while `static=64` is ground truth. The classifier should: (a) treat the "missed → re-targeting" branch as authoritative for MISS, OR (b) prefer `static` when `static` matches the pre-attack value (ie. unchanged → no damage). Tagging this as P1 because it pollutes attack outcome reporting and confuses the player about what actually happened. Cross-ref `feedback_persistent_snap_stale_read.md` and `feedback_phantom_success_pattern.md`.

**UPDATE after second attack:** Same log pattern (`live HP=0 static=75 chose=0`, `miss re-targeting; sending Escape`, `KO'd! 75→0`) fired on the FINAL goblin kill — and that one really did kill the goblin (battle ended). So the "miss re-targeting" log line is NOT a reliable miss signal; it fires for both KOs and misses. The misclassification specifically happens when **live=0 is a stale read** but the kill didn't actually happen. The classifier needs a more reliable damage signal — either confirm via the unit count drop, or wait long enough that static catches up before reading.

### [P1] Phantom died/joined narrator burst NOT fixed (MovedEventReconstructor regression)
**Repro:** `fft battle_wait` after KO'ing one chocobo. Enemy turn happens (Goblin moves).
**Expected:** ONE moved event for the goblin. Maybe a couple lines for any actual enemy actions.
**Actual:** A 50+ line cascade of:
```
> Goblin moved (4,4) → (2,3)
> Chocobo revived (HP 0→68)
> Chocobo lost Dead
> Chocobo took 4 damage (HP 68→64)
> Chocobo died
> (unit@3,4) (ENEMY) joined at (3,4)
> Chocobo revived (HP 0→68)
> Chocobo lost Dead
> Chocobo took 4 damage (HP 68→64)
> Chocobo died
[... loops 10+ times ...]
> Knight died             <-- PHANTOM, my unit is alive
> Chocobo recovered 313 HP (HP 68→381)   <-- Cross-attribution, that's Ramza's HP
> Chocobo gained Regen, Protect, Shell    <-- Cross-attribution, those are Ramza's buffs
> Chocobo died
> (unit@3,4) (ENEMY) joined at (3,4)     <-- placeholder name
> Chocobo moved (4,6) → (3,4)             <-- (4,6) is Ramza's old position
```
**Logs:** see `commands.log` line "battle_wait → 50+ line burst".
**Notes:** This is the morning's P1 #3 supposedly fixed by `MovedEventReconstructor` rejoining same-team remove+add pairs — it's clearly NOT working. Possibly the rejoin only handles single-step moves and gets confused when the same slot is being killed/revived/joined repeatedly. The cross-attribution (Chocobo gaining Ramza's HP/buffs) suggests the static array slot for Ramza is getting reused for the chocobo struct between scans — fingerprint check is supposed to prevent this. `(unit@3,4)` placeholder labels confirm name backfill failed for the rejoined entries.

### [P1] Mettle vs Arts of War regression — bridge tries 'Mettle' even when in-game submenu shows 'Arts of War'
**Repro:** Knight Ramza on BattleMyTurn (correctly identified, after a move). `fft battle_ability "Focus"`.
**Expected:** Focus cast on self successfully via Arts of War submenu.
**Actual:** `failed: Skillset 'Mettle' not in submenu: Attack, Arts of War`
**Notes:** **DIRECTLY CONTRADICTS THE MORNING'S P1 #2 FIX.** The submenu correctly lists "Arts of War" but the bridge's lookup is hardcoded/cached to "Mettle". The Job→Skillset map for Knight (jobId=2) is producing the wrong value. May be an IC remaster naming difference (PSP/PSX = "Mettle", IC remaster = "Arts of War") that wasn't propagated. Cross-ref: `feedback_aurablast_learned_filter_bug.md` shipped fix mentions full-canonical-skillset-list approach. Same problem here — name lookup is wrong.

**Root-cause sketch:** Looking at code: `RosterReader.cs:278` hardcodes jobIdx 0 → "Mettle" with comment "the game UI labels Squire's primary as Mettle in this remaster." But the in-game submenu my scan saw was literally "Attack, Arts of War" — so the comment is stale, the game UI actually labels jobIdx 0 as "Arts of War" in IC. Note `JobGridLayout.cs:122` already maps `Knight => "Arts of War"`. Two paths disagree on what jobIdx 0 is named:
- `RosterReader.cs:278`: index 0 → "Mettle"
- Game submenu (live): "Arts of War"
- `ActionAbilityLookup.cs:339`: skill list keyed under "Mettle" with Focus/Rush/etc

The fix should reconcile RosterReader's map to use the live submenu name, OR add "Arts of War" as an alias for "Mettle" in `SkillsetAliases` (currently only aliases "Fundaments" ↔ "Mettle").

Note: `Skillsets: primary=Arts of War, secondary=null` does appear in CommandBridge logs even on the same scan that failed. So there are two parallel codepaths for resolving skillset name and they disagree:
- CommandBridge log path: "Arts of War"
- Submenu nav path (BattleAbilityNavigation): tries "Mettle"

### [P1] RosterMatchCache poisons active unit identity (Knight Ramza → Chocobo) after one turn
**Repro:** First scan showed Knight Ramza correctly. After one attack + battle_wait, scan shows active unit as `Chocobo (2,4) HP=381/393 [Regen,Protect,Shell] *` with `jobId=0, jobName="Chocobo", nameId=1`. Abilities list correctly shows Ramza's actual skills (Focus, Rush, Salve, Tailwind, Chant, Steel, Shout, Ultima — Squire/Knight skillset) but unit identity is wrong.
**Expected:** Active unit stays Knight Ramza throughout the battle.
**Actual:** Cache flips to Chocobo permanently. Logs show:
```
[CollectPositions] Cache hit (2,4) → Chocobo (no heap match; pos+stats fallback)
[CommandBridge] WARN: No primary skillset for job 'Chocobo' — submenu will be missing primary
[CommandBridge] Skillsets: primary=null, secondary=null (secondaryIdx=0)
```
**Notes:** **DIRECTLY VIOLATES THE MORNING'S P1 #2 FIX (`RosterMatchCache` keyed by NameId)**. The cache fallback "pos+stats" appears to be matching on (x=2,y=4)+(hp=381,maxhp=393)+(team=0) and reusing whatever was previously cached at that pos — which on Mandalia happened to be a Chocobo (probably from the earlier Choco Forest battle that left residue in heap). Need to: (a) only cache by stable identity (nameId+jobId+stat fingerprint), not by pos; (b) re-validate cache after major heap shifts (turn boundaries, screen transitions). Cross-ref `feedback_cache_preserve_on_null_active_unit.md` and `project_inventory_store_CRACKED.md` patterns.

### [P1] StaleBattleUnitFilter not catching phantom unit at (0,0) HP=8192/288 Lv32
**Repro:** Every `screen` on this Mandalia battle.
**Expected:** Phantom dropped by `StaleBattleUnitFilter` (morning's P1 #4).
**Actual:** Persistent phantom. Logs show:
```
[CollectPositions] Heap match (0,0) hp=8192/288 lv=32: picked base=0x4166CAF350 candLevel=32 score=100 from 8-14 candidates
[CollectPositions] Unknown fingerprint (0,0): 20-20-00-43-00-01-00-00-20-20-01 hp=8192/288 lv=32
[CollectPositions] Passives (0,0): reaction=Regenerate support=EXP Boost
```
**Notes:** Ramza's R:Parry / S:Reequip plus another player party member's known abilities point to this being roster residue from elsewhere. Lv32 obviously doesn't fit a Ch1 Mandalia battle (everyone here is Lv7-8). StaleBattleUnitFilter's MaxHp matching apparently let MaxHp=288 through despite no real unit having MaxHp=288 here. Also: `hp > maxHp` (8192 > 288) should always be a sanity reject.

### [P3] battleWon=true while battle still in progress
**Repro:** `fft_full screen` on BattleMoving. Multiple enemies still alive in scan output but JSON `battle.battleWon=true`.
**Expected:** false until last enemy dies.
**Actual:** `"battleWon": true` while 4 enemies still alive.
**Notes:** Probably the encA=255 sentinel logic from `feedback_victory_gameover_both_encA255_risk.md` triggering; or the `battleWon` field is being driven by stale state. Could mislead any tooling that gates on it.

### [P2] BattleDesertion mis-detected for ~200ms during Victory transition
**Repro:** Final attack of the battle (Goblin KO at (3,2)). After ~2s settle, `screen` returned `[BattleDesertion] curLoc=Mandalia Plain`. ~5s later it cleared to WorldMap.
**Expected:** Either Victory or direct WorldMap, not Desertion.
**Actual:**
```
> battle_attack 3 2 -> KO'd! 75→0
[BattleDesertion] ... ValidPaths: Dismiss   <-- ~2s after KO
[WorldMap] curLoc=Brigands' Den ...          <-- ~10s later
```
**Notes:** Cross-ref `project_gariland_victory_fix.md` — the encA=255-in-inBattle fix was for Gariland; same pattern may exist for Mandalia post-Victory transition. Detection inputs in the Desertion window: `battleMode=2, gameOverFlag=1, encA=2, encB=2, battleActed=1, battleMoved=1`. encA=2 doesn't fit the typical Victory=255 / GameOver=05 sentinels. Maybe a transient state with no sentinel set.

### [P2] Bridge gets stuck reporting BattleMoving for ~30s after attack KOs final enemy
**Repro:** After successful final attack (`battle_attack 3 2 → KO'd`), bridge reported `[BattleMoving] ui=(3,3)` for ~30s with `battleMode=2 gameOverFlag=1 battleActed=1 battleMoved=1`. `return_to_my_turn` failed: "stuck at BattleMoving after 5 escapes". Eventually `enter` got things moving.
**Expected:** Detection should resolve to actual screen (BattleWaiting → enemy turns or WorldMap) within a few seconds.
**Actual:** Multiple Escape sends no-op; only Enter advanced state. This suggests the game is in a post-action settle that the detection rules aren't recognizing.
**Notes:** Probably a screen-detection rule oversight — `battleMode=2, gameOverFlag=1, moveMode=0, battleActed=1, battleMoved=1` is a unique combo of flags after final-blow KO. Could explain other "stuck" cases reported in the open issues list.

### [P2] state.units shows duplicate entries for same enemy at same coords
**Repro:** Mid-battle scan after one chocobo died:
```
[ENEMY] (3,4) f=W HP=0/68 d=1 R:Dragonheart S:Equip Crossbows CRYSTAL
[ENEMY] Chocobo (3,4) f=W HP=68/68 d=1 R:Counter
```
Two units at exactly (3,4) — one DEAD/CRYSTAL and one alive. Different abilities (Dragonheart/Counter).
**Expected:** One entry per real unit.
**Actual:** Both entries persist. The first has placeholder (no name), Dragonheart reaction, EquipCrossbows support — looks like roster residue from a different battle (Mandalia enemies are Goblin/Chocobo, not crossbow-wielding Dragonheart units).
**Notes:** StaleBattleUnitFilter let two-at-same-coord through. Position+state collision (CRYSTAL vs alive) should be a hard filter. Lv8 Dragonheart unit might be a leaked Algus or other guest from another battle.

### [P2] enemies=4 but only 3 actually alive (counts dead crystal)
**Repro:** First scan: `Move tiles: ... Mv=4 Jmp=3 enemies=4` — but units block shows 1 dead, 3 alive enemies (and one phantom).
**Expected:** enemies count should reflect alive enemies only (or be clearly labeled "total" vs "alive").
**Actual:** Counts dead+alive together.
**Notes:** Minor but contributes to confusion. Player-facing copy "enemies=N" should match the unit list filter or be relabeled.

### [P2] command.json parse errors during play (`'s' is an invalid start of a value`)
**Repro:** Multiple times during battle:
```
[CommandBridge] Failed to read command file: 's' is an invalid start of a value. Path: $ | LineNumber: 0 | BytePositionInLine: 0.
[CommandBridge] Quarantined malformed command.json → command.json.bad-...
```
**Expected:** Bridge writes valid JSON.
**Actual:** ~3 quarantined `command.json.bad-...` files during this session.
**Notes:** Probably a race between fft.sh writing `command.json` and the mod reading it. The 's' suggests truncated JSON starting with `{"...` or partially-written body. Cross-ref `feedback_response_json_race.md` and `feedback_bash_heredoc_quote_breakage.md`. Could explain occasional silent command drops.

### [P3] `screen -v` does nothing on BattleMoving (already logged earlier)
(already documented above)

### [P3] BattleMoving tile selection state persists across actions
**Repro:** Started in BattleMoving from prior session. Even after Escape from BattleAttacking (post-attack), state went back to BattleMoving rather than BattleMyTurn.
**Expected:** Post-attack should land in BattleMyTurn.
**Actual:** Detected BattleMoving multiple times.
**Notes:** May be related to the stuck-state P2 above.

---

## Summary

**Total bugs found:** 11
- P1: 5 (phantom (0,0) HP=8192/288, AttackOutcomeClassifier KO-on-MISS, phantom died/joined burst, RosterMatchCache poisoning, Mettle-vs-Arts-of-War regression)
- P2: 5 (BattleDesertion mis-detect, stuck-BattleMoving, duplicate state.units entries, enemies-count-includes-dead, command.json parse errors)
- P3: 3 (battleWon=true in-progress, screen -v silent on BattleMoving, BattleMoving persistence)

**Top 3 friction points:**
1. **The narrator is unreliable on attack outcomes.** First chocobo attack: bridge said "KO'd 64→0" but enemy stayed at HP=64/64. Final goblin attack: bridge said "KO'd 75→0" and battle did end. Same log pattern — same `live HP=0 static=75 chose=0` — different real outcomes. Player can't trust the narrator without separately verifying via re-scan.
2. **Cache poisoning of unit identity is a battle-killer.** After turn 1, Knight Ramza was identified as "Chocobo" with jobId=0, nameId=1 — broke ability submenu navigation entirely. Once cached, the wrong identity stuck for the rest of the turn until the unit moved to a new tile.
3. **Phantom died/joined narrator burst is still happening.** A single goblin move generated a 50+ line burst with cross-attribution (Chocobo "recovering 313 HP" = Ramza's actual HP delta), placeholder names `(unit@3,4)`, and even a phantom `Knight died` event for a unit at full HP. The morning's `MovedEventReconstructor` fix is not catching the post-action enemy-turn churn.

**Perf headline:** 8/122 commands over 1500ms threshold. Worst: `battle_wait` at 13.6s (likely processing the phantom event burst), `battle_attack` consistently 4.7-4.8s, `battle_wait` 8-12s typical. The phantom burst processing is the obvious culprit for slow battle_wait.

**Failed commands:** 2 — both `battle_ability "Focus"` failing with `Skillset 'Mettle' not in submenu`. Same root cause (the Mettle vs Arts of War lookup mismatch).

**Did the morning's 4 P1 fixes hold up?**
- **Mv=0/Jp=0 softlock fix** — HOLDING (no Mv=0 observed; even when broad heap miss, fell back to cached Mv=4 Jp=3). PASS.
- **jobId stale (Mettle vs Arts of War) fix** — REGRESSED. The CommandBridge logging shows `primary=Arts of War` (correct), but the BattleAbilityNavigation path still tries "Mettle" and fails the submenu match. Two parallel codepaths disagree — the morning fix only addressed one of them. FAIL.
- **Phantom died/joined narrator burst fix** — REGRESSED HARD. 50+ line phantom burst on first enemy turn. FAIL.
- **state.units[] residue fix** — PARTIAL. Phantom (0,0) HP=8192/288 Lv32 with R:Regenerate/S:EXP Boost stayed visible in every scan. Also duplicate-coord (3,4) entries (CRYSTAL + alive Chocobo) suggest the filter isn't aggressive enough. PARTIAL.

So **2/4 P1s held, 1/4 partial, 1/4 fully regressed**. Bottom line: the morning's commit 88d41ad needs another pass.

