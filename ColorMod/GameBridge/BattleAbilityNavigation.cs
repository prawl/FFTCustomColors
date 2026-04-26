using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure logic for finding abilities in battle menus.
    /// Maps ability names to their skillset and position within the skillset.
    /// </summary>
    public static class BattleAbilityNavigation
    {
        public struct AbilityLocation
        {
            public string skillsetName;
            public int indexInSkillset;
            public bool isSelfTarget;
            /// <summary>
            /// True for abilities like Focus/Shout (HRange=Self, AoE=1) that apply instantly.
            /// False for self-radius abilities like Chakra/Cyclone (HRange=Self, AoE>1) that
            /// show an AoE preview and need an extra confirmation.
            /// </summary>
            public bool isTrueSelfOnly;
            /// <summary>
            /// Cast Speed. 0 = instant action. &gt;0 = charge-time spell that queues in the
            /// Combat Timeline and resolves when its CT counter reaches 100. BattleAbility
            /// reports "Queued ..." for ct&gt;0 to signal the turn isn't over until Wait.
            /// </summary>
            public int castSpeed;
        }

        /// <summary>
        /// Find which skillset an ability belongs to and its position within that skillset.
        /// If availableSkillsets is provided, searches those first (matching the unit's actual skills).
        /// Falls back to searching all skillsets if not found in the available ones.
        /// Returns null if the ability is not found in any known skillset.
        /// </summary>
        public static AbilityLocation? FindAbility(string abilityName, string[]? availableSkillsets = null)
        {
            if (abilityName == "Attack")
            {
                return new AbilityLocation
                {
                    skillsetName = "Attack",
                    indexInSkillset = 0,
                    isSelfTarget = false,
                    castSpeed = 0
                };
            }

            // "Jump" is a synthetic collapsed entry from CollapseJumpAbilities.
            // In the game menu, selecting the first Jump ability (index 0) works
            // for any jump — the game uses the highest learned range automatically.
            if (abilityName == "Jump" && availableSkillsets?.Contains("Jump") == true)
            {
                return new AbilityLocation
                {
                    skillsetName = "Jump",
                    indexInSkillset = 0,
                    isSelfTarget = false,
                    castSpeed = 0
                };
            }

            // Numbered-family normalization: AbilityCompactor renders
            // `Aim +1`...`Aim +20` as a single `Aim (+1 to +20)` line and
            // the agent then types just `Aim`. Resolve to the lowest
            // available level (`Aim +1`). Live-flagged 2026-04-26 P3:
            // agent saw `Aim` in skillset list, called battle_ability
            // "Aim" → "not found" (Aim IS listed in error). 2026-04-26.
            string resolvedName = ResolveNumberedFamilyName(abilityName, availableSkillsets);

            // Search available skillsets first (unit's primary + secondary)
            if (availableSkillsets != null)
            {
                foreach (var skillsetName in availableSkillsets)
                {
                    var abilities = ActionAbilityLookup.GetSkillsetAbilities(skillsetName);
                    if (abilities == null) continue;
                    for (int i = 0; i < abilities.Count; i++)
                    {
                        if (abilities[i].Name == resolvedName)
                        {
                            bool isSelf = abilities[i].HRange == "Self";
                            return new AbilityLocation
                            {
                                skillsetName = skillsetName,
                                indexInSkillset = i,
                                isSelfTarget = isSelf,
                                isTrueSelfOnly = isSelf && abilities[i].AoE <= 1,
                                castSpeed = abilities[i].CastSpeed
                            };
                        }
                    }
                }
            }

            // Fall back to searching all skillsets (ability might be in an undetected secondary).
            // Skip skillsets already searched above to avoid false matches (e.g. synthetic "Jump").
            var searched = new HashSet<string>(availableSkillsets ?? System.Array.Empty<string>());
            foreach (var (skillsetName, abilities) in ActionAbilityLookup.AllSkillsets)
            {
                if (searched.Contains(skillsetName)) continue;
                for (int i = 0; i < abilities.Count; i++)
                {
                    if (abilities[i].Name == resolvedName)
                    {
                        bool isSelf = abilities[i].HRange == "Self";
                        return new AbilityLocation
                        {
                            skillsetName = skillsetName,
                            indexInSkillset = i,
                            isSelfTarget = isSelf,
                            isTrueSelfOnly = isSelf && abilities[i].AoE <= 1,
                            castSpeed = abilities[i].CastSpeed
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Resolve a numbered-family alias like `Aim` or `Aim (+1 to +20)`
        /// to the lowest-level concrete ability name (`Aim +1`) that
        /// exists in any available skillset. Returns the input unchanged
        /// if no family match. Live-flagged 2026-04-26 P3 playtest.
        /// </summary>
        public static string ResolveNumberedFamilyName(
            string requestedName, string[]? availableSkillsets)
        {
            if (string.IsNullOrEmpty(requestedName)) return requestedName;
            // Strip the "(+1 to +20)" suffix if present.
            string baseName = requestedName;
            int parenIdx = requestedName.IndexOf(" (+");
            if (parenIdx > 0) baseName = requestedName.Substring(0, parenIdx);
            // If the literal name already matches an ability, don't rewrite.
            if (NameExistsInSkillsets(requestedName, availableSkillsets))
                return requestedName;
            // Look for a concrete `<base> +N` family in available skillsets.
            string? bestMatch = null;
            int bestNum = int.MaxValue;
            void TryMatch(IReadOnlyList<ActionAbilityInfo> abilities)
            {
                foreach (var a in abilities)
                {
                    if (!a.Name.StartsWith(baseName + " +")) continue;
                    var suffix = a.Name.Substring(baseName.Length + 2).Trim();
                    if (int.TryParse(suffix, out int n) && n < bestNum)
                    {
                        bestMatch = a.Name;
                        bestNum = n;
                    }
                }
            }
            if (availableSkillsets != null)
            {
                foreach (var ss in availableSkillsets)
                {
                    var abilities = ActionAbilityLookup.GetSkillsetAbilities(ss);
                    if (abilities != null) TryMatch(abilities);
                }
            }
            if (bestMatch != null) return bestMatch;
            // Fall back: search ALL skillsets.
            foreach (var (_, abilities) in ActionAbilityLookup.AllSkillsets)
                TryMatch(abilities);
            return bestMatch ?? requestedName;
        }

        private static bool NameExistsInSkillsets(string name, string[]? skillsets)
        {
            if (skillsets == null) return false;
            foreach (var ss in skillsets)
            {
                var abilities = ActionAbilityLookup.GetSkillsetAbilities(ss);
                if (abilities == null) continue;
                foreach (var a in abilities)
                    if (a.Name == name) return true;
            }
            return false;
        }

        /// <summary>
        /// Find the index of a skillset in the Abilities submenu items.
        /// Returns -1 if not found.
        /// </summary>
        public static int FindSkillsetIndex(string skillsetName, string[] submenuItems)
        {
            for (int i = 0; i < submenuItems.Length; i++)
            {
                if (submenuItems[i] == skillsetName)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Returns the effective menu cursor position, correcting for stale memory reads.
        /// After battle_move: game shows Abilities (1) but memory reads 0.
        /// After battle_ability (no move): game shows Move (0) but memory reads 1.
        /// When disabled (DontAct etc): Abilities slot is greyed and skipped — if memory
        /// reads 1, the visible cursor is actually on Wait (2).
        /// </summary>
        public static int EffectiveMenuCursor(int memoryCursor, bool moved, bool acted, bool disabled = false)
        {
            // DontAct (and other action-blocking statuses) gray out the
            // Abilities slot. The visible action-menu cursor auto-skips
            // greyed slots, so when memory says "Abilities (1)" the visible
            // cursor is actually on Wait (2). Without this correction,
            // battle_wait's Down press overshoots into Status — live-flagged
            // 2026-04-26 playtest #9. Takes priority over moved/acted
            // corrections (disable trumps stale-byte rules).
            if (disabled && memoryCursor == 1)
                return 2;
            if (moved && !acted && memoryCursor == 0)
                return 1; // after move, game is at Abilities, memory is stale
            if (acted && !moved && memoryCursor == 1)
                return 0; // after ability-only, game is at Move, memory is stale
            return memoryCursor;
        }
    }
}
