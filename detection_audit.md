# Screen Detection Audit — Session 2026-04-14

One row per observed state. Columns: ground truth → raw inputs → detected → match?

---

## TL;DR — Key Findings (45 samples)

### Addresses that are RELIABLE
- `party` — 1=party menu / status screen, 0=otherwise (reliable discriminator)
- `rawLocation` — **0-42 means AT a named location, 255 means "unspecified"** (not "in battle"). The `clearlyOnWorldMap` variable is misnamed.
- `paused` — 1=pause menu open (reliable)
- `slot0 == 0xFFFFFFFF` — true on fresh process + formation + some dialogue transitions
- `slot0 == 0x000000FF` — in-battle or stale-in-battle residue
- `battleTeam` — 0=player turn, 1=enemy, 2=NPC/uninit
- `battleActed / battleMoved` — turn-state flags (1 after action on that turn). Earlier "submenu mirror" theory was WRONG.

### Addresses that are UNRELIABLE / CONTEXT-DEPENDENT
- `menuCursor` — OVERLOADED. Meaning changes based on context:
  - Action menu (submenuFlag=0): 0-4 (Move/Abilities/Wait/Status/AutoBattle)
  - Abilities submenu (submenuFlag=1): skillset index (can be 5+)
  - Targeting: target list index
  - Enemy turn: enemy-side cursor
  - Pause menu: pause item index
- `battleMode` — ALSO OVERLOADED. Encodes (submode × cursor-tile-class):
  - During Move: 2=on unit's tile, 1=on other tile
  - During basic Attack: 4=on valid target, 1=on invalid
  - During Cast: 5=on caster's tile, 4=on valid target, 1=on invalid
  - Top-level menu: 3
  - Paused: 0
  - Fresh process: 255
  - Formation: 1 (separate meaning)
- `encA/encB` — **NOISE counters that drift independently and re-sync**. NOT a reliable encounter signal. Every rule using encA!=encB is a coincidence-detector.
- `gameOverFlag` — **STICKY across process lifetime once set**. Don't use as "game over is happening now" — it means "game over happened AT SOME POINT."
- `submenuFlag` & `gameOverFlag` share the same memory address (aliased). Disambiguate via other flags.
- `eventId` — range-dependent: 1-399 = real story event ID, 400+ = stale unit nameId, 65535 = unset.

### Addresses we SHOULD be reading but aren't
- `hover` — already read but NOT passed to ScreenDetectionLogic. Might disambiguate WorldMap vs TravelList (not yet confirmed).
- Unknown — we need a signal to split:
  - WorldMap vs TravelList (byte-identical in current 18 inputs)
  - Shop types at a village (Outfitters vs Warrior Guild vs Save Game all byte-identical)
  - Different Cutscenes from each other

### Confirmed bugs in current detection rules

| # | Rule | Bug |
|---|---|---|
| 1 | `Battle_AutoBattle` | Requires `submenuFlag==1`, but real top-level AutoBattle hover has `submenuFlag=0`. Rule NEVER fires correctly. Triggers SPURIOUSLY inside Abilities submenu when `cursor==4` (5th skillset). → ROOT CAUSE of "Auto-Battle instead of Wait" bug. |
| 2 | `Battle_Casting` | Requires `battleMode==1`, but cast-time and instant targeting both give `battleMode=4` when on a valid target. Separate "casting" detection is physically impossible from memory. → Collapse into Battle_Attacking; use client-side ability tracking for queued-vs-instant. |
| 3 | `WorldMap` | Rule `party==0 && ui==0`. Actual world map has `party=0, ui=1` — rule never fires. Fresh-load WorldMap byte-identical to TitleScreen in 18 inputs. |
| 4 | `TravelList` | Rule `party==0 && ui==1`. Matches correctly IF preempted TitleScreen check passes. Currently doesn't. |
| 5 | `PartyMenu` | Rule `party==1`. Would work, but preempted by `rawLocation==255 → TitleScreen`. |
| 6 | `EncounterDialog` | Uses `encA != encB` which is unreliable noise. |
| 7 | `Battle_Victory` | Uses `encA != encB` which is unreliable noise. Also requires `gameOverFlag==0` which is sticky 1 after first GameOver. |
| 8 | `Battle_Desertion` | Uses `encA == encB` which is coincidence-dependent. |
| 9 | `TitleScreen` | Rule `rawLocation==255` catches too much — also fires for post-load WorldMap / TravelList / PartyMenu / EncounterDialog. Missing sentinel-tightening. Also: two distinct TitleScreen states exist (fresh process vs post-GameOver) with different memory layouts. |
| 10 | `Battle_Dialogue` / `Cutscene` | `eventId < 200` filter misses real story events in the 200-399 range. Saw `eventId=302` in a cutscene. |
| 11 | `LoadGame` | No rule exists at all. Falls through to Battle_EnemiesTurn via stale flags. |
| 12 | `LocationMenu` (shops/services) | No rule for rawLocation=0-42 && !inBattle. Falls through to TravelList or WorldMap. |
| 13 | `Battle_ChooseLocation` | No rule. Falls through to WorldMap. |

### Meta-finding: rule ordering is broken

The `rawLocation==255 → TitleScreen` rule runs too early, catching many valid world-side states. Several rules (PartyMenu, TravelList, WorldMap, EncounterDialog) are unreachable because of this.

### Recommended fix sequence (when we pivot to coding)

1. Tighten TitleScreen rule — require full uninit sentinels
2. Move specific rules (PartyMenu, EncounterDialog) ahead of TitleScreen
3. Add new rules (LoadGame, LocationMenu, Battle_ChooseLocation)
4. Delete Battle_AutoBattle rule — UI label handles it
5. Collapse Battle_Casting into Battle_Attacking
6. Remove `encA/encB`-dependent rules, replace with stable signals
7. Remove `gameOverFlag==0` requirement — treat as sticky
8. Reinterpret `menuCursor` based on `submenuFlag + battleMode` context
9. Add location-type annotation to project_location_ids_verified.md (village vs campaign)
10. Memory-scan for missing disambiguators (WorldMap vs TravelList, shop types)

### Samples captured (45)

