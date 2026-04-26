using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Bundled turn intent for the `execute_turn` bridge action. Claude sends
    /// one command with the full plan (optional move, optional ability, then
    /// wait); the bridge expands it into the existing sub-actions
    /// (battle_move / battle_ability / battle_wait). Cuts ~6 round trips per
    /// turn to 1. Pure conversion — no game reads.
    ///
    /// TODO §1 Tier 5 "execute_turn action". Session 47.
    /// </summary>
    public class TurnPlan
    {
        /// <summary>Target x,y for the move step. Null → no move.</summary>
        public int? MoveX { get; set; }
        public int? MoveY { get; set; }

        /// <summary>Ability name (e.g. "Attack", "Cure", "Shout"). Null → no action.</summary>
        public string? AbilityName { get; set; }

        /// <summary>Target x,y for the ability. Null for self-target abilities (Shout/Chakra).</summary>
        public int? TargetX { get; set; }
        public int? TargetY { get; set; }

        /// <summary>Optional facing direction for wait (N/S/E/W). Null → auto-pick.</summary>
        public string? Direction { get; set; }

        /// <summary>Skip the trailing battle_wait. Lets callers inject state checks.</summary>
        public bool SkipWait { get; set; }

        /// <summary>
        /// Expand this plan into an ordered list of sub-actions. Pure function —
        /// no game state consulted. Dispatch layer runs each step with the
        /// existing battle_* handlers.
        /// </summary>
        public IEnumerable<TurnStep> ToSteps()
        {
            bool hasMove = MoveX.HasValue && MoveY.HasValue;
            bool hasAbility = !string.IsNullOrEmpty(AbilityName);

            if (hasMove)
            {
                yield return new TurnStep
                {
                    Action = "battle_move",
                    X = MoveX!.Value,
                    Y = MoveY!.Value,
                };
            }

            if (hasAbility)
            {
                var step = new TurnStep
                {
                    Action = "battle_ability",
                    AbilityName = AbilityName,
                };
                if (TargetX.HasValue && TargetY.HasValue)
                {
                    step.X = TargetX.Value;
                    step.Y = TargetY.Value;
                    step.HasTarget = true;
                }
                yield return step;
            }

            // Move-only-as-reposition: per Commands.md "DOES NOT END THE
            // TURN" + BattleTurns.md, `execute_turn 6 5` (just two args)
            // should leave the unit at (6,5) with Act/Wait still
            // available — caller will rescan and decide their action
            // from the new position. battle_wait is appended only when
            // there's an actual action (ability) OR the plan is
            // genuinely empty (intentional skip-the-turn). SkipWait
            // overrides everything.
            if (SkipWait) yield break;
            bool moveOnly = hasMove && !hasAbility;
            if (moveOnly) yield break;

            yield return new TurnStep
            {
                Action = "battle_wait",
                Direction = Direction,
            };
        }
    }

    /// <summary>
    /// One step of an expanded <see cref="TurnPlan"/>. Shape mirrors the
    /// existing battle_* command request fields: <see cref="Action"/>
    /// names the handler, <see cref="X"/>/<see cref="Y"/> carry the target
    /// tile, <see cref="AbilityName"/> carries the ability name for
    /// battle_ability steps, <see cref="Direction"/> carries the optional
    /// facing for battle_wait.
    /// </summary>
    public class TurnStep
    {
        public string Action { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public bool HasTarget { get; set; }
        public string? AbilityName { get; set; }
        public string? Direction { get; set; }
    }
}
