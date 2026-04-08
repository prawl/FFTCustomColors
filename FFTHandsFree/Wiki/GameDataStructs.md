# Game Data Structures (Ivalice Chronicles / TIC Remaster)

Source: FFHacktics Wiki data tables. Denuvo adds +0x0C00 to each address.

## Job Data (0x31 bytes per entry)
Table Address: `0x780560`

| Offset | Size | Field |
|--------|------|-------|
| 0x00 | 1 | Skillset ID |
| 0x01 | 2 | Innate Ability 1 |
| 0x03 | 2 | Innate Ability 2 |
| 0x05 | 2 | Innate Ability 3 |
| 0x07 | 2 | Innate Ability 4 |
| 0x09 | 1 | Equippable Items 1 (bit flags: 0x80=Unarmed, 0x40=Knife, 0x20=Ninja Blade, 0x10=Sword, 0x08=Knight's Sword, 0x04=Katana, 0x02=Axe, 0x01=Rod) |
| 0x0A | 1 | Equippable Items 2 (0x80=Staff, 0x40=Flail, 0x20=Gun, 0x10=Crossbow, 0x08=Bow, 0x04=Instrument, 0x02=Book, 0x01=Polearm) |
| 0x0B | 1 | Equippable Items 3 (0x80=Pole, 0x40=Bag, 0x20=Cloth, 0x10=Shield, 0x08=Helmet, 0x04=Hat, 0x02=Hair Adornment, 0x01=Armor) |
| 0x0C | 1 | Equippable Items 4 (0x80=Clothing, 0x40=Robe, 0x20=Shoes, 0x10=Armguard, 0x08=Ring, 0x04=Armlet, 0x02=Cloak, 0x01=Perfume) |
| 0x0D | 1 | Equippable Items 5 (0x10=Fell Sword?, 0x08=Lip Rouge?) |
| 0x0E | 1 | HP Growth |
| 0x0F | 1 | HP Multiplier |
| 0x10 | 1 | MP Growth |
| 0x11 | 1 | MP Multiplier |
| 0x12 | 1 | Speed Growth |
| 0x13 | 1 | Speed Multiplier |
| 0x14 | 1 | PA Growth |
| 0x15 | 1 | PA Multiplier |
| 0x16 | 1 | MA Growth |
| 0x17 | 1 | MA Multiplier |
| 0x18 | 1 | Move |
| 0x19 | 1 | Jump |
| 0x1A | 1 | C-EV (Class Evade) |
| 0x1B-0x1F | 5 | Innate Statuses (bitfield) |
| 0x20-0x24 | 5 | Status Immunity (bitfield) |
| 0x25-0x29 | 5 | Starting Statuses (bitfield) |
| 0x2A | 1 | Absorbed Elements |
| 0x2B | 1 | Nullified Elements |
| 0x2C | 1 | Halved Elements |
| 0x2D | 1 | Elemental Weakness |
| 0x2E | 1 | Monster Portrait |
| 0x2F | 1 | Monster Palette |
| 0x30 | 1 | Monster Graphic |

## Item Data (0x0C bytes per entry)
Table Address: `0x807B40`

| Offset | Size | Field |
|--------|------|-------|
| 0x00 | 1 | Palette |
| 0x01 | 1 | Sprite ID |
| 0x02 | 1 | Required Level (random generation) |
| 0x03 | 1 | Type Flags (0x80=Weapon, 0x40=Shield, 0x20=Headgear, 0x10=Armor, 0x08=Accessory, 0x02=Rare, 0x01=Immune to Steal/Break) |
| 0x04 | 1 | Second Table ID |
| 0x05 | 1 | Item Type (01=Knife..22=Item, see full list below) |
| 0x07 | 1 | Item Attributes ID |
| 0x08 | 2 | Price (gil) |
| 0x0A | 1 | Shop Availability (chapter/event unlock tier 0x00-0x10) |

### Item Type IDs
01=Knife, 02=Ninja Blade, 03=Sword, 04=Knight Sword, 05=Katana, 06=Axe, 07=Rod, 08=Staff, 09=Flail, 0A=Gun, 0B=Crossbow, 0C=Bow, 0D=Instrument, 0E=Book, 0F=Polearm, 10=Pole, 11=Bag, 12=Cloth, 13=Shield, 14=Helmet, 15=Hat, 16=Hair Adornment, 17=Armor, 18=Clothing, 19=Robe, 1A=Shoes, 1B=Armguard, 1C=Ring, 1D=Armlet, 1E=Cloak, 1F=Perfume, 20=Throwing, 21=Bomb, 22=Item

### Shop Availability Tiers
0x01=Ch1 Start, 0x02=Ch1 Enter Igros, 0x03=Ch1 Save Elmdor, 0x04=Ch1 Kill Miluda, 0x05=Ch2 Start, 0x06=Ch2 Save Ovelia, 0x07=Ch2 Meet Draclau, 0x08=Ch2 Save Agrias, 0x09=Ch3 Start, 0x0A=Ch3 Zalmo, 0x0B=Ch3 Meet Velius, 0x0C=Ch3 Save Rafa, 0x0D=Ch4 Start, 0x0E=Ch4 Bethla, 0x0F=Ch4 Kill Elmdor, 0x10=Ch4 Kill Zalbag

## Weapon Secondary Data (0x08 bytes per entry)
Table Address: `0x808740`

| Offset | Size | Field |
|--------|------|-------|
| 0x00 | 1 | Range |
| 0x01 | 1 | Attack Flags (0x80=Striking, 0x40=Lunging, 0x20=Direct, 0x10=Arc, 0x08=2 Swords, 0x04=2 Hands, 0x02=Throwable, 0x01=Forced 2 Hands) |
| 0x02 | 1 | Formula ID |
| 0x04 | 1 | Weapon Power (WP) |
| 0x05 | 1 | Evade % |
| 0x06 | 1 | Element (0x80=Fire, 0x40=Lightning, 0x20=Ice, 0x10=Wind, 0x08=Earth, 0x04=Water, 0x02=Holy, 0x01=Dark) |
| 0x07 | 1 | Inflict Status / Cast Spell ID |

## Shield Secondary Data (0x02 bytes per entry)
Table Address: `0x808B40`

| Offset | Size | Field |
|--------|------|-------|
| 0x00 | 1 | Physical Evade % |
| 0x01 | 1 | Magical Evade % |

## Head/Body Secondary Data (0x02 bytes per entry)
Table Address: `0x808B60`

| Offset | Size | Field |
|--------|------|-------|
| 0x00 | 1 | HP Bonus |
| 0x01 | 1 | MP Bonus |

## Accessory Secondary Data (0x02 bytes per entry)
Table Address: `0x808BE0`

| Offset | Size | Field |
|--------|------|-------|
| 0x00 | 1 | Physical Evade % |
| 0x01 | 1 | Magical Evade % |

## Item Attributes (0x19 bytes per entry)
Table Address: `0x808F50`

| Offset | Size | Field |
|--------|------|-------|
| 0x00 | 1 | PA bonus |
| 0x01 | 1 | MA bonus |
| 0x02 | 1 | Speed bonus |
| 0x03 | 1 | Move bonus |
| 0x04 | 1 | Jump bonus |
| 0x05-0x09 | 5 | Innate Statuses (bitfield) |
| 0x0A-0x0E | 5 | Status Immunity (bitfield) |
| 0x0F-0x13 | 5 | Starting Statuses (bitfield) |
| 0x14 | 1 | Absorbed Elements |
| 0x15 | 1 | Nullified Elements |
| 0x16 | 1 | Halved Elements |
| 0x17 | 1 | Elemental Weakness |
| 0x18 | 1 | Elements Strengthened |

## Element Bitfield Encoding
Used in weapons, items, abilities, and job data:

| Bit | Element |
|-----|---------|
| 0x80 | Fire |
| 0x40 | Lightning |
| 0x20 | Ice |
| 0x10 | Wind |
| 0x08 | Earth |
| 0x04 | Water |
| 0x02 | Holy |
| 0x01 | Dark |

## Primary Ability Data (0x08 bytes per ability, 0x200 abilities)
Table Address: `0x77F240`

| Offset | Size | Field |
|--------|------|-------|
| 0x00 | 2 | JP Cost |
| 0x02 | 1 | Chance to Learn |
| 0x03 | 1 | Flags: high nibble = misc (0x80=Learn w/JP, 0x40=Display Name, 0x20=Learn on Hit, 0x10=?), low nibble = type (1=Normal, 2=Item, 3=Throw, 4=Jump, 5=Aim, 6=Math, 7=Reaction, 8=Support, 9=Movement) |
| 0x04 | 1 | AI Flags 1 (0x80=HP, 0x40=MP, 0x20=Cancel Status, 0x10=Add Status, 0x08=Stats, 0x04=Unequip, 0x02=Target Enemies, 0x01=Target Allies) |
| 0x05 | 1 | AI Flags 2 (0x80=Target Map, 0x40=Reflectable, 0x20=Undead Reverse, 0x10=Check CT/Target, 0x08=Random Hits, 0x04=Faith-based, 0x02=Evadeable, 0x01=Silence-affected) |
| 0x06 | 1 | AI Flags 3 (0x80=Arced, 0x40=Stop at obstacle, 0x20=Linear, 0x10=Non-spear, 0x08=3-dir melee, 0x04=3-dir ranged, 0x02=Magical, 0x01=Physical) |
| 0x07 | 1 | AI Flags 4 (0x80=Usable by AI, 0x40=Only allies/self, 0x20=Only enemies, 0x08=Monster skill?, 0x04=Weapon range, 0x01=Evade with motion) |

## Secondary Ability Data (0x14 bytes per ability, 0x2E0 abilities)
Table Address: `0x784700`

| Offset | Size | Field |
|--------|------|-------|
| 0x00 | 1 | Range |
| 0x01 | 1 | Effect Area (AoE size) |
| 0x02 | 1 | Vertical tolerance |
| 0x03 | 1 | Flags 1 (0x20=Weapon Range, 0x10=Vertical Fixed, 0x08=Vertical Tolerance, 0x04=Weapon Strike, 0x02=Auto-Target, 0x01=Target Self) |
| 0x04 | 1 | Flags 2 (0x80=Hit Enemies, 0x40=Hit Allies, 0x20=Top-down Targeting, 0x10=Follow Target, 0x08=Random Fire, 0x04=Linear, 0x02=3 Directions, 0x01=Hit Caster) |
| 0x05 | 1 | Flags 3 (0x80=Reflect, 0x40=Math Skill, 0x20=Silence-affected, 0x10=Mimic, 0x08=Blocked by Golem, 0x04=Persevere, 0x02=Quote, 0x01=Animate on miss) |
| 0x06 | 1 | Flags 4 (0x80=Counter Flood, 0x40=Counter Magic, 0x20=Direct, 0x10=Blade Grasp, 0x08=Requires Sword, 0x04=Requires Materia Blade, 0x02=Evadeable, 0x01=Targeting) |
| 0x07 | 1 | Element (same bitfield as weapons) |
| 0x08 | 1 | Formula ID |
| 0x09 | 1 | X parameter |
| 0x0A | 1 | Y parameter |
| 0x0B | 1 | Inflict Status ID |
| 0x0C | 1 | Charge Time (CT) |
| 0x0D | 1 | MP Cost |

## Consumable Item Secondary Data (0x03 bytes per entry)
Table Address: `0x808C20`

| Offset | Size | Field |
|--------|------|-------|
| 0x00 | 1 | Formula |
| 0x01 | 1 | Z value (healing/damage amount) |
| 0x02 | 1 | Inflict Status ID |

## Move-Find Item Data (0x06 bytes per entry)
Table Address: `0x77CC70`

| Offset | Size | Field |
|--------|------|-------|
| 0x00 | 1 | X,Y coordinates (two nibbles) |
| 0x01 | 1 | Tile Behavior (0x80=No activation, 0x20=Always Trap, 0x10=Disable Trap, 0x08=Steel Needle, 0x04=Sleeping Gas, 0x02=Death Trap, 0x01=Degenerator) |
| 0x02-0x03 | 2 | Common Treasure Item ID |
| 0x04-0x05 | 2 | Rare Treasure Item ID |

## Settlement Shop Availability Bitmask

In WORLD.BIN at offset `0x000AD844`, 256 rows x 2 bytes. Controls which shops are available at which settlements.

| Byte 0 Bit | Settlement | Byte 1 Bit | Settlement |
|-------------|-----------|-------------|-----------|
| 0x80 | Lesalia | 0x80 | Gollund |
| 0x40 | Riovanes | 0x40 | Dorter |
| 0x20 | Eagrose (Igros) | 0x20 | Zaland |
| 0x10 | Lionel | 0x10 | Goug |
| 0x08 | Limberry | 0x08 | Warjilis |
| 0x04 | Zeltennia | 0x04 | Bervenia |
| 0x02 | Gariland | 0x02 | Sal Ghidos |

## PSX File Layout (Mutually Exclusive in RAM)

Files are large and share RAM addresses. Only one set can be loaded at a time:

| File | When Loaded | Purpose |
|------|-------------|---------|
| SCUS_942.21 | Always | Game executable |
| WORLD.BIN | Outside battle | Shops, towns, saving, formation screen |
| WLDCORE.BIN | With WORLD.BIN | Sprite/portrait data |
| BATTLE.BIN | In combat or events | Ability formulas, map rendering, events |
| ATTACK.OUT | Deployment screen | Deployment graphics/handling |
| REQUIRE.OUT | Ending battle | Post-battle dismissal, Brave/Faith leaving |
| EQUIP.OUT | Re-equip in battle | Equipment changing |
| CARD.OUT | Saving between events | Memory card save |
| BUNIT.OUT | Unit list in battle | Formation screen snapshot |
| JOBSTTS.OUT | Job/ability view in battle | Scrollable jobs/abilities menus |

**Key:** BATTLE.BIN and WORLD.BIN are mutually exclusive. Routines from one cannot be called while the other is loaded.

## TIM Image Format (PSX 2D Graphics)

FFT sprites use .TIM format. Header starts with `10 00 00 00`.

| BPP | ID Tag | CLUT | Width Multiplier |
|-----|--------|------|------------------|
| 4BPP | `08 00 00 00` | 16 colors/CLUT (32 bytes) | Width * 4 |
| 8BPP | `09 00 00 00` | 256 colors/CLUT (512 bytes) | Width * 2 |
| 16BPP | `02 00 00 00` | None (15-bit BGR direct) | Width * 1 |
| 24BPP | `03 00 00 00` | None (24-bit RGB direct) | Width / 1.5 |

**Color format:** 15-bit BGR, Little Endian: `{ggg}{rrrrr} {0}{bbbbb}{gg}`. Multiply each 5-bit channel by 8 for 24-bit RGB.
