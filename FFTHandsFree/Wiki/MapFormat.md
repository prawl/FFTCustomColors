# Map File Format (PSX / TIC Remaster)

Source: FFHacktics Wiki. Maps are in the MAP/ directory with three file types.

## File Types

| Type | Field D Value | Description |
|------|---------------|-------------|
| GNS | (index file) | Tells game which texture/mesh to load for weather, day/night, room arrangement |
| Texture | 0x1701 | 256x1024 bitmap, 4BPP, always 131,072 bytes. No header — raw pixel data |
| Mesh | 0x2E01 (primary), 0x2F01 (override), 0x3001 (alternate) | Polygons, terrain, palettes, lighting, animations |

## GNS File Structure

20-byte records mapping conditions to files:

| Field | Offset | Meaning |
|-------|--------|---------|
| A | 0x00-0x01 | Map state type (0x22=Outdoors, 0x30=Room arrangement, 0x70=Deep Dungeon) |
| B | 0x02 | Room arrangement ID |
| C | 0x03 | Time/weather: bit 7=Night, bits 4-6=weather (0=None, 2=Light, 3=Normal, 4=Heavy) |
| D | 0x04-0x05 | File type (0x1701=Texture, 0x2E01=Primary Mesh, 0x2F01=Override, 0x3001=Alt, 0x3101=Ignore) |
| E | 0x08-0x09 | CD sector number (LBA) |
| F | 0x0C-0x0F | File length (rounded to 2048-byte sector boundary) |

## Mesh File Header (196 bytes)

Table of 32-bit pointers to data chunks. 0 = chunk not present.

| Pointer Offset | Chunk |
|----------------|-------|
| 0x40 | Primary mesh (polygons) |
| 0x44 | Texture palettes (color) |
| 0x64 | Lighting: 3 directional lights + ambient + background gradient |
| 0x68 | **Terrain** (tile heights, slopes, surface types) |
| 0x6C | Texture animation instructions |
| 0x70 | Palette animation instructions |
| 0x7C | Texture palettes (grayscale, used for move/action highlighting) |
| 0x8C | Mesh animation instructions |
| 0x90-0xAC | Animated meshes 1-8 |
| 0xB0 | Polygon render properties (camera angle visibility) |

## Terrain Chunk (Most Important for Gameplay)

**Header:** 2 bytes — X dimension, Z dimension (product <= 256 tiles)

**Tile data:** 2 levels (e.g. bridge + water below), 256 tiles per level, 8 bytes per tile.

| Bits | Field |
|------|-------|
| 2 | Unknown |
| 6 | **Surface type** (grass, water, stone — used for Geomancy) |
| 8 | Unknown |
| 8 | **Height** (bottom of slope for sloped tiles) |
| 3 | **Depth** |
| 5 | **Slope height** (top - bottom difference) |
| 8 | **Slope type** (see table below) |
| 3 | Unknown |
| 5 | Thickness |
| 1 | Can walk/cursor through but not stand on |
| 3 | Unknown |
| 2 | Tile shading (0=Normal, 1=Dark, 2=Darker, 3=Darkest) |
| 1 | **Can't walk** |
| 1 | **Can't cursor** |
| 8 | Camera auto-rotate angles (8 bits for 8 compass directions) |

Total terrain chunk: always 2 + 256*8*2 = 4098 bytes regardless of map size.

### Surface Types (6-bit field, used for Geomancy)

| ID | Surface | Geomancy Ability | ID | Surface |
|----|---------|------------------|----|---------|
| 0x00 | Natural Surface | Pitfall | 0x14 | Wooden floor |
| 0x01 | Sand area | Sand Storm | 0x15 | Stone floor |
| 0x02 | Stalactite | Carve Model | 0x16 | Roof |
| 0x03 | Grassland | Hell Ivy | 0x17 | Stone wall |
| 0x04 | Thicket | Hell Ivy | 0x18 | Sky |
| 0x05 | Snow | Blizzard | 0x19 | Darkness |
| 0x06 | Rocky cliff | Local Quake | 0x1A | Salt |
| 0x07 | Gravel | Local Quake | 0x1B | Book |
| 0x08 | Wasteland | Sand Storm | 0x1C | Obstacle |
| 0x09 | Swamp | Quicksand | 0x1D | Rug |
| 0x0A | Marsh | Quicksand | 0x1E | Tree |
| 0x0B | Poisoned marsh | Quicksand (+ Poison) | 0x1F | Box |
| 0x0C | Lava rocks | Lava Ball | 0x20 | Brick |
| 0x0D | Ice | Blizzard | 0x21 | Chimney |
| 0x0E | Waterway | Water Ball | 0x22 | Mud wall |
| 0x0F | River | Water Ball | 0x23 | Bridge |
| 0x10 | Lake | Water Ball | 0x24 | Water plant |
| 0x11 | Sea | Water Ball | 0x25 | Stairs |
| 0x12 | Lava | Lava Ball | 0x26 | Furniture |
| 0x13 | Road | Local Quake | 0x27-0x3E | Various/unused |
| | | | 0x3F | Cross section |

### Slope Types

| Value | Meaning | Value | Meaning |
|-------|---------|-------|---------|
| 0x00 | Flat | 0x41 | Convex NE |
| 0x85 | Incline N | 0x11 | Convex SE |
| 0x52 | Incline E | 0x14 | Convex SW |
| 0x25 | Incline S | 0x44 | Convex NW |
| 0x58 | Incline W | 0x96 | Concave NE |
| | | 0x66 | Concave SE |
| | | 0x69 | Concave SW |
| | | 0x99 | Concave NW |

## Primary Mesh Chunk

**Header:** 4 uint16s — counts of textured triangles (max 512), textured quads (max 768), untextured triangles (max 64), untextured quads (max 256).

**Data blocks in order:**
1. XYZ coordinates (int16 per axis, per vertex)
2. Normal vectors (fixed 1,3,12 format — divide int16 by 4096.0)
3. UV texture coordinates + palette number + texture page
4. Untextured polygon data
5. Polygon-to-terrain-tile mapping (7-bit Z + 1-bit height level + 8-bit X per polygon)

## Texture Palettes

16 palettes x 16 colors. Each color is 16-bit Little Endian BGR:
```
Bit layout: A BBBBB GGGGG RRRRR
If R=G=B=A=0 → transparent
```

## Lighting

- 3 directional lights: color (fixed 1,3,12 RGB) + position (int16 XYZ)
- Ambient light: 3 bytes (RGB)
- Background gradient: 6 bytes (top RGB + bottom RGB)
- Light colors can overflow past 255 for intensity effects

## Polygon Render Properties (4096 bytes)

Per-polygon visibility flags (16-bit Little Endian):
- Bit 0: Unlit (pure texture, no lighting)
- Bits 2-5: Visible from SW/NW/NE/SE (top angles)
- Bits 6-13: Visible from 8 sub-compass directions
- Used to remove walls/obstructions based on camera angle

## Mesh Animations

14,620 bytes total:
- 128 keyframes (80 bytes each): rotation/position/scale + tween parameters
- 64 mesh states (16 instructions each): keyframe ID, next frame, duration
- 64 mesh properties (4 bytes each): parent ID for linked animations
- 8 animated mesh slots, each with 8 invokable states
- Tween types: 0x05=TweenTo, 0x06=TweenBy, 0x0A=Oscillate, 0x12=OscillateOffset

## Texture Format

256x1024 pixels, 4BPP (2 pixels per byte, left pixel in low nibble). No header. Palettes stored in mesh file. Always exactly 131,072 bytes.
