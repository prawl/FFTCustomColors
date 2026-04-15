using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Decides whether the player's gil should be surfaced on a given screen.
    /// Shown on shop-adjacent and purchase-decision screens (where affordability
    /// informs a decision); hidden elsewhere to keep responses lean during
    /// combat turns.
    /// </summary>
    public static class ShopGilPolicy
    {
        private static readonly HashSet<string> GilScreens = new()
        {
            "WorldMap",         // deciding where to travel/shop
            "PartyMenu",        // equipment browsing leads to shop decisions
            "LocationMenu",     // at a settlement, picking a shop
            "SettlementMenu",   // inside a settlement, picking Buy/Sell/Fitting
            "OutfitterBuy",
            "OutfitterSell",
            "OutfitterFitting",
        };

        public static bool ShouldShowGil(string screenName) =>
            GilScreens.Contains(screenName);
    }
}
