# Transcript - Mandalia Plain Playtest (2026-04-26)

## Open

OK, fresh-eyes time. Game is sitting on BattleMoving in Mandalia Plain — Brigands' Den, classic story battle. Cursor at ui=(2,4). Looks like a previous Claude left this in move-tile selection mode. My job is to play this out, mix moves/attacks/abilities/items, and watch for the four P1 regressions: Mv=0/Jp=0 softlock, jobId stale (Mettle vs Arts of War), phantom died/joined burst, and state.units residue from prior battles.

Plan: scan first to get the lay of the land, then probably commit a move from the BattleMoving state we're already in.

## Orient

`fft_full screen` from BattleMoving gives me a JSON with **only 2 units** in the units list (Ramza alive, one Lv8 dead enemy at -1,-1). Suspicious. battleWon=true also reads weird — there's no way a Mandalia battle is won with this little engagement.

Cancel back to BattleMyTurn. First Escape didn't reach the game (just got an "Escape" key receipt but state still BattleMoving). Second Escape took. Now I have a real scan.

Knight Ramza Lv8 at (2,4), HP 393/393. Job is **Knight** — that's the morning's "Mettle vs Arts of War" P1 fix concern. jobId=2 = Knight. Good. Status: [Regen, Protect, Shell] — strong. Probably from previous turn's setup.

Then I see something gnarly: `[ENEMY] (0,0) f=S HP=8192/288 d=6 R:Regenerate S:EXP Boost [Confuse,Regen,Slow,Stop]`. **HP=8192 vs MaxHP=288.** That's a phantom slot — 0,0 is a classic uninit/sentinel position, and the four-status stack of [Confuse, Regen, Slow, Stop] is wildly improbable. The hp > maxHp by 28× is the dead giveaway. Logged P1.

Real enemies look like the standard Mandalia open-field set: 2 Chocobos (one alive at (1,4), one at (4,7)), 1 Goblin at (4,4), and a dead enemy at (3,4). That's a *really* late-stage state — most of the map cleared already? Maybe the previous Claude got far. There's no Algus listed, which is weird because Mandalia's "Brigands' Den" objective normally includes him as a guest.

Anyway, Chocobo at (1,4) is in attack range right next to me. d=1. Let me poke it. Knight should be able to one-shot a 64hp chocobo with Attack.

## Attack 1 — narrator says KO, ground truth says MISS

`fft battle_attack 1 4` returned `Attacked (1,4) from (2,4) — KO'd! (64→0/64)`. Confident as hell. Re-scan immediately: Chocobo at (1,4) HP=64/64. **Liar.** [ACTED] flag set so my turn was consumed though.

Reading the logs: `Pre-attack HP=64/64`, `Post-attack: live HP=0 static=64 chose=0`, `Still on BattleAttacking post-animation (miss re-targeting); sending Escape`, then `KO'd! 64→0`. The bridge has TWO signals — live=0 (suggests kill) and "still on BattleAttacking" (signals re-targeting after a miss) — and picks the kill signal even when it contradicts everything else. This is the phantom-success pattern from my memory.

## battle_wait → 50-line phantom event burst

`fft battle_wait` ends my turn. Should be quick. Goblin moves once, maybe attacks me. Should generate ~3 narrator lines.

Instead I get a >50-line cascade:
- 8x repeats of "Chocobo revived → lost Dead → took 4 damage → died"
- Cross-attribution ("Chocobo recovered 313 HP" — that's Ramza's HP, applied to a chocobo)
- Cross-attribution ("Chocobo gained Regen, Protect, Shell" — those are Ramza's buffs)
- Phantom event ("Knight died" — my unit is alive at full HP)
- Placeholder labels ("`(unit@3,4)` (ENEMY) joined at (3,4)" — name backfill failed)
- Bizarre move ("Chocobo moved (4,6) → (3,4)" — (4,6) was Ramza's position before he moved)

This is the "phantom died/joined" P1 from this morning's commit 88d41ad. **NOT FIXED.**

## Cache poison: Ramza is now "Chocobo"

Post-burst scan: active unit is `Chocobo (2,4) HP=381/393 [Regen,Protect,Shell] *`. Same coords as Ramza, same HP, same buffs, but JOB IS CHOCOBO. jobId=0 in JSON. Skills list still shows Mettle abilities (Focus, Rush, Salve, Shout etc) — proves under the hood it's still Ramza, but the surface label is wrong.

Logs reveal the cause: `Cache hit (2,4) → Chocobo (no heap match; pos+stats fallback)`. The RosterMatchCache fell back to (pos+stats) for identity and the (2,4)-keyed cache had a leftover Chocobo from somewhere. **The morning's RosterMatchCache fix is supposed to key by NameId — but the fallback path still poisons.**

## Trying Focus on cached-as-Chocobo Ramza

`fft battle_ability "Focus"` → `failed: Skillset 'Mettle' not in submenu: Attack`. Submenu has only "Attack" because the bridge thinks this is a chocobo. Escape, retry — submenu now reads "Attack, Arts of War" — and then `failed: Skillset 'Mettle' not in submenu: Attack, Arts of War`. So the bridge knows Focus is in skillset "Mettle", but the live submenu uses "Arts of War", and the alias map doesn't bridge them. Two of the morning's fixes failing simultaneously.

## Move resets identity

I move to (3,3). Now scan shows `[PLAYER] Knight (3,3) f=N HP=381/393 [Regen,Protect,Shell] *` — back to Knight! So the cache is per-position, and a fresh position re-runs identification correctly. (Until the next time it lookup-misses and falls back.)

## Attack 2 (the goblin)

Long sequence: stuck on BattleMoving for ~30s after the first attack KO'd the chocobo (which... wait, did it? See above — narrator unreliable). Bridge sends Escapes that don't land. `return_to_my_turn` errors out. Eventually `enter` jolts state forward.

Goblin moves to (3,2), I attack from rear. Bridge says `KO'd! (75→0/80)`. Same log pattern as before (`live=0 static=75 chose=0`, `still on BattleAttacking; sending Escape`). But this time the kill is REAL — battle ends.

So the same log pattern produces both real KOs and phantom KOs. The classifier really has no way to tell.

## Brief Desertion mis-detect

After the killing blow, screen briefly reports `[BattleDesertion]`. Detection inputs: `battleMode=2, gameOverFlag=1, encA=2, encB=2`. Several seconds later it cleanly transitions to WorldMap. Brief mis-detect during transition.

## Wrap

Battle is won. Phantom (0,0) Lv32 HP=8192/288 vanished from scans once the battle truly ended. WorldMap clean.

Findings written to feedback.md.