| # | Screen | Match |
|---|---|---|
| 1 | Battle_MyTurn | ✓ |
| 2 | Battle_Moving (cursor on own tile) | ✓ |
| 3 | Battle_Abilities (mid-cast) | ✓ |
| 4 | Battle_Abilities (fresh turn) | ✓ (but revealed behavior quirk) |
| 5 | Battle_MyTurn (fresh, after submenu) | ✓ |
| 6 | Battle_MyTurn (after real move) | ✓ |
| 7 | (background sampler log) | — |
| 8 | (theory notes) | — |
| 9 | Battle_Attacking (Lloyd, valid target) | ✓ |
| 10 | Battle_Casting (Haste, valid yellow target) | ❌ |
| 11 | Battle_Attacking (Wilham, valid target) | ✓ |
| 12 | Battle_Waiting (N/E/W all identical) | ✓ |
| 13 | Battle_Moving (cursor on other tile) | ❌ |
| 14 | Battle_Moving (cursor back on own) | ✓ |
| 15 | Battle_Attacking (cursor on invalid tile) | ❌ |
| 16 | Battle_Casting (cursor on caster's tile) | ❌ |
| 17-19 | Battle_Waiting facings | ✓ |
| 20 | Battle_MyTurn hovering AutoBattle | ✓ |
| 21 | Battle_Paused | ✓ |
| 22 | Battle_Status | ✓ |
| 23 | Battle_EnemiesTurn | ✓ |
| 24-26 | WorldMap (post-flee, stale) | ❌ all |
| 27 | TitleScreen (fresh process) | ✓ |
| 28-29 | WorldMap (post-restart+load) | ❌ |
| 30 | TravelList | ❌ |
| 31 | PartyMenu | ❌ |
| 32 | Battle_Encounter dialog | ❌ |
| 33 | Battle_Formation | ✓ |
| 34 | GameOver | ✓ |
| 35 | LoadGame | ❌ (no rule) |
| 36 | Outfitters | ❌ |
| 37 | Tavern | ❌ |
| 38 | Warrior Guild | ❌ (byte-identical to Outfitters) |
| 39 | Poachers' Den | ❌ (byte-identical) |
| 40 | Save Game | ❌ (byte-identical) |
| 41 | TitleScreen (post-GameOver) | ❌ |
| 42 | Battle_ChooseLocation (Orbonne sub-locations) | ❌ |
| 43 | Pre-battle Cutscene (Loffrey dialogue) | ❌ |
| 44 | Battle_Desertion (encA≠encB — missed) | ❌ |
| 45 | Battle_Desertion (encA==encB — caught by luck) | ✓ |

**Still unmeasured:** Battle_Victory (clean win — desertion blocked it), Battle_Dialogue (mid-battle chatter).

---

## 46. Battle_Dialogue (mid-battle scripted chatter) ✓ detected-as-Cutscene

**Ground truth:** Mid-battle dialogue event playing during an active battle.

| input | value | 🔑 |
|---|---|---|
| party | 0 | |
| ui | 1 | |
| rawLocation | 255 | |
| **slot0** | **0xFFFFFFFF** | ⭐ NOT the typical in-battle 0x000000FF |
| slot9 | 0xFFFFFFFF | |
| battleMode | 0 | |
| moveMode | 0 | |
| paused | 0 | |
| **gameOverFlag** | **1** | sticky |
| battleTeam | 0 | |
| **battleActed** | **1** | |
| **battleMoved** | **1** | |
| **eventId** | **5** | ⭐ low event ID (real story event, < 200 range) |
| submenuFlag | 1 | |
| menuCursor | 0 | |
| encA/encB | 0/0 | |

**Detected (old logic):** `Cutscene` — the out-of-battle path fires because `slot0=0xFFFFFFFF` means `inBattle=false` (not unitSlotsPopulated, not battleModeActive).

**🎯 KEY INSIGHT — mid-battle dialogue ALSO has slot0=0xFFFFFFFF:**

This contradicts our earlier model. Mid-battle cutscenes apparently temporarily clear slot0 (unit slots get torn down/rebuilt during event sequences). So mid-battle dialogue routes through the `!inBattle` path.

**For detection, the distinction "mid-battle dialogue" vs "pre-battle cutscene" is very fuzzy.** They share:
- slot0=0xFFFFFFFF
- rawLocation=255 (mid-battle) vs rawLocation=18 (pre-battle at Orbonne)
- eventId in real-event range

**Possible discriminators:**
- `battleActed=1, battleMoved=1` in this sample vs `0, 0` in pre-battle (#43). Suggests mid-battle dialogue happens AFTER at least one action has occurred.
- `submenuFlag=1` in both — same
- `gameOverFlag=1` in both (sticky after first session) — not useful here

**Refined Cutscene/Battle_Dialogue rules:**
- `rawLocation in 0-42 && eventId 1-399 && slot0=0xFFFFFFFF` → pre-battle Battle_Dialogue (at named location)
- `rawLocation=255 && eventId 1-399 && slot0=0xFFFFFFFF && (acted=1 OR moved=1)` → mid-battle Battle_Dialogue (in the field)
- `rawLocation=255 && eventId 1-399 && slot0=0xFFFFFFFF && acted=0 && moved=0` → regular Cutscene

Note: eventId=5 falls in the conservative < 200 range. The audit's earlier finding that 302 is a real event only applies to SOME cutscenes — not all.

---

## 47. WorldMap live-test post-rewrite ❌ MISMATCH — rawLocation is ALSO sticky

**Ground truth:** User on the world map.

| input | value | vs audit-assumed-WorldMap |
|---|---|---|
| **rawLocation** | **26** | ⚠️ STALE from previous battle at Siedge Weald |
| slot0 | 0x000000FF | stale |
| slot9 | 0xFFFFFFFF | |
| moveMode | 20 | NEW value |
| hover | 254 | — |
| battleMode | 0 | |
| eventId | 65535 | |

**Detected (new logic):** `LocationMenu` — ❌ should be `WorldMap`

**🚨 CRITICAL CORRECTION — `rawLocation` is ALSO sticky after leaving a battle:**

The theory from #36 (Outfitters at rawLocation=9) that "rawLocation=0-42 means AT a named location" was WRONG. It actually means "rawLocation still holds the last-visited location," regardless of whether the player is currently there.

This invalidates the `atNamedLocation` override in the rewritten logic. We need a different signal to distinguish:
- Actually AT a shop/village/battle (dialog overlays, menus visible)
- On the world map with stale rawLocation

**New candidate signals:**
- `hover` values: 254 here (world map), 255 in other states, 0-42 in EncounterDialog (audit #32 had hover=255). Need more samples.
- `moveMode`: 20 here (world map), 255 in-battle, 0 in some menus. NEW value — worth investigating.
- **`ui` may be the best split:** at Outfitters `ui=1`, at Tavern `ui=0`. On world map here `ui=0`. At WorldMap-post-load `ui=1`. Not consistent either.

**This is a significant regression in the rewrite.** The `atNamedLocation` override now makes EVERY world-map reading misdetect as LocationMenu.

**Fix options:**
1. Revert the `atNamedLocation` override; require MORE than rawLocation alone to detect "at a location."
2. Find a reliable "am I at a location vs on world map" discriminator. `moveMode` shows a non-255 non-0 value (13 or 20) only on the world map — might be it.

Preferred: **Use `moveMode` as the world-map discriminator.** Our previous samples showed `moveMode=13` on post-flee world map and `moveMode=20` here. 255 in battle. 0 in menus. Non-zero-non-255 moveMode may be the "on world map" signal.

---

## 48. WorldMap 2nd reading + 49. Outfitters post-rewrite — `hover` is the world-map discriminator

**World map (sample #48, user reconfirmed):** `rawLocation=26, hover=254, moveMode=20, submenuFlag=0`
**Outfitters (sample #49, user at shop):** `rawLocation=9, hover=255, moveMode=20, submenuFlag=0`

**🎯 `hover` SPLITS them:**
- `hover=254` → world map (cursor hovering an unnamed map location OR "empty" world tile)
- `hover=255` → at a named location (cursor/UI focus is on the location itself, not a map tile)

**moveMode is NOT the discriminator** — both read `moveMode=20`. The earlier sample showing `moveMode=13` (post-flee) was a different session state.

**submenuFlag also NOT the discriminator** in this session — both read 0. Earlier samples had `submenuFlag=1` at Outfitters; the "is a menu panel visible" state may be an additional layer we haven't decoded yet.

**Proposed rule update:**
- At a named location: `rawLocation in 0-42 && hover == 255` (rawLocation matches AND the hover confirms we're focused on the location, not a map tile)
- World map with stale rawLocation: `rawLocation in 0-42 && hover != 255` → treat as WorldMap
- But `hover` isn't currently passed to ScreenDetectionLogic.Detect() — need to plumb it through.

**Action:** add `hover` as an input to ScreenDetectionLogic.Detect(), then use it as the primary discriminator for "at a location vs on the map."

---

## 50. Outfitters after restart ❌ MISMATCH — hover discriminator BUSTED

**Ground truth:** Player re-entered Outfitters after fresh restart+load+boot.

| input | value | vs #49 (Outfitters pre-restart) | vs #47/#48 (world map) |
|---|---|---|---|
| rawLocation | 9 | same | world map had 26 |
| **hover** | **254** | ⚠️ was 255! | world map also had 254 |
| slot0 | **0xFFFFFFFF** | ⚠️ was 0x000000FF | was 0x000000FF |
| battleMode | **255** | ⚠️ was 0 | was 0 |
| moveMode | **255** | ⚠️ was 20 | was 20 |
| battleTeam | **2** | was 0 | was 0 |
| acted/moved | **255/255** | was 0/0 | was 0/0 |
| all else | matches travel pattern | | |

**Detected:** `TravelList` — ❌ should be `LocationMenu`

**💥 MAJOR FINDING — post-restart Outfitters is structurally different from pre-restart Outfitters:**

All the "is battle active / was battle active" sentinels (slot0, battleMode, moveMode, battleTeam, acted, moved) are in their uninit/fresh state. Only rawLocation=9 persists (from the save).

**And hover=254 both on the world map AND at Outfitters after restart — the hover discriminator doesn't work in fresh state.**

Compare pre- vs post-restart at Outfitters:
- Pre-restart (#49): active in-session, had in-battle residue, hover=255
- Post-restart (#50): fresh process, no session history, hover=254

**Possible interpretation:** hover may track "has the game recalibrated the cursor to a UI focus." Post-restart, the cursor hasn't been "placed" yet on the world-map-but-really-at-shop screen, so hover stays at its fresh default.

**This means hover is STATEFUL across process restarts.** Not a reliable fundamental signal.

**Back to square one for Outfitters vs WorldMap detection** — they're byte-identical in this fresh state. Even rawLocation is ambiguous (post-restart: "saved at Warjilis, now at Outfitters of Warjilis" both read 9).

**Honest conclusion:** With the 19 memory inputs we currently read, we cannot reliably distinguish:
- Post-restart WorldMap at town X vs Outfitters of town X
- Post-restart TravelList vs Outfitters

We need additional addresses:
- A "focused UI widget type" or "active menu ID" address
- A "is there a menu panel visible" counter

Until those are found, LocationMenu detection will be hit-or-miss post-restart.

---

## 51. Battle_Acting post-rewrite ❌ UNDETECTABLE with current inputs

**Ground truth:** Player used an ability, returned to top-level action menu with Abilities grayed out.

| input | value |
|---|---|
| party | 1 |
| ui | 0 |
| rawLocation | 255 |
| slot0 | 0x000000FF |
| slot9 | 0xFFFFFFFF |
| battleMode | 3 |
| **battleActed** | **0** |
| **battleMoved** | **0** |
| submenuFlag | 0 |
| menuCursor | 1 (Abilities — where cursor was left) |
| eventId | 401 |

**Detected:** `Battle_MyTurn` — ❌ should be `Battle_Acting`

**🚨 KEY FINDING — battleActed / battleMoved are NOT reliable post-action flags:**

Despite the user confirming an action was used, both battleActed=0 and battleMoved=0. The flags must flip back to 0 once the player returns to the top-level menu. They're not turn-state flags.

Reviewing the earlier background sampler log (rows 4→5): battleActed/Moved DID flip 0→1 during the action sequence, but apparently transition back to 0 at the menu return.

**Possible interpretation (pending investigation):**
- battleActed/Moved might be "is the turn-executing ANY action RIGHT NOW" (combat animation flags)
- Not "has acted/moved at some point THIS turn"
- Visual "grayed out" state for Abilities must live in a different address

**Consequence:** The concept of `Battle_Acting` cannot be reliably detected from the current 19 inputs. `Battle_Acting` will currently detect as `Battle_MyTurn`, which is OK for most navigation purposes (same valid paths exist: Wait, Status, and possibly Reset Move).

**Pragmatic fix:** Track "action taken this turn" client-side in the mod using `_movedThisTurn` + a new `_actedThisTurn` flag. Pass to DetectScreen as extra state, have the call site override the detected name when client tracking indicates Battle_Acting. This is out-of-scope for pure ScreenDetectionLogic.

---

## 52. LocationMenu post-restart ❌ BYTE-IDENTICAL to TravelList

**Ground truth:** User entered a shop at Warjilis (rawLocation=9) after fresh restart.

| input | value |
|---|---|
| party | 0 |
| ui | 1 |
| rawLocation | 9 |
| slot0 | 0xFFFFFFFF |
| slot9 | 0xFFFFFFFF |
| battleMode | 255 |
| moveMode | 255 |
| hover | 255 |
| all else | fresh/uninit |

**Compared to TravelList (previous sample):**
| input | LocationMenu | TravelList |
|---|---|---|
| rawLocation | 9 | 26 |
| all other | identical | identical |

**Only rawLocation differs. And rawLocation is STICKY — it carries the last-visited location regardless of current screen.**

**Detected:** `TravelList` (falls through — my slot0==255 guard excludes fresh-session LocationMenu).

**🚨 CONFIRMED LIMITATION:**
With current 19 memory inputs, we cannot distinguish:
- At a shop/village after fresh restart (no battle entered yet)
- TravelList showing this location as a travel destination

Both have party=0, ui=1, hover=255, slot0=0xFFFFFFFF, fresh sentinels. Only rawLocation differs, but rawLocation is sticky and can't be trusted as "current screen" info.

**Resolution requires new memory addresses** — specifically a menu-type or submenu-depth indicator. Memory scan needed.

**Current behavior:** Both detect as TravelList. Accept as known limitation until scan.

---

## 53. Live verification session (2026-04-14, post-rewrite)

After deploying the rewrite, verified live state-by-state with user providing ground truth.

**✅ Detecting correctly:**
- WorldMap (with cursor hovering a named location, hover=0-42)
- TravelList
- EncounterDialog
- PartyMenu
- Battle_MyTurn (fresh + hovering AutoBattle)
- Battle_Moving
- Battle_Abilities
- Battle_Attacking (including what used to be Battle_Casting)
- Battle_Waiting
- Battle_Paused
- Battle_Status (clicked-in variant only)
- Battle_EnemiesTurn
- Battle_Formation
- Battle_Dialogue (including Guest-Joined variant)
- GameOver
- LoadGame
- Cutscene
- TitleScreen (fresh uninit memory)
- Post-battle stale (transitioning back to WorldMap) → WorldMap

**Bugs found and fixed during verification:**
- Battle_Status firing on hover-only: tightened to require submenuFlag=1 (#fixed)
- Post-load TravelList mis-detecting as Battle_MyTurn: added `onWorldMapByMoveMode` override using moveMode=13/20 (#fixed)
- GameOver routing to LocationMenu: added `paused=1` override to skip atNamedLocation (#fixed)
- Battle_Dialogue-during-in-battle-stale routing to TitleScreen: reordered so eventId check precedes post-battle fallback (#fixed)
- Post-battle stale at rawLocation=255 routing to TitleScreen: changed fallback to WorldMap (more semantically correct) (#fixed)
- WorldMap w/ cursor on named location routing to TitleScreen: added `hover in 0-42 → WorldMap` rule (#fixed)

**Known limitations remaining (require memory scan):**
- Battle_Acting — acted/moved flags transient, unreadable from current inputs (#51)
- LocationMenu vs TravelList post-restart — byte-identical (#52)
- Shop types (Outfitters/Tavern/Warrior Guild/Save Game) — all collapse to single LocationMenu state
- Battle_ChooseLocation post-restart — byte-identical to WorldMap post-restart
- WorldMap pristine-no-cursor-focus — byte-identical to fresh TitleScreen until user moves cursor

**Not tested this session:**
- Battle_Victory (clean win — blocked by desertion threshold)
- Battle_AlliesTurn (NPC turn — rare)

---

## 1. Battle_MyTurn ✓ MATCH

**Ground truth:** Ramza's turn at The Siedge Weald, action menu on Move (no action taken yet)

| input | value |
|---|---|
| party | 0 |
| ui | 0 |
| rawLocation | 255 |
| slot0 | 0x000000FF |
| slot9 | 0xFFFFFFFF |
| battleMode | 3 |
| moveMode | 0 |
| paused | 0 |
| gameOverFlag | 0 |
| battleTeam | 0 |
| battleActed | 0 |
| battleMoved | 0 |
| encA | 2 |
| encB | 2 |
| isPartySubScreen | false |
| eventId | 401 (nameId=Ramza) |
| submenuFlag | 0 |
| menuCursor | 0 |

**Detected:** `Battle_MyTurn` — ✓ matches ground truth

**Notes:** eventId=401 is the active-unit nameId (reused addr), not a script event — correctly filtered by `eventId < 200` guard.

---

## 2. Battle_Moving ✓ MATCH

**Ground truth:** Pressed Enter on Move, tile selector now active

| input | value | Δ from MyTurn |
|---|---|---|
| battleMode | 2 | 3 → 2 (action menu → tile selection) |
| moveMode | 255 | 0 → 255 (tile selection active) |
| gameOverFlag | 1 | 0 → 1 (submenuFlag reused — shares addr) |
| submenuFlag | 1 | 0 → 1 (Move submode active) |

All other inputs identical to Battle_MyTurn.

**Detected:** `Battle_Moving` — ✓ matches ground truth

**Notes:**
- `submenuFlag` and `gameOverFlag` share the same memory (0x140D3A10C). They're aliased in `DetectScreen` — both read from `v[18]`. This means "submenu active" and "game over" must be disambiguated by *other* flags, which is why the post-battle/desertion rules check `paused` + `acted` combos.
- `battleMode=2 && menuCursor≠2` correctly routes to Battle_Moving (not Battle_Waiting).

---

## 3. Battle_Abilities ✓ MATCH

**Ground truth:** Abilities submenu open (skillset list visible). Ramza has a cast-time ability already queued (acted/moved flags set from prior action).

| input | value |
|---|---|
| battleMode | 3 |
| submenuFlag | 1 |
| menuCursor | 1 (Abilities) |
| battleTeam | 0 |
| battleActed | 1 |
| battleMoved | 1 |

**Detected:** `Battle_Abilities` — ✓ matches ground truth

**Notes:**
- Rule requires `battleActed==1 || battleMoved==1` to distinguish from "Abilities menu freshly opened with no action." Here both are 1 because a spell is queued — happens to satisfy the guard.
- ⚠️ **Potential gap:** What if Ramza opens Abilities on his *first* turn with nothing queued? `battleActed=0, battleMoved=0` would fail this guard and fall through to Battle_MyTurn. Worth testing explicitly next reachable opportunity.

---

## 4. Battle_Abilities (FRESH TURN, no cast queued) ✓ MATCH — but investigate

**Ground truth:** Fresh turn, unit has not moved or acted. Abilities submenu open.

| input | value | 🔍 notes |
|---|---|---|
| battleMode | 3 | |
| submenuFlag | 1 | |
| menuCursor | 1 | |
| battleTeam | 0 | |
| **battleActed** | **1** | ⚠️ unexpectedly 1 on a "fresh" turn |
| **battleMoved** | **1** | ⚠️ unexpectedly 1 on a "fresh" turn |
| slot0 | 0x0000002A | ≠ 0xFF (was 0xFF in earlier reads!) |
| encA/encB | 3/3 | incremented from 2/2 |

**Detected:** `Battle_Abilities` — ✓ matches, but the *reason* it matches is surprising.

**🔍 KEY FINDING — "acted/moved" flags don't mean what the code comment says:**

The detection logic assumes `battleActed==1 || battleMoved==1` distinguishes "action taken" from "fresh turn." But here, on a fresh turn with no action, BOTH are already 1. These flags don't reflect the *current unit's* action state — they appear to be **set by the mere act of entering the Abilities submenu** (or reflect some other unit's state entirely).

Interpretation candidates:
1. Flags reflect the *last* unit to act, not the current one
2. Flags flip to 1 as soon as you enter any submenu (submenuFlag side-effect)
3. These addresses (0x14077CA8C, 0x14077CA9C) aren't what we think they are

Also: `slot0` changed from `0xFF` (Ramza's turn, sentinel) to `0x2A` (42). This hints `slot0`'s meaning shifts based on submenu state — not just "is battle active."

**Implication for detection logic:** The "acted/moved" guard is LOAD-BEARING in 6 detection rules (`postBattle`, `postBattlePaused`, Battle_Dialogue filter, `Battle_Abilities`, `Battle_MyTurn`, `Battle_Acting`). If these flags can be 1 on a fresh turn, then:
- Battle_MyTurn rule (`acted==0 && moved==0`) would MISS this state
- Battle_Acting rule (`acted==1 || moved==1`) would FIRE spuriously

This is very likely a source of real detection bugs. Worth scanning what's at those addresses mid-turn vs start-of-turn.

---

## 5. Battle_MyTurn (fresh, AFTER Abilities → Escape) ✓ MATCH

**Ground truth:** Same fresh turn as #4, pressed Escape to leave Abilities submenu.

| input | value | vs #4 (Abilities) |
|---|---|---|
| battleMode | 3 | same |
| submenuFlag | 0 | was 1 |
| menuCursor | 0 | was 1 |
| **battleActed** | **0** | was 1 ⬅️ FLIPPED |
| **battleMoved** | **0** | was 1 ⬅️ FLIPPED |
| gameOverFlag | 0 | was 1 (aliased to submenuFlag) |
| slot0 | 0x2A | same (stays 0x2A after menu touch) |
| encA/encB | 2/2 | was 3/3 ⬅️ CHANGED BACK |

**Detected:** `Battle_MyTurn` — ✓ matches

**🔍 REVISED FINDING — acted/moved are submenu-driven, NOT turn-state:**

Same fresh turn, no action taken between readings. Yet:
- In top-level menu: `acted=0, moved=0` ✓ (as code expects)
- In Abilities submenu: `acted=1, moved=1` ⚠️ (contradicts code's assumption)

**Conclusion:** `battleActed` and `battleMoved` at addresses 0x14077CA8C / 0x14077CA9C **flip to 1 when ANY submenu is opened** (submenuFlag=1 side-effect). They do NOT reliably indicate "the unit has acted this turn."

**Additional oddity:** `encA`/`encB` changed from 2/2 → 3/3 → 2/2 across readings — these may be encounter *counters* that tick per-frame or per-input, not stable state. Rules that rely on `encA != encB` (Battle_Victory detection) may need to confirm persistence over multiple reads.

**Implications for detection rules:**
1. The `Battle_Abilities` rule's `(acted==1 || moved==1)` guard isn't verifying action state — it's redundantly verifying `submenuFlag==1` (which is already checked). Remove that guard.
2. `Battle_Acting` rule likely misfires whenever a submenu is open, regardless of actual action state.
3. `postBattle` rule's `(acted==1 || moved==1)` guard may have the same issue — what if those flags are ALSO sticky across submenu visits?

This is probably the root cause of the "Auto-Battle triggers instead of Wait" bug (rule #1 in handoff): after a move, acted/moved flags are set, but then ALSO set when browsing menus, confusing the `Battle_Acting` vs `Battle_MyTurn` detection and leaving menu cursor tracking desynced.

**Next investigation target:** Dump memory while in Battle_Acting (after a real action) to compare — do acted/moved stay at 1 after leaving the submenu? If they do, these flags ARE meaningful after real actions but get spuriously set during submenu browsing.

---

## 6. Battle_MyTurn AFTER real move ❌ DETECTION LOGIC IS WRONG

**Ground truth:** Unit was moved to a new tile (real move, confirmed landing). Now back on top-level action menu.

| input | value |
|---|---|
| battleMode | 3 |
| submenuFlag | 0 |
| menuCursor | 0 |
| **battleActed** | **0** |
| **battleMoved** | **0** |

Identical to #5 (before the move). **Zero observable difference** in these 18 inputs between "before move" and "after move" at the top-level menu.

**🚨 MAJOR FINDING — CONFIRMED:**

`battleActed` and `battleMoved` at 0x14077CA8C / 0x14077CA9C are **NOT turn-state flags at all**. They are pure submenu-state mirrors:
- `submenuFlag==1` → both addresses contain 1
- `submenuFlag==0` → both addresses contain 0
- **The actual move/action state of the unit is not reflected anywhere in the 18 inputs we're reading.**

This means:
1. **Battle_Acting detection cannot work** via these addresses. The rule `acted==1 || moved==1` fires while ANY submenu is open and extinguishes the moment you leave. Whether the unit moved is invisible to detection.
2. **The `DetectedScreen.UI` label** showing "Reset Move" vs "Move" depends on `BattleMoved==1 || _movedThisTurn` (CommandWatcher.cs:2245). The `BattleMoved` part is always 0 at the top-level menu, so it relies entirely on the C# `_movedThisTurn` flag — which is tracked manually by the mod, not read from memory.
3. **The handoff's bug #1** (Auto-Battle triggers instead of Wait) now has a plausible root cause: after a move, the code thinks acted/moved are authoritative, but they're just submenu echoes. The menu cursor has actually advanced to some state these flags can't describe.

**Action items:**
- Find the REAL acted/moved addresses (or confirm they only exist on the active unit struct, which would explain why these proxies were chosen).
- Until the real addresses are found: **delete the `acted/moved` checks from every detection rule** and rely on the in-mod `_movedThisTurn` tracker instead. The current checks add noise, not signal.
- Rules that MUST be fixed: Battle_Abilities (drop redundant guard), Battle_Acting (currently unreachable without submenu open — broken), Battle_MyTurn (works only because acted/moved happen to be 0 in the top-level menu — fragile).

---

## 7. Background Sampler — 47 transitions across 3 turns

**Revised understanding — acted/moved ARE turn-state flags, but with a twist:**

Hypothesis #6 ("they're just submenu mirrors") was WRONG. Counter-examples in the log:
- Row 4→5 (t=04:34:03→04:34:04): `submenuFlag=0→0` stayed constant, but `acted/moved=0,0 → 1,1`. That's a turn action leaving a trace.
- Row 9→10 (t=04:34:12.2→12.6): `submenuFlag=0→1` went up, but `acted/moved=0,0 → 0,0` stayed. Opening a submenu did NOT flip them.

**So the side-effect seen in sample #4 wasn't a submenu side-effect. Something else caused it.**

Correct model from the log:
1. **Turn START:** `(acted=0, moved=0)` regardless of submenu.
2. **After Move completes:** flips to `(acted=1, moved=1)` — and stays even through submenu transitions.
3. **Next unit's turn:** resets to `(0,0)` at turn-start.
4. **Enemy turn (team=1):** flags carry whatever state the enemy is in. Row 37 shows `team=1, acted=0, moved=0` — enemy at start of own turn. Row 21 shows `team=1, acted=1, moved=1` — enemy mid-action.

**What ACTUALLY happened in sample #4 ("fresh turn" Battle_Abilities):** The user's "fresh turn" wasn't fresh. Probably the previous battle action (from handoff: "Aurablast selection broken" / earlier move attempts) left acted/moved set, and they persisted across the Abilities submenu entry. The submenu entry WASN'T the cause — prior action was.

**So the 6 detection rules relying on acted/moved are likely CORRECT. The earlier hypothesis #5 is WITHDRAWN.**

**New findings from the log:**
- `menuCursor` spans 0-5 (not 0-4 as documented). Row 25 shows `cursor=5`. That's off by one from the "5 items: 0..4" assumption. Worth investigating — is 5 a transient value during menu exit animation? If so, the `case 5 → null` in DetectScreen.UI map means Claude sees UI=null briefly.
- `cursor=0` appears with `acted=1, moved=1, submenuFlag=1` (row 26 t=04:34:36): this is "Reset Move" post-move. Consistent with the `hasMoved` logic.
- `slot0` transitions: `0x2A → 0xFF → 0x93 → 0xFF` — `0x2A` (42) and `0x93` (147) are unit IDs (active unit's index?). `0xFF` is the sentinel "no submenu cursor unit." This address is overloaded: it's both the battle-active sentinel AND a submenu cursor pointer.

**Implications for the real bugs:**
- **"Auto-Battle instead of Wait" (bug #1):** Cursor=4 (AutoBattle) appears twice (rows 6, 18) as a transient while exiting Abilities back to top-level. If Claude samples during this transient, `menuCursor==4` + `submenuFlag==1` matches the Battle_AutoBattle rule, giving the wrong screen name for ~200ms. This IS a real detection bug but it's about settling/timing, not broken flags.
- **`menuCursor=5`** (row 25): likely the "Reset Move + extra step" state — need to investigate what cursor 5 actually IS on screen.
- **`submenuFlag` transients** (rows 10, 16 — flipping to 1 then back to 0 without any submenu change): suggests submenuFlag can flicker, not just state-transition. Screen detection happening during these flickers would misclassify.

**Revised action plan:**
1. **DO NOT** rip out the acted/moved guards — they work.
2. **DO** investigate `menuCursor=5` — what screen is that?
3. **DO** add a "two-consecutive-match" settle filter to any screen that uses `submenuFlag` or `menuCursor` directly (some already have this via `DetectScreenSettled` — verify it's applied everywhere it needs to be).
4. **DO** replace my sample #4/#6 conclusions in memory — the theory was wrong.

---

## 8. FINAL ROOT CAUSE — menuCursor is a context-dependent overloaded address

The user confirmed: during turn 3 they opened Monk's Martial Arts submenu and scrolled through abilities. The `cursor=5` reading (row 25) coincides with this. Combined with enemy-turn readings (rows 22-28, cursor values 1,2,3,5,0,2), this proves:

**`menuCursor` at 0x1407FC620 is the ACTIVE UI CURSOR — whatever cursor the game is currently rendering.** Its meaning changes based on context:

| Context | menuCursor meaning | Valid range |
|---|---|---|
| `team=0, battleMode=3, submenuFlag=0` | Action menu (0=Move,1=Abilities,2=Wait,3=Status,4=AutoBattle) | 0-4 |
| `team=0, battleMode=3, submenuFlag=1` | Skillset / ability list index (Abilities submenu) | 0-N (skillset count) |
| `team=0, battleMode=2` | Move/facing cursor | variable |
| `team=0, battleMode=1 or 4` | Target list index | 0-N (tile count) |
| `team=1 or 2` | Enemy/NPC cursor — do not interpret for player UI | any |

**Detection rules currently using menuCursor as if it's always the action menu:**
- `Battle_Status`: `inBattle && paused==1 && menuCursor==3` — OK only because this fires under `paused==1`, which forces action-menu context
- `Battle_Waiting`: `inBattle && battleMode==2 && menuCursor==2` — PROBABLY BUGGY: during move/facing selection, cursor=2 could mean something else entirely
- `Battle_AutoBattle`: `inBattle && submenuFlag==1 && battleMode==3 && menuCursor==4` — BUGGY: in Abilities submenu with 5+ skillsets, cursor=4 means "5th skillset" not "AutoBattle"
- `Battle_Abilities`: `inBattle && submenuFlag==1 && battleMode==3 && battleTeam==0 && (acted||moved) && menuCursor==1` — only matches when cursor happens to land on abilities slot 1 in the submenu, which isn't what we want

**The `battle_attack` / Wait bugs:**
- When in Abilities submenu with cursor=4 (5th skillset), detection returns `Battle_AutoBattle`. Claude's command dispatcher sees this, sends a "get out of AutoBattle" sequence that actually navigates through the abilities list, lands somewhere wrong, picks the wrong option → ends turn as AutoBattle.
- Same problem for Wait: after moving, opening Abilities to cancel, cursor might be at position 2 in the submenu (3rd skillset). Detection says `Battle_Waiting` (because cursor=2 + battleMode=2 somewhere). Wrong screen → wrong keys.

**The real fix: scope `menuCursor` interpretation to `submenuFlag=0 && team=0`.** Inside submenus, we need separate signals (battle menu tracker already exists for this per `_battleMenuTracker`).

Before changing anything, confirm by reproducing: open Abilities submenu, scroll to 5th skillset, dump detection. Should return `Battle_AutoBattle` (the bug). Then apply the fix, same repro should return `Battle_Abilities`.

---

## 9. Battle_Attacking (Lloyd, basic attack targeting) ✓ MATCH

**Ground truth:** Lloyd in basic-attack targeting mode (instant-action cursor up).

| input | value |
|---|---|
| battleMode | 4 |
| moveMode | 255 |
| submenuFlag | 1 |
| gameOverFlag | 1 (aliased) |
| battleActed | 1 |
| battleMoved | 1 |
| battleTeam | 0 |
| **menuCursor** | **1** |
| eventId | 401 (Ramza nameId — stale) |

**Detected:** `Battle_Attacking` — ✓ matches

**Notes:**
- `menuCursor=1` during attack targeting → this is **a target cursor index**, NOT the action-menu "Abilities" slot. Confirms the overload theory.
- `eventId=401` (Ramza) while Lloyd is active — eventId address stale, doesn't track active unit on every turn. Not a detection concern since we filter by `<200`.
- `battleMode=4` is the only required discriminator for this state. The rule doesn't use menuCursor — good.

---

## 10. Battle_Casting (Wilham casting Haste, yellow target tiles) ❌ MISMATCH

**Ground truth:** Wilham (Monk w/ Time Magicks secondary) casting Haste. Yellow target tiles visible. Cast-time ability, should be queued.

| input | value |
|---|---|
| **battleMode** | **4** | ⚠️ expected 1 |
| moveMode | 255 |
| submenuFlag | 1 |
| battleActed | 1 |
| battleMoved | 1 |
| menuCursor | 1 |
| eventId | 401 |

**Detected:** `Battle_Attacking` — ❌ should be `Battle_Casting`

**🚨 FINDING — `battleMode` does NOT split cast-time from instant abilities the way we thought:**

ScreenDetectionLogic comment (lines 30-44) states:
- `battleMode=1` = cast-time magick targeting (Fire, Haste, Cura, ...)
- `battleMode=4` = instant targeting (basic Attack, Throw, Potion, ...)

**But Haste (cast-time, should be mode 1) reads as mode 4.**

Possible causes:
1. The `battleMode` split theory is wrong — both cast-time and instant use mode 4, and mode 1 is something else entirely (or only certain specific spells).
2. The Ivalice Chronicles remaster changed this vs the older PSX/WotL data the comment is based on.
3. There's a different address that distinguishes "this is a queued cast" vs "this is instant."

**Consequence:** `Battle_Casting` detection is almost never firing — every "cast targeting" state is misdetected as `Battle_Attacking`. This explains feedback_cast_time_abilities.md ("ct>0 abilities are queued, not instant — must Wait after") — Claude can't tell them apart from memory alone.

**Possible signal to disambiguate:** The "queued" state might show up in some other address. Ideas to explore:
- A per-unit "CT" timer starting to count
- A flag on the unit's ability entry
- The yellow-vs-red tile color source data

**For now:** the fix for #8 (scope menuCursor to submenu=0) is still valid, but `Battle_Casting` is a deeper problem. Worth a dedicated investigation.

---

## 11. Battle_Attacking (Wilham basic attack, 4 cardinal dirs) ✓ detected, but identical to #10

**Ground truth:** Wilham in basic-attack targeting (4 cardinal-direction cursor, instant).

| input | value | vs #10 (Haste cast) |
|---|---|---|
| battleMode | 4 | **same** |
| moveMode | 255 | same |
| submenuFlag | 1 | same |
| battleActed | 1 | same |
| battleMoved | 1 | same |
| menuCursor | 1 | same |
| eventId | 401 | same |
| party/ui/team/paused/encA/encB/slot0/slot9 | all identical | same |

**🚨 PROVEN: All 18 inputs are BYTE-FOR-BYTE IDENTICAL between "basic attack targeting" (instant) and "Haste targeting" (cast-time).**

**Detected:** `Battle_Attacking` — correct for this sample; but same memory inputs also gave `Battle_Attacking` when the ground truth was `Battle_Casting` (#10). Detection physically CANNOT distinguish these two states from memory alone.

**Final conclusion on Battle_Casting:**
- The 18 inputs fed to ScreenDetectionLogic do NOT contain a cast-vs-instant discriminator.
- Either the discriminator is elsewhere in memory (unobserved), or the game truly treats these as the same UI state internally.
- **For Claude's UI navigation purposes, this doesn't matter** — both screens are "pick a tile, press F to confirm." The cursor movement, confirm key, and tile targeting semantics are identical.
- **Where it DOES matter** is post-action: cast-time queues, instant executes. But that's a property of the **ability that was selected**, not the **targeting screen**. We can track this client-side in the mod when an ability is picked (we already have `_lastAbilityName`), and look up the ability's ct value separately.

**Recommended action:**
1. **Collapse `Battle_Casting` into `Battle_Attacking`** in ScreenDetectionLogic. They're the same screen. Rename to `Battle_Targeting` if we want to be honest about it.
2. Track cast-time status **at the ability selection moment** using ability metadata, stored in `_lastAbilityName` / adjacent state.
3. When a cast-time ability completes targeting, the mod knows "we just queued a cast" and returns "Queued" not "Used" in the response.

This kills two birds:
- Removes a detection branch that literally cannot fire correctly.
- Fixes feedback_cast_time_abilities.md (queued vs used) with client-side logic instead of broken screen detection.

---

## 12. Battle_Waiting (Wilham, post-action facing selection) ✓ MATCH

**Ground truth:** Wilham post-action, picking facing direction (Battle_Waiting).

| input | value |
|---|---|
| battleMode | 2 |
| menuCursor | 2 |
| submenuFlag | 1 |
| battleActed | 0 |
| battleMoved | 0 |
| battleTeam | 0 |

**Detected:** `Battle_Waiting` — ✓ matches

**Notes:**
- Uses rule `battleMode==2 && menuCursor==2` — the menuCursor=2 here is genuine (action-menu Wait slot highlighted/locked).
- ⚠️ But acted/moved=0,0 is surprising — Wilham just finished an action, so we'd expect 1,1. This might reset when Wait finalizes the turn, OR acted/moved only reflect move-button and action-button specifically, not "Wait-to-facing" transitions.
- Conflict with earlier: sample #2 (Battle_Moving) had `battleMode=2 + menuCursor=0` and worked. Here `battleMode=2 + menuCursor=2` is Waiting. Rule ordering is correct (Waiting checked before Moving).
- 🔍 Overlap concern: During `battleMode=2` (tile selection / move), if the cursor happens to land on a target tile that the game internally indexes as 2, this rule could misfire as Waiting. Need confirmation: in Battle_Moving, does menuCursor stay 0 or does it change with tile selection?

---

## 13. Battle_Moving (Wilham, cursor on different tile than character) ❌ MISMATCH

**Ground truth:** Wilham in Move mode, cursor moved to highlight a tile different from his current position.

| input | value | vs #2 (Ramza Battle_Moving, cursor on unit) |
|---|---|---|
| **battleMode** | **1** | was **2** ⬅️ CHANGED |
| moveMode | 255 | 255 |
| submenuFlag | 1 | 1 |
| menuCursor | 0 | 0 |
| battleActed | 0 | 0 |
| battleMoved | 0 | 0 |
| battleTeam | 0 | 0 |

**Detected:** `Battle_Casting` — ❌ should be `Battle_Moving`

**🚨 BIG FINDING — battleMode flips to 1 when move-cursor leaves the unit's starting tile:**

Compare to sample #2 (Ramza in Move mode with cursor on his own tile): `battleMode=2`. Now Wilham in same Move mode with cursor on a DIFFERENT tile: `battleMode=1`.

**Hypothesis:** `battleMode` at 0x140900650 isn't "which targeting submode is active" — it's **what the cursor is currently highlighting**:
- `2` = cursor on a "move-valid" tile (BFS path starts here) — i.e., ON the unit or trivially valid tiles
- `1` = cursor on an "action-valid" tile for targeting (hovering a tile where an action/spell would land)
- `4` = cursor on an instant-targeting tile (target list selection for Attack, etc.)

If this hypothesis is right, it also explains #10 vs #11: both are "cursor over target list" → `battleMode=4`. And it explains this reading: moving the cursor *off* the unit puts it over a different conceptual tile class → `battleMode=1`.

**Ramification:** battleMode is NOT a stable "what screen am I on" signal. It's a per-cursor-tile classification. Rules that key off specific battleMode values will flip while the cursor moves within a single screen.

**Bugs this creates:**
- Battle_Moving misdetects as Battle_Casting when the user moves the cursor (this sample)
- Battle_Casting misdetects as Battle_Attacking when a cast-time ability's cursor hovers certain tiles (#10)
- "What screen am I on" answer **depends on cursor position**, not on user intent

**Fix implications:**
- Need a screen-state signal that DOESN'T vary with cursor position within a screen
- Candidates: `submenuFlag`, `moveMode`, or a new address for "which submode the game is in" (not "what the cursor is over")
- `moveMode=255` is stable across #2 and #13 (both in Move mode, different cursor) — THAT might be the real "am I in move mode" signal. We've been ignoring it or calling it "VOLATILE/unused" per the address comment.

---

## 14. Battle_Moving (Wilham, cursor back on own tile) ✓ MATCH — and CONFIRMS the theory

**Ground truth:** Same Wilham Battle_Moving session, cursor now moved back onto his own tile.

| input | value | vs #13 (cursor on different tile) |
|---|---|---|
| **battleMode** | **2** | was **1** ⬅️ FLIPPED BACK |
| moveMode | 255 | 255 same |
| submenuFlag | 1 | 1 |
| menuCursor | 0 | 0 |
| battleActed | 0 | 0 |
| battleMoved | 0 | 0 |

**Detected:** `Battle_Moving` — ✓ matches

**🎯 THEORY CONFIRMED — `battleMode` tracks CURSOR TILE CLASS, not screen state:**

Three data points on the same Move screen:
- #2 (Ramza, cursor on own tile): `battleMode=2`, `moveMode=255`
- #13 (Wilham, cursor on different tile): `battleMode=1`, `moveMode=255`
- #14 (Wilham, cursor back on own tile): `battleMode=2`, `moveMode=255`

**`battleMode` flips purely based on cursor position. `moveMode` stays at 255 throughout.**

New model for battleMode:
- `1` = cursor hovering a non-origin valid-action tile
- `2` = cursor on the unit's own tile (or equivalently "move-starting-position" class)
- `4` = cursor on an instant-attack target tile
- `3` = top-level action menu (from samples #1, #3, #4, #5, #6 — no cursor tile at all)
- `0` = out-of-battle / fallback

**Real screen-state signal candidates:**
- `moveMode` (0x14077CA5C) — was called "VOLATILE/unused" in the code comment. Actually the stable "in Move submode" signal (255 throughout moving, presumably 0 outside).
- `submenuFlag` (0x140D3A10C) — 1 during any active submode, 0 at top-level action menu.
- Combined: `submenuFlag==1 && moveMode==255` would reliably indicate "in Move mode, cursor wherever."

**This fundamentally rewrites the fix plan:**
1. **DO NOT** use `battleMode` as a screen-state signal for in-battle submodes.
2. **DO** re-investigate `moveMode` — the comment saying it's deprecated was wrong.
3. **DO** explore what other addresses might distinguish: Abilities-targeting, Attack-targeting, Waiting, etc. We need ONE stable signal per screen that doesn't depend on cursor tile.
4. **All previous "mismatch" findings (#10, #13) and some "match" findings (#2, #9, #11, #12) are correctly keyed to cursor tile class — not screen. The detection code is measuring the wrong thing.**

**Next tests required to map the cursor-tile model completely:**
- In Battle_Attacking: move cursor to a tile with NO valid target (e.g., empty floor). Does battleMode stay 4 or change?
- In Battle_Casting (cast-time ability): does cursor over caster's tile give battleMode=2 (same as Move), or different?
- In Battle_Waiting (facing selection): cursor on each cardinal direction. Does battleMode stay 2 or vary?

---

## 15. Battle_Attacking (Wilham, cursor on INVALID/no-target tile) — IMPORTANT

**Ground truth:** Wilham in basic-attack mode, cursor moved to an empty tile (no enemy, outside red target tiles).

| input | value | vs #11 (Wilham, cursor on valid attack target) |
|---|---|---|
| **battleMode** | **1** | was **4** ⬅️ FLIPPED |
| moveMode | 255 | 255 |
| submenuFlag | 1 | 1 |
| menuCursor | 1 | 1 |
| battleActed | 1 | 1 |
| battleMoved | 1 | 1 |

**Detected:** `Battle_Casting` — ❌ should be `Battle_Attacking`

**🎯 FURTHER CONFIRMATION — battleMode classifies cursor-tile, not screen:**

Same screen (basic attack targeting). Same unit. Only difference: cursor moved from a red-valid-target tile to an empty tile. `battleMode` flipped 4 → 1.

**Updated cursor-tile model:**
- `battleMode=4` = cursor on a VALID instant-attack target tile (red tile)
- `battleMode=1` = cursor on a tile that is NOT a valid target (outside highlights) — during any targeting mode
- `battleMode=2` = cursor on the unit's own tile / move-valid tile
- `battleMode=3` = no tile cursor (top-level menu)

So `battleMode=1` isn't "cast-time" OR "off-highlight" alone — it's "cursor over a tile that the current action CANNOT land on." When in basic-attack mode, an empty tile out-of-range gives you `1`. When in cast-time mode, *everything* gives you `1` (because cast-time abilities don't have the instant-target-highlight UX).

This also explains why in #10 (Haste, cursor on yellow target tile) battleMode was 4 not 1 — Haste's valid target tiles ARE highlighted (yellow), they just behave like "valid targets" for the battleMode classifier. Maybe. Or maybe the reading I got in #10 was at a specific cursor position that happened to overlap a tile classed differently.

**Unified theory now refining to:**
- battleMode = cursor-tile classification, across ALL battle submodes
- The "4 valid modes" we documented were just coincidences of "what tile was the cursor on at the moment of each sample"
- True screen signal must come from `moveMode`, `submenuFlag`, or a yet-undiscovered address

---

## 16. Battle_Casting Haste, cursor on CASTER'S own tile — NEW VALUE

**Ground truth:** Wilham casting Haste, cursor placed on his own tile (self-target).

| input | value | vs #10 (Haste, cursor on yellow target) | vs #14 (Move, cursor on own tile) |
|---|---|---|---|
| **battleMode** | **5** | was **4** | was **2** |
| moveMode | 255 | 255 | 255 |
| submenuFlag | 1 | 1 | 1 |
| menuCursor | 1 | 1 | 0 |

**Detected:** `Battle_Acting` — ❌ should be `Battle_Casting`

**NEW battleMode value: 5** — not seen before. And ScreenDetectionLogic doesn't handle 5, so it falls through to the default Battle_Acting rule (because `acted||moved` is 1).

**Cursor-tile model now has another class:**
- `battleMode=5` = cursor on "caster's own tile while cast-time targeting" (and possibly "self-targetable")
- Previously known: 0, 1, 2, 3, 4
- So battleMode is at least a 6-way classifier, not 4-way

Compare carefully:
- #14 (Move mode, cursor on unit's own tile): battleMode=**2**
- #16 (Haste cast-targeting, cursor on caster's own tile): battleMode=**5**

The "own tile" classification differs between Move and Cast submodes. So battleMode isn't purely cursor-over-tile — it's **(submode × cursor-tile-class)** as a combined classifier. That's different from the pure "cursor tile class" theory from #13-#15.

Refined theory:
- battleMode encodes BOTH the current submode AND some cursor property
- Each submode uses a different subset of values
  - Move mode: 2 on valid tiles, 1 on invalid tiles (maybe 0 off-map)
  - Attack targeting: 4 on valid targets, 1 off-targets
  - Cast targeting: 4 on valid yellow targets, 1 off-targets, 5 on self
- So battleMode DOES contain submode information, but it's entangled with cursor state

**Implication for detection:** The signal *is* there, but extracting "which submode am I in" requires aggregating over cursor positions or finding the split pattern. A bit more complex but doable.

**A cleaner fix path might be:**
1. Add `battleMode==5` handling (currently falls through) so Battle_Casting can fire on cast-time cursor-on-self
2. Accept that cursor-over-invalid-tile (`battleMode=1`) is ambiguous across all submodes, and default to "last known submode" via client-side tracking
3. Use `submenuFlag` + ability-being-executed (`_lastAbilityName`) for authoritative submode

---

## 17. Battle_Waiting facing NORTH ✓ MATCH

**Ground truth:** Wilham confirming Wait, facing-direction cursor on North.

| input | value |
|---|---|
| battleMode | 2 |
| menuCursor | 2 |
| submenuFlag | 1 |
| acted | 0 |
| moved | 0 |

**Detected:** `Battle_Waiting` — ✓ matches

**Notes:** Same as #12 (different unit but same Wait state). Both samples were facing some direction; if battleMode changes per facing we'll see it in subsequent samples.

**encA/encB now 1/1** — drifted from earlier 2/2 and 3/3 readings. Still tracking in lockstep (matched pair). Worth noting the encounter counter is not monotonic — may actually be "last-flee-counter" style tracking.

---

## 18. Battle_Waiting facing EAST ✓ MATCH

Identical to #17 except `encA/encB` 1→2 (background drift, not related to facing). **Facing direction is NOT visible in these 18 inputs.** The game tracks facing somewhere we're not reading.

---

## 19. Battle_Waiting facing WEST ✓ MATCH

Byte-for-byte identical to #18 (East). Three directions (N/E/W) sampled, all identical. **Facing direction confirmed invisible to screen detection** — confirmed already handled client-side via empirical rotation logic per memory/project_facing_rotation.md, so no action needed.

---

## 20. Battle_MyTurn, cursor HOVERING AutoBattle (top-level, cursor=4) — IMPORTANT

**Ground truth:** Top-level action menu, cursor moved DOWN to the AutoBattle slot (5th item). NO submenu opened. This is "Battle_MyTurn with AutoBattle highlighted."

| input | value |
|---|---|
| battleMode | 3 |
| moveMode | 0 |
| submenuFlag | **0** |
| menuCursor | **4** |
| acted | 0 |
| moved | 0 |

**Detected:** `Battle_MyTurn` — ✓ matches ground truth

**🔑 KEY CONFIRMATION:** Hover on AutoBattle at top-level is **submenuFlag=0 + menuCursor=4**. This is NOT the same as `Battle_AutoBattle` rule (which requires `submenuFlag=1`).

**So when does `Battle_AutoBattle` rule actually fire?** Only inside a submenu where menuCursor happens to be 4 — i.e., the buggy case from #8 where cursor=4 in the Abilities submenu means "5th skillset" but detection says "AutoBattle."

**Conclusion:** The `Battle_AutoBattle` detection rule is broken — it literally cannot fire on the real AutoBattle hover (which is submenuFlag=0), and only fires spuriously inside submenus. Either:
1. Delete the rule entirely — hovering AutoBattle is still just Battle_MyTurn, and the DetectedScreen.UI label already maps cursor=4 → "AutoBattle" for the MyTurn case
2. Rewrite it as `submenuFlag==0 && menuCursor==4 && battleMode==3`

**Option 1 (delete) is cleaner** — the "hovering AutoBattle" state already gets rendered correctly through the UI label. There's no user-facing need for a separate `Battle_AutoBattle` screen name.

**This directly fixes the "Auto-Battle instead of Wait" bug** from handoff #1. The buggy sequence was:
1. User moves, then opens Abilities submenu
2. Cursor is at position 4 inside the submenu (5th skillset)
3. Detection returns `Battle_AutoBattle` (wrong!)
4. Claude's handler for Battle_AutoBattle navigates to exit AutoBattle, lands Wait in the wrong spot

Deleting the Battle_AutoBattle rule makes this case return `Battle_Abilities` correctly, which has the right handler.

---

## 21. Battle_Paused, cursor on Units ✓ MATCH

**Ground truth:** Pause menu open, cursor on "Units" option.

| input | value |
|---|---|
| battleMode | **0** |
| paused | **1** |
| submenuFlag | 0 |
| menuCursor | 4 |
| acted | 0 |
| moved | 0 |
| encA/encB | 5/5 (drifted) |

**Detected:** `Battle_Paused` — ✓ matches

**Notes:**
- First sample with `battleMode=0` during in-battle state. Pause seems to SUSPEND the cursor-tile classification entirely, leaving battleMode at 0.
- `menuCursor=4` here is the Pause-menu cursor (indexing into pause-menu items), NOT action menu or submenu. Another context overload — but pause is disambiguated by `paused==1`, so rules are safe.
- `moveMode=0` (not 255) — confirms moveMode tracks "is cursor active on battlefield," and pause kills it.

**Useful:** `paused==1` is a strong, unambiguous screen-state signal. All pause-menu detection rules can safely key on it first, then disambiguate submenus by menuCursor.

---

## 22. Battle_Status (clicked INTO Status screen) ✓ MATCH — but surprises

**Ground truth:** Pause → Status, unit's stats sheet visible.

| input | value | surprises |
|---|---|---|
| **party** | **1** | ⚠️ was 0 at Battle_Paused |
| paused | 1 | same |
| battleMode | 3 | was 0 at Battle_Paused — changed! |
| moveMode | 0 | same |
| submenuFlag | 1 | was 0 at Battle_Paused — changed! |
| gameOverFlag | 1 | aliased with submenuFlag |
| menuCursor | 3 | was 4 at Battle_Paused |
| battleMoved | 1 | was 0! (submenu entry side-effect after all?) |
| encA/encB | 10/10 | drifted +5 from 5/5 |

**Detected:** `Battle_Status` — ✓ matches

**Major surprises:**

1. **`party=1`** — in-battle, but party flag flipped to 1 (normally only on WorldMap/PartyMenu). The Status sheet borrows the party-menu rendering system. This means `PartyMenu` detection could misfire here without in-battle disambiguation (currently the rule checks `party==1 && ui==0` but also requires falling through all in-battle rules first — safe by rule ordering).

2. **`battleMode` went 0 → 3** when entering Status. Pause set it to 0 (no cursor); entering Status subscreen set it back to 3 (top-level-ish). Confirms battleMode is influenced by UI subscreen, not just cursor.

3. **`battleMoved` 0 → 1** when entering Status submenu. **PARTIAL VALIDATION of my earlier hypothesis #5!** Submenu entry DOES sometimes flip battleMoved to 1 — specifically when that submenu is a pause subscreen. Not all submenus (sampler log showed it stayed 0 for normal Abilities entry), so the rule is narrower than "any submenu."

    Possible refined theory: `battleMoved` mirrors "did we go into a screen where the active unit is considered 'occupied' (can't be interacted with)?" Pause → Status might mark the unit as "viewing," hence moved. Needs more data.

4. **`encA/encB` jumped 5→10** since #21. These counters are definitely ticking even while paused.

**Implications:**
- Battle_Status rule `inBattle && paused==1 && menuCursor==3` works — but relies on rule ordering (Battle_Status checked before generic Battle_Paused).
- The `party==1` flip is benign here only because the in-battle guard catches it first. If in-battle ever returned false during Status, this would falsely go to PartyMenu.

---

## 23. Battle_EnemiesTurn ✓ MATCH (time-sensitive capture)

**Ground truth:** Enemy's turn — enemy AI acting.

| input | value |
|---|---|
| battleTeam | **1** (enemy) |
| battleMode | 5 |
| moveMode | 255 |
| submenuFlag | 1 |
| paused | 0 |
| acted/moved | 0/0 |
| party | **1** ⚠️ |

**Detected:** `Battle_EnemiesTurn` — ✓ matches

**Notes:**
- `battleTeam==1` is the decisive discriminator; rule is `inBattle && battleTeam==1 && acted==0 && moved==0` — clean.
- `battleMode=5` during enemy turn — same value seen in #16 (Haste cast-targeting on self). The enemy was probably also targeting a self-class ability at this moment. Further confirms battleMode encodes cursor/target-tile classification.
- `party==1` here — yet another spurious flip of the party flag during in-battle. In-battle rule ordering protects the detection.
- Snapshot caught during action; `encA/encB` drifted back to 3/3 after being 10/10 in #22. Counter truly is not monotonic.

---

## 24. WorldMap ❌ MISMATCH — huge bug

**Ground truth:** Player is on the World Map (no battle, not in any menu).

| input | value | red flag |
|---|---|---|
| **party** | **1** | ⚠️ should be 0 for WorldMap rule to fire |
| ui | 0 | |
| rawLocation | **255** | ⚠️ should be 0-42 for a world-map location |
| **slot0** | **0x000000FF** | ⚠️ = 255 = sentinel for "in battle" per unitSlotsPopulated check |
| slot9 | 0xFFFFFFFF | = sentinel for "in battle" |
| battleMode | 0 | |
| **moveMode** | **13** | new value! was 0 or 255 in every other sample |
| encA/encB | 255/255 | max-value sentinel |
| eventId | 65535 | 0xFFFF = uninitialized sentinel |

**Detected:** `Battle_MyTurn` — ❌ should be `WorldMap`

**🚨 CRITICAL — in-battle detection is STUCK AS ON:**

Rule: `unitSlotsPopulated = slot0 == 255 && slot9 == 0xFFFFFFFF` → TRUE here. `clearlyOnWorldMap = rawValidLocation && party == 0 && battleMode == 0` → requires rawLocation in 0-42 AND party==0, BOTH fail here. So `inBattle=true` → rule ordering drops us into Battle_MyTurn.

**Why rawLocation=255 on the world map?** Maybe this is a fresh-load state, before the world-map location scanner populated the real location. OR the world map just doesn't use the standard location slots (0-42) and uses 255 as "on world map, position tracked elsewhere via hover."

**Why slot0/slot9 still show battle sentinels?** Classic stale memory. After a battle ends, the slots weren't reset. The code comment at ScreenDetectionLogic.cs:26-27 acknowledges this: "Unit slots (0xFF) persist after leaving battle, so we need clearlyOnWorldMap to override." But `clearlyOnWorldMap` requires `rawValidLocation`, which we don't have here.

**So the bug is:** `clearlyOnWorldMap` needs a way to recognize "we're on the world map but location is not in the valid range yet." Candidates for a clean world-map signal:
- `moveMode=13` — this NEW value appeared only here. Might be the "world map cursor moving" state signal.
- `eventId=65535` (0xFFFF) — the "uninitialized" sentinel. If we're out of battle AND eventId is 0xFFFF, we're probably on the world map / title.
- `encA=255` — another sentinel that might specifically mean "at rest on world map."

**Proposed fix:** Add a "world map override" — if `party==1 && battleMode==0 && ui==0 && moveMode != 0`, force `clearlyOnWorldMap=true`. Or specifically: `moveMode==13` probably IS the world-map cursor signal.

**This explains a class of real bugs:** any time the player returns to the world map after a battle, detection may lag until the slots get cleared. The stale state described in feedback_flee_stale_state.md might be exactly this problem.

---

## 25. WorldMap with a location HOVERED ❌ STILL MISMATCH — persistent, not transient

**Ground truth:** Player hovering a location (cursor over a named place on the world map).

| input | value | vs #24 |
|---|---|---|
| party | 1 | same |
| rawLocation | 255 | same (still not a valid 0-42) |
| slot0/slot9 | 0xFF/0xFFFFFFFF | same (stale) |
| moveMode | 13 | same |
| battleMode | 0 | same |
| encA/encB | 8/8 | drifted 255→8 |
| eventId | 65535 | same |

**Identical to #24 on all detection-relevant inputs.** Only the background counter drifted.

**Detected:** `Battle_MyTurn` — still ❌

**This is NOT a transient stale state. It's persistent.** The stale battle sentinels + `rawLocation=255` are the **actual world-map state** in this build — not a leftover. Detection is permanently wrong on WorldMap until we fix the rule.

**Solid signal set for a clean WorldMap rule:**
- `party==1` (not 0 as the current rule expects)
- `ui==0`
- `battleMode==0`
- `moveMode==13`  ← key discriminator
- `eventId==0xFFFF` (uninitialized sentinel)
- `paused==0`
- `submenuFlag==0`

**Proposed rule rewrite:**
```csharp
// World map: party flag set (UI is world-map mode), no menu, moveMode=13 is the
// world-map cursor signal. rawLocation=255 is normal here — location is tracked
// via hover, not the location slot.
if (party == 1 && ui == 0 && battleMode == 0 && moveMode == 13
    && paused == 0 && submenuFlag == 0)
    return "WorldMap";
```

This rule must be checked BEFORE `inBattle` to override the stale battle sentinels. Current code has the order reversed.

**But wait — the current rules have:**
```
if (party == 0 && ui == 0) return "WorldMap";
```

**`party==0` is wrong.** It should be `party==1`. This might be a recently-regressed bug, OR the party flag meaning inverted in IC Remaster. Either way, the current rule can never fire on the real world map.

**Verification needed:** check `git log` on ScreenDetectionLogic to see if the `party==0` rule was ever correct.

**NOTE — investigation still incomplete:** These dumps are from a world-map state reached by fleeing/returning from a battle without a clean victory screen. The stale slot0/slot9 sentinels might be specific to THIS entry path. We should verify by:
1. Saving + loading to reach a clean world-map state
2. Dumping again from that clean entry

If clean world map has different inputs (e.g., slot0=0 and party=0 like the existing rule expects), then the current rule IS correct for normal cases, and this is a stale-state-after-flee problem — which matches `feedback_flee_stale_state.md` perfectly.

---

## 26. WorldMap after SAVE + LOAD ❌ STILL MISMATCH

**Ground truth:** Saved game, loaded, world map hovering a location.

| input | value | vs #25 (before save/load) |
|---|---|---|
| **party** | **0** | was 1 ⬅️ FLIPPED |
| **ui** | **1** | was 0 ⬅️ FLIPPED |
| slot0/slot9 | 0xFF/0xFFFFFFFF | still stale ⚠️ |
| rawLocation | 255 | same |
| moveMode | 13 | same |
| battleMode | 0 | same |
| eventId | 65535 | same |

**Detected:** `Battle_MyTurn` — still ❌

**Key observation:** Save/load DID change `party` (1→0) and `ui` (0→1), but did NOT clear `slot0/slot9`. Those battle sentinels are process-lifetime state, not save-state — require a full game restart to reset.

Note: `party=0, ui=1` matches the TravelList rule, not WorldMap rule. Hovering a location on the world map may mean the travel submenu is already up — ambiguous, need fresh-restart reading.

**Pending next:** Full process restart dump will confirm whether slot0/slot9 reset and WorldMap detection fires correctly. That data point will determine scope of the fix.

---

## 27. TitleScreen (after full restart, before loading save) ✓ MATCH — and IMPORTANT baseline

**Ground truth:** Game relaunched, title screen visible, no save loaded yet.

| input | value | observations |
|---|---|---|
| slot0 | **0xFFFFFFFF** | ⭐ DIFFERENT from in-game 0xFF sentinel — proves slot0 really does reset |
| slot9 | 0xFFFFFFFF | same pre/post battle |
| battleMode | 255 | uninitialized sentinel |
| moveMode | 255 | same as battle, but also title |
| battleActed | 255 | uninitialized |
| battleMoved | 255 | uninitialized |
| battleTeam | 2 | uninitialized (!= 0, 1) |
| rawLocation | 255 | uninitialized |
| eventId | 65535 | uninitialized |
| party | 0 | |
| ui | 0 | |
| paused | 0 | |
| submenuFlag | 0 | |
| menuCursor | 0 | |

**Detected:** `TitleScreen` — ✓ matches (caught by `rawLocation==255 && eventId != 0xFFFF` check failing, then falling to the generic rawLocation==255 rule).

**🔑 KEY FINDINGS:**

1. **Fresh process has `slot0 == 0xFFFFFFFF`** (four-byte all-ones), distinct from in-battle sentinel `slot0 == 0x000000FF` (one byte = 255). Previous world-map readings (#24-26) showed `0x000000FF` — these are STALE battle residue, not world-map natural state.

2. **Multiple fields use 255 as "uninitialized":** battleMode, moveMode, battleActed, battleMoved, rawLocation. This means `unitSlotsPopulated = slot0 == 255 && slot9 == 0xFFFFFFFF` can falsely match any state where slot0 happens to be 0x000000FF (the 1-byte read of which is 255). Fragile.

3. **slot0 interpretation matters:** Reading it as 4 bytes (how it's declared in ScreenAddresses), you get:
   - Fresh: `0xFFFFFFFF` (uninitialized memory, all ones)
   - In battle: `0x000000FF` (low byte = 255, upper = 0 — battle sentinel)
   - Post-battle world map: `0x000000FF` (stuck stale)
   - The check `slot0 == 255` matches BOTH `0x000000FF` and NOT `0xFFFFFFFF`, which is actually correct. Good.

**For the WorldMap fix:** Once we reach the real world map post-restart, we'll see what slot0 is. Prediction based on this: a clean world map post-restart has `slot0 == 0xFFFFFFFF` (never battled this session) OR `slot0 == 0x000000FF` (at least one battle happened) OR `slot0 == 0` (cleaned up properly after a clean victory). If post-restart-never-battled WorldMap gives `party=0, ui=0, slot0=0xFFFFFFFF`, then `unitSlotsPopulated` becomes false, `clearlyOnWorldMap` fails on `rawValidLocation`, and we fall through to the existing `party==0 && ui==0 → WorldMap` rule correctly.

Keep proceeding: load save → world map → dump. And when you hit Battle_Encounter on the way: shout and we'll capture it too.

---

## 28. WorldMap post-restart+load ❌ STILL MISMATCH — but clean-state baseline captured

**Ground truth:** Full restart + save load, now on world map.

| input | value | vs #27 (TitleScreen) |
|---|---|---|
| party | 0 | same |
| **ui** | **1** | was 0 ⬅️ FLIPPED (save loaded) |
| slot0 | **0xFFFFFFFF** | ⭐ STAYED FRESH — no battle entered yet |
| slot9 | 0xFFFFFFFF | same |
| battleMode | 255 | uninit |
| moveMode | 255 | uninit |
| rawLocation | 255 | uninit |
| eventId | 65535 | uninit |
| other battle flags | 255 | uninit |

**Detected:** `TitleScreen` — ❌ should be `WorldMap`

**🎯 CRITICAL — this is the CLEANEST world-map state possible, and detection STILL fails:**

With slot0=`0xFFFFFFFF` (NOT 0x000000FF), the `unitSlotsPopulated` check correctly returns FALSE. So `inBattle` should be false. But detection still returned `TitleScreen`, not `WorldMap`.

**Why TitleScreen?** The rule: `if (rawLocation == 255 || rawLocation < 0) return "TitleScreen"`. rawLocation IS 255 → TitleScreen fires before the party==0 && ui==0 WorldMap check.

**But the user says it's the WorldMap.** So **rawLocation=255 is ALSO how the world map represents "position"** — not just title screen. The location is tracked via `hover` (which we're not reading in the detection inputs), not `rawLocation`.

**The fundamental issue:**
- `rawLocation == 255` is ambiguous: means both "title screen" AND "on world map, position tracked via hover"
- `eventId == 0xFFFF` is ambiguous: means both "uninitialized at title" AND "no cutscene running"
- Detection treats these as TitleScreen, but they're actually the world-map defaults too

**Disambiguating signals available now:**
- `ui == 1` (title has ui==0, world map has ui==1) — strong discriminator in this dump
- `moveMode == 13` vs 255 (this dump has 255; earlier #24-26 had 13 — what changed?)

Wait — `moveMode=255` here but was `13` in #24-26. Same ground truth (world map with cursor hover). The difference: this was *immediately* after load, #24-26 were *after playing* for a while. `moveMode=13` may only set when the player has moved the cursor once; before that it stays at the 255 uninit. Worth another sample after moving the cursor.

**So the REAL fix for WorldMap detection:**
```csharp
// World map: rawLocation=255 (position tracked via hover), ui=1 (world-map cursor mode),
// party=0 (not in party menu), not in a battle/cutscene/pause.
if (party == 0 && ui == 1 && !inBattle && !cutscene && paused == 0
    && submenuFlag == 0 && battleMode == 0)
    return "WorldMap";
```

**But there's still ambiguity with TravelList** — existing rule is `party==0 && ui==1 → TravelList`. And we saw `ui=1` here where ground truth is WorldMap, not TravelList. Are these actually the same screen in memory? The game distinguishes them via which widget has focus, possibly readable via another address.

**Next test:** dump once on pure world map (nothing hovered) vs with Travel menu opened explicitly. If they're byte-identical, we may need `hover` value ranges to disambiguate.

---

## 29. WorldMap after moving cursor (post-restart+load) ❌ STILL MISMATCH

**Ground truth:** Same fresh world map, cursor was moved with arrow keys (hovering a different location than start).

| input | value | vs #28 (no cursor movement) |
|---|---|---|
| All 18 inputs | **IDENTICAL** | no change |

**Hypothesis busted:** moveMode stayed at 255, did NOT flip to 13. So moveMode=13 is NOT triggered by moving the world-map cursor.

**Where DID moveMode=13 come from in #24-26?**

Reviewing: #24-26 were post-battle stale state (after flee, no clean victory). So `moveMode=13` may be:
- Post-battle residue from something that set it to 13 during battle (not 255)
- Or a world-map state that only activates after a battle has been completed once this process lifetime
- Or tied to which save slot / progression state is loaded

**Cannot trust moveMode=13 as a WorldMap signal.** It appears conditionally.

**Disambiguator still needed.** With all 18 inputs IDENTICAL between "fresh-load world map, cursor hovering a location, having moved the cursor" (this sample #29) AND "hovering different locations via arrow keys" (same sample by design), **the current detection inputs cannot distinguish WorldMap from TitleScreen in this fresh-load state.**

The game must track "am I on the world map" somewhere. Candidates to investigate:
- **The `hover` field at 0x140787A22** — we read it in DetectScreen but don't pass it to ScreenDetectionLogic. On world map it should be 0-42 (valid location ID), on title it should be 0 or 255. **This is probably the missing signal.**
- A scene/mode flag we haven't mapped
- The camera/rendering module state

**FIX PROPOSAL:** Add `hover` to ScreenDetectionLogic inputs and use `hover` being 0-42 as the world-map discriminator:

```csharp
// World map: ui==1, party==0, no menu, no battle, hover is a valid world-map location (0-42).
// rawLocation == 255 is the world-map default; hover is the authoritative position.
bool hoverIsValidLocation = hover >= 0 && hover <= 42;
if (party == 0 && ui == 1 && hoverIsValidLocation && !inBattle
    && paused == 0 && submenuFlag == 0 && battleMode == 0)
    return "WorldMap";
// (TravelList would be the same but with submenuFlag==1 or some other menu-open signal)
```

Needs verification — does hover on the title screen stay at 0/255/uninit? If so, hover in 0-42 is clean "world map" signal.

**Verified hover in this sample: hover=255.** So hover is NOT the discriminator on fresh-load world map either. 😑

**Summary so far on WorldMap detection:**

Two distinct WorldMap memory states observed:

| State | slot0 | moveMode | all else |
|---|---|---|---|
| #24-26 Post-battle (stale in-battle flags, never cleaned) | 0x000000FF | 13 | various |
| #28-29 Post-restart+load | 0xFFFFFFFF | 255 | identical to TitleScreen in 18 inputs |

**Detection can never work with just these 18 inputs on post-restart world map.** We need to read MORE addresses. Candidates to investigate next:
- `hover` at 0x140787A22 showed 255 here but might hold something else
- Location variables we haven't mapped
- A scene/stage ID somewhere in the game struct

Until we find a reliable WorldMap-only signal, detection will continue to return TitleScreen for fresh-load world maps.

**Practical workaround:** In-mod, after the WaitForCondition settles on TitleScreen but we know we issued a "load save" command, treat the result as WorldMap. But this is flaky.

**Proper fix:** memory scan for a difference between TitleScreen and WorldMap and find the real signal. Save as separate follow-up investigation.

---

## 30. TravelList open (post-restart) ❌ MISMATCH, INDISTINGUISHABLE from WorldMap

**Ground truth:** Travel list menu open on world map.

| input | value | vs #29 (WorldMap, no menu) |
|---|---|---|
| All 18 inputs | **BYTE-IDENTICAL** | no change at all (except encA/encB drift) |

**Detected:** `TitleScreen` — ❌ should be `TravelList`

**💥 WorldMap, TravelList, and TitleScreen are ALL byte-identical in the 18 inputs after a fresh restart+load.**

Three distinct screens → same memory state (per our reads). The game must track the Travel-list submenu state somewhere else (menu-open flag, focused-widget ID, a UI stack).

**Summary of broken detection in post-restart state:**

| Ground truth | Detection says | Why broken |
|---|---|---|
| WorldMap (no menu) | TitleScreen | Can't distinguish from title |
| TravelList (menu open) | TitleScreen | Can't distinguish from WorldMap or title |
| EncounterDialog | TitleScreen (likely) | Probably same |

**This explains a LOT of the world-side navigation bugs.** The mod has been running blind on fresh-restart sessions, and only becomes partially correct once a battle happens and state changes.

**Action items (new):**
1. Memory-scan for differences between fresh-load WorldMap vs TitleScreen
2. Memory-scan for differences between WorldMap vs TravelList  
3. Memory-scan for differences between WorldMap vs EncounterDialog
4. Each found address becomes a new input to ScreenDetectionLogic

Until those scans, post-restart world navigation detection is fundamentally broken.

---

## 31. PartyMenu ❌ MISMATCH — but real signal found!

**Ground truth:** Party menu open (from world map).

| input | value | vs #30 (TravelList) |
|---|---|---|
| **party** | **1** | was 0 ⬅️ FLIPPED |
| ui | 1 | same |
| all else | same | |

**Detected:** `TitleScreen` — ❌ should be `PartyMenu`

**🎯 PartyMenu has a real, unambiguous discriminator: `party==1`.**

So `party` flag IS meaningful — it reliably distinguishes:
- `party=0, ui=1` → WorldMap / TravelList / EncounterDialog (still ambiguous among these 3)
- `party=1, ui=1` → PartyMenu
- (need more data for other party values)

**Why still detected as TitleScreen?** Rule ordering. The `rawLocation==255 → TitleScreen` rule fires before any `party` check. This is the real cause of broken world-side detection — `rawLocation==255` catches ALL the world-side states because FFT:IC represents "no in-engine location slot active" as 255 for all of them.

**THE FIX (for several broken world-side rules):**

Current order:
```
1. inBattle checks
2. rawLocation == 255 && eventId>0 → Cutscene
3. rawLocation == 255 → TitleScreen  ⬅️ catches everything else
4. encA != encB → EncounterDialog
5. isPartySubScreen → PartySubScreen
6. party == 1 → PartyMenu   ⬅️ unreachable for us
7. party == 0 && ui == 1 → TravelList   ⬅️ unreachable
8. party == 0 && ui == 0 → WorldMap   ⬅️ unreachable
```

Move TitleScreen check to AFTER all the party/ui checks, OR tighten its guard to require additional "truly uninitialized" signals (slot0=0xFFFFFFFF AND eventId=0xFFFF AND battleMode=255):
```csharp
// TitleScreen: all battle/world-map state is uninitialized (fresh process, no save loaded).
if (rawLocation == 255 && slot0 == 0xFFFFFFFF && battleMode == 255
    && eventId == 0xFFFF && ui == 0)
    return "TitleScreen";
```

With this tighter guard, #27 (actual title) still matches (ui=0), but #28/#29/#30/#31 (loaded saves) would correctly fall through to party/ui rules.

**Impact of this change (predicted):**
- #28, #29 (WorldMap): `party=0, ui=1` → matches TravelList rule — WRONG. We still need to distinguish WorldMap from TravelList.
- #30 (TravelList): `party=0, ui=1` → matches TravelList rule — CORRECT.
- #31 (PartyMenu): `party=1` → matches PartyMenu rule — CORRECT.

So the tightened-TitleScreen fix alone would correctly identify PartyMenu and TravelList, but WorldMap would still be misidentified as TravelList. Better than current state (everything → TitleScreen), but not complete.

**To fully disambiguate WorldMap vs TravelList**, we still need to find a signal that flips when the travel submenu is open. Likely candidates from existing addresses:
- a submenu depth counter
- a focused widget ID
- the `hover` field (travel list may show hover=255 always, while world map hover tracks the highlighted location — worth re-checking WITH a known-hovered location)

**Plan:** capture a WorldMap dump where user is hovering a known location (e.g., Orbonne Monastery) AFTER the restart+load state. Check whether `hover` becomes 0-42 on the world map but stays 255 in the travel list.

---

## 32. Battle_Encounter (random encounter dialog) ❌ MISMATCH — but real discriminator present

**Ground truth:** Random-encounter dialog popped up on world-map travel ("You've been ambushed" or similar).

| input | value | 🔑 |
|---|---|---|
| party | 0 | |
| ui | **0** | was 1 in TravelList — FLIPPED |
| **encA** | **11** | ⭐ different from encB |
| **encB** | **3** | ⭐ |
| slot0/slot9 | 0xFFFFFFFF | fresh, no battle yet |
| all else | 255/uninit | same |

**Detected:** `TitleScreen` — ❌ should be `EncounterDialog`

**🎯 `encA != encB` IS the real discriminator** — this is what the existing rule expects. It fires correctly in theory. But again, rule ordering: `rawLocation==255 → TitleScreen` catches this first.

**Two things confirmed here:**
1. **EncounterDialog has a real unambiguous signal:** `encA != encB` (all other world-side states have `encA == encB`).
2. **ui flipped from 1 (TravelList) to 0 (Encounter)** — could be used as additional disambiguation but encA/encB is cleaner.

**With the proposed TitleScreen guard tightening, this state would:**
- Fail the new TitleScreen guard (`slot0==0xFFFFFFFF` passes but `ui==0` is the title requirement too — passes also). Hmm, this encounter dialog has `ui==0, slot0=0xFFFFFFFF, battleMode=255, eventId=0xFFFF, rawLocation=255` — ALL the "title sentinels" match!

**So my proposed tightening isn't strict enough for this case.** Need a different tiebreaker.

**Better rule:** Check encA/encB difference BEFORE TitleScreen:
```csharp
// EncounterDialog: distinctive pattern — encA != encB, nothing else in battle.
if (rawLocation == 255 && encA != encB && !inBattle)
    return "EncounterDialog";

// TitleScreen: all truly uninitialized AND no encounter pattern.
if (rawLocation == 255 && slot0 == 0xFFFFFFFF && battleMode == 255
    && eventId == 0xFFFF && ui == 0 && encA == encB)
    return "TitleScreen";
```

This correctly catches the EncounterDialog even when other signals look title-like.

**Updated findings summary:**

| State | Real discriminator (found) | Currently detected as |
|---|---|---|
| TitleScreen | ui=0, slot0=0xFFFFFFFF, battleMode=255, eventId=0xFFFF, encA==encB | ✓ TitleScreen |
| WorldMap (post-load) | ??? (indistinguishable from TravelList in 18 inputs) | ❌ TitleScreen |
| TravelList (post-load) | ??? (indistinguishable from WorldMap) | ❌ TitleScreen |
| PartyMenu | party==1 | ❌ TitleScreen |
| EncounterDialog | encA != encB | ❌ TitleScreen |

---

## 33. Battle_Formation (pre-battle unit placement) ✓ MATCH

**Ground truth:** Pre-battle formation screen (placing units on blue tiles before battle starts).

| input | value |
|---|---|
| party | 0 |
| ui | 1 |
| slot0 | **0xFFFFFFFF** |
| slot9 | **0xFFFFFFFF** |
| battleMode | 1 |
| moveMode | 255 |
| encA/encB | 3/3 |
| battleTeam | 2 (uninit) |
| battleActed/Moved | 255/255 (uninit) |
| eventId | 65535 |

**Detected:** `Battle_Formation` — ✓ matches

**Rule that fires:** `inBattle && slot0 == 0xFFFFFFFF && battleMode == 1 → Battle_Formation`.

**Notes:**
- `slot0 == 0xFFFFFFFF` (not 0x000000FF) during formation — units aren't placed yet.
- `battleMode == 1` here is NOT "cast-time casting" but "formation placement." Another context overload for battleMode, but rule is safe because formation uniquely has `slot0 == 0xFFFFFFFF`.
- `inBattle` evaluates true because the rule includes `slot9 == 0xFFFFFFFF && battleMode == 1`. OK.
- This is one of the rare samples where the existing rule works correctly — kudos to whoever designed this discriminator. The `slot0 == 0xFFFFFFFF && battleMode == 1` combo is specific enough.

**Comparison:** This is the ONLY "post-restart" world-side state so far that gets correctly detected. Why? Because the rule for Battle_Formation checks `slot0 == 0xFFFFFFFF` — and we have that for truly clean post-restart states. The WorldMap/TravelList rules don't, but could.

**Implication for the WorldMap/TravelList fix:** Since fresh-load WorldMap also has `slot0 == 0xFFFFFFFF`, we could try combining that with other signals. But Battle_Formation also matches → collision risk. The `battleMode` value is what distinguishes them:
- Battle_Formation: `battleMode == 1`
- Fresh WorldMap/TravelList: `battleMode == 255` (uninit)

So `battleMode == 255 && slot0 == 0xFFFFFFFF && !paused && rawLocation == 255` narrows to "not in battle, not in menu, freshly loaded." Combined with `party` and `ui`:
- `party=0, ui=1, battleMode=255, encA==encB` → WorldMap or TravelList (still ambiguous)
- `party=1, ui=1, battleMode=255` → PartyMenu
- `party=0, ui=1, encA != encB` → EncounterDialog
- `party=0, ui=0, battleMode=255, eventId=65535` → TitleScreen

---

## 34. GameOver ✓ MATCH

**Ground truth:** Game over screen (all party units defeated).

| input | value |
|---|---|
| party | 0 |
| ui | 1 |
| slot0/slot9 | 0x000000FF / 0xFFFFFFFF (in-battle residue) |
| **paused** | **1** |
| **gameOverFlag** | **1** |
| battleMode | 0 |
| moveMode | 255 |
| **battleTeam** | **1** (enemy — they won) |
| battleActed | 0 |
| battleMoved | 0 |
| submenuFlag | 1 (= gameOverFlag, aliased) |
| menuCursor | 2 |
| eventId | 400 |

**Detected:** `GameOver` — ✓ matches

**Rule fires:** `inBattle && paused==1 && battleMode==0 && gameOverFlag==1 && battleActed==0 && battleMoved==0 → GameOver`.

**Notes:**
- Clean signature. `paused=1 && gameOverFlag=1 && acted/moved=0` is distinctive.
- `battleTeam=1` here is meaningful — the ENEMY team was acting when the player lost (final killing blow). Not a detection input but informative.
- `eventId=400` (nameId territory — 400 is in the 200-500 range for named units). Probably nameId of the unit that dealt the killing blow or is considered "the winner." Again not a detection input.
- Confirms `paused + gameOverFlag + acted=0` is a safe combo to distinguish from Battle_Desertion (which has acted=1 or moved=1).

---

## 35. LoadGame screen ❌ MISMATCH — new state not in detection logic

**Ground truth:** Load-game screen (accessed from GameOver menu — choosing to load a save rather than retry).

| input | value | vs #34 GameOver |
|---|---|---|
| **paused** | **0** | was 1 ⬅️ FLIPPED |
| **battleTeam** | **1** | same |
| slot0/slot9 | 0x000000FF / 0xFFFFFFFF | same stale |
| battleMode | 0 | same |
| moveMode | 255 | same |
| gameOverFlag | 1 | same |
| submenuFlag | 1 | same |
| acted/moved | 0/0 | same |
| menuCursor | 2 | same |
| eventId | 400 | same |
| ui | 1 | same |

**Detected:** `Battle_EnemiesTurn` — ❌ should be `LoadGame` (or some variant)

**🚨 THERE IS NO `LoadGame` SCREEN RULE at all in ScreenDetectionLogic.** This entire screen is undefined in detection.

**Why Battle_EnemiesTurn?** 
- `inBattle` = true (unitSlotsPopulated via slot0=0xFF + slot9=0xFFFFFFFF)
- Falls through pause/status/moving/waiting/attacking/abilities/myturn/alliesturn
- Lands at: `inBattle && battleTeam==1 && acted==0 && moved==0 → Battle_EnemiesTurn`

**Signals for proposed LoadGame rule:**
- `gameOverFlag == 1` (set before we got here, via GameOver)
- `paused == 0` (distinguishes from GameOver which has paused=1)
- `battleMode == 0`
- `battleTeam == 1` (stale from killing blow)
- `eventId == 400` (stale from GameOver)

Proposed:
```csharp
// Load Game menu reached from GameOver — similar memory layout but paused=0.
if (inBattle && paused == 0 && gameOverFlag == 1 && battleMode == 0
    && battleActed == 0 && battleMoved == 0)
    return "LoadGame";
```

Must be placed BEFORE the EnemiesTurn rule to preempt it.

**Broader concern:** How many OTHER screens like this exist — reachable in gameplay but undefined in detection? Title menu's Continue/New Game options, save dialogs, shop menus, maybe others. The audit revealed these gaps.

---

## 36. Outfitters (shop menu at a location) ❌ MISMATCH — but HUGE clue for WorldMap

**Ground truth:** At the Outfitters shop (one of the village shop menus).

| input | value | 🔑 |
|---|---|---|
| party | 0 | |
| ui | 1 | |
| **rawLocation** | **9** | ⭐ FIRST VALID LOCATION VALUE (0-42) IN ENTIRE AUDIT |
| slot0/slot9 | 0x000000FF / 0xFFFFFFFF | stale in-battle |
| battleMode | 0 | |
| moveMode | 255 | |
| **gameOverFlag** | **1** | stale from earlier GameOver |
| battleTeam | 1 | stale from GameOver |
| acted/moved | 0/0 | |
| eventId | 65535 | cleared from GameOver |
| submenuFlag | 1 | |
| menuCursor | 2 | |
| encA/encB | 8/8 | |

**Detected:** `TravelList` — ❌ should be `Outfitters`/`Shop` (no rule exists for this)

**🎯 MAJOR DISCOVERY — rawLocation DOES populate in some states:**

Previous world-side samples (WorldMap/TravelList/Encounter/PartyMenu) all had `rawLocation=255`. Now at a shop: `rawLocation=9` (Warjilis? or another town). This proves:
1. `rawLocation` IS meaningful — it's 0-42 when you're AT a location (entered village), 255 when on the open world map.
2. The `_lastWorldMapLocation` fallback in DetectScreen.cs exists because `rawLocation=255` was assumed to mean "in battle, use cached." But it actually means "on world map cursor," and `rawLocation=0-42` means "entered a town/shop."

**This fixes world detection in a big way:**
- WorldMap (cursor mode): `rawLocation=255`
- AT a village/shop: `rawLocation=0-42` — actually a valid location
- TravelList: `rawLocation=255` (same as WorldMap — menu overlay doesn't change it)
- The `clearlyOnWorldMap = rawValidLocation && party==0 && battleMode==0` guard in ScreenDetectionLogic expects `rawValidLocation`, which is only true at a shop — NOT on the open world map cursor.

**So `clearlyOnWorldMap` as currently named is misleading — it's "in a town/shop", not "on world map."**

**Why detected as TravelList?** `party=0, ui=1 → TravelList` fires because rawLocation=9 ≠ 255 makes `clearlyOnWorldMap = true`, which sets `inBattle = false`. Then falls through menu/battle rules. Hits `party==0 && ui==1 → TravelList`. Needs a specific rule for shop menus (and each sub-location type).

**Summary of NEW model for rawLocation:**
- `0-42`: Player is AT a specific location (village/shop/etc.) — `rawLocation` is the location ID
- `255`: Player is on the open world map, or in a screen that doesn't correspond to any location

**Summary of NEW model for clearlyOnWorldMap:**
- The name is wrong — should be `atNamedLocation`
- True when player is at a specific location (shop, town)
- This is NOT the same as "on the world map"

**New detection tree draft:**
```csharp
// At a named location (shops, villages): rawLocation is 0-42
if (rawLocation >= 0 && rawLocation <= 42 && party == 0 && ui == 1
    && battleMode == 0 && !inBattle)
    return "LocationMenu"; // or specific shop type if we can tell

// PartyMenu: party flag set
if (party == 1 && !inBattle)
    return "PartyMenu";

// EncounterDialog: distinctive
if (rawLocation == 255 && encA != encB && !inBattle)
    return "EncounterDialog";

// Open world map: rawLocation=255, no menu
if (rawLocation == 255 && party == 0 && ui == 1 && !inBattle && ??? )
    return "WorldMap"; // still need to disambiguate from TravelList

// TravelList: rawLocation=255, travel submenu open
// (need a discriminator — both look identical in our inputs)
```

**STILL MISSING:** way to distinguish WorldMap vs TravelList, and way to distinguish different shop types (Outfitters vs Item shop vs Tavern). Both require a memory signal we're not currently reading.

---

## 37. Tavern ❌ MISMATCH — differs from Outfitters only by `ui`

**Ground truth:** At the Tavern (different shop type at same village).

| input | value | vs #36 Outfitters |
|---|---|---|
| **ui** | **0** | was 1 ⬅️ FLIPPED |
| rawLocation | 9 | same — same village |
| party | 0 | same |
| submenuFlag | 1 | same |
| battleMode | 0 | same |
| moveMode | 255 | same |
| all else | same | |

**Detected:** `WorldMap` — ❌ should be `Tavern` (no rule)

**🎯 `ui` DOES discriminate shop types within a single location:**
- Outfitters: `ui=1`
- Tavern: `ui=0`
- (Item Shop, Blacksmith, etc. — would need sampling)

**Why detected as WorldMap?** `rawLocation=9, party=0, ui=0 → triggers the "party==0 && ui==0 → WorldMap" rule via clearlyOnWorldMap being true.

**Now the WorldMap rule fires here — because `clearlyOnWorldMap` correctly evaluates to true (atNamedLocation=true, party=0, battleMode=0).** But ground truth is Tavern, not WorldMap. The rule is badly named and badly positioned.

**More broken detections than we thought:** Every village-with-multiple-shops loads differently:
- First shop entered (Outfitters): ui=1 → TravelList misdetect
- Tavern: ui=0 → WorldMap misdetect

**For the fix:** `rawLocation in 0-42` should ALWAYS mean "at a named location, NOT on world map." The `clearlyOnWorldMap` naming is actively misleading.

**Ideal detection tree for rawLocation=0-42:**
```csharp
// At a named location — shop/village/town (NOT the world map)
if (rawLocation >= 0 && rawLocation <= 42 && !inBattle && party == 0) {
    if (ui == 0) return "LocationMenu"; // or Tavern, or similar
    if (ui == 1) return "ShopMenu";     // or Outfitters
    // still more combos possible for Blacksmith, Item Shop, etc.
}
```

We'd still need to further split Tavern vs other "ui=0 at named location" screens, and Outfitters vs other "ui=1 at named location" screens. That's likely a memory scan task (or might require additional addresses for shop type).

---

## 38. Warrior Guild ❌ MISMATCH — BYTE-IDENTICAL to Outfitters

**Ground truth:** Warrior Guild menu at same village.

| input | value | vs #36 Outfitters |
|---|---|---|
| All 18 inputs | **IDENTICAL** | no difference |

**Detected:** `TravelList` — same as Outfitters

**🔑 Warrior Guild and Outfitters are INDISTINGUISHABLE in these 18 inputs.** Both have `rawLocation=9, ui=1, party=0, submenuFlag=1` etc.

**Updated shop-type model:**
- `ui=1`: Outfitters OR Warrior Guild (and probably more)
- `ui=0`: Tavern (and probably more)

**Detecting specific shop types requires additional memory reads** — likely a "current menu ID" or "focused widget" address we haven't mapped. This is a memory-scan task for later.

**For now, a single `LocationMenu` detection (rawLocation=0-42 && !inBattle) is the best we can do with current inputs.** Claude would have to rely on the command history ("I just pressed the Outfitters option") to know which shop is actually open.

---

## 39. Poachers' Den ❌ MISMATCH — byte-identical to Outfitters & Warrior Guild

All 18 inputs identical to #36, #38. Confirms that **all ui=1 shop types at a given location are indistinguishable** in current inputs. Not adding new signal — moving on.

---

## 40. Save Game screen ❌ MISMATCH — byte-identical to shop menus

All 18 inputs identical to #36/#38/#39 (only encA/encB drifted 8→11). **Save Game is indistinguishable from Outfitters/Warrior Guild/Poachers' Den** in current inputs.

**Expanded "indistinguishable cluster" at location 9:**
- Outfitters, Warrior Guild, Poachers' Den, Save Game → all `party=0, ui=1, rawLocation=9, submenuFlag=1`

Every `ui=1` screen accessed at a named location collapses into this single memory state. A memory scan for "what menu ID is focused" is needed to split them.

---

## 41. TitleScreen reached via GameOver → Return ❌ MISMATCH

**Ground truth:** Title screen, but reached this session by going through GameOver rather than fresh process launch.

| input | value | vs #27 (clean post-restart title) |
|---|---|---|
| party | 0 | same |
| **ui** | **1** | was 0 ⬅️ DIFFERENT! |
| rawLocation | 255 | same |
| **slot0** | **0x000000FF** | was 0xFFFFFFFF ⬅️ stale battle sentinel |
| slot9 | 0xFFFFFFFF | same |
| **battleMode** | **0** | was 255 ⬅️ initialized |
| moveMode | 255 | same |
| **battleActed** | **0** | was 255 ⬅️ initialized |
| **battleMoved** | **0** | was 255 ⬅️ initialized |
| **gameOverFlag** | **1** | was 0 ⬅️ stale from earlier GameOver |
| **submenuFlag** | **1** | was 0 |
| menuCursor | 2 | was 0 |
| eventId | 65535 | same |
| encA/encB | 4/4 | varied |

**Detected:** `Battle_EnemiesTurn` — ❌ should be `TitleScreen`

**🚨 MAJOR FINDING — Two very different "TitleScreen" states in memory:**

1. **Fresh process TitleScreen** (#27): slot0=0xFFFFFFFF, battleMode=255, acted/moved=255 (all uninit)
2. **Returned-from-GameOver TitleScreen** (#41): slot0=0x000000FF, battleMode=0, acted/moved=0, gameOverFlag=1 (stale in-battle residue)

**The `rawLocation==255 → TitleScreen` rule catches #27 correctly. But #41 gets detected as Battle_EnemiesTurn because stale `slot0=0x000000FF` + `slot9=0xFFFFFFFF` trigger `unitSlotsPopulated=true`, so `inBattle=true`, and we fall through to EnemiesTurn via `battleTeam=1` stale.

**This means my earlier proposed "tightened TitleScreen" rule (`slot0==0xFFFFFFFF`) would correctly identify #27 but would fail #41** — #41's title screen has stale sentinels.

**Real discriminator for TitleScreen:**
- Both: rawLocation=255, eventId=65535, paused=0, party=0
- Unique: neither has a clean signature distinguishing from "post-GameOver stale" OR "post-battle world map"

**What actually distinguishes #41 from #28 (post-load WorldMap)?**
- #41 TitleScreen (via GameOver): slot0=0x000000FF, ui=1, gameOverFlag=1, submenuFlag=1, menuCursor=2
- #28 WorldMap post-load: slot0=0xFFFFFFFF, ui=1, gameOverFlag=0, submenuFlag=0, menuCursor=0

**So `gameOverFlag=1 && submenuFlag=1 && slot0=0x000000FF` signals "TitleScreen via GameOver."** That's a specific combo we can detect.

**Signal overload summary:**
- `gameOverFlag`: 1 means "in a gameover-related screen" (GameOver, LoadGame, this post-GameOver TitleScreen, possibly others)
- The flag is sticky across process state until truly reset (maybe only via New Game?)
- Detection needs to handle gameOverFlag=1 across multiple screens via other discriminators

**Updated title-screen rules needed:**
```csharp
// Fresh TitleScreen (clean process launch)
if (rawLocation == 255 && slot0 == 0xFFFFFFFF && eventId == 0xFFFF
    && ui == 0 && battleMode == 255)
    return "TitleScreen";

// Post-GameOver TitleScreen (returned from game-over menu)
// Stale battle state but rawLocation=255 and no active battle-mode signals.
if (rawLocation == 255 && gameOverFlag == 1 && submenuFlag == 1
    && paused == 0 && battleMode == 0 && battleActed == 0 && battleMoved == 0
    && menuCursor == 2)  // menuCursor=2 might be title menu position — verify
    return "TitleScreen";
```

Or better: add a "title screen" memory address we haven't mapped yet.

---

## 42. Battle_ChooseLocation (Orbonne Monastery, Vaults — 4th Level) ❌ MISMATCH

**Ground truth:** Pre-battle sub-location selection screen. Multi-stage battlegrounds (like Orbonne) show this before battle starts. Cursor navigates between sub-locations connected by arrows. "Commence Battle" button on the left.

| input | value | comparable to |
|---|---|---|
| party | 0 | |
| **ui** | **0** | like Tavern (#37) |
| **rawLocation** | **18** | Orbonne Monastery location ID — VALID 0-42 |
| slot0 | 0x000000FF | in-battle residue |
| slot9 | 0xFFFFFFFF | |
| battleMode | 0 | |
| moveMode | 255 | |
| gameOverFlag | 1 | sticky |
| submenuFlag | 1 | |
| battleTeam | 1 | stale |
| acted/moved | 0/0 | |
| eventId | 65535 | |
| menuCursor | 2 | |

**Detected:** `WorldMap` — ❌ should be `Battle_ChooseLocation`

**User's intuition was close but we can do better:** Not identical to Battle_Encounter (#32) — Encounter had `encA != encB` (distinctive). This has `encA == encB`.

**Actually IDENTICAL to Tavern (#37)** — same `rawLocation=0-42, party=0, ui=0, submenuFlag=1`. The only difference is location ID (9=village vs 18=Orbonne). But location ID alone doesn't distinguish sub-screen types.

**Interpretation:**
- `rawLocation=18` (Orbonne) + `ui=0` at Orbonne means "sub-location selection within Orbonne" because Orbonne is a multi-battle location type
- `rawLocation=9` (village) + `ui=0` at a village means "Tavern" because that village doesn't have a sub-location selector

**So the state IS distinguishable — but only if we know which locations have sub-battle selectors vs which have taverns.** That's data we can encode (a location type table).

**This adds a new entry to the "at named location" model:**
- Battle-campaign location (Orbonne, etc.) + ui=0 → Battle_ChooseLocation
- Battle-campaign location + ui=1 → (something else, TBD)
- Village location + ui=0 → Tavern
- Village location + ui=1 → Outfitters/Warrior Guild/etc.

**Fix implication:** Location detection needs a per-location-type table. Without it, the audit's `LocationMenu` catch-all isn't precise enough for actionable detection — the mod needs to know "this location type shows Battle_ChooseLocation" vs "this location type is a village with shops."

**Good news:** Location ID tables already exist in `project_location_ids_verified.md` (43 location IDs). Just needs an annotation of "is this a pre-battle selector?" per entry.

---

## 43. Pre-battle Cutscene (Orbonne dialogue — Loffrey speaking) ❌ MISMATCH

**Ground truth:** Pre-battle dialogue sequence. "Loffrey: Keep watch here, I will press on with Lord Folmarv." Character portraits visible, speech bubble, triangle/button-press advance.

| input | value | 🔑 |
|---|---|---|
| party | 0 | |
| ui | 1 | |
| rawLocation | 18 | Orbonne |
| slot0 | **0xFFFFFFFF** | ⭐ cleared! (was 0x000000FF in all recent battle states) |
| slot9 | 0xFFFFFFFF | |
| battleMode | 0 | |
| moveMode | 255 | |
| **gameOverFlag** | **1** | sticky |
| battleTeam | 0 | ⭐ reset (was 1 stale) |
| acted/moved | 0/0 | |
| **eventId** | **302** | ⭐ STORY EVENT ID — new! |
| encA/encB | 0/0 | reset |
| submenuFlag | 1 | |
| menuCursor | 0 | |

**Detected:** `TravelList` — ❌ should be `Cutscene` or `Battle_Dialogue`

**🎯 `eventId=302` — this is the missing Cutscene/Dialogue signal.**

The existing rule: `if (eventId > 0 && eventId != 0xFFFF && (rawLocation == 255 || rawLocation < 0)) return "Cutscene"` — requires `rawLocation==255`. But here `rawLocation=18` (we're at Orbonne), so the guard fails and we fall through to TravelList.

**eventId values seen across audit:**
- `0xFFFF (65535)`: uninitialized sentinel (title, fresh-load, no cutscene active)
- `401` in-battle: reused as active unit nameId (not a real event)
- `400` in GameOver/LoadGame: reused as stale unit nameId
- **`302` here**: real story event ID (< 200 range was documented but 302 is ≥ 200... actually 302 < 400 so it's a new range)

**The `eventId < 200` filter in Battle_Dialogue rule doesn't match this either** (302 > 200). Maybe the filter should be `< 400` (to exclude nameIds).

**Updated understanding of eventId:**
- `0xFFFF` = unset/idle
- Values like 401, 400 (≥ 400) = active unit nameIds (reused address)
- Values like 302 (200-400) = real story event IDs — CUTSCENE
- Values < 200 = documented as real story IDs (from existing code)

So the correct Cutscene/Dialogue discriminator is: **`eventId is in a specific range AND not the stale-nameId range**.

Specific proposed values:
- `eventId >= 1 && eventId < 400 && eventId != 0xFFFF` → real story event
- Depending on location: rawLocation==255 → Cutscene, rawLocation in 0-42 → Battle_Dialogue

**Proposed rule:**
```csharp
// Cutscene: story event playing, not on a named-location screen.
if (eventId >= 1 && eventId < 400 && eventId != 0xFFFF && rawLocation == 255 && !inBattle)
    return "Cutscene";

// Pre-battle / Battle dialogue: story event playing at a named battle location.
if (eventId >= 1 && eventId < 400 && eventId != 0xFFFF && rawLocation >= 0 && rawLocation <= 42)
    return "Battle_Dialogue";
```

**Also noteworthy:** `slot0=0xFFFFFFFF` here (cleared!) even though gameOverFlag is sticky. This is the first time since the fresh restart we've seen slot0 clear back to 0xFFFFFFFF mid-session. The pre-battle dialogue sequence must reset the unit slot memory — perhaps because a new battle is being loaded and the slots are torn down before re-populating.

---

## 44. Post-battle screen (likely Battle_Desertion) ❌ MISMATCH

**Ground truth:** Post-battle — user expected Victory screen but Desertion fires because a unit's Brave/Faith dropped below threshold.

| input | value | 🔑 |
|---|---|---|
| party | 0 | |
| ui | 1 | |
| **rawLocation** | **26** | The Siedge Weald (battle location) — VALID 0-42 |
| slot0 | 0x000000FF | |
| slot9 | 0xFFFFFFFF | |
| **paused** | **1** | ⭐ paused flag set (unusual for mid-battle) |
| **gameOverFlag** | **1** | ⭐ |
| battleMode | 0 | |
| moveMode | 0 | |
| battleTeam | 0 | |
| **battleActed** | **1** | ⭐ sticky from battle |
| **battleMoved** | **1** | ⭐ sticky from battle |
| **encA** | **6** | ⭐ |
| **encB** | **5** | ⭐ **DIFFERS** — victory-like |
| eventId | 402 (stale nameId) | |
| submenuFlag | 1 | |
| menuCursor | 1 | |

**Detected:** `EncounterDialog` — ❌ should be `Battle_Desertion` or `Battle_Victory`

**🔑 KEY SIGNALS:**
- `encA != encB` (6 ≠ 5) — matches Victory rule (`postBattle && encA != encB → Battle_Victory`)
- But also `paused=1` — matches Desertion-paused variant (`postBattlePaused && encA==encB && submenuFlag=1 → Battle_Desertion`)

Looking at the existing ScreenDetectionLogic rules:
```csharp
bool postBattle = unitSlotsPopulated && battleMode == 0 && gameOverFlag == 0
                  && (battleActed == 1 || battleMoved == 1) && rawValidLocation;
```

**This sample fails `postBattle` because `gameOverFlag==1`** — but the rule requires `gameOverFlag==0`. Hence it falls through to EncounterDialog (which fires because `encA != encB` and rawLocation=26 is valid).

**Why is gameOverFlag=1 here?** It's been sticky since an earlier GameOver. The flag doesn't reset between battles/loads — it's process-level sticky. This is the root cause: current rules were written assuming `gameOverFlag=0` in normal post-battle state, but we've seen it stays at 1 across the entire session after the first GameOver.

**This breaks multiple detections:**
- Battle_Victory: expects gameOverFlag=0
- Battle_Desertion (non-paused path): expects gameOverFlag=0  
- Battle_Dialogue: explicitly filters against acted/moved=1

**So the "gameOverFlag stuck at 1" bug has a cascading impact across many post-battle screens.**

**Proposed fix approach:**
- Stop treating gameOverFlag as "game over happened right now"
- Treat it as "at least one GameOver happened this process lifetime, flag is permanently sticky"
- Remove `gameOverFlag==0` requirement from postBattle rules
- Use a different signal to distinguish "post-battle" from "GameOver now": `paused` + `acted/moved` combos work without needing gameOverFlag

**Desertion vs Victory discriminator:**
- Desertion has `paused=1 + submenuFlag=1` (pause menu with warning dialog layered)
- Victory has `paused=0` (auto-advance screen)

This sample has `paused=1 && submenuFlag=1 && rawValidLocation && (acted=1 || moved=1)` — uniquely matches **Battle_Desertion**. Unfortunately `encA != encB` here — not the `encA == encB` the current rule demands. Maybe encA/encB drifting undermines that rule too.

**Refined rules:**
```csharp
// Post-battle state: acted or moved is 1, battle over.
bool postBattle = unitSlotsPopulated && battleMode == 0
                  && (battleActed == 1 || battleMoved == 1) && rawValidLocation;

// Desertion: post-battle with pause + submenu (warning dialog overlay)
if (postBattle && paused == 1 && submenuFlag == 1)
    return "Battle_Desertion";

// Victory: post-battle, no pause (auto-advancing)
if (postBattle && paused == 0)
    return "Battle_Victory";
```

This drops the unreliable `gameOverFlag==0` requirement and the fragile `encA vs encB` comparison in favor of signals that are actually stable.

---

## 45. Battle_Desertion (second read, same screen) ✓ MATCH

**Ground truth:** Same Desertion screen as #44, re-dumped seconds later.

| input | value | vs #44 |
|---|---|---|
| **encA** | **9** | was 6 ⬅️ drifted |
| **encB** | **9** | was 5 ⬅️ drifted — NOW MATCHES encA |
| all else | identical | |

**Detected:** `Battle_Desertion` — ✓ matches

**🚨 encA/encB are NOT a reliable per-screen signal — they drift independently and eventually re-sync.**

The rule `postBattlePaused && encA == encB && submenuFlag == 1 → Battle_Desertion` only fired on this second read because they happened to re-synchronize. On the first read seconds earlier (#44), `encA != encB` so the rule missed.

**Earlier theory:** `encA != encB` meant "encounter triggered" (used by EncounterDialog rule #32). That theory is now WEAKER — encA/encB seems to be a pair of counters that drift naturally. They can be != for various reasons, not just encounter dialogs.

**Implications:**
- `Battle_Victory` rule (`encA != encB`) is a coincidence-detector, not a reliable discriminator
- `EncounterDialog` rule (`encA != encB`) has the same problem
- The real Encounter discriminator in #32 might have been something else (ui=0, slot0=0xFFFFFFFF)

**Revised rule:** Don't rely on encA/encB at all. Use paused/submenuFlag/acted/moved combos which ARE stable.

**For Desertion specifically:**
```csharp
// Desertion: post-battle at named location, pause menu with warning dialog overlay.
// Paused + acted/moved set means battle just ended with a brave/faith warning.
if (rawValidLocation && paused == 1 && submenuFlag == 1
    && (battleActed == 1 || battleMoved == 1)
    && battleMode == 0)
    return "Battle_Desertion";
```

No reliance on encA/encB or gameOverFlag. This would have correctly detected BOTH #44 and #45.
