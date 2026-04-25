namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Resolve the in-battle Abilities-submenu row added by a Support
    /// ability, if any. IC remaster has exactly two such abilities
    /// (per AbilityData.cs descriptions + 2026-04-25 live verification):
    ///   Reequip        → "Reequip"
    ///   Evasive Stance → "Evasive Stance"
    /// The IC remaster's Abilities submenu shows the ABILITY NAME as
    /// the menu row label (not the action it triggers — descriptions
    /// like "Adds the Defend command" describe the IN-GAME EFFECT, but
    /// the menu row text is the ability name). Live-verified at
    /// Siedge Weald 2026-04-25 with both abilities.
    ///
    /// All other Support abilities are passive (stat boosts, equipment
    /// unlocks, etc.) and don't add a menu row.
    ///
    /// Caller appends the resolved name to GetAbilitiesSubmenuItems
    /// after the Attack / primary / secondary entries so the submenu
    /// row count and cursor labeling match the in-game menu.
    /// </summary>
    public static class SupportAbilityBattleCommand
    {
        public static string? Resolve(string? supportAbilityName)
        {
            if (string.IsNullOrWhiteSpace(supportAbilityName)) return null;
            return supportAbilityName switch
            {
                "Reequip" => "Reequip",
                "Evasive Stance" => "Evasive Stance",
                _ => null,
            };
        }
    }
}
