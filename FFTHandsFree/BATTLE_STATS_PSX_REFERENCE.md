<!-- This file should not be longer than 200 lines, if so prune me. -->
# PSX Battle Stats Reference (for mapping to IC Remaster)

Source: FFT Hacking community wiki. PSX struct stride = 0x1C0 (448 bytes) per unit.

## PSX Unit Slot Addresses
- ENTD slots 1-16: 0x801908CC + (slot-1)*0x1C0
- Player units 1-5: 0x801924CC + (unit-1)*0x1C0

## Stats List (PSX Offsets)

### Identity & Flags
| Offset | Size | Field |
|--------|------|-------|
| 0x00 | byte | Character Identity (0x80=GenericM, 0x81=GenericF, 0x82=Monster, 0xXX=Special) |
| 0x01 | byte | ENTD slot (0xFF = doesn't exist) |
| 0x02 | byte | Formation index |
| 0x03 | byte | Current Job |
| 0x04 | byte | Sprite Palette (0=Default, 1=Hokuten, 2=Nanten, 3=DeathCorps, 4=Church, 5=Shadow) |
| 0x05 | byte | Team/Control flags (0x80=AlwaysPresent, 0x20+0x10=LightBlue, 0x20=Green, 0x10=Red, 0x08=Control, 0x04=Immortal) |
| 0x06 | byte | Gender/flags (0x80=Male, 0x40=Female, 0x20=Monster, 0x10=JoinAfter, 0x08=LoadFormation, 0x01=SaveFormation) |
| 0x07 | byte | Death Counter (3=alive, 0=crystal/chest) |
| 0x09 | byte | Zodiac sign (0x00=Aries..0xC0=Serpentarius) |

### Abilities & Equipment
| Offset | Size | Field |
|--------|------|-------|
| 0x0A | uint16 | Innate Ability #1 |
| 0x0C | uint16 | Innate Ability #2 |
| 0x0E | uint16 | Innate Ability #3 |
| 0x10 | uint16 | Innate Ability #4 |
| 0x12 | byte | Primary Skillset |
| 0x13 | byte | Secondary Skillset |
| 0x14 | uint16 | Reaction Ability |
| 0x16 | uint16 | Support Ability |
| 0x18 | uint16 | Movement Ability |
| 0x1A | byte | Head equipment |
| 0x1B | byte | Body equipment |
| 0x1C | byte | Accessory |
| 0x1D | byte | Right Hand Weapon |
| 0x1E | byte | Right Hand Shield |
| 0x1F | byte | Left Hand Weapon |
| 0x20 | byte | Left Hand Shield |

### Core Stats
| Offset | Size | Field |
|--------|------|-------|
| 0x21 | byte | Experience |
| 0x22 | byte | Level |
| 0x23 | byte | Original Brave |
| 0x24 | byte | Brave |
| 0x25 | byte | Original Faith |
| 0x26 | byte | Faith |
| 0x27 | byte | Transparent Removal Flag |
| 0x28 | uint16 | HP |
| 0x2A | uint16 | Max HP |
| 0x2C | uint16 | MP |
| 0x2E | uint16 | Max MP |

### Attributes
| Offset | Size | Field |
|--------|------|-------|
| 0x30 | byte | PA (without equipment) |
| 0x31 | byte | MA (without equipment) |
| 0x32 | byte | SP (without equipment) |
| 0x33 | byte | PA (equipment bonus) |
| 0x34 | byte | MA (equipment bonus) |
| 0x35 | byte | SP (equipment bonus) |
| 0x36 | byte | PA (total) |
| 0x37 | byte | MA (total) |
| 0x38 | byte | SP (total) |
| 0x39 | byte | CT |
| 0x3A | byte | Move |
| 0x3B | byte | Jump |

### Combat Display
| Offset | Size | Field |
|--------|------|-------|
| 0x3C | byte | Right-Hand WP |
| 0x3D | byte | Left-Hand WP |
| 0x3E | byte | Right-Hand W-EV |
| 0x3F | byte | Left-Hand W-EV |
| 0x40 | byte | A-EV (physical) |
| 0x41 | byte | LH S-EV (physical) |
| 0x42 | byte | RH S-EV (physical) |
| 0x43 | byte | C-EV |
| 0x44 | byte | A-EV (magical) |
| 0x45 | byte | LH S-EV (magical) |
| 0x46 | byte | RH S-EV (magical) |

### Position
| Offset | Size | Field |
|--------|------|-------|
| 0x47 | byte | X Coordinate |
| 0x48 | byte | Y Coordinate |
| 0x49 | byte | Elevation/Facing (bits: 0x03=facing: 0=S,1=W,2=N,3=E) |

### Equip Type Flags
| Offset | Size | Field |
|--------|------|-------|
| 0x4A-0x4D | 4 bytes | Equipment type bitflags |

### Status Effects
| Offset | Size | Field |
|--------|------|-------|
| 0x4E-0x52 | 5 bytes | Innate statuses |
| 0x53-0x57 | 5 bytes | Status immunity |
| 0x58-0x5C | 5 bytes | Current statuses |
| 0x5D-0x6C | 16 bytes | Status effect CTs (Poison, Regen, Protect, Shell, Haste, Slow, Stop, Wall, Faith, Innocent, Charm, Sleep, DontMove, DontAct, Reflect, DeathSentence) |

### Elemental Properties
| Offset | Size | Field |
|--------|------|-------|
| 0x6D | byte | Elemental Absorb |
| 0x6E | byte | Elemental Cancel |
| 0x6F | byte | Elemental Half |
| 0x70 | byte | Elemental Weak |
| 0x71 | byte | Elemental Strengthen |

### Raw Stats & Multipliers
| Offset | Size | Field |
|--------|------|-------|
| 0x72 | 3 bytes | Raw HP |
| 0x75 | 3 bytes | Raw MP |
| 0x78 | 3 bytes | Raw SP |
| 0x7B | 3 bytes | Raw PA |
| 0x7E | 3 bytes | Raw MA |
| 0x81-0x8A | bytes | HPC, HPM, MPC, MPM, SPC, SPM, PAC, PAM, MAC, MAM |

### Ability Bitfields (Reaction/Support/Movement as bitflags)
| Offset | Size | Field |
|--------|------|-------|
| 0x8B-0x8E | 4 bytes | Reaction abilities (bitflags) |
| 0x8F-0x92 | 4 bytes | Support abilities (bitflags) |
| 0x93-0x95 | 3 bytes | Movement abilities (bitflags) |

### Job Unlock & Learned Abilities
| Offset | Size | Field |
|--------|------|-------|
| 0x96-0x98 | 3 bytes | Unlocked Jobs bitfield |
| 0x99-0xD1 | varies | Learned abilities bitfield (per-job, 3 bytes each: actions 1-8, actions 9-16, R/S/M) |

### Job Levels & JP
| Offset | Size | Field |
|--------|------|-------|
| 0xD2-0xDB | bytes | Job levels (nibble-packed pairs: Base/Chem, Knight/Archer, etc.) |
| 0xDC-0x103 | uint16 each | Current JP per job (20 jobs) |
| 0x104-0x12B | uint16 each | Total JP per job (20 jobs) |

### Names & IDs
| Offset | Size | Field |
|--------|------|-------|
| 0x12C-0x13B | 16 bytes | Unit Nickname |
| 0x13C-0x149 | 14 bytes | Job Nickname |
| 0x15C | byte | KO count this battle |
| 0x15D | byte | Charged Ability Remaining CT (0xFF if not charging) |
| 0x15F | byte | Spritesheet ID |
| 0x160 | byte | Job/Portrait Palette |
| 0x161 | byte | Unit ID |
| 0x162 | byte | Special/Base Job Skillset (0x00 for generics) |
| 0x163 | byte | War Trophy |
| 0x164 | byte | Bonus Money Mod (*100 = Gil) |

### AI
| Offset | Size | Field |
|--------|------|-------|
| 0x165-0x166 | 2 bytes | AI X/Y Target |
| 0x167 | byte | AI/Autobattle setting (bitflags) |
| 0x168 | byte | Prioritized Target |
| 0x169-0x16B | 3 bytes | ENTD unknowns + Save CT flag (0x16A bit 0x04) |
| 0x16C | uint16 | Unit Quote/Name ID (0x00XX=Special, 0x01XX=GenericM, 0x02XX=GenericF, 0x03XX=Monster) |

### Target & Action Data
| Offset | Size | Field |
|--------|------|-------|
| 0x16E | byte | Attacker/Self ID |
| 0x16F | byte | Skillset of Last Attack |
| 0x170 | uint16 | Last Attack Used ID (charging action) |
| 0x172 | uint16 | Calculator Type Ability ID |
| 0x174 | uint16 | Calculator Multiplier Ability ID |
| 0x176 | uint16 | Used Item/Equip ID |
| 0x178 | uint16 | Reaction ID |
| 0x179 | byte | Current Ability Target (unit order 1-16) |
| 0x17A | uint16 | Ability X target panel |
| 0x17C | uint16 | Ability target elevation |
| 0x17E | uint16 | Ability Y target panel |

### Unit State Flags
| Offset | Size | Field |
|--------|------|-------|
| 0x182 | byte | Mount Info (0x80=Riding, 0x40=BeingRidden, low bits=mount ENTD slot) |
| 0x183 | byte | Existence flag (0x00=doesn't exist but can, 0x01=exists, 0x02=removing, 0x80=disabled, 0xFF=can't exist) |
| 0x184 | byte | Equipped flags (0x08=Sword, 0x04=MateriaBlade) |
| 0x186 | byte | **Is This Unit's Turn** (0x01=yes, 0x00=no) |
| 0x187 | byte | Movement Taken (0x01=moved) |
| 0x188 | byte | Action Taken (0x01=acted) |
| 0x189 | byte | Ability Outcome (0x02=target hit, 0x01=turn ended) |
| 0x18A | byte | Misc Unit Data ID |
| 0x18B | byte | Ability CT |
