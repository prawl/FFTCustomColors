<!-- This file should not be longer than 200 lines, if so prune me. -->
# Claude Game Bridge — Automation Reference

## Overview

Bridge lets Claude control and read state from FFT: The Ivalice Chronicles via file protocol and memory reads.

## Autonomous Loop

```
1. Kill game:     taskkill //IM FFT_enhanced.exe //F
2. Build:         dotnet build ColorMod/FFTColorCustomizer.csproj -c Release
3. Deploy:        powershell.exe -ExecutionPolicy Bypass -File ./BuildLinked.ps1
4. Launch:        "c:/program files (x86)/steam/steamapps/common/FINAL FANTASY TACTICS - The Ivalice Chronicles/Reloaded/Reloaded-II.exe" --launch "C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\FFT_enhanced.exe"
5. Wait:          Poll for state.json (~3-10s)
6. Boot:          Enter (3s) Enter (3s) Enter — loads save to world map
7. Play:          Send commands, read state, navigate game
```

## File Bridge Protocol

Bridge dir: `{modPath}/claude_bridge/`
Mod path: `c:\program files (x86)\steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\Mods\FFTColorCustomizer\`

### Commands (command.json)
```json
{ "id": "x", "keys": [{"vk": 13, "name": "ENTER"}], "delayBetweenMs": 150 }
{ "id": "x", "action": "dump_unit", "slot": 0 }
{ "id": "x", "action": "search_near", "searchValue": 741, "searchLabel": "hp" }
{ "id": "x", "action": "snapshot", "searchLabel": "before" }
{ "id": "x", "action": "diff", "fromLabel": "before", "toLabel": "after" }
{ "id": "x", "action": "report_state" }
```

### Responses
- `response.json` — command result | `state.json` — auto-updated every 2s
- `hexdump_slot_N.txt` / `search_near_*.txt` / `diff_*.txt` — diagnostic outputs

## VK Key Codes

| Key | Code | Key | Code | Key | Code |
|-----|------|-----|------|-----|------|
| Enter | 13 | Up | 38 | Q | 81 |
| Escape | 27 | Down | 40 | E | 69 |
| Left | 37 | Right | 39 | T | 84 |
| Space | 32 | Tab | 9 | Y | 89 |

## Game Navigation Map

```
Title Screen → Enter×3 (3s delays) → World Map
World Map: Escape=Party Menu, T=Travel List, Enter=travel/enter
Travel List: Q/E=tabs, Up/Down=scroll, Enter=select→Enter again=travel
Party Menu: Q/E=tabs [Units|Inventory|Chronicle|Options]
  Units: grid nav, Enter=character status
    Status sidebar: Equipment(default), Job, Combat Sets
    Equipment: 2-col grid (equip|abilities), Enter=selection list
    Job grid: Enter→Left=Learn/Right=Change Job→Enter
Settlement: Outfitter(default), Tavern, Warriors' Guild, Poachers' Den
  Outfitter: Buy(default), Sell, Fitting
    Buy: A/D=category tabs, Up/Down=items, Enter→qty→Enter→confirm
```

### Party Menu Grid
```
Row 0: Ramza(0), Kenrick(1), Lloyd(2), William(3), Alicia(4)
Row 1: Lavian(5), Mustadio(6), Agrias(7), Rapha(8), Marach(9)
Row 2: ???(10), Orlandeau(11), Meliadoul(12), Reis(13), ???(14)
Row 3: Cloud(15), ...
```
Grid index = row×5 + col. Cursor starts at Ramza (0,0).

### Key Gotchas
- Status screen focus starts on SIDEBAR, not content
- Job grid cursor starts on character's CURRENT job, not (0,0)
- Ramza has 2 extra jobs (Dark Knight, Golden Knight) at start of grid
- Equipment list is circular — unequip by selecting the equipped item
- Job change: Enter → Right (Change Job) → Enter

## Memory Layout

### Roster (AoB-found, stable at 0x1411A18D0)
55 slots × 0x258 bytes. See UNIT_DATA_STRUCTURE.md for full field map.

### UI State Buffer (0x1407AC7CA)
| Offset | Field | Offset | Field |
|--------|-------|--------|-------|
| +0x00 | Cursor grid index | +0x08 | Current MP |
| +0x02 | Current HP | +0x0C | Max MP |
| +0x06 | Max HP | +0x20 | Job ID (STALE after C+Up) |
| +0x1E | Cursor index (dup) | +0x22 | Brave (STALE after C+Up) |
| +0x28 | Cursor index (3rd) | +0x24 | Faith (STALE after C+Up) |

Read byte at 0x1407AC7CA = cursor grid index. Right +1, Down +5.
**WARNING:** Job/Brave/Faith from UI buffer are stale after C+Up cycling. Use roster (+0x02/+0x1E/+0x1F) instead, matched by level.

### World Map State
| Address | Field |
|---------|-------|
| 0x14077D208 | Current location (unreliable — stores last-passed node) |
| 0x140787A22 | Hover cursor (0xFF = no hover) |
| 0x1411A0FB6 | Story objective location (yellow diamond) |
| 0x140D39CD0 | Gil (uint32) |
| 0x140900824/828 | Encounter detection (different = Fight/Flee dialog) |
| 0x1411A0FBC | Travel list count (uint32) |
| 0x1411A0FC0 | Travel list entries (uint32 × N) |
| 0x1411A10B0 | Location unlock mask (byte × 43) |

### Location IDs
**Settlements (0-14):** 0=Lesalia, 1=Zeltennia, 2=Lionel, 3=Bervenia Castle, 4=Gariland, 5=Limberry, 6=Dorter, 7=Yardrow, 8=Gollund, 9=Zaland, 11=Goug, 12=Warjilis, 13=Bervenia Free, 14=Sal Ghidos
**Battlegrounds (24-42):** 24=Mandalia, 25=Fovoham, 26=Siedge Weald, 27=Mt.Bervenia, 28=Gollund Desert, 29=Lenalia, 30=Tchigolith, 31=Paoad, 32=Araguay, 33=Gough, 34=Medilla, 35=Zeirchele, 36=Dorvauldar, 37=Balias Tor, 38=Duguera, 39=Balias Swale, 40=Viura's Creek, 41=Lake Poescas, 42=Mt.Germinas
**Misc (15-23):** 15=Golgorand, 16=Mullonde, 17=Brigands' Den, 18=Orbonne, 19=Golgoland Gallows, 21=Fort Besselat, 22=Midlight's Deep, 23=Nelveska. IDs 10,20 unobserved.

### Battle Stat Arrays (offsets from roster base)
| Array | Offset | Type |
|-------|--------|------|
| Current MP | -0x17F694 | uint32 |
| Current HP | -0x17CA08 | uint32 |
| Max HP | -0x1133E8 | uint32 |

### Ability IDs
See ABILITY_IDS.md for reaction/support/movement ability hex IDs and secondary ability indices.

## DirectInput Key Simulation

For held keys (C+Up scanning, Ctrl fast-forward): use `SendInput` with `KEYEVENTF_SCANCODE` flag, `wScan = MapVirtualKey(vk, 0)`, `wVk = 0`. Also send via `keybd_event` and `PostMessage`. Re-assert held key before each action press.

## Known Issues
- Boot sequence needs 3s delays between Enter presses
- Game window doesn't need focus — PostMessage works in background
- All addresses verified on FFT_enhanced.exe
- Screenshots: `powershell.exe -ExecutionPolicy Bypass -File ./screenshot.ps1`
