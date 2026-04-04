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

---

## Claude Intelligence Levels

Control how smart Claude plays by varying how much game knowledge it has access to. Not about nerfing inputs — about controlling the context window.

**Beginner (Blind Playthrough)**
- No wiki data, no damage formulas, no job guides
- Claude discovers mechanics organically: "Wait, this enemy just healed itself??"
- Makes genuine mistakes, learns from them
- Most entertaining to watch — authentic first-time player experience
- Story reactions are real, not performative

**Normal (Casual Fan)**
- Basic mechanics: what each job does, how damage roughly works, element weaknesses
- Knows ability names and general effects but doesn't optimize
- Plays like someone who read the manual but not a strategy guide
- Makes reasonable decisions but misses advanced combos

**Expert (Min-Maxer)**
- Full wiki dump: damage formulas, zodiac compatibility, speed values, AI patterns
- Knows optimal job builds, ability synergies, enemy weaknesses
- Plays like a veteran on their 5th playthrough
- Could include FFHacktics community knowledge, speedrun strats

**Implementation:**
- Separate context files per difficulty: `context_beginner.md`, `context_normal.md`, `context_expert.md`
- Loaded into the system prompt or CLAUDE.md before each session
- Story knowledge is ALWAYS "new player" regardless of level — never spoil the plot
- Could be mixed per system: Expert battle tactics + Beginner story reactions
- Could escalate mid-game: start Beginner, unlock Normal after Chapter 1, Expert after Chapter 3
- The knowledge files would include/exclude: damage formulas, job unlock trees, enemy bestiary, ability tier lists, positioning guides, zodiac tables
