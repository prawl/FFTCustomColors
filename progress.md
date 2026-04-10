# Session Progress — Enemy Class Fingerprint Discovery

Last updated: 2026-04-10 (mid-session snapshot before context handoff)

## The big win
**Enemy job/class detection now works reliably.** Previously every enemy was labeled "Chemist" (because the UI buffer was unreliable and the condensed struct has no jobId byte). Now the system reads 11 bytes at heap unit struct offset `+0x69`, uses bytes 1-10 as a class fingerprint, and looks them up in a dictionary. 50+ classes covered.

See `memory/project_class_fingerprint.md` for the full investigation and current pitfalls.

## Infrastructure delivered this session

### 1. Fingerprint-based class detection
- `ColorMod/GameBridge/ClassFingerprintLookup.cs` — new file. Dictionary `FingerprintToJob` keyed by 10-byte hex string, plus `FingerprintByTeam` for classes that share fingerprints (e.g. Arithmetician player vs Ahriman enemy).
- `ColorMod/GameBridge/NavigationActions.cs` — `CollectUnitPositionsFull()` has a new loop that, after roster matching, searches the heap for each unit's HP+MaxHP pattern, reads 11 bytes at struct+0x69, and sets `unit.JobNameOverride` from the fingerprint lookup.
- Scan now renders `unit.JobNameOverride ?? GetJobName(u.Job)` with the UI-buffer fallback restricted to player units (team=0) only. Enemies without a fingerprint match show as `(?)` instead of inheriting the active player's job via UI buffer leak.
- Zero-fingerprint fallback: tries successive heap matches if the first one lands on a dead/reserved slot.
- Range-filtered heap search via `SearchBytesInAllMemory(pattern, maxResults, minAddr, maxAddr)` — eliminates graphics-buffer false positives that were exhausting the match budget.

### 2. Story character roster fix
- `ColorMod/GameBridge/CharacterData.cs` — added `StoryCharacterJob` dict (nameId → canonical job) and `GetStoryJob(nameId)` API. Fixes story characters like Marach/Beowulf/Rapha/Mustadio/Agrias whose roster +0x02 field is their nameId, not a job ID.
- `NavigationActions.cs` calls `GetStoryJob` after the roster match and overrides `JobNameOverride` if the character is in the dict.

### 3. Scan cache invalidation
- `ColorMod/Utilities/CommandWatcher.cs` — `scan_move` now calls `_turnTracker.ShouldAutoScan(...)` before checking `HasCachedScan`. Previously the cache only invalidated on battle_move/attack/ability/wait/flee, so a scan on a new turn without any prior action returned stale data forever. Now the cache correctly invalidates when `battleUnitId` or `battleUnitHp` changes between scans.

### 4. Live log file
- `ColorMod/Core/ConsoleLogger.cs` — tees all mod output to `claude_bridge/live_log.txt` (truncated on first write per session). Claude can now `grep` or `tail` this file for fresh logs without the user copy-pasting from the game console window.
- `fft.sh` — new `logs` helper: `logs` (last 40), `logs 100` (last 100), `logs grep <pattern>`.

### 5. Title screen detection fix
- `ColorMod/GameBridge/ScreenDetectionLogic.cs` — treat `eventId == 0xFFFF` as "no event" so a freshly-launched game on the title screen isn't misclassified as Cutscene.
- New test: `DetectScreen_TitleScreen_EventIdUninitializedSentinel_ReturnsTitleScreen`.

### 6. `boot` / `restart` robustness
- `fft.sh` — new `running` helper checks `tasklist` for `FFT_enhanced.exe`. `boot` now launches the game if not running, waits for bridge, then advances past title. `restart` full cycle: kill → build → deploy → launch → advance.
- Cursor reset for Pause menu: `ReturnToWorldMap` now presses Up x6 before Down x4 because the Pause menu remembers its last cursor position.
- Shorter default timeout (5s vs 15s) with `running` print on timeout so crashes are immediately visible.
- Reset `FFT_START` / `_FFT_DONE` on entry so the 30s total-script budget doesn't kill a long boot.

### 7. `battle_flee` cursor reset
- `ColorMod/GameBridge/NavigationActions.cs:BattleFlee` — same Up x6 before Down x4 fix.
- `ColorMod/GameBridge/NavigationPaths.cs:ReturnToWorldMap` / `ReturnToTitle` — same fix.

## Fingerprint table state

**51 classes covered**:

### Monsters (26)
Chocobo, Black Chocobo, Black Goblin, Gobbledygook, Bomb, Grenade, Exploder, Red Panther, Coeurl, Vampire Cat, Skeleton, Bonesnatch, Skeletal Fiend, Ghast, Ghoul, Revenant, Floating Eye, Ahriman, Plague Horror, Dragon, Red Dragon, Wisenkin, Minotaur, Malboro, Ochu, Piscodaemon, Squidraken, Jura Aevis, Steelhawk, Cockatrice, Behemoth

### Generic humans (15)
Squire, Chemist, Knight, Archer, Monk, White Mage, Black Mage, Time Mage, Summoner, Thief, Mystic, Geomancer (via Ramza roster), Dragoon, Samurai, Arithmetician, Bard, Mime

### Story characters via fingerprint (6)
Gallant Knight (Ramza — unreliable, varies per save), Automaton (Construct 8), Divine Knight (Meliadoul), Nightblade (Isilud), White Knight (Wiegraf — may change through story), Machinist (Barich — distinct fingerprint from Mustadio)

