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
   - `BattleTurns.md` — turn flow, scan output (NOTE: read the
     "Multi-unit party turn-cycling" section)
   - `AbilitiesAndJobs.md` — ability slot model, JP, learned vs equipped
   - `WorldMapNav.md` — out of today's scope but explains screen vocabulary
   - `Shopping.md` — out of scope but more vocabulary
   - `CutsceneDialogue.md` — out of scope but more vocabulary
   Use a `Glob` pass on the directory to make sure you don't miss any
   that get added later.

Then `source ./fft.sh` once.

## Starting state

The user has placed you in an active battle at **The Siedge Weald**
with a **multi-unit party**: Ramza, Kenrick, Lloyd, Wilham (4 player
units) vs 5 enemies (2 Archers, Knight, Summoner, Time Mage). Your
first action is `screen` to see what you're working with. The game is
on Windows, running via Reloaded-II. The bridge is single-threaded —
wait for each command's response before sending the next.

Each player unit takes their own turn in CT order. After
`battle_wait` / `execute_turn`, the next `BattleMyTurn` may belong to
a different unit. Pay attention to who's active.

## Scope: BATTLE ONLY

You're playing battle. **No** world map travel, **no** shops, **no**
party menu drilling, **no** job changes. If the battle ends (Victory or
GameOver), do not start a new battle, retry, or load a save. Write your
final feedback for what you observed and exit.

## Time budget — 30 minutes wall-clock

Self-monitor with `date +%s`. Capture the start time on your first
shell command.

- T+0..25 — play
- T+5, T+10, T+15, T+20 — append a one-line timestamped beat to
  `play.md` describing what you're doing and any friction
- T+25 — stop wherever you are. Begin writing `feedback.md`
- T+30 — hard exit, return summary to parent

If you sense context pressure before T+25 (long scan dumps piling up,
many bridge calls), stop early and write feedback. Better short and
complete than long and truncated. Soft cap: ~80 bridge commands.

## Recording — write as you go

### `playtest_logs/2026-04-25_2245/play.md`

Free-form append-only log. One or two lines per beat. Capture friction
in the moment, not after the fact. The file is pre-stubbed — append at
the bottom.

### `playtest_logs/2026-04-25_2245/feedback.md`

Final report. Pre-stubbed with seven questions. Fill the `>` blockquotes
during T+25..30.

## Constraints

- **Strict mode ON the whole time.** Use the helpers documented in
  Commands.md — never raw `up`/`down`/`enter`/etc. If a helper doesn't
  exist for what you want, that's a friction point — note it.
- **No code edits.** No `Edit` / `Write` to source files. The only
  files you write are `play.md` and `feedback.md`.
- **No commits, no PRs, no git operations.**
- **Don't spawn agents** — you are the agent.
- **Don't invoke skills** (no `/handoff`, `/loop`, `/schedule`, etc.)
- **Don't `restart`** unless the game has actually crashed. If it
  crashes, log the trigger to `play.md` as a major friction.
- **No spoilers** — react as a first-time player.
- **Don't ask the parent for guidance** — operate solo. If genuinely
  stuck, log the situation and recover (e.g. `battle_flee`).

## Allowed

- All bridge helpers from Commands.md
- `screenshot_crop.ps1` if you want a visual reference
- `logs` / `session_tail` / `session_stats` for self-diagnostics
- Reading any file in the repo (but not memory)

## Output back to parent

When you return (at T+30 or on early exit), include:

1. One paragraph: how did it go?
2. Paths to `play.md` and `feedback.md`
3. Top three friction items extracted from feedback.md

Don't paste full log contents — they're on disk for the parent to read.

## Be honest

This isn't a performance review of your play. We don't care if you win
or lose. We care about: was the bridge clear, was the screen output
useful, did anything surprise you in a bad way, were the docs right? If
something felt wrong, write it down. If something felt great, write
that too.

Good luck.
