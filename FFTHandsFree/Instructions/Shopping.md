<!-- This file should not be longer than 200 lines, if so prune me. -->
# Shopping

How to enter a settlement, navigate its shops, and buy/sell items.

## Overview

Settlements (ID 0-14: Lesalia, Riovanes, Eagrose, Lionel, Limberry, Zeltennia, Gariland, Yardrow, Gollund, Dorter, Zaland, Goug, Warjilis, Bervenia, Sal Ghidos) have shops. Battlegrounds don't.

The screen flow when you enter a settlement:

```
WorldMap ──EnterLocation──► LocationMenu ──EnterShop──► SettlementMenu ──Select──► OutfitterBuy / OutfitterSell / OutfitterFitting
   ▲                             │                            │                            │
   │                             │                            │                            │
   │                          Leave                         Leave                        Cancel
   │                             │                            │                            │
   └─────────────────────────────┴────────────────────────────┴────────────────────────────┘
```

**State names:**
- `LocationMenu` — the shop-list inside a settlement (Outfitter / Tavern / Warriors' Guild / Poachers' Den / Save Game).
- `SettlementMenu` — inside a chosen shop, at the sub-action selector (e.g. Buy / Sell / Fitting for an Outfitter).
- `OutfitterBuy`, `OutfitterSell`, `OutfitterFitting` — the per-sub-action item list.

Tavern / Warriors' Guild / Poachers' Den sub-actions aren't mapped yet — they report as `SettlementMenu` when selected.

## Commands

All actions go through `execute_action <name>`. The current screen's ValidPaths list tells you what actions are available.

```bash
source ./fft.sh
screen                       # check current state
execute_action EnterLocation # WorldMap → LocationMenu
execute_action EnterShop     # LocationMenu → SettlementMenu
execute_action Select        # SettlementMenu → OutfitterBuy (or Sell/Fitting)
```

On `SettlementMenu`, the shop type is exposed via `screen.UI` (e.g. `ui=Outfitter`, `ui=Tavern`). Use `execute_action CursorDown` / `CursorUp` to change which sub-action is highlighted, then `Select` to enter.

## Gil

Your current gil shows up in the screen line as `gil=2,605,569` whenever you're on a shop-adjacent screen (WorldMap, PartyMenuUnits, LocationMenu, SettlementMenu, Outfitter_*). Check before you buy.

## Leaving a shop

Shops open a farewell dialog ("Come back anytime") when you exit. The `Leave` action handles this automatically by sending Escape + Enter. Do NOT send a raw Escape — you'll be stuck on the farewell dialog.

```bash
execute_action Leave   # SettlementMenu → LocationMenu (handles farewell)
```

## Buying (OutfitterBuy)

Inside the Buy screen:

```bash
screen               # Shows row=N (currently highlighted item index)
execute_action ScrollDown  # Move cursor to next item
execute_action ScrollUp    # Move cursor to previous item
execute_action Select      # Purchase the highlighted item (opens quantity dialog)
execute_action Cancel      # Back to SettlementMenu
```

### Known gotcha: item names not yet surfaced

`screen.UI` doesn't currently show the item name at the highlighted row — only the row index (e.g. `row=2`). Matching row to item is a work in progress. For now, you'll need to look at the game screen to see what's highlighted.

**2026-04-14 update — what we DO know now:** item IDs in the game's roster memory use the **FFTPatcher canonical encoding (0-315)** — the same keys as `ColorMod/GameBridge/ItemData.cs`. Verified by reading live equipment off Ramza/Kenrick/Mustadio. No translation table is needed: read the u16, look up the ID directly. This unblocks dynamic shop-stock scanning — see `SHOP_ITEMS.md` "Dynamic Shop Stock — Next-Session Investigation Plan" for the concrete AoB-scan procedure using known item IDs as anchors.

### Purchase confirmation

