<!-- This file should not be longer than 200 lines, if so prune me. -->
# Shopping

How to enter a settlement, navigate its shops, and buy/sell items.

## Overview

Settlements (ID 0-14: Lesalia, Riovanes, Eagrose, Lionel, Limberry, Zeltennia, Gariland, Yardrow, Gollund, Dorter, Zaland, Goug, Warjilis, Bervenia, Sal Ghidos) have shops. Battlegrounds don't.

The screen flow when you enter a settlement:

```
WorldMap ──EnterLocation──► LocationMenu ──EnterShop──► SettlementMenu ──Select──► Outfitter_Buy / Outfitter_Sell / Outfitter_Fitting
   ▲                             │                            │                            │
   │                             │                            │                            │
   │                          Leave                         Leave                        Cancel
   │                             │                            │                            │
   └─────────────────────────────┴────────────────────────────┴────────────────────────────┘
```

**State names:**
- `LocationMenu` — the shop-list inside a settlement (Outfitter / Tavern / Warriors' Guild / Poachers' Den / Save Game).
- `SettlementMenu` — inside a chosen shop, at the sub-action selector (e.g. Buy / Sell / Fitting for an Outfitter).
- `Outfitter_Buy`, `Outfitter_Sell`, `Outfitter_Fitting` — the per-sub-action item list.

Tavern / Warriors' Guild / Poachers' Den sub-actions aren't mapped yet — they report as `SettlementMenu` when selected.

## Commands

All actions go through `execute_action <name>`. The current screen's ValidPaths list tells you what actions are available.

```bash
source ./fft.sh
screen                       # check current state
execute_action EnterLocation # WorldMap → LocationMenu
execute_action EnterShop     # LocationMenu → SettlementMenu
execute_action Select        # SettlementMenu → Outfitter_Buy (or Sell/Fitting)
```

On `SettlementMenu`, the shop type is exposed via `screen.UI` (e.g. `ui=Outfitter`, `ui=Tavern`). Use `execute_action CursorDown` / `CursorUp` to change which sub-action is highlighted, then `Select` to enter.

## Gil

Your current gil shows up in the screen line as `gil=2,605,569` whenever you're on a shop-adjacent screen (WorldMap, PartyMenu, LocationMenu, SettlementMenu, Outfitter_*). Check before you buy.

## Leaving a shop

Shops open a farewell dialog ("Come back anytime") when you exit. The `Leave` action handles this automatically by sending Escape + Enter. Do NOT send a raw Escape — you'll be stuck on the farewell dialog.

```bash
execute_action Leave   # SettlementMenu → LocationMenu (handles farewell)
```

## Buying (Outfitter_Buy)

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

### Purchase confirmation

After `Select`, the game opens a quantity selector and then a "Buy N for X gil?" confirmation modal. The modal detection isn't wired up yet (pending a memory scan), so you'll need to handle the confirmation manually for now.

## Selling (Outfitter_Sell)

Same shape as Buy — ScrollUp/ScrollDown, Select, Cancel. The list shows items you own that can be sold.

## Fitting (Outfitter_Fitting)

Same shape — ScrollUp/ScrollDown pick a character / slot / item depending on how deep you are in the picker; Select advances; Cancel goes back.

## Not yet implemented

- Tavern (Rumors / Errands)
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
