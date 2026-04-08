<!-- This file is exempt from the 200 line limit. -->
# Session Progress — 2026-04-08

## Completed Today (11 commits)

1. **Story objective on world map** — Read yellow diamond at 0x1411A0FB6, show `objective=18(OrbonneMonastery)` in screen output
2. **FFT Wiki** — 15 reference docs covering all game mechanics, verified by 10 cross-checking agents
3. **Status effects per unit** — Found static battle array at 0x140893E45, 5-byte PSX bitfield decoded into 40 named statuses
4. **Fix stale location** — Use hover (0x140787A22) as authoritative position on WorldMap instead of unreliable rawLocation
5. **Unit name identification** — Match scanned units to roster by level+origBrave+origFaith, lookup story character names
6. **CT, Speed, and Combat Timeline turn order** — Speed from condensed +0x06, turn order from C+Up scan order (matches in-game sidebar)
7. **Fix fast-unit scan** — Ninja appearing multiple times in timeline no longer stops scan early
8. **Battle playtest bugs documented** — Counter-KO, auto-wait after Move+Act, dead/friendly tile blocking
9. **Fix battle_wait after Move+Act** — BattleWaitLogic detects auto-facing state, skips menu navigation
10. **Battle playtest bugs** — Additional bugs found during live play
11. **Learned ability lookup** — Read FFFF-terminated ability list from condensed struct +0x28, map IDs to names/range/MP/effects via ActionAbilityLookup. Mettle fully verified in-game.

## Verified Working

- `scan_units` / `scan_move` returns: name, team, level, HP/MaxHP, MP, CT, Speed, position, statuses, abilities (active unit only), turn order
- Ramza identified by name, statuses detected (Regen, Shell, Protect, Dead, etc.)
- Turn order matches in-game Combat Timeline
- Shout ability manually executed via key presses — confirmed working in-game
- Move → Attack → Wait flow works end-to-end (auto-facing fix)

## Known Issues / Next Steps

### PA not updating after Shout
- Ramza used Shout (PA+1/MA+1/Speed+1/Brave+10) but scan still shows PA=100
- The condensed struct PA at +0x18 may not reflect battle-modified stats
- Need to verify: is the PA we read actually PA, or something else? Memory scan notes say "+0x18 is CT gauge=100, not PA=20"

### Ability execution needs a proper command
- Currently used raw key presses to navigate Abilities → Mettle → Shout → Enter → Enter
- Need `battle_ability <name> [x] [y]` command that handles menu navigation automatically
- Need screen state detection for submenu states: Battle_Abilities, Battle_Attack, Battle_Mettle, etc.

### Ability IDs for other skillsets unverified
- Only Mettle IDs verified in-game (0x41=Focus through 0xE7=Ultima)
- All other skillsets (Items, White Magicks, Black Magicks, etc.) use estimated PSX IDs — need verification
- Process: equip secondary skillset on a unit, read condensed struct +0x28, cross-reference with in-game menu

### Remaining battle TODOs
- `battle_ability <name> [x] [y]` command implementation
- Screen state detection for ability submenus
- Dead/friendly units blocking movement (BFS doesn't account for occupied tiles)
- Counter-attack KO recovery
- Fix job name mapping (IC remaster uses different job IDs than PSX)
- Dead/KO unit detection (HP=0 units invisible in some scan states)

## Key Memory Addresses Discovered

| Address | Field | Notes |
|---------|-------|-------|
| 0x1411A0FB6 | Story objective | Yellow diamond location ID |
| 0x140787A22 | Hover location | Authoritative standing position on WorldMap |
| 0x140893E45 + slot*0x200 | Status bytes (5) | PSX bitfield, matched by HP+MaxHP |
| 0x140893E0C + slot*0x200 | Stat pattern | exp/lv/origBrave/brave/origFaith/faith in static array |
| Condensed +0x06 | Speed | Base speed (1 byte) |
| Condensed +0x28 | Ability list | FFFF-terminated uint16 LE, learned ability IDs (active unit only) |
