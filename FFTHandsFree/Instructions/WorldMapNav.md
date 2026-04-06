<!-- This file should not be longer than 200 lines, if so prune me. -->
# World Map Navigation

## Overview

The world map is where you move between locations in Ivalice. You stand on a node (city, fortress, battleground) and travel to other nodes along connected paths. When traveling, your character physically walks across the map, passing through other nodes along the way. Random encounters can happen at any battleground node you pass through.

## The Travel List

Press **T** to open the Travel List. It has three tabs you cycle through with **E** (next) and **Q** (previous):

1. **Settlements** — Cities, castles, towns (15 locations)
2. **Battlegrounds** — Open fields, forests, deserts, mountains (19 locations)
3. **Miscellaneous** — Special locations like monasteries, fortresses, deep dungeons (8 locations)

When you open the list, the cursor starts on your current standing location (on the correct tab). If you switch to a different tab, the cursor goes to the top of that tab (index 0).

### How to Travel

1. Press **T** to open the Travel List
2. Use **E/Q** to switch to the correct tab
3. Use **Up/Down** to highlight your destination
4. Press **Enter** to select it (this closes the list and places the world map cursor on that node)
5. Press **Enter** again to start walking there

Your character will now walk along the path to the destination. This takes a few seconds. Hold **Ctrl** to speed up the movement.

### Encounters Along the Way

As you walk, you may pass through battleground nodes. Each one has a chance of triggering a random encounter. When this happens:

- Your character stops
- A dialog appears with two choices (1 column, 2 rows): **Fight** or **Flee**
- Fight is selected by default (top option)

To fight: Press **Enter**
To flee: Press **Escape** (or Down + Enter)

After fleeing, your character **automatically continues** walking toward the original destination. You don't need to re-issue the travel command. More encounters may trigger along the way — just keep fleeing (or fighting) until you arrive.

### If You Choose to Fight

After accepting the fight, the **Formation Screen** appears:
1. Blue tiles show where you can place your units
2. The cursor starts on a valid tile — press **Enter** to open the character list
3. Select a character with **Enter** to place them on that tile
4. The cursor auto-moves to the next valid tile
5. Repeat until you've placed up to 5 characters (random battles) or fewer (story battles)
6. Press **Space** when done placing — "Commence battle?" dialog appears (Yes/No)
7. Press **Enter** on Yes to start the battle

For quick testing with just Ramza: Enter (place him) -> Space (done) -> Enter (yes)

## Location IDs

Every location has a unique numeric ID. These are used in the `travel` command:

```
travel 26    # Travel to The Siedge Weald
travel 0     # Travel to Royal City of Lesalia
travel 17    # Travel to Brigands' Den
```

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

## Reading the Screen State

After any action, call `screen` to see where you are:

```
[WorldMap] loc=26(TheSiedgeWeald) hover=26 ui=Move status=completed
```

- **loc** = your current standing location (ID and name)
- **hover** = where the world map cursor is pointing (usually matches loc, or shows last travel list selection)
- **status** = completed/encounter/failed

When an encounter triggers:
```
[EncounterDialog] loc=32(AraguayWoods) hover=26 ui=Move status=completed
```
This means you're at Araguay Woods and an encounter popped up. Your original destination (hover=26, Siedge Weald) is still your target.

## Tips

- **Always check state** after travel. The character may have stopped at an encounter along the way.
- **After fleeing**, call `screen` to check if another encounter appeared or if you arrived.
- **Orbonne Monastery (ID 18)** has a story mission with a different encounter screen — avoid traveling there during normal exploration.
- **The travel_to command handles everything automatically**: opens list, navigates tabs, scrolls to destination, confirms. You just need to handle encounters that pop up during travel.
- **Settlements** don't trigger random encounters. Only **Battlegrounds** do.
- **The path between locations is fixed** — the game decides which nodes you pass through. You can't choose the route.
