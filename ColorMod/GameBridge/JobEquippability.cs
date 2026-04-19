using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Reverse lookup over <see cref="WeaponEquippability"/> and
    /// <see cref="ArmorEquippability"/>. Given a job name, returns every
    /// equipment type the job can inherently equip (no Equip-* support
    /// ability).
    ///
    /// Use case: EquippableWeapons picker rendering — given the currently
    /// viewed unit's job, surface which items the unit COULD equip so the
    /// UI can flag mismatches. Also supports `availableWeapons[]` verbose
    /// catalog per TODO §10.6.
    /// </summary>
    public static class JobEquippability
    {
        /// <summary>
        /// All equipment types (weapons and armor) the job can inherently
        /// equip, without any Equip-* cross-class skill. Exclusion-list
        /// armors (Hat, Clothes) default to "allowed" for unknown jobs
        /// per the underlying table semantics — callers that want a strict
        /// whitelist should filter to <see cref="CharacterData.GetJobName"/>-known
        /// job names first.
        /// </summary>
        public static IReadOnlyCollection<string> GetEquipmentTypes(string job)
        {
            if (string.IsNullOrWhiteSpace(job))
                return System.Array.Empty<string>();

            var result = new List<string>();

            foreach (var weaponType in WeaponEquippability.AllWeaponTypes)
            {
                if (WeaponEquippability.CanJobEquip(job, weaponType))
                    result.Add(weaponType);
            }

            foreach (var armorType in ArmorEquippability.AllArmorTypes)
            {
                if (ArmorEquippability.CanJobEquip(job, armorType))
                    result.Add(armorType);
            }

            return result;
        }
    }
}
