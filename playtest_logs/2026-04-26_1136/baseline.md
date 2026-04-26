# Baseline snapshot — 2026-04-26_1136

Ninth playtest. Fresh battle this time — playtest #7 + #8 both ran on
the Lloyd-last-stand seed which produced GameOver in 1 action. This
seed gives the agent a full healthy party with enemies far off so they
can exercise multi-turn play, scan→move→ability→wait cycles, ally
positioning, etc.

## Bridge log offsets
- live_log.txt — 217819 bytes / 2346 lines

## Battle: The Siedge Weald (loc 26) — full party fresh start

```
[BattleMyTurn] ui=Move Lloyd(Chemist) (10,9) HP=432/432 MP=79/79
  primary=Items secondary=Time Magicks

  [PLAYER] Ramza(Gallant Knight) (8,10) HP=719/719 [Regen,Protect,Shell]
  [PLAYER] Kenrick(Dragoon)        (9,9)  HP=626/626
  [PLAYER] Lloyd(Chemist)          (10,9) HP=432/432 *active*
  [PLAYER] Wilham(Samurai)         (9,8)  HP=528/528
  [ENEMY] Archer (2,4) HP=472/472 d=13
  [ENEMY] Archer (3,4) HP=475/475 d=12
  [ENEMY] Summoner (0,5) HP=333/333 d=14
  [ENEMY] Time Mage (1,4) HP=345/345 d=14 [Charging]
  [ENEMY] (4,4) HP=458/458 d=11 R:Nature's Wrath
```

Lloyd as Chemist (Items + Time Magicks) — has Potion/Hi-Potion/X-Potion
+ Phoenix Down + status cures + Time Magicks utility. Distance 11+ from
all enemies, party still in formation. Exercises full battle surface.

## Fixes shipped between playtest #8 and this playtest

The agent shouldn't be told these — fresh-eyes test.

1. **ui= now first key=value field after the screen bracket**, on
   battle AND world screens. Previously battle did `[Screen] Unit ui=X`
   and world did `[Screen] loc=N ui=X`. Both flipped to `[Screen] ui=X
   Unit ...` / `[Screen] ui=X loc=N ...` so the decision surface (which
   menu / mode am I in) sits at a fixed first-position regardless of
   screen. Bash + C# formatter + ScreenCompactFormatterTests updated.

That's the only behavioral fix this round — playtest #8 dev ran short
because the seed was effectively unwinnable. This run validates the
reorder + provides much more bridge surface area to exercise so we can
collect deeper friction signal.

## Current state — confirmed live

```
[BattleMyTurn] ui=Move Lloyd(Chemist) (10,9) HP=432/432 ...
                ^^^^^^^^ first after the bracket
```

Confirmed by `screen` against the live battle.