### Story characters via roster nameId (14)
Ramza, Delita, Orlandeau, Reis, Gaffgarion, Mustadio, Marach, Agrias, Beowulf, Rapha, Meliadoul, Cloud, Construct 8, Balthier, Luso

## What's still missing

### Monster families with gaps
| Family | Missing |
|---|---|
| Chocobo | Red Chocobo (tier 3) |
| Goblin | Goblin (tier 1) |
| Dragon | Blue Dragon (tier 2) |
| Bull | Sacred (tier 2) |
| Malboro | Great Malboro (tier 3) |
| Piscodaemon | Mindflayer (tier 3) |
| Behemoth | Behemoth King (tier 2), Dark Behemoth (tier 3) |

### Entire missing families
- **Pig/Boar**: Pig, Swine, Wild Boar
- **Treant**: Dryad, Treant, Elder Treant
- **Hydra**: Hydra, Greater Hydra, Tiamat

### Missing generic humans
- Orator, generic Ninja (as enemy), Dancer, Onion Knight, Dark Knight
- Note: User mentioned Dark Knight / Onion Knight mods will be enabled next restart

### Lucavi / bosses (story-fight only, low priority)
- Belias, Zalera, Adrammelech, Hashmal, Ultima, Cuchulainn, Archaeodaemon, Ultima Demon

## Battlegrounds visited this session
All 19 battlegrounds visited (24-42). Some re-rolled multiple times. Notable:
- **Grogh Heights (33)** — mod-forced battle with 11 lv99 enemies, unit structs at 0x430xxxx (outside hardcoded heap range). **Skipped** — mod-only enemies shouldn't be labeled as canonical.
- **Mandalia Plain (24)** — also mod-forced, but with story characters (Isilud, Wiegraf, Barich). Labeled because those chars DO appear normally in story fights.
- **Balias Tor (37)** — Meliadoul battle, added her Divine Knight fingerprint (her roster match was failing due to brave/faith read issues).

## Known bugs and workarounds

### Fingerprint heap search fails in some saves
Hardcoded range `0x4160000000..0x4180000000` misses heap unit structs in some saves. Observed in Grogh Heights where structs were at 0x430xxxx. Current behavior: enemies show as `(?)`. Future fix: dynamically discover the heap region by searching for any known unit's HP pattern first.

### Ramza's fingerprint varies per save
Ramza has had 5+ different fingerprints across saves depending on his job/equipment/growth. His roster lookup via nameId=1 is the authoritative path. I never added Ramza as a fingerprint entry for this reason. (A couple of his entries leaked into the table earlier in the session by accident — need to audit and remove if they're still there.)

### First turn of a newly loaded battle can return stale cached scan
Right after loading a save into a battle, the first `scan_move` can return data from the previous battle's cache. Workaround: `battle_wait` through the first turn. See `memory/feedback_scan_first_turn_bug.md`.

### Screen detection during formation loading
After `execute_action Fight`, the bridge reports `TravelList` for 3-6 seconds during the formation scene load. The key sequence (Enter Enter Space Enter) still works — just sleep and send it blindly. See `ClassFingerprintLabeling.md`.

### Story char roster match needs level+brave+faith
The roster match uses a `RosterMatcher.Match` with Level+Brave+Faith as the key. If brave/faith are polluted by UI buffer leak (showing 100/100 for an enemy), the match fails. Meliadoul (nameId=42) regularly fails to match — her fingerprint is now a backup.

## Git state
Branch: `auto-play-with-claude`
Remote: `prawl/FFTCustomColors` (moved from `FFT_Color_Mod`)
Recent commits (HEAD first):
- `05f2c1d` Add 4 monster fingerprints: Cockatrice, Plague Horror, Red Dragon, Minotaur
- `d87c784` Fix battle_flee cursor reset and add 3 story char fingerprints
- `24f10a1` Skip zero fingerprints, add Chocobo/Piscodaemon/Divine Knight
- `34b867a` Add story char job lookup by nameId, 4 new fingerprints
- `9e8c34b` Fix heap search range, add story char jobs, add 4 fingerprints
- `c4d184d` Add 9 more class fingerprints
- `a650e63` Expand class fingerprint table and ignore byte 0
- `dab579c` Add class fingerprint lookup for enemy job names
- `e0c9250` Tee all mod logs to claude_bridge/live_log.txt
- `7c95e96` Shorter default timeout and print running status on timeout
- `5656ad2` Fix title screen detection and restart hang
- `a8f2f75` Add running helper, auto-launch game in boot

Uncommitted changes (staged for this final commit):
- `ClassFingerprintLookup.cs` — Vampire Cat, Jura Aevis, Malboro, Skeleton
- `progress.md` — this file
- `FFTHandsFree/Instructions/ClassFingerprintLabeling.md` — new
- `FFTHandsFree/Instructions/Commands.md` — added running/boot/logs entries
- `FFTHandsFree/Instructions/Rules.md` — added Class Labeling Mode section

## How to continue in a new context

1. Read `FFTHandsFree/Instructions/ClassFingerprintLabeling.md` for the loop procedure.
2. Read `memory/project_class_fingerprint.md` for the technical background.
3. Read `memory/project_battle_loop.md` for the detailed flee→travel→formation→scan flow.
4. Check `progress.md` (this file) for current state and what's missing.
5. Run `boot` to get into the game, `battle_wait` if needed to get past first-turn cache bug, then start the loop.
6. Target the missing monster families listed above. Each battleground has a different enemy pool — try unvisited ones first.
7. Commit after every 3-5 new entries with a descriptive message.
