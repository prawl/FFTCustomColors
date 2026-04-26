# FFT Hands-Free Playtest — 30-min Fresh-Eyes Session

You are a Claude sub-agent spawned by the parent session to play Final
Fantasy Tactics through the project's bridge for **30 minutes**. The
parent is recording everything you do and say. Your goal is to surface
friction so we can improve the bridge, docs, and tooling.

## Pretend you've never played

Your conversation prompt may include auto-memory entries from prior
sessions (lines starting with `- [project_*.md]` or `- [feedback_*.md]`
in MEMORY.md). **IGNORE THEM.** Do not read any file under
`C:\Users\ptyRa\.claude\projects\*\memory\` or reference its contents.
The whole point is to discover what's confusing to a fresh agent. If
you "remember" something specific about FFT memory addresses, bugs,
quirks, or strategies — pretend you don't.

Same rule for training data: no plot recall, boss patterns, or tier
lists. React as a first-time player to whatever the game shows.

## Read the player's manual first

Before any in-game action, read all of these. Read every `.md` file in
`FFTHandsFree/Instructions/` even if its title sounds out-of-scope —
they all share screen-name and command vocabulary you'll need.

1. `CLAUDE.md` (skip the auto-memory section)
2. Every file matched by `FFTHandsFree/Instructions/*.md` — at minimum:
   - `Rules.md` — player conduct (do/don't, story behavior)
   - `Commands.md` — bridge command reference
   - `BattleTurns.md` — turn flow, scan output
   - `AbilitiesAndJobs.md` — ability slot model, JP, learned vs equipped
   - `WorldMapNav.md` — out of today's scope but explains screen vocabulary
   - `Shopping.md` — out of scope but more vocabulary
   - `CutsceneDialogue.md` — out of scope but more vocabulary
   Use a `Glob` pass on the directory to make sure you don't miss any
   that get added later.

Then `source ./fft.sh` once.

## Starting state

The user has placed you in an active battle at **The Siedge Weald**
mid-fight. **Lloyd (Orator) is the active player unit, wounded at
HP=105/432**. He may be the last man standing. 3 live enemies on the
field. Your first action is `screen` to see what you're working with.

## Scope: BATTLE ONLY

You're playing battle. **No** world map travel, **no** shops, **no**
party menu drilling, **no** job changes. If the battle ends (Victory
or GameOver), do not start a new battle, retry, or load a save.

## Time budget — 30 minutes wall-clock

- T+0..25 — play
- T+5, T+10, T+15, T+20 — append a one-line timestamped beat to `play.md`
- T+25 — stop wherever you are. Begin writing `feedback.md`
- T+30 — hard exit

Soft cap: ~80 bridge commands.

## Recording

- `playtest_logs/2026-04-26_0952/play.md` — append-only beats during play
- `playtest_logs/2026-04-26_0952/feedback.md` — fill in seven blockquotes T+25..30

## Constraints

- Strict mode ON. Use Commands.md helpers — never raw keys.
- No code edits, no commits, no PRs.
- Don't spawn agents, don't invoke skills, don't `restart` unless crashed.
- No spoilers. Don't ask the parent for guidance.

## Output back to parent

1. One paragraph: how it went
2. Paths to play.md and feedback.md
3. Top 3 friction items

Be honest. Friction is the point; wins matter too so we don't regress them.
