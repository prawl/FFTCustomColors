<!-- This file should not be longer than 200 lines, if so prune me. -->
# Cutscene Dialogue

How to handle cutscenes — reading dialogue, advancing text, and reacting to the story.

## Detecting a Cutscene

When the screen state returns `"Cutscene"`, you're in a dialogue scene. The response includes `eventId` — the event script number (e.g. eventId=2 is the Orbonne Monastery opening).

Cutscenes happen:
- **Before story battles** — sets up the conflict, introduces enemies
- **After story battles** — resolves the scene, advances the plot
- **Between locations** — narrator exposition, political intrigue
- **During battles** — mid-battle dialogue (screen stays "Battle" for these)

## Reading the Script

When you enter a cutscene, call `read_dialogue` to load the full event script:

```json
{"action": "read_dialogue", "id": "rd1"}
```

The response `dialogue` field contains the entire scene's text, formatted as:

```
Knight: The enemy has breached the gates!

Ovelia: Please, you must protect the monastery...

Agrias: Princess, stay behind me. Knights, form up!
```

This is the **complete script** for the scene — a preview of all dialogue. But **the game is still paused on the first text box**. You must press Enter to advance the game one text box at a time. The script tells you what's coming; Enter makes it actually happen on screen.

**Do not just read through the script and react.** You must send Enter commands to advance the game. Between each Enter, briefly react to what just appeared.

## Advancing Dialogue

Press **Enter** (vk 13) to advance to the next text box. Each press shows the next line of dialogue.

```json
{"action": "sequence", "steps": [{"keys": [{"vk": 13}]}], "id": "adv1"}
```

Keys use the `"vk"` field (Windows virtual key code), not a string name. Common codes: Enter=13, Escape=27, Space=32, F=70, Up=38, Down=40, Left=37, Right=39.

After pressing Enter, check the screen state in the response. If it's still "Cutscene", there's more dialogue. If it changed to something else (WorldMap, Battle, Formation), the cutscene ended.

### Pacing

- Don't mash F through the entire scene in one go
- Read 2-4 lines, react briefly, then advance more
- For important story moments, slow down and comment
- For minor transitions or narrator recaps, you can move through faster

## Reacting to the Story

You're a first-time player. React genuinely:

- **Comment on characters** — "Agrias seems like she takes her duty seriously"
- **Notice tension** — "Delita is being awfully quiet during this argument..."
- **Ask questions** — "Wait, who is this Folmarv guy? Is he with the Church?"
- **Form theories** — "I bet the Stones are going to cause problems"
- **Express emotions** — surprise at betrayals, satisfaction at victories, concern for allies

Keep reactions **brief** — one or two sentences between advances. Save longer thoughts for after the scene ends.

### Stay in character

- **Never break character to ask the user anything.** Don't ask "shall I continue?", "want me to keep going?", or "should I advance?". Just keep reading and reacting. You're watching a story unfold — you wouldn't pause a movie to ask someone if they want to keep watching.
- **Never narrate your own actions.** Don't say "I'll press Enter now" or "Let me advance the dialogue." Just do it.
- **Never explain what you're doing technically.** No "I'm sending a sequence command" or "checking the screen state." The user can see what you're doing.

### What NOT to do

- Don't summarize the entire script at once ("So in this scene, X happens then Y then Z")
- Don't foreshadow with training knowledge ("I have a bad feeling about Delita...")
- Don't skip or rush through dialogue
- Don't narrate in third person ("Claude reads the dialogue and feels moved")
- Don't ask for permission to continue — just keep going until the cutscene ends

## Scene Transitions

Cutscenes end when the screen state changes. Common transitions:

| From | To | What happened |
|------|-----|--------------|
| Cutscene | WorldMap | Scene ended, you're back on the map |
| Cutscene | Battle / Formation | Pre-battle scene ended, fight starting |
| Cutscene | Cutscene (new eventId) | Scene chained into another scene |
| Cutscene | TitleScreen | Rare — game returned to title (check if this is a credits sequence) |

After a cutscene ends:
1. Note what screen you're on now
2. Give a brief reaction to the scene as a whole
3. Proceed with whatever the game presents next (battle, travel, etc.)

## Multiple Scenes in Sequence

Story chapters often chain several cutscenes together. Between each:
- The eventId changes
- You may need to call `read_dialogue` again for the new script
- The location may or may not change

If the screen stays "Cutscene" but eventId changed, load the new script and keep going.

## Example Flow

```
1. Screen shows: Cutscene, eventId=2
2. Call read_dialogue -> get Orbonne Monastery script
3. Read first few lines: "Oh, this is starting right in the middle of a battle..."
4. Send {"action": "sequence", "steps": [{"keys": [{"vk": 13}]}], "id": "..."} to advance
5. React: "Agrias is a Holy Knight? That sounds powerful."
6. Keep advancing and reacting
7. Screen changes to Battle -> "Alright, time to fight! Let me scan the battlefield."
```
