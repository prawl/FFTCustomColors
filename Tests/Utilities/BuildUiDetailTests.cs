using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.Utilities
{
    /// <summary>
    /// Unit tests for <see cref="CommandWatcher.BuildUiDetail"/> — the pure
    /// lookup that populates the `uiDetail` payload when the EqA cursor is on
    /// a specific slot. Focused on the 2026-04-15 session 17 extended
    /// ItemInfo fields (attributeBonuses, equipmentEffects, attackEffects,
    /// canDualWield, canWieldTwoHanded, element). These tests lock in the
    /// round-trip from ItemData.cs → UiDetail so a typo or field rename
    /// surfaces immediately in CI rather than in a live session.
    ///
    /// Hero items covered: Ragnarok (Knight Sword, Auto-Shell, Dual+Two),
    /// Excalibur (Holy element, Auto-Haste), Chaos Blade (on-hit Stone),
    /// Bracer (PA+3 only), Reflect Ring (Auto-Reflect only), Chantage
    /// (Perfume, permanent effects). A catch-all case confirms unpopulated
    /// items still return a valid UiDetail with nulls.
    /// </summary>
    public class BuildUiDetailTests
    {
        // col=0 is the Equipment column on EquipmentAndAbilities; all
        // equipment-slot rows use that column. row is slot-specific but
        // BuildUiDetail doesn't consult it for equipment (it looks up by
        // name), so we pass 0.
        private const int EquipmentCol = 0;
        private const int AnyRow = 0;

        [Fact]
        public void Ragnarok_ExposesAutoShellAndWeaponFlags()
        {
            var d = CommandWatcher.BuildUiDetail("Ragnarok", EquipmentCol, AnyRow);
            Assert.NotNull(d);
            Assert.Equal("Ragnarok", d!.Name);
            Assert.Equal("Knight's Sword", d.Type);
            Assert.Equal(24, d.Wp);
            Assert.Equal("Auto-Shell", d.EquipmentEffects);
            Assert.True(d.CanDualWield);
            Assert.True(d.CanWieldTwoHanded);
        }

        [Fact]
        public void Excalibur_CarriesHolyElement()
        {
            var d = CommandWatcher.BuildUiDetail("Excalibur", EquipmentCol, AnyRow);
            Assert.NotNull(d);
            Assert.Equal("Holy", d!.Element);
            Assert.Equal("Auto-Haste", d.EquipmentEffects);
        }

        [Fact]
        public void ChaosBlade_CarriesAttackEffect()
        {
            var d = CommandWatcher.BuildUiDetail("Chaos Blade", EquipmentCol, AnyRow);
            Assert.NotNull(d);
            Assert.Equal("Auto-Regen", d!.EquipmentEffects);
            Assert.NotNull(d.AttackEffects);
            Assert.Contains("Stone", d.AttackEffects);
        }

        [Fact]
        public void Bracer_PaBoostOnly()
        {
            var d = CommandWatcher.BuildUiDetail("Bracer", EquipmentCol, AnyRow);
            Assert.NotNull(d);
            Assert.Equal("PA+3", d!.AttributeBonuses);
            Assert.Null(d.EquipmentEffects);
            Assert.Null(d.Element);
        }

        [Fact]
        public void ReflectRing_AutoReflect()
        {
            var d = CommandWatcher.BuildUiDetail("Reflect Ring", EquipmentCol, AnyRow);
            Assert.NotNull(d);
            Assert.Equal("Auto-Reflect", d!.EquipmentEffects);
            Assert.Null(d.AttributeBonuses);
        }

        [Fact]
        public void Chantage_PerfumeLasingEffects()
        {
            var d = CommandWatcher.BuildUiDetail("Chantage", EquipmentCol, AnyRow);
            Assert.NotNull(d);
            Assert.NotNull(d!.EquipmentEffects);
            Assert.Contains("Reraise", d.EquipmentEffects);
            Assert.Contains("Regen", d.EquipmentEffects);
        }

        [Fact]
        public void AegisShield_MaBonusAndEvades()
        {
            var d = CommandWatcher.BuildUiDetail("Aegis Shield", EquipmentCol, AnyRow);
            Assert.NotNull(d);
            Assert.Equal("MA+1", d!.AttributeBonuses);
            Assert.Equal(10, d.Pev);
            Assert.Equal(50, d.Mev);
        }

        [Fact]
        public void IceShield_ElementalEffectsSurfaced()
        {
            var d = CommandWatcher.BuildUiDetail("Ice Shield", EquipmentCol, AnyRow);
            Assert.NotNull(d);
            Assert.Contains("Ice", d!.EquipmentEffects);
            Assert.Contains("Fire", d.EquipmentEffects);
        }

        [Fact]
        public void Ribbon_ImmuneStatus()
        {
            var d = CommandWatcher.BuildUiDetail("Ribbon", EquipmentCol, AnyRow);
            Assert.NotNull(d);
            Assert.Contains("status", d!.EquipmentEffects, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void LordlyRobe_ProtectShell()
        {
            var d = CommandWatcher.BuildUiDetail("Lordly Robe", EquipmentCol, AnyRow);
            Assert.NotNull(d);
            Assert.Contains("Protect", d!.EquipmentEffects);
            Assert.Contains("Shell", d.EquipmentEffects);
        }

        [Fact]
        public void UnpopulatedItem_ReturnsValidDetailWithNulls()
        {
            // Defender is a Knight Sword that was NOT populated with the
            // extended fields in the first pass — verifies the record still
            // renders (just without the extended decorations).
            var d = CommandWatcher.BuildUiDetail("Iron Sword", EquipmentCol, AnyRow);
            Assert.NotNull(d);
            Assert.Equal("Iron Sword", d!.Name);
            Assert.Equal(6, d.Wp);
            Assert.Null(d.AttributeBonuses);
            Assert.Null(d.EquipmentEffects);
            Assert.Null(d.AttackEffects);
            Assert.Null(d.Element);
            Assert.False(d.CanDualWield);
        }

        [Fact]
        public void UnknownItem_ReturnsNameOnly()
        {
            var d = CommandWatcher.BuildUiDetail("Not A Real Item", EquipmentCol, AnyRow);
            Assert.NotNull(d);
            Assert.Equal("Not A Real Item", d!.Name);
            Assert.Null(d.Type);
            Assert.Equal(0, d.Wp);
        }
    }
}
