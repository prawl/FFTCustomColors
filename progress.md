# Session Progress — 2026-04-06

## What Was Accomplished

### 1. Memory Scan (30 agents, 3 waves)
- Discovered ~120 memory fields across 8 regions
- Verified same-session AND cross-restart stability
- Full results in [docs/MEMORY_SCAN_WAVE1.md](docs/MEMORY_SCAN_WAVE1.md)

### 2. Battle State Reporting
Active unit now reports: nameId, name (story characters), ct, jobId, brave, faith, equipped abilities (with names), equipment IDs, hoveredAction.

### 3. Screen Detection Fixes
- Battle_Moving uses `moveMode==255 || battleMode==2` (fixed from battleMode-only)
- `hoveredAction` replaces raw menuCursor number
- `ui` field on screen response ("Move", "Abilities", "Wait", etc.)
- Location falls back to saved world map location during battle

### 4. Map Auto-Detection (MAJOR BREAKTHROUGH)
- **Heap scenario struct found** at dynamic address with MAP ID (+0x30) and location ID (+0x24)
- C# `FindScenarioMapId()` scans ~300MB of heap memory, pattern-matches the struct
- Filters to low-address candidates (live battle data), picks highest scenario ID
- 128 map names hardcoded from MapTrapFormationData.xml
- **Successfully detects MAP074 "The Siedge Weald" automatically**
- Reset flag added so map re-scans on new battle

### 5. Data Files Created
- `CharacterData.cs` — ~140 entries (story characters + job/monster names)
- `ItemData.cs` — 293 items (FFTPatcher IDs, need remapping for IC remaster)
- `MapLoader.GetMapName()` — 128 battle map names

### 6. Text Encoding Research
- FFT PSX encoding cracked: 0x0A-0x23=A-Z, 0x24-0x3D=a-z, 0x00-0x09=0-9
- Text in compressed .pac files, custom encoding, PAGE_READONLY heap memory
- 40+ agents searched — text strings not in static module memory

## Current Bug: Map Detection False Positives

### Problem
The heap scan finds ~500+ candidates matching the scenario struct pattern. For Siedge Weald (MAP074) it works because the correct match has the highest scenario ID among low-address candidates. But for Araguay Woods (MAP080), the correct match only appears at HIGH addresses (0x42C4+) in lookup tables, NOT in low-address live data.

### Root Cause
The scenario struct layout may differ for random encounters vs story battles, OR the random encounter's struct has field values outside our validation ranges (scenario 50-600, subType <20, music <200, etc.).

### Candidates File
Written to `claude_bridge/map_candidates.txt` on each scan. Shows all matches with scenario, location, map, and address.

### What Needs Investigation
1. **Widen the search** — relax validation constraints or try different field offsets
2. **Check if random encounters use a different struct** — read the actual bytes at the correct map=80 addresses (0x42C4000480) to understand the layout
3. **Verify our struct offsets** — the +0x24/+0x30 offsets were derived from ONE battle (Siedge Weald scenario 284). Other battles may have different layouts.
4. **Consider alternative approach** — search for JUST the map ID byte (0x50 for MAP080) in a smaller, more targeted heap region, then validate context

### Approach for Next Session
1. Load into Araguay Woods battle
2. Read the two high-address candidates for map=80 (0x42C4000480, 0x42DC0013F8) 
3. Dump 96 bytes from each to see the full struct
4. Compare layout with the Siedge Weald struct to find what's different
5. Adjust validation to handle both cases

## Pitfalls Found During Battle Play

1. **battleMode=1** not recognized as move mode (fixed with moveMode fallback)
2. **acted/moved flags reset** between turn phases
3. **MoveToEnemy silently fails** without loaded MAP
4. **Keys sent during unrecognized state** get processed by game
5. **UI buffer shows cursor-selected unit** not active unit
6. **Condensed nameId is sequential** (1,2,3...) not character identifier
7. **Condensed header values misidentified** — +0x18 is CT gauge, +0x06 is NOT speed
8. **Item IDs don't match FFTPatcher** — IC remaster uses different numbering
9. **Map detection picks wrong map** for some encounters (false positives)
10. **_battleMapAutoLoaded didn't reset** between battles (fixed)
11. **Too many concurrent agents crash the bridge** — limit to <5 at a time for bridge reads

## Still TODO

### Critical
- [ ] Fix map detection for all encounter types (Araguay Woods fails)
- [ ] Effective stats (Speed, PA, MA, Move, Jump) — only in heap struct
- [ ] Roster-to-battle unit matching — condensedNameId is sequential, need equipment-based matching

### Important
- [ ] Generic character names — game text in custom encoding, compressed
- [ ] Item ID remapping for IC remaster
- [ ] Verify map detection across 5+ different battle locations
- [ ] Game over flag verification (0x140D3A10C)

### Nice to Have
- [ ] Turn order prediction from CT table
- [ ] Status effects from heap struct +0x50-0x5F
- [ ] Facing direction from heap struct +0x39
- [ ] Item name lookup table with correct IC remaster IDs
