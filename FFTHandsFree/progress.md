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

### 5. eventId wired into CommandWatcher
- Address `0x14077CA94` at ScreenAddresses index 19
- Reads v[19], passes to ScreenDetectionLogic.Detect
- Sets `screen.EventId` when screen is "Cutscene"
- `DetectedScreen.EventId` serialized in JSON (omitted when 0)
- 2 unit tests (serialization)

### 6. read_dialogue bridge action
- `{"action": "read_dialogue"}` reads eventId from memory, looks up script
- Returns formatted dialogue text in `response.dialogue` field
- `CommandResponse.Dialogue` serialized in JSON (omitted when null)
- Added to InfrastructureActions (always allowed in strict mode)
- 2 unit tests (response serialization)

### 7. .mes file deployment
- EventScriptLookup initialized in ModBootstrapper.InitializeGameBridge
- Reads from `claude_bridge/scripts/` directory
- BuildLinked.ps1 copies 298 .mes files from Pac Files source
- Gracefully handles missing directory (0 scripts loaded)

## What's Left

### 8. Claude's cutscene behavior (NEXT)
- Detect [Cutscene] screen state
- Load the event script via read_dialogue
- Read through dialogue, commenting on plot, characters, humor
- Press Enter/F to advance dialogue
- Track which lines have been read (dialogue index within event)
- Follow PLAYER_RULES.md — react genuinely, no spoilers

## File Locations
- `.mes` files: `c:/Users/ptyRa/OneDrive/Desktop/Pac Files/0002.en/fftpack/text/event###.en.mes`
- Deploy target: `claude_bridge/scripts/event###.en.mes`
- 298 event files covering the entire game script
- PSX encoding table: MesDecoder.cs (also documented in project_memory_scan_results.md)

## Key Addresses
| Address | Field | Notes |
|---------|-------|-------|
| 0x14077CA94 | Event ID / Active NameId | Event number during cutscenes, nameId during battle |
