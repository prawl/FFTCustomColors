using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Characterization tests for the BattleUnitState.WeaponTag contract:
    /// whatever shape NavigationActions.CollectUnitPositionsFull picks for
    /// the weapon-tag string must stay equivalent to ItemData.ComposeWeaponTag.
    /// These tests pin the contract at the helper layer so a future refactor
    /// can't silently drift the shape (e.g. drop the "onHit:" prefix or add
    /// a leading space) without a visible test failure.
    /// </summary>
    public class BattleUnitStateWeaponTagTests
    {
        [Fact]
        public void BasicWeapon_ProducesNameOnlyTag()
        {
            // Broadsword (19) has no AttackEffects — tag is just the name.
            var tag = ItemData.ComposeWeaponTag(new List<int> { 19 });
            Assert.Equal("Broadsword", tag);
        }

        [Fact]
        public void ChaosBladeRamzaLoadout_ProducesOnHitTag()
        {
            // god_ramza's current loadout: Chaos Blade + Sortilege + Grand Helm +
            // Maximillian + Escutcheon strong. ComposeWeaponTag must find Chaos
            // Blade first (weapon slot is scanned first in the equipment list)
            // and return the on-hit-stripped form.
            var ramza = new List<int> { 37, 239, 156, 185, 143 };
            var tag = ItemData.ComposeWeaponTag(ramza);
            Assert.Equal("Chaos Blade onHit:chance to add Stone", tag);
        }

        [Fact]
        public void UnarmedPlayer_ProducesEmptyTag()
        {
            // Null equipment — the scan pipeline treats this as unarmed.
            // The shell Units-list renderer skips the tag entirely when
            // weaponTag is null/empty.
            Assert.Equal("", ItemData.ComposeWeaponTag(null));
            Assert.Equal("", ItemData.ComposeWeaponTag(new List<int>()));
        }

        [Fact]
        public void OnlyNonWeaponEquipment_ProducesEmptyTag()
        {
            // Shields/helms/armor — no weapon in the list.
            var armorOnly = new List<int> { 130, 156, 185 };
            Assert.Equal("", ItemData.ComposeWeaponTag(armorOnly));
        }

        [Fact]
        public void WeaponTagShape_StartsWithWeaponName()
        {
            // Invariant: the tag always starts with the weapon's canonical name,
            // never with "[" or "onHit:" or any decorator. The shell render
            // wraps it in brackets itself: `[{weaponTag}]`.
            Assert.StartsWith("Chaos Blade", ItemData.ComposeWeaponTag(new List<int> { 37 }));
            Assert.StartsWith("Broadsword", ItemData.ComposeWeaponTag(new List<int> { 19 }));
            Assert.StartsWith("Dagger", ItemData.ComposeWeaponTag(new List<int> { 1 }));
        }

        [Fact]
        public void WeaponTagShape_NoLeadingOrTrailingSpaces()
        {
            // The shell concatenates ` [${weaponTag}]` — a leading/trailing
            // space in the tag would look like `[ Chaos Blade]` which is ugly.
            var tag = ItemData.ComposeWeaponTag(new List<int> { 37 });
            Assert.Equal(tag.Trim(), tag);
        }
    }
}
