namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Maps shopTypeIndex (from 0x140D435F0) to a human-readable shop name
    /// used as screen.UI on LocationMenu / SettlementMenu.
    ///
    /// Mapped live 2026-04-14 at Dorter (0-3). SaveGame (4) is a scaffold
    /// guess — needs live verification at Warjilis or any settlement with a
    /// Save Game option. Unknown values render as "Shop{N}" so a blank UI
    /// never leaves Claude without a handle.
    /// </summary>
    public static class ShopTypeLabels
    {
        public static string ForIndex(int shopTypeIndex) => shopTypeIndex switch
        {
            0 => "Outfitter",
            1 => "Tavern",
            2 => "WarriorsGuild",
            3 => "PoachersDen",
            4 => "SaveGame",  // TODO live-verify at a city with the SaveGame menu option (session 38: Warjilis/Yardrow don't expose it from LocationMenu)
            _ => $"Shop{shopTypeIndex}",
        };
    }
}
