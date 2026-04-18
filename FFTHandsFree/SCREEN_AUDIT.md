<!-- Battle-output audit. Scope: `screen` / `screen -v` on BattleMyTurn and
nested battle states. Scoring against 3-test principle from TODO.md §"What
Goes In Compact vs Verbose vs Nowhere". -->

# Battle Screen Output Audit — 2026-04-17

Design principle:
1. Would a human consult this? 2. Need HERE or can Claude navigate to it?
3. Missing = worse decision or extra round-trip? → surface.
Noise penalty: every field dilutes the others. Compact = tight. Verbose = liberal.

Source: `fft.sh` lines 2593–2694 (the BattleMyTurn branch that fires on `screen`
and `screen -v`).

---

## What Claude currently sees (compact)

```
[BattleMyTurn] ui=Move Ramza(Gallant Knight) (8,10) HP=604/604 MP=148/148

Abilities:
  Attack → (8,9) (7,10) (8,11)
  Focus → (8,10)<Ramza ALLY>
  Throw Stone → (8,6) (7,7) (8,7) (9,7) (6,8) ... 27 tiles ...
  Ultima ct=20 → (8,6) (7,7) ... 27 tiles ...
  Potion [x4] → (8,6) (7,7) ... 27 tiles ...
  (16 more item abilities with full 27-tile lists each)

Move tiles: (7,10 h=3) (8,11 h=4.5) ... 19 tiles ... — 19 tiles from (8,10) Mv=4 Jmp=4 enemies=3
Attack tiles: Up→(9,10)  Down→(7,10)  Left→(8,11)  Right→(8,9)
Facing: Face East — 3 front, 0 side, 0 back

Units:
  [ENEMY] (Black Goblin) (2,6) f=E HP=445/445 d=10
  [ENEMY] (Black Goblin) (1,9) f=N HP=440/440 d=8
  [ENEMY] (Black Goblin) (5,11) f=E HP=439/439 d=4
  [PLAYER] Ramza(Gallant Knight) (8,10) f=W HP=604/604 [Shell] *
  [PLAYER] Wilham(Monk) (10,10) f=W HP=477/477 d=2
```

## Problems

### 1. Ability target tiles explode on AoE-99 abilities — BIGGEST ISSUE
Items skillset (`Potion`, `X-Potion`, `Phoenix Down` etc.) have target=ally AoE=1
but the valid-target list is the full 27-tile spell range. Every Items entry
repeats 27 coord tuples. With ~18 items, that's **450+ coordinate tuples** of
noise.

**Fix**: compactor should already filter empty tiles for ally-target abilities
the same way it filters empty enemy tiles. Verify and extend if broken.
Target for Items: only show occupied-ally + self-tile (the tiles Claude actually
considers).

### 2. `Move tiles:` lists all 19 tiles inline
Human doesn't read these. Claude picks from the JSON array. Format is also
wide — wraps on narrow terminals.

**Fix**: drop from compact. Show only a summary: `Move: 19 tiles (Mv=4 Jmp=4)`.
Full list stays in verbose.

### 3. Enemy units show `(Black Goblin)` instead of `Black Goblin`
The current code wraps jobName in parens: `cl=u.jobName?'('+u.jobName+')':''`.
When there's no `name` (enemy case), the line reads `[ENEMY] (Black Goblin)`
— the empty string before the paren reads weirdly.

**Fix**: when `name` is empty, drop the parens around `jobName`.

### 4. `f=E` facing suffix for allies is low-value
Facing on your own units rarely drives a decision on your turn (you Wait AT the
end and auto-face there). Facing on ENEMIES drives backstab — keep those.
Showing `f=W` on all 4 of my own units every scan is noise.

**Fix**: show `f=X` only on non-active enemies (they're the only ones whose
facing the backstab logic consumes).

### 5. `[ENEMY] ... HP=445/445` repeats even when HP hasn't changed
Not a quick-fix — but worth noting: on most turns, enemy HP didn't change.
Showing full HP/MaxHP every scan is noisy vs. a delta indicator.

**Fix (later)**: track last-seen HP per unit; render `HP=445/445` first time,
then just `HP=445` unchanged or `HP=445 (-50)` when damaged since last scan.

### 6. `d=N` distance is surfaced for everyone but skipped for active
Useful for "how close is the nearest enemy" — keep.

### 7. Status lists like `[Shell]` are single-line inline — good.
No change.

### 8. `Attack tiles:` line
```
Attack tiles: Up→(9,10)  Down→(7,10)  Left→(8,11)  Right→(8,9)
```
When ALL 4 cardinals are empty (nobody adjacent), this line is 95% noise —
just cardinals labeling empty tiles. Useful ONLY when at least one cardinal
has an occupant.

**Fix**: drop line entirely when all 4 cardinals are empty. Render only if
at least one has an occupant.

### 9. `Facing: Face East — 3 front, 0 side, 0 back`
Only meaningful at end-of-turn (Wait decision). On a `screen` early in turn
planning, irrelevant until you decide to Wait.

**Fix**: show only when `BattleMyTurn` and after any move has been committed
(the wait-facing matters). Or keep but abbreviate: `Wait: E (3F/0S/0B)`.

### 10. `activeUnit` showing `HP=604/604` duplicates the Units block entry
Ramza is in the Units block AND in the header banner. Header banner is the
anchor — keep. Units block entry for the active unit (marked with `*`) is
redundant positionally but carries status tags and name.

Actually — the `*` marker is useful in the Units block to confirm which one
is active. Keep.

---

## Verbose mode issues

### 11. `PA=undefined MA=undefined Br=undefined Fa=undefined`
Already flagged last round. Fields not populated in the battle scan backend.
Shell prints literal `undefined`. Fix: skip when null.

### 12. Verbose unit line gets VERY long when many fields populate:
```
[PLAYER] Ramza(Gallant Knight) (8,10) f=W HP=604/604 PA=undefined MA=undefined Spd=11 CT=10 Br=undefined Fa=undefined R:Counter S:Reequip M:Jump +2 +abs:Earth ^str:Fire,Lightning,Ice,Earth [Shell] *
```
That's 200+ chars. Consider splitting: stat line / abilities line / affinities
line per unit when verbose.

---

## Punch list — ranked by ROI

| # | Action | Effort | Impact |
|---|--------|--------|--------|
| 1 | Filter empty tiles from ally-target abilities (Items skillset noise) | Small | **HUGE** — removes ~450 coord tuples per scan |
| 2 | Move `Move tiles:` list to verbose; compact shows just summary | Trivial | High |
| 3 | Suppress facing `f=X` for allies; keep for enemies | Trivial | Medium |
| 4 | Drop `Attack tiles:` line when all 4 cardinals are empty | Trivial | Medium |
| 5 | Fix `[ENEMY] (Black Goblin)` → `[ENEMY] Black Goblin` when name missing | Trivial | Small |
| 6 | Skip `undefined` fields in verbose unit line | Trivial | Medium |
| 7 | Defer `Facing: ...` line until after a move is committed | Small | Small |
| 8 | Add HP-delta to Units block (track last-seen) | Larger | Nice-to-have |
| 9 | Split long verbose unit line across 2-3 lines | Small | Nice-to-have |

**#1 is the biggest win** — the Items-spam is the single largest compact-output
bloat by far.

---

## Recommendation

Ship #1–#6 as one "battle render polish" commit. Defer #7–#9.
