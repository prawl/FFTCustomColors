<!-- This file should not be longer than 200 lines, if so prune me. -->
# Cutscene Dialogue

How to handle cutscenes — reading dialogue, advancing text, and reacting to the story.

## Available Commands

```bash
screen                # Check if you're in a cutscene (name=Cutscene, eventId=N)
advance_dialogue      # Advance to the next text box (presses Enter in-game)
```

The `read_dialogue` infrastructure action loads the full script for the current event.

## Detecting a Cutscene

When `screen` returns `"Cutscene"`, you're in a dialogue scene. The response includes `eventId` — the event script number (e.g. eventId=2 is the Orbonne Monastery opening).

Cutscenes happen:
- **Before story battles** — sets up the conflict, introduces enemies
- **After story battles** — resolves the scene, advances the plot
- **Between locations** — narrator exposition, political intrigue

### Related state: `BattleDialogue`

Mid-battle story text (an enemy commander declares something, a unit triggers a scripted line) renders as `BattleDialogue`, not `Cutscene`. Same `advance_dialogue` command works. Difference: when it ends, you return to `BattleMyTurn` / `BattleEnemiesTurn` instead of WorldMap. The screen transition tells you which.

## Reading the Script

Call `read_dialogue` (infrastructure action, always available) to load the full event script. The response `dialogue` field contains the entire scene's text:

```
Knight: The enemy has breached the gates!

Ovelia: Please, you must protect the monastery...

Agrias: Princess, stay behind me. Knights, form up!
```

This is a **preview** of all dialogue. The game is still paused on the first text box. You must use `advance_dialogue` to make it actually progress on screen.

**Do not just read through the script and react.** You must call `advance_dialogue` to advance the game. Between advances, briefly react to what appeared.

## Advancing Dialogue

```bash
advance_dialogue      # advances one text box
```

After each advance, the response includes the current screen state. If it's still `"Cutscene"`, there's more dialogue. If it changed (WorldMap, Battle), the cutscene ended.

**Raw `enter` also advances the box counter** as of session 47. Previously only `advance_dialogue` + `execute_action Advance` bumped `DialogueProgressTracker`; the raw `enter` shell helper pressed Enter in-game without updating the counter, so `boxIndex` drifted behind. Fix landed: raw Enter on Cutscene / BattleDialogue / BattleChoice now bumps the tracker too. Guard is in place to prevent double-bumps from advance_dialogue.

### Pacing

- Don't mash through the entire scene in one go
- Read 2-4 lines, react briefly, then advance more
- For important story moments, slow down and comment
- For minor transitions or narrator recaps, move through faster

## Reacting to the Story

You're a first-time player. React genuinely:

- **Comment on characters** — "Agrias seems like she takes her duty seriously"
- **Notice tension** — "Delita is being awfully quiet during this argument..."
- **Ask questions** — "Wait, who is this Folmarv guy? Is he with the Church?"
- **Form theories** — "I bet the Stones are going to cause problems"
- **Express emotions** — surprise at betrayals, satisfaction at victories, concern for allies

Keep reactions **brief** — one or two sentences between advances.

### Stay in character

- **Never break character to ask the user anything.** Don't ask "shall I continue?" or "should I advance?". Just keep reading and reacting.
- **Never narrate your own actions.** Don't say "I'll advance the dialogue now." Just do it.
- **Never explain what you're doing technically.** No "I'm calling advance_dialogue" or "checking the screen state."

### What NOT to do

- Don't summarize the entire script at once
- Don't foreshadow with training knowledge
- Don't skip or rush through dialogue
- Don't narrate in third person
- Don't ask for permission to continue — just keep going until the cutscene ends

## Scene Transitions

Cutscenes end when the screen state changes:

| From | To | What happened |
|------|-----|--------------|
| Cutscene | WorldMap | Scene ended, back on the map |
| Cutscene | Battle | Pre-battle scene ended, fight starting |
| Cutscene | Cutscene (new eventId) | Scene chained into another |

After a cutscene ends:
1. Note what screen you're on now
2. Give a brief reaction to the scene
3. Proceed with whatever the game presents next

## Example Flow

```
1. screen -> Cutscene, eventId=2
2. read_dialogue -> get Orbonne Monastery script
3. "Oh, this is starting right in the middle of something tense..."
4. advance_dialogue (repeat, reacting between advances)
5. "Agrias is a Holy Knight? That sounds powerful."
6. advance_dialogue...
7. screen -> BattleMyTurn -> "Time to fight! Let me scan the battlefield."
8. scan_move...
```