After `Select`, the game opens a quantity selector and then a "Buy N for X gil?" confirmation modal. The modal detection isn't wired up yet (pending a memory scan), so you'll need to handle the confirmation manually for now.

## Selling (OutfitterSell)

Same shape as Buy — ScrollUp/ScrollDown, Select, Cancel. The list shows items you own that can be sold.

## Fitting (OutfitterFitting)

Same shape — ScrollUp/ScrollDown pick a character / slot / item depending on how deep you are in the picker; Select advances; Cancel goes back.

## Tavern (Rumors / Errands)

Reach the Tavern with one command from WorldMap:

```bash
enter_tavern               # WorldMap → LocationMenu → cursor to Tavern → EnterShop
```

Lands on the `Tavern` root screen (barkeep greeting, two options). From there:

```bash
read_rumor                 # Open Rumors list
read_rumor 2               # Open Rumors + scroll cursor to row 2 (0-based)
read_rumor "Zodiac Braves" # Look up a rumor body by title (exact or substring match)
read_rumor "Riovanes"      # Substring fallback — any distinctive phrase works
read_errand                # Open Errands list
read_errand 0              # Open Errands + land on the first errand
list_rumors                # Dump the 26-entry hardcoded rumor corpus
scan_tavern                # From TavernRumors/TavernErrands: count entries available
```

**What you CAN see today:**
- Navigate to any specific rumor or errand row with one command
- See the `[TavernRumors]` / `[TavernErrands]` state confirmed
- **Read rumor body text.** The bridge `get_rumor` action resolves via four tiers: exact title → body substring → `{locationId, unitIndex}` via `CityRumors` per-city row map → integer index. The shell `read_rumor` helper uses the first two tiers; direct JSON callers can use all four. The 26 brave-story rumors from `world_wldmes_bin.en.bin` are hardcoded into the mod (no external file ships) and returned as `{title, body}`.
- Know the count of entries via `scan_tavern` (caveat: depends on `cursorRow` being surfaced — currently not wired on TavernRumors, so `scan_tavern` reports "≥30" as a placeholder)

**What you CAN'T see today — errand bodies and a few rumor titles.**
- Errand metadata (quester / days / fee / reward) is NOT yet decoded — it likely lives in a separate NXD layout.
- A small class of rumor titles (e.g. "At Bael's End") are NOT in the current corpus. Searching RAM for UI title text has consistently failed — the titles are composed by UE4 Slate widgets from a text table we haven't located. For rumors in the corpus, pass the title (or any distinctive phrase) to `read_rumor` to pull the body directly.

## Still not implemented

- Warriors' Guild (Recruit / Rename / Dismiss)
- Poachers' Den (Process / Sell Carcasses)
- Save Game menu

Entering any of these will land you at `SettlementMenu` with the right `ui=` label, but no named sub-action ValidPaths yet — you'd have to navigate with raw Up/Down/Enter (strict mode permitting).

## Typical flow

```bash
source ./fft.sh
screen                         # confirm WorldMap, see gil
execute_action EnterLocation   # into the settlement's shop list
execute_action CursorDown      # scroll to Outfitter (if not already highlighted)
execute_action EnterShop       # into the Outfitter
execute_action Select          # into Buy (default cursor position)
execute_action ScrollDown      # scroll to desired item (check game screen for names)
execute_action Select          # open purchase dialog (you'll need to handle this manually today)
# ... when done ...
execute_action Cancel          # back to SettlementMenu
execute_action Leave           # back to LocationMenu (farewell handled)
execute_action Leave           # back to WorldMap
```

## Tips

- **Save before major purchases** — with `execute_action Save` (when implemented) or by visiting a Save Game option in a settlement.
- **Check gil first** — the gil in the screen line is your authoritative balance.
- **Don't mash Enter** — the `Select` → quantity → confirm flow has three distinct Enter presses; do them with `execute_action` so the screen state is verified between each.
- **Shop stock changes per chapter** — don't assume an item will still be there after story progress.
