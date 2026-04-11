<!-- This file should not be longer than 200 lines, if so prune me. -->
# World Map Navigation

## Available Commands

```bash
screen                    # Check current screen and location
world_travel_to <id>      # Travel to a location by ID
execute_action <name>     # Execute a validPath (PartyMenu, TravelList, etc.)
save                      # Save the game
load                      # Load most recent save
```

## Overview

The world map is where you move between locations in Ivalice. Use `world_travel_to <id>` to travel — it handles everything automatically: opens the travel list, navigates tabs, selects the destination, and confirms.

```bash
world_travel_to 26    # Travel to The Siedge Weald
world_travel_to 0     # Travel to Royal City of Lesalia
```

Your character walks across the map, passing through nodes along the way. Random encounters can trigger at battleground nodes.

## Encounters Along the Way

When a random encounter triggers, the screen changes to `EncounterDialog`. Use `execute_action` to respond:

```bash
execute_action Fight    # Accept the encounter
execute_action Flee     # Skip it and continue walking
```

After fleeing, your character **automatically continues** toward the original destination. **Do NOT re-issue `world_travel_to`** — just call `screen` to check if you arrived or hit another encounter. Keep fleeing (or fighting) until you arrive.

## After Accepting a Fight

The **Formation Screen** appears:
1. Blue tiles show where you can place units
2. Use `execute_action` to place units and start the battle
3. Press Space when done placing, Enter to commence

## Screen States

| State | Meaning |
|-------|---------|
| `WorldMap` | Standing on a location node |
| `TravelList` | Travel list is open, selecting destination |
| `EncounterDialog` | Random encounter popup (Fight/Flee) |
| `PartyMenu` | Party management menu |

## Reading the Screen

```bash
screen
# Output: [WorldMap] loc=26(TheSiedgeWeald) hover=26 ui=Move status=completed objective=18(OrbonneMonastery)
```

- **loc** = your current location (ID and name)
- **hover** = where the cursor is pointing
- **status** = completed/blocked/failed
- **objective** = story destination (yellow diamond on the map). This is where the story wants you to go next. Only shown when there's an active story objective.

## Location IDs

### Settlements (IDs 0-14)
| ID | Name |
|----|------|
| 0 | Royal City of Lesalia |
| 1 | Riovanes Castle |
| 2 | Eagrose Castle |
| 3 | Lionel Castle |
| 4 | Limberry Castle |
| 5 | Zeltennia Castle |
| 6 | Magick City of Gariland |
| 7 | Walled City of Yardrow |
| 8 | Mining Town of Gollund |
| 9 | Merchant City of Dorter |
| 10 | Castled City of Zaland |
| 11 | Clockwork City of Goug |
| 12 | Port City of Warjilis |
| 13 | Free City of Bervenia |
| 14 | Trade City of Sal Ghidos |

### Battlegrounds (IDs 24-42)
| ID | Name |
|----|------|
| 24 | Mandalia Plain |
| 25 | Fovoham Windflats |
| 26 | The Siedge Weald |
| 27 | Mount Bervenia |
| 28 | Zeklaus Desert |
| 29 | Lenalian Plateau |
| 30 | Tchigolith Fenlands |
| 31 | The Yuguewood |
| 32 | Araguay Woods |
| 33 | Grogh Heights |
| 34 | Beddha Sandwaste |
| 35 | Zeirchele Falls |
| 36 | Dorvauldar Marsh |
| 37 | Balias Tor |
| 38 | Dugeura Pass |
| 39 | Balias Swale |
| 40 | Finnath Creek |
| 41 | Lake Poescas |
| 42 | Mount Germinas |

### Miscellaneous (IDs 15-23)
| ID | Name |
|----|------|
| 15 | Ziekden Fortress |
| 16 | Mullonde |
| 17 | Brigands' Den |
| 18 | Orbonne Monastery |
| 19 | Golgollada Gallows |
| 21 | Fort Besselat |
| 22 | Midlight's Deep |
| 23 | Nelveska Temple |

Note: ID 20 is unused.

## Tips

- **Settlements don't trigger random encounters.** Only battlegrounds do.
- **Save before traveling** to dangerous locations: `save`
- **After travel**, call `screen` to check if you arrived or hit an encounter.
- **Orbonne Monastery (ID 18)** has a story mission — avoid during normal exploration.

## Known-Broken Battlegrounds (skip for class-labeling runs)

Some battlegrounds spawn **mod-forced battles** where enemy unit structs live at heap addresses outside our search range. Class fingerprint lookup fails and every enemy renders as `(?)`. If you're doing class discovery work, flee these immediately and move on:

- **Grogh Heights (33)** — 11 mod-only units at ~0x430xxxx
- **Dugeura Pass (38)** — 6 enemies, all unreachable
- **Tchigolith Fenlands (30)** — 6 enemies, all unreachable
- **Mandalia Plain (24)** — story battle with 11 units, all unreachable

Regular story play on these maps still works (move, attack, etc.) — just don't expect class names or ability lists for the enemies.

## Post-Flee State Gotcha

After `battle_flee` succeeds, the screen state can stay stuck reporting `Battle_MyTurn` for several seconds even though the player is back on the world map. Travel commands may error out during this window. If you see this, the fast recovery is `restart` (full rebuild + relaunch). Don't try to force-send keys — you can't control a battle that's already ended.
