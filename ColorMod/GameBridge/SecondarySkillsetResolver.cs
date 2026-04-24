using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// S60: resolve the active unit's secondary skillset from a scan result.
    ///
    /// The `SecondaryAbility` byte reads 0 transiently in the IC Remaster
    /// (live-confirmed S59 repro on Ramza's second turn — byte toggled
    /// between 6/Items and 0/null between scans). Without persistence the
    /// downstream `GetAbilitiesSubmenuItems` returns `[Attack, Mettle]`
    /// and the battle_ability helper can't find "Items" in the submenu
    /// when Claude tries to use Phoenix Down / Potion / etc.
    ///
    /// Resolution order:
    ///   1. Byte reads &gt; 0 → trust the byte (highest confidence).
    ///   2. Byte == 0 but abilities[] list contains a non-primary entry →
    ///      infer the secondary from that entry's skillset.
    ///   3. Neither works → preserve the previous cache value (don't blank
    ///      on a transient 0-read; the secondary probably didn't change).
    /// </summary>
    public static class SecondarySkillsetResolver
    {
        /// <summary>
        /// Returns the new cache value for _cachedSecondarySkillset.
        /// </summary>
        /// <param name="byteResolvedSecondary">Non-null when SecondaryAbility byte &gt; 0
        ///   and the caller has mapped it to a skillset name. Null when byte read 0.</param>
        /// <param name="abilityNames">Names from the active unit's scanned abilities
        ///   list (may be null or empty).</param>
        /// <param name="primarySkillset">Active unit's primary skillset (so the
        ///   inference can skip it when looking for a non-primary entry).</param>
        /// <param name="previousCache">The last-known-good _cachedSecondarySkillset.
        ///   Used as the fallback when the current scan can't confirm anything.</param>
        /// <param name="getSkillsetForAbility">Lookup delegate
        ///   (ActionAbilityLookup.GetSkillsetForAbility in production).</param>
        public static string? Resolve(
            string? byteResolvedSecondary,
            IReadOnlyList<string>? abilityNames,
            string? primarySkillset,
            string? previousCache,
            Func<string, string?> getSkillsetForAbility)
        {
            if (!string.IsNullOrEmpty(byteResolvedSecondary))
                return byteResolvedSecondary;

            if (abilityNames != null && abilityNames.Count > 0
                && !string.IsNullOrEmpty(primarySkillset)
                && getSkillsetForAbility != null)
            {
                foreach (var abilityName in abilityNames)
                {
                    if (string.IsNullOrWhiteSpace(abilityName)) continue;
                    var set = getSkillsetForAbility(abilityName);
                    if (!string.IsNullOrEmpty(set) && set != primarySkillset)
                        return set;
                }
            }

            // Transient 0 byte + no inferable ability → preserve prior cache.
            return previousCache;
        }
    }
}
