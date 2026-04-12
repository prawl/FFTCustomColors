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
                    isSelfTarget = false
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
                    isSelfTarget = false
                };
            }

            // Search available skillsets first (unit's primary + secondary)
            if (availableSkillsets != null)
            {
                foreach (var skillsetName in availableSkillsets)
                {
                    var abilities = ActionAbilityLookup.GetSkillsetAbilities(skillsetName);
                    if (abilities == null) continue;
                    for (int i = 0; i < abilities.Count; i++)
                    {
                        if (abilities[i].Name == abilityName)
                        {
                            return new AbilityLocation
                            {
                                skillsetName = skillsetName,
                                indexInSkillset = i,
                                isSelfTarget = abilities[i].HRange == "Self"
                            };
                        }
                    }
                }
            }

            // Fall back to searching all skillsets only if no available skillsets were specified
            if (availableSkillsets == null)
            {
                foreach (var (skillsetName, abilities) in ActionAbilityLookup.AllSkillsets)
                {
                    for (int i = 0; i < abilities.Count; i++)
                    {
                        if (abilities[i].Name == abilityName)
                        {
                            return new AbilityLocation
                            {
                                skillsetName = skillsetName,
                                indexInSkillset = i,
                                isSelfTarget = abilities[i].HRange == "Self"
                            };
                        }
                    }
                }
            }

            return null;
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
        /// After battle_move or battle_ability, the game auto-advances the cursor to
        /// Abilities (1) but memory at 0x1407FC620 still reads 0.
        /// </summary>
        public static int EffectiveMenuCursor(int memoryCursor, bool menuCursorStale)
        {
            if (menuCursorStale && memoryCursor == 0)
                return 1; // game is at Abilities, memory is stale
            return memoryCursor;
        }
    }
}
