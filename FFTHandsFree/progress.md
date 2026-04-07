<!-- This file is exempt from the 200 line limit. -->
# Story Objective on World Map — Implementation Plan

## Goal

When Claude calls `screen` on the world map, include the story objective location so Claude knows where to go next. The yellow diamond marker address (`0x1411A0FB6`) is already known.

**Before:**
```
[WorldMap] loc=26(TheSiedgeWeald) hover=26 ui=Move status=completed
```

**After:**
```
[WorldMap] loc=26(TheSiedgeWeald) hover=26 ui=Move status=completed objective=18(OrbonneMonastery)
```

## Steps

### 1. Find where GameState fields are populated
- Trace how `location`, `locationName`, `hover` get into the response JSON
- Likely in `GameStateReporter.cs` or `CommandWatcher.cs`

### 2. Add `storyObjective` field to GameState model
- Add `StoryObjective` (byte) to the response model in `CommandBridgeModels.cs`
- Read the byte at `0x1411A0FB6` alongside the existing location/hover reads

### 3. Map objective ID to location name
- Reuse the existing location name dictionary (same IDs as world map locations)
- Add `StoryObjectiveName` string field so the response includes both ID and name

### 4. Surface in fft.sh output
- Parse `storyObjective` and `storyObjectiveName` from response JSON
- Append `objective=ID(Name)` to the screen summary line
- Only show when on WorldMap and value is non-zero

### 5. Update WorldMapNav.md
- Document the objective field in the "Reading the Screen" section
- Note that this tells Claude where the story wants it to go

### 6. Tests
- Test that the objective byte is read and included in WorldMap responses
- Test that objective=0 or missing is handled gracefully (no objective shown)
- Test fft.sh parsing of the new field

### 7. Mark TODO complete
- Check off "Story objective location" in TODO.md
