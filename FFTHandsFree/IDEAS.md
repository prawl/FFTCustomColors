# FFT Hands-Free — Ideas

## Smart Auto-Battle Mod (Release Candidate)

FFT already has a built-in "Auto-Battle" feature but the AI is notoriously bad — it wastes MP on weak spells, walks into AoE, ignores positioning, heals full-HP units, and generally makes the player cringe.

**Idea:** Replace the auto-battle AI with Claude. Package it as a standalone mod that players can download and use. When the player hits Auto-Battle, instead of the dumb built-in AI, Claude takes over and plays each turn intelligently.

**Why this could work:**
- Solves a real pain point — everyone hates FFT's auto-battle AI
- Scoped release — don't need world map nav, party management, story reading
- Just battle automation: scan units, pick targets, move, attack, wait
- Players still control the story, party builds, equipment — Claude just fights
- Could be a toggle: "Classic Auto-Battle" vs "Smart Auto-Battle (Claude)"

**What it needs:**
- The C+Up unit scanning + grid movement we already built
- Battle decision engine: threat assessment, target priority, positioning
- Hook into the Auto-Battle button press to trigger Claude instead of built-in AI
- Run without Claude Code CLI — would need a local LLM or API key setup
- Package as a Reloaded-II mod with config UI

**Open questions:**
- Would players accept needing an API key (costs money) for a mod?
- Could a small local model (like a fine-tuned Llama) handle FFT tactics?
- Should it explain its reasoning in a chat overlay? ("Attacking the Black Mage first because they're charging Holy")
- How to handle the latency of LLM calls during battle? Pre-compute while enemies animate?
- Could we train a lightweight model specifically on FFT battle data instead of using a general LLM?
