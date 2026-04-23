using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure planner for the "escape to known state" pre-step of <c>battle_ability</c>.
    ///
    /// Problem: the submenu cursor (Attack/White Magicks/...) AND the ability-list
    /// cursor (Cure/Cura/...) inside a skillset are both UE4 widget heap bytes that
    /// REMEMBER the previously-selected index across open/close cycles within a single
    /// turn. If <c>battle_ability</c> tries to navigate via "blind Down×N from assumed
    /// idx 0" it lands on the wrong ability when the true starting index isn't 0.
    ///
    /// S55 spent two sessions trying AOB-based cursor reads; both failed structurally
    /// (widget offsets shuffle between allocations). See <c>project_ability_list_cursor_addr.md</c>
    /// "Option 3" — escape ALL the way back to BattleMyTurn before each ability nav.
    /// The widgets get fully reset on re-entry: submenu cursor returns to Attack(0),
    /// ability-list cursor returns to the first entry(0). From those known starting
    /// states, plain Down×submenuIdx and Down×abilityIdx work correctly.
    ///
    /// This class emits the key sequence to reach BattleMyTurn from whatever battle
    /// screen we're currently on. The sequence is then fired by the caller before
    /// the normal battle_ability flow enters the submenu.
    ///
    /// Known menu depths (shallowest to deepest):
    ///   BattleMyTurn / BattleActing  (depth 0 — already at known state)
    ///   BattleAbilities              (depth 1 — submenu open)
    ///   Battle&lt;Skillset&gt;        (depth 2 — ability list open)
    ///   BattleAttacking / BattleCasting (depth 3 — targeting mode; Cancel steps back through)
    ///
    /// Each depth step back costs one Escape.
    /// </summary>
    public static class BattleAbilityEntryReset
    {
        /// <summary>
        /// Returns the number of Escape keys to press to reach a known BattleMyTurn
        /// state from the given current screen. Returns 0 if already there (or if
        /// screen is null / unknown — caller should fall through to legacy behavior).
        /// </summary>
        public static int EscapeCountToMyTurn(string? screenName)
        {
            if (string.IsNullOrEmpty(screenName)) return 0;
            switch (screenName)
            {
                case "BattleMyTurn":
                case "BattleActing":
                    return 0;

                case "BattleAbilities":
                    return 1;

                // S59 live-play bug: battle_move from ui=Abilities sometimes
                // leaks into BattlePaused. One Escape closes the pause modal,
                // returning to BattleMyTurn.
                case "BattlePaused":
                    return 1;

                // S59: in-battle modals reached via action-menu slots. One
                // Escape returns to BattleMyTurn from each.
                case "BattleStatus":        // Status slot opens unit card
                case "BattleAutoBattle":    // Auto-battle slot opens sub-picker
                    return 1;

                // Any Battle<Skillset> list
                case "Battle_Mettle":
                case "Battle_Items":
                case "Battle_WhiteMagicks":
                case "Battle_BlackMagicks":
                case "Battle_TimeMagicks":
                case "Battle_Arcane":
                case "Battle_Summon":
                case "Battle_Mysticism":
                case "Battle_Draw_Out":
                case "Battle_Jump":
                case "Battle_Punch_Art":
                case "Battle_Steal":
                case "Battle_Charge":
                case "Battle_Aim":
                case "Battle_Throw":
                case "Battle_Math_Skill":
                case "Battle_Snipe":
                case "Battle_Sing":
                case "Battle_Dance":
                case "Battle_Elemental":
                case "Battle_Talk_Skill":
                case "Battle_Yin_Yang_Magicks":
                    return 2;

                // Targeting mode (mid-select) — Cancel from here goes back through
                // the ability list, then the submenu, then to BattleMyTurn.
                case "BattleAttacking":
                case "BattleCasting":
                    return 3;

                // Not a reset-able state (WorldMap, Cutscene, etc.): return 0 and
                // let the caller handle screen-state validation.
                default:
                    return 0;
            }
        }

        /// <summary>
        /// True if <paramref name="screenName"/> is a battle-menu screen that the
        /// escape-to-MyTurn reset can handle. False for non-battle screens, where
        /// the caller should reject the reset outright.
        /// </summary>
        public static bool IsResetableBattleScreen(string? screenName)
        {
            if (string.IsNullOrEmpty(screenName)) return false;
            return EscapeCountToMyTurn(screenName) >= 0
                && (screenName == "BattleMyTurn"
                    || screenName == "BattleActing"
                    || screenName == "BattleAbilities"
                    || screenName == "BattlePaused"
                    || screenName == "BattleStatus"
                    || screenName == "BattleAutoBattle"
                    || screenName == "BattleAttacking"
                    || screenName == "BattleCasting"
                    || IsBattleSkillsetList(screenName));
        }

        private static bool IsBattleSkillsetList(string screenName)
        {
            return screenName.StartsWith("Battle_");
        }

        /// <summary>
        /// Returns the list of actions this reset will perform as a sequence of
        /// "Escape" strings — useful for dry-run / logging / unit tests. The
        /// actual key-press firing happens in the caller.
        /// </summary>
        public static IReadOnlyList<string> PlanSequence(string? screenName)
        {
            int n = EscapeCountToMyTurn(screenName);
            var seq = new List<string>(n);
            for (int i = 0; i < n; i++) seq.Add("Escape");
            return seq;
        }
    }
}
