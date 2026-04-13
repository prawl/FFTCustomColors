# Save & Load Guide

## Overview

FFT: The Ivalice Chronicles uses a single save slot system. Saves can be made on the world map or at specific story points.

## Saving

### From World Map
1. Open the menu (Tab or Start)
2. Select "Save" from the menu options
3. Confirm the save

### Auto-save
- The game auto-saves at certain story checkpoints
- Auto-save happens when entering a new location

## Loading

### From Title Screen
1. On the title screen, select "Continue"
2. The most recent save loads automatically

### From Game Over
1. After a Game Over screen, select "Retry"
2. This reloads the save from before the battle

## Save Data Location

The IC Remaster stores save data as encrypted UMIF PNG files. The save contains:
- Party roster (all unit stats, jobs, abilities, equipment)
- Story progress and location
- Gil and inventory
- World map unlock state

## Bridge Commands

```bash
# No direct save/load bridge commands yet
# Use key commands to navigate save/load menus:
execute_action Save    # from world map menu
execute_action Load    # from title screen
```

## Notes

- Save frequently before difficult battles
- The game has one save slot — saving overwrites the previous save
- Battle saves are separate from world map saves in some versions
