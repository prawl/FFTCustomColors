<!-- This file should not be longer than 200 lines, if so prune me. -->
# Cutscene Reading System — Progress

## Goal
Claude reads cutscene dialogue in real-time and reacts like a first-time player.

## What's Done

### 1. Decoded .mes files (PSX text encoding)
- **MesDecoder.cs** — decodes FFT's PSX text encoding (0x0A='A', 0x24='a', etc.)
- Handles speaker tags (`[Knight]`, `[Ovelia]`), control codes, punctuation
- 17 unit tests covering all byte values and file parsing
- Successfully decoded event002.en.mes (Orbonne opening) through event010.en.mes (Gariland battle)

### 2. EventScriptLookup.cs
- Loads all 298 `.mes` files from `0002.en/fftpack/text/` directory
- Caches decoded scripts keyed by event number
- `GetScript(eventId)` returns list of DialogueLine(Speaker, Text)
- `GetFormattedScript(eventId)` returns readable "Speaker: text" format
- 5 unit tests

### 3. Cutscene screen detection
- Added `eventId` parameter to ScreenDetectionLogic.Detect
- When location=255 and eventId>0 (non-battle), returns "Cutscene" instead of "TitleScreen"
- 3 unit tests
- Pre-battle cutscenes (unit slots populated) still detected as Battle — TODO

### 4. Event ID memory address found
- **`0x14077CA94`** — stores the current event file number during cutscenes
- Verified: event002 (Orbonne), event004 (first battle), event008 (narrator), event010 (Gariland)
- Stable within same cutscene, changes between scenes
- During battle this address holds active unit nameId (dual-purpose)
- Already defined in code as `AddrActiveNameId` in BattleTracker.cs

## What's Left

### 5. Wire eventId into CommandWatcher (NEXT)
- Add `0x14077CA94` to ScreenAddresses array (index 19+)
- Pass eventId to ScreenDetectionLogic.Detect call
- Add `eventId` field to DetectedScreen class
- Screen response will show `[Cutscene] eventId=2` instead of `[TitleScreen]`

### 6. Add `read_dialogue` action
- New bridge command: `{"action": "read_dialogue"}` or auto-include in screen response
- Reads eventId from memory, loads script via EventScriptLookup
- Returns formatted dialogue text in response
- Claude reads it and reacts

### 7. Deploy .mes files
- Copy `0002.en/fftpack/text/*.mes` to bridge directory (or read from pac files path)
- EventScriptLookup needs to know where the files are at runtime
- BuildLinked.ps1 may need to deploy them

### 8. Claude's cutscene behavior
- Detect [Cutscene] screen state
- Load the event script
- Read through dialogue, commenting on plot, characters, humor
- Press Enter/F to advance dialogue
- Track which lines have been read (dialogue index within event)
- Follow PLAYER_RULES.md — react genuinely, no spoilers

## File Locations
- `.mes` files: `c:/Users/ptyRa/OneDrive/Desktop/Pac Files/0002.en/fftpack/text/event###.en.mes`
- 298 event files covering the entire game script
- PSX encoding table: MesDecoder.cs (also documented in project_memory_scan_results.md)

## Key Addresses
| Address | Field | Notes |
|---------|-------|-------|
| 0x14077CA94 | Event ID / Active NameId | Event number during cutscenes, nameId during battle |

## Logs That Helped
The Reloaded-II mod loader logs (docs/logs.txt) showed:
```
[FFTPack] Accessing file 5 -> event_test_evt.bin (OVERRIDEN to /script/enhanced/event004.e)
loaded: nxd/text/scenario/scenario0020.pzd
loaded: sound/voice/scenario/scenario0010/vo_scenario0010_003_000.sab
```
This confirmed the game loads event scripts by number and uses scenario IDs for voice files.
