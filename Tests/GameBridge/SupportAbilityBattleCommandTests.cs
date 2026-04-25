using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Two Support abilities in IC remaster add an extra command row to
    /// the in-battle Abilities submenu:
    ///   Reequip       → "Reequip" command (change equipment mid-turn)
    ///   Evasive Stance → "Defend" command
    /// Live-verified 2026-04-25 — Ramza S:Reequip showed Reequip as the
    /// 4th row (after Attack / Mettle / Items); user confirmed
    /// Evasive Stance behaves identically with Defend.
    ///
    /// Caller (CommandWatcher.GetAbilitiesSubmenuItems) consults the
    /// active unit's Support slot and appends the resolved command to
    /// the submenu items list when non-null.
    /// </summary>
    public class SupportAbilityBattleCommandTests
    {
        [Fact]
        public void Resolve_Reequip_AddsReequipCommand()
        {
            Assert.Equal("Reequip",
                SupportAbilityBattleCommand.Resolve("Reequip"));
        }

        [Fact]
        public void Resolve_EvasiveStance_AddsEvasiveStanceRow()
        {
            // AbilityData.cs:169 description says "Adds the Defend
            // command" — that's the in-game EFFECT. The MENU ROW LABEL
            // in IC remaster is the ability name itself. Live-verified
            // 2026-04-25 Siedge Weald: Ramza S:Evasive Stance showed
            // "Evasive Stance" as the 4th submenu row, not "Defend".
            Assert.Equal("Evasive Stance",
                SupportAbilityBattleCommand.Resolve("Evasive Stance"));
        }

        [Fact]
        public void Resolve_PassiveSupport_ReturnsNull()
        {
            // Stat boosts / equipment unlocks add NO menu command.
            Assert.Null(SupportAbilityBattleCommand.Resolve("Equip Heavy Armor"));
            Assert.Null(SupportAbilityBattleCommand.Resolve("Equip Swords"));
            Assert.Null(SupportAbilityBattleCommand.Resolve("Magick Defense Boost"));
            Assert.Null(SupportAbilityBattleCommand.Resolve("JP Boost"));
            Assert.Null(SupportAbilityBattleCommand.Resolve("Concentration"));
            // Throw Items modifies the existing Items command (range +4)
            // but doesn't add a new menu row.
            Assert.Null(SupportAbilityBattleCommand.Resolve("Throw Items"));
        }

        [Fact]
        public void Resolve_NullOrEmpty_ReturnsNull()
        {
            Assert.Null(SupportAbilityBattleCommand.Resolve(null));
            Assert.Null(SupportAbilityBattleCommand.Resolve(""));
            Assert.Null(SupportAbilityBattleCommand.Resolve("   "));
        }

        [Fact]
        public void Resolve_UnknownAbility_ReturnsNull()
        {
            Assert.Null(SupportAbilityBattleCommand.Resolve("Some Made Up Ability"));
        }
    }
}
