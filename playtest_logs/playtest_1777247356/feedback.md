# Playtest Feedback — Mandalia Plain (1777247356)

## Bugs / Friction

### [P2] Enemies labeled `[ENEMY]` with no class name in screen scan
**Repro:** Battle at Mandalia Plain (Brigands' Den objective). `screen` shows 4 enemies as `[ENEMY] (4,4) f=W HP=64/64 d=5` etc, no class.
**Expected:** A class/job (e.g. "Goblin", "Black Goblin", "Red Panther") so the player knows what they're up against.
**Actual:** Just `[ENEMY]`. Player has zero class info to plan with.
**Logs:**
```
[CollectPositions] Unit 1: (4,4) t1 lv7 hp=64/64 br=61 fa=51
...
[MemoryExplorer] SearchBytes: 21 regions, 10MB searched, 0 matches
[CollectPositions] No heap match for (4,4) hp=64/64
```
**Notes:** Heap match for non-active enemies fails (HP=HP byte search returns 0 hits in 10MB). May be the same per-unit struct hunt deferred in `project_per_unit_ct_hunt_deferred.md` — non-Ramza units don't have a discoverable heap struct, so class can't be resolved. Brigands' Den at Mandalia normally features Goblins+Panthers; player would benefit from at least seeing those.

### [P3] Repeated `[EqA row drift] SM row=0 label='Move' but mirror says 'Chaos Blade'` log spam
**Repro:** Open BattleMyTurn screen, no input.
**Expected:** Either log once on first detection, or no log at all if it's expected drift on this menu.
**Actual:** Logs ~5-8 times per `screen` call. Pollutes logs.
**Logs:** see logs 80 — `[EqA row drift]` line 5 occurrences in 30 log lines.
**Notes:** This isn't EqA, it's BattleMyTurn. Equipment mirror code is being polled in the wrong context. Likely in `EqA` cursor probe code firing on unrelated screens. Worth gating on `screen.Name == "EqA"`.

### [P1] Massive narrator phantom `joined`/`died` spam during enemy turn
**Repro:** Mandalia Plain, Ramza solo, ended turn with `battle_wait` after moving to (2,4). Enemy turn played out, narrator response was ~30 lines of:
```
> (unit@3,4) died
> (unit@2,4) (ENEMY) joined at (2,4)
> (unit@2,4) died
> (unit@3,4) (ENEMY) joined at (3,4)
> ...
> (unit@8,5) died
> (unit@0,0) (ENEMY) joined at (0,0)
```
**Expected:** "3 enemies moved to (3,4), (2,3), (4,5); none died." The actual battlefield: 4 enemies still alive, just moved closer.
**Actual:** Narrator emits `died` + `joined` pairs that look like KO+spawn events but are just moves. `(unit@0,0)` and `(unit@8,5)` events are entirely phantom — no unit was at (0,0) at any point. `(unit@2,3) (ENEMY) joined at (2,3)` then `(unit@2,3) died` then re-`joined` looks like the same enemy being tracked across multiple intermediate frames as it walks.
**Logs:**
```
[BattleTracker] Unit moved (8,5)→(6,4) hp=75/75    # only 1 actual moved event!
[BattleTracker] 12 healed at (2,4): 379→391/391
```
The BattleTracker logged 1 move event but the narrator surfaced ~30 events. Mismatch.
**Notes:** This is exactly the `PhantomKoCoalescer` / `CollidingMoveFilter` bug surface mentioned in the playtest brief — duplicate-name enemies (4× `[ENEMY]`) confusing the move-vs-KO heuristic. With class names missing (see other bug above), every enemy is identical from the tracker's perspective, so any tile change → "old position died, new position joined". Per `project_scan_diff_identity_collision.md`, fix path is "add ClassFingerprint as secondary identity key". Without class names, even fingerprinting wouldn't help. **Root cause is upstream in the heap-struct match failure.**

### [P2] Adjacent enemy marked `[TOO CLOSE]` and `inRange=false` despite R:1 attack
**Repro:** Ramza at (2,4), enemy at (2,3). Both height shown h=2. `screen` reports:
```
Attack tiles: Right→(3,4) enemy HP=64  Down→(2,3) enemy HP=68 >rear [TOO CLOSE]
```
And response.json:
```json
{"x":2,"y":3,"arrow":"Down","occupant":"enemy","hp":68,"maxHp":68,"arc":"back","inRange":false}
```
**Expected:** R:1 attack should hit the (2,3) enemy normally. Distance is 1, both tiles height h=2 (per screen), no obstacles between.
**Actual:** Marked out-of-range with `[TOO CLOSE]` text — which is contradictory ("too close" usually means inside minimum range, but Attack has no minimum range).
**Logs:** Need to dig into AttackTilePathFinder. Notes:`arrow:Down` with `arc:back` suggests the system thinks I'm facing North or something — but f=S in screen output puts the South tile in FRONT. Possibly arc/range computation has a frame-of-reference flip.
**Notes:** "TOO CLOSE" string is unusual for attacks. Likely this is the wrong literal — the bridge labels something like "ABOVE" or "different height" as "TOO CLOSE". Check `BuildAttackAbilityInfo` or wherever the inRange flag flips for melee.

### [P2] Battle wait facing override: requested East, got South
**Repro:** At (2,4) with enemies to East/North/South/etc., `battle_wait` (no arg) — recommendation was "Face East" — 3 front, 1 side, 0 back.
**Expected:** Default `battle_wait` should follow the recommended facing; or at minimum log which direction was used.
**Actual:** Ramza ended up f=S. After enemy turn, screen shows `Ramza(Knight) (2,4) f=S`. Two enemies are at (3,4) [East] and (2,3) [North], and the recommended "Face East" had 3 front 1 side 0 back. Facing S means the (2,3) enemy is now BEHIND Ramza, opening rear attacks.
**Logs:** No facing log emitted. Just `[BattleMyTurn] ui=Move ... t=6352ms[battle_wait]`.
**Notes:** Per `project_facing_byte_s30.md`, facing is at slot+0x35 (0=S,1=W,2=N,3=E). Default arg may map to "no change" / "South" rather than "use recommendation". Either accept the recommendation as default, or document arg required.

### [P1] After turn rollover, Mv=0 Jmp=0 for Ramza — bridge prevents all movement
**Repro:** Mandalia Plain, Ramza alone. Move to (2,4), attack (3,4), wait east. Enemy turn. New turn arrives, Ramza HP 391→393, MP max 13→14 (level up).
After level up, all subsequent `screen` calls show:
```
Move tiles: (none)  — 0 tiles from (2,4) Mv=0 Jmp=0 enemies=3
```
Try `battle_move 1 4` → `failed: Tile (1,4) is not in the valid move range. (0 valid tiles available)`.
**Expected:** Mv=4 (Knight base 3 + Movement+1) and 6+ valid tiles like before the level up.
**Actual:** Bridge says 0 valid tiles, refuses all move commands.
**Logs:**
```
[TryReadMoveJumpFromHeap] HP=393/393 broad=False: 0 heap matches
[TryReadMoveJumpFromHeap] HP=393/393: narrow miss, retrying broad
[TryReadMoveJumpFromHeap] HP=393/393 broad=True: 0 heap matches
[CollectPositions] Active unit HP=393/393: heap Move/Jump read failed, setting Mv=0 Jp=0 (was UIBuffer fallback — wrong data)
```
Same heap struct hunt that earlier returned 6 candidates (all rejected) for HP=391, now returns ZERO candidates for HP=393. Suggests the HP fingerprint changes after level up and heap struct is no longer at the discoverable address.
**Notes:** Per `feedback_cache_preserve_on_null_active_unit.md`, "per-unit caches must preserve prior value when scan returns null." When heap miss happens for active Ramza, `setting Mv=0 Jp=0` is **not** preservation — it's clobbering. Should fall through to last-known good value (Mv=3, Jmp=3 from prior scan) or to job-table base values. **This bug effectively soft-locks the player** — bridge rejects all `battle_move` / `battle_attack` calls because validTiles is empty.

### [P1] `state.json`/`response.json` `units[]` array contains stale battle-array residue (Team 2 lv25, phantom HP=45 lv4 etc)
**Repro:** Same scan post-level-up. Top-level `battle.units[]` correctly shows 4 enemies + Ramza (5 total, all valid). But `state.json`-style block returned in same response has **17 unit entries** including:
```
team:2 level:25 (5,1) hp:174/263  // Lv25?? Mandalia is early Ch1
team:2 level:25 (-1,-1) hp:134/203 positionKnown:false
team:1 level:7 (4,6) hp:114/114
team:2 level:3 (4,6) hp:56/56     // multiple units at (4,6)
team:1 level:6 (4,6) hp:55/55
team:2 level:4 (5,6) hp:61/61
team:1 level:6 (3,6) hp:39/51
team:2 level:5 (5,6) hp:61/64
team:1 level:5 (3,6) hp:50/50
team:1 level:5 (4,6) hp:54/54
team:1 level:4 (4,6) hp:45/45
team:0 level:7 (4,6) hp:391/391    // duplicate "old Ramza" before level up
team:1 level:7 (6,6) hp:64/64
team:1 level:7 (6,6) hp:68/68
team:1 level:7 (3,5) hp:75/75
team:1 level:8 (6,6) hp:0/68 dead
team:0 level:8 (4,6) hp:393/393 ACTIVE
```
None of those Team 2 units exist on the actual battlefield (4 enemies + Ramza only).
**Expected:** units[] reflects only the current battle's units.
**Actual:** units[] contains residue from one or more prior battles. Top-level `battle.units` is correct, but the second block leaks stale data.
**Logs:** N/A — pure scan output.
**Notes:** This is the static battle array (0x140893C00, slots) being read without filtering by `inBattle` or current map. Per `feedback_persistent_snap_stale_read.md`, the static array lags live writes; here it's not even from THIS battle. Whichever code path emits this second block needs to filter by team-1-currently-on-map or ignore (-1,-1) sentinels and check `lifeState`.

### [P1] Bridge offers Mettle abilities (Focus, Rush, Throw Stone, Salve, Tailwind, Chant, Steel, Shout, Ultima) but submenu is just Attack
**Repro:** Post-level-up screen shows abilities list:
```
Abilities:
  Attack R:1 → (+1 empty tiles)
  Focus R:Self → (2,4)<? SELF>
  Rush R:1 → (+3 empty tiles)
  Throw Stone R:4 → (4,4)<?> (4,5)<?> (+28 empty tiles)
  Salve R:1 ...
  Tailwind R:3 ...
  Chant R:1 ...
  Steel R:3 ...
  Shout R:Self ...
  Ultima R:4 AoE:2 ct=20 ...
```
Try `battle_ability "Throw Stone" 4 4` → `failed: Skillset 'Mettle' not in submenu: Attack`.
**Expected:** If Mettle isn't usable, don't list its abilities. Or if these are part of the selected primary skillset, allow the action.
**Actual:** Bridge advertises 9 abilities but only 1 (Attack) is actually selectable. Player gets stuck — UI promises options the engine refuses.
**Logs:** `failed: Skillset 'Mettle' not in submenu: Attack` — single line.
**Notes:** Ramza's primary is "Arts of War" (Knight) but bridge appears to list **Mettle** (Squire) as the active skillset. Two sources of truth disagree:
- `screen.abilities.primary = "Arts of War"`
- `activeUnit.jobId = 0` (Squire, decodes to Mettle)
The ability scanner uses jobId=0 → Mettle skills, but the actual menu loaded shows Knight Arts of War. **Job ID detection is wrong post-level-up** — likely cached job changed when struct shifted (see Mv=0 bug).

### [P1] Phantom unit at (0,0) with HP=8192/288, statuses [Confuse,Regen,Slow,Stop]
**Repro:** After two `battle_wait` cycles, screen reports:
```
[ENEMY] (0,0) f=S HP=8192/288 d=6 [Confuse,Regen,Slow,Stop]
```
HP=8192 exceeds maxHp=288 (current > max is impossible by game rules). 4 statuses including Stop+Confuse+Slow simultaneously. Position (0,0) is a sentinel "no position" value, not an actual map tile.
**Expected:** Filter out units with `(x,y) == (0,0)` AND/OR `hp > maxHp` as phantom/garbage.
**Actual:** Reported as a real enemy. enemies=4 count includes this phantom. Narrator emits `(unit@0,0) joined at (0,0)` and `(unit@0,0) died` events that reference this phantom.
**Logs:**
```
> (unit@0,0) (ENEMY) joined at (0,0)    # appears across multiple turns
> (unit@0,0) died
```
**Notes:** This is the same `state.json units[]` stale-array residue issue as the previous bug, but now leaking into the screen scan (which previously filtered correctly to 4 real units). Filter rule should be: skip if `(x==0 && y==0)` or `hp > maxHp`.

### [P3] `[CollectPositions] heap Move/Jump read failed, setting Mv=0 Jp=0` for Ramza (active unit)
**Repro:** First scan in BattleMyTurn at Mandalia Plain.
**Expected:** Heap struct match succeeds, returns correct Move/Jump.
**Actual:** 6 heap matches but all `mv=0 jp=0 valid=False`, fallback fires "setting Mv=0 Jp=0 (was UIBuffer fallback — wrong data)". The reported `Mv=3 Jmp=3` in `screen` text is from a different source (possibly a roster cache).
**Logs:**
```
[TryReadMoveJumpFromHeap] HP=391/391 broad=True: 6 heap matches
  struct 0x4187811890 mv=0 jp=0 valid=False
  ... (6 of these)
[CollectPositions] Active unit HP=391/391: heap Move/Jump read failed, setting Mv=0 Jp=0 (was UIBuffer fallback — wrong data)
```
**Notes:** All 6 heap candidates are treated as invalid. Is the validity rule too strict for endgame Knight w/ Movement+1 (Mv=4 effective)? Or is the matched struct not the unit struct at all? See `project_heap_unit_struct_movejump.md` — struct base = HP-pattern-match - 0x10. May need re-anchoring after a recent game patch.

