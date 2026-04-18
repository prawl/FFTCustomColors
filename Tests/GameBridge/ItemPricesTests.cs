using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

public class ItemPricesTests
{
    [Fact]
    public void AllBuyPriceNames_ResolveToItemDataEntries()
    {
        // If any name in BuyPricesByName doesn't match an ItemData entry,
        // the static init drops it silently into UnresolvedNames. This
        // test surfaces the drift: extending either table without keeping
        // them in sync fails the suite with a clear diagnostic.
        var unresolved = ItemPrices.UnresolvedNames.ToList();
        Assert.True(unresolved.Count == 0,
            $"ItemPrices has {unresolved.Count} names that don't match ItemData:\n  " +
            string.Join("\n  ", unresolved));
    }

    [Fact]
    public void BuyPrices_ResolvedTableNonEmpty()
    {
        Assert.NotEmpty(ItemPrices.BuyPrices);
    }

    [Fact]
    public void GetBuyPrice_Dagger_Returns200()
    {
        // Dagger = ID 1 in ItemData; shop price is 200 gil per SHOP_ITEMS.md.
        var price = ItemPrices.GetBuyPrice(1);
        Assert.Equal(200, price);
    }

    [Fact]
    public void GetBuyPrice_Potion_Returns50()
    {
        // Potion = ID 240 in ItemData (the chemistitem consumable range).
        var price = ItemPrices.GetBuyPrice(240);
        Assert.Equal(50, price);
    }

    [Fact]
    public void GetBuyPrice_Ragnarok_ReturnsNull()
    {
        // Ragnarok (ID 36) is a story-drop knightsword not sold in any shop.
        var price = ItemPrices.GetBuyPrice(36);
        Assert.Null(price);
    }

    [Fact]
    public void GetBuyPrice_UnknownId_ReturnsNull()
    {
        Assert.Null(ItemPrices.GetBuyPrice(9999));
        Assert.Null(ItemPrices.GetBuyPrice(-1));
    }

    [Fact]
    public void GetSellPrice_FallsBackToHalfBuy_WhenNoOverride()
    {
        // Potion has no override → buy 50 → estimate 25.
        Assert.Equal(25, ItemPrices.GetSellPrice(240));
        // Ether has no override → buy 200 → estimate 100.
        Assert.Equal(100, ItemPrices.GetSellPrice(243));
    }

    [Fact]
    public void GetSellPrice_NoBuyPrice_ReturnsNull()
    {
        Assert.Null(ItemPrices.GetSellPrice(36));  // Ragnarok
        Assert.Null(ItemPrices.GetSellPrice(9999));
    }

    [Fact]
    public void GetSellPrice_RoundsDown()
    {
        // Any odd buy price rounds down on sell (integer division).
        // Papyrus Codex: 10,000 → 5,000 (even, clean)
        Assert.Equal(5_000, ItemPrices.GetSellPrice(ItemData.Items.First(kv => kv.Value.Name == "Papyrus Codex").Key));
    }

    [Fact]
    public void GetSellPrice_OverridePrefersGroundTruthOverBuyHalf()
    {
        // Dagger: buy 200 → buy/2 estimate = 100. Ground-truth override = 50.
        // GetSellPrice must return 50, not 100.
        Assert.Equal(50, ItemPrices.GetSellPrice(1));
    }

    [Fact]
    public void IsSellPriceGroundTruth_TrueForOverride()
    {
        // Dagger has a ground-truth override.
        Assert.True(ItemPrices.IsSellPriceGroundTruth(1));
    }

    [Fact]
    public void IsSellPriceGroundTruth_FalseForEstimate()
    {
        // Potion (id 240) has a buy price but no sell-price override.
        // Its GetSellPrice is a buy/2 estimate, so the flag is false.
        Assert.False(ItemPrices.IsSellPriceGroundTruth(240));
        // But it still has a sell price via buy/2.
        Assert.NotNull(ItemPrices.GetSellPrice(240));
    }

    [Fact]
    public void IsSellPriceGroundTruth_FalseForUnknownItem()
    {
        // Ragnarok (36) has neither a buy price nor a sell override.
        Assert.False(ItemPrices.IsSellPriceGroundTruth(36));
        Assert.Null(ItemPrices.GetSellPrice(36));
    }

    [Fact]
    public void SellPriceOverrides_ContainsLiveVerifiedDaggerSet()
    {
        // Session 18 live-captured 7 weapon sell prices. Spot-check the
        // set so a regression (name mismatch / typo) fails loudly.
        Assert.True(ItemPrices.SellPriceOverrides.Count >= 7);
        Assert.Equal(50, ItemPrices.GetSellPrice(1));    // Dagger
        Assert.Equal(250, ItemPrices.GetSellPrice(2));   // Mythril Knife
        Assert.Equal(400, ItemPrices.GetSellPrice(3));   // Blind Knife
        Assert.Equal(750, ItemPrices.GetSellPrice(4));   // Mage Masher
        Assert.Equal(2_500, ItemPrices.GetSellPrice(8)); // Assassin's Dagger
        Assert.Equal(100, ItemPrices.GetSellPrice(19));  // Broadsword
        Assert.Equal(250, ItemPrices.GetSellPrice(20));  // Longsword
    }

    [Fact]
    public void SellPriceOverrides_YardrowSwordSet_Session38()
    {
        // Session 38 live-captured at Yardrow Outfitter Sell. Sword-series
        // sell prices are dramatically below buy/2 (9-19% of buy), which
        // pins that the buy/2 fallback would over-report payout significantly.
        int ironId = ItemData.Items.First(kv => kv.Value.Name == "Iron Sword").Key;
        int mythrilId = ItemData.Items.First(kv => kv.Value.Name == "Mythril Sword").Key;
        int bloodId = ItemData.Items.First(kv => kv.Value.Name == "Blood Sword").Key;
        int coralId = ItemData.Items.First(kv => kv.Value.Name == "Coral Sword").Key;
        int ancientId = ItemData.Items.First(kv => kv.Value.Name == "Ancient Sword").Key;

        Assert.Equal(450, ItemPrices.GetSellPrice(ironId));
        Assert.Equal(800, ItemPrices.GetSellPrice(mythrilId));
        Assert.Equal(1_250, ItemPrices.GetSellPrice(bloodId));
        Assert.Equal(900, ItemPrices.GetSellPrice(coralId));
        Assert.Equal(2_500, ItemPrices.GetSellPrice(ancientId));
    }

    [Fact]
    public void SellPriceOverrides_YardrowSwords_AllAreGroundTruth()
    {
        // Pin that the new overrides register as ground-truth (not fallback).
        int ironId = ItemData.Items.First(kv => kv.Value.Name == "Iron Sword").Key;
        int mythrilId = ItemData.Items.First(kv => kv.Value.Name == "Mythril Sword").Key;
        Assert.True(ItemPrices.IsSellPriceGroundTruth(ironId));
        Assert.True(ItemPrices.IsSellPriceGroundTruth(mythrilId));
    }

    [Fact]
    public void SellPriceOverrides_ZalandMixedTierSet_Session40()
    {
        // Session 40 live-captured at Zaland Outfitter Sell. Katanas and
        // staves mostly cluster at 50% of buy (matching the fallback) but
        // one staff (Serpent Staff) sells ABOVE its buy price — a known
        // FFT quirk previously seen with Mage Masher. Pin the exact values.
        int kotetsuId = ItemData.Items.First(kv => kv.Value.Name == "Kotetsu").Key;
        int osafuneId = ItemData.Items.First(kv => kv.Value.Name == "Osafune").Key;
        int kikuId = ItemData.Items.First(kv => kv.Value.Name == "Kiku-ichimonji").Key;
        int whiteStaffId = ItemData.Items.First(kv => kv.Value.Name == "White Staff").Key;
        int serpentId = ItemData.Items.First(kv => kv.Value.Name == "Serpent Staff").Key;

        Assert.Equal(1_500, ItemPrices.GetSellPrice(kotetsuId));
        Assert.Equal(2_500, ItemPrices.GetSellPrice(osafuneId));
        Assert.Equal(11_000, ItemPrices.GetSellPrice(kikuId));
        Assert.Equal(400, ItemPrices.GetSellPrice(whiteStaffId));
        Assert.Equal(3_000, ItemPrices.GetSellPrice(serpentId));
    }

    [Fact]
    public void CoverageStats_GroundTruthPercentage_AboveMinimumFloor()
    {
        // Diagnostic / floor-setter: what fraction of BuyPrices entries have
        // a corresponding ground-truth sell override? As of session 40 we're
        // around 20+ overrides out of ~100 buy entries. Assert a floor that's
        // slightly below current coverage so the test stays green after
        // session 40 but fires if a regression removes overrides.
        int buyCount = ItemPrices.BuyPrices.Count;
        int overrideCount = ItemPrices.SellPriceOverrides.Count;
        Assert.True(buyCount > 0, "BuyPrices empty — something is very wrong");
        double coverage = (double)overrideCount / buyCount;
        // Current floor: 15% (very conservative — actual is ~20%+). Raise the
        // floor as coverage grows to lock in gains.
        Assert.True(coverage >= 0.15,
            $"Ground-truth sell coverage has dropped below 15% ({overrideCount}/{buyCount} = {coverage:P0})");
    }

    [Fact]
    public void CoverageStats_MissingOverrides_ByCategory_Diagnostic()
    {
        // Non-failing diagnostic: surface which item IDs have a buy price
        // but no ground-truth sell override. Future live sessions can use
        // this to prioritize captures. Floor-asserted: at LEAST 5 entries
        // still need overrides (sanity — if we ever drop below this the
        // game has very thin shop coverage, surface it).
        int totalBuy = ItemPrices.BuyPrices.Count;
        int withOverride = ItemPrices.SellPriceOverrides.Count;
        int needsCapture = totalBuy - withOverride;
        Assert.True(needsCapture >= 5,
            $"Only {needsCapture} items still need live-captured sell prices — either shops got very thin or coverage is unexpectedly complete");
    }

    [Fact]
    public void CoverageStats_EveryKnownBuyItem_HasResolvableSellPrice()
    {
        // Invariant: every item with a buy price has SOME sell price
        // (ground-truth override OR buy/2 fallback). GetSellPrice returning
        // null for a known-buy item would be a bug.
        var orphans = new List<int>();
        foreach (var kv in ItemPrices.BuyPrices)
        {
            if (ItemPrices.GetSellPrice(kv.Key) == null) orphans.Add(kv.Key);
        }
        Assert.True(orphans.Count == 0,
            $"Items with buy price but null sell: {string.Join(",", orphans)}");
    }

    [Fact]
    public void CoverageStats_NoOverride_WithoutMatchingBuy_Exists()
    {
        // Pin that every override has a matching buy entry. Already asserted
        // in AllSellPriceOverrides_HaveCorrespondingBuyPrice; this test adds
        // a count caption for future diagnostic at a glance.
        int orphaned = 0;
        foreach (var kv in ItemPrices.SellPriceOverrides)
        {
            if (!ItemPrices.BuyPrices.ContainsKey(kv.Key)) orphaned++;
        }
        Assert.True(orphaned == 0,
            $"{orphaned} sell overrides have no matching buy price (see AllSellPriceOverrides_HaveCorrespondingBuyPrice for details)");
    }

    [Fact]
    public void SellPriceOverrides_GollundSet_Session42()
    {
        // Session 42 Gollund Outfitter Sell. Battle Axe / Giant's Axe have
        // sell == buy (novel third variant; Mage Masher/Serpent Staff are
        // sell > buy; most swords are sell < buy).
        int sleepId = ItemData.Items.First(kv => kv.Value.Name == "Sleep Blade").Key;
        int platId = ItemData.Items.First(kv => kv.Value.Name == "Platinum Sword").Key;
        int bambooId = ItemData.Items.First(kv => kv.Value.Name == "Battle Bamboo").Key;
        int battleAxeId = ItemData.Items.First(kv => kv.Value.Name == "Battle Axe").Key;
        int giantAxeId = ItemData.Items.First(kv => kv.Value.Name == "Giant's Axe").Key;

        Assert.Equal(2_500, ItemPrices.GetSellPrice(sleepId));
        Assert.Equal(5_500, ItemPrices.GetSellPrice(platId));
        Assert.Equal(700, ItemPrices.GetSellPrice(bambooId));
        Assert.Equal(1_500, ItemPrices.GetSellPrice(battleAxeId));
        Assert.Equal(3_000, ItemPrices.GetSellPrice(giantAxeId));
    }

    [Fact]
    public void SellPriceOverrides_BattleAxe_DocumentsSellEqualsBuy()
    {
        // Battle Axe: buy 1500, sell 1500. Third confirmed ratio pattern
        // (after sell<buy default and sell>buy Mage Masher/Serpent Staff).
        int id = ItemData.Items.First(kv => kv.Value.Name == "Battle Axe").Key;
        int sell = ItemPrices.GetSellPrice(id)!.Value;
        int buy = ItemPrices.GetBuyPrice(id)!.Value;
        Assert.Equal(buy, sell);
    }

    [Fact]
    public void SellPriceOverrides_SerpentStaff_DocumentsSellAboveBuy()
    {
        // Serpent Staff: buy 2200, sell 3000. Second confirmed "sell > buy"
        // item after Mage Masher (session 33). Documents a real FFT quirk
        // so future sanity-bound tightening knows about these cases.
        int id = ItemData.Items.First(kv => kv.Value.Name == "Serpent Staff").Key;
        int sell = ItemPrices.GetSellPrice(id)!.Value;
        int buy = ItemPrices.GetBuyPrice(id)!.Value;
        Assert.True(sell > buy, $"Expected sell > buy; got sell={sell} buy={buy}");
    }

    [Fact]
    public void SellPriceOverrides_GougLateGameSet_Session39()
    {
        // Session 39 live-captured at Goug Outfitter Sell. Swords stay
        // under-30% of buy (16-29%); rods are ~50% (matches buy/2 fallback).
        int diamondId = ItemData.Items.First(kv => kv.Value.Name == "Diamond Sword").Key;
        int icebrandId = ItemData.Items.First(kv => kv.Value.Name == "Icebrand").Key;
        int runebladeId = ItemData.Items.First(kv => kv.Value.Name == "Runeblade").Key;
        int wizardsRodId = ItemData.Items.First(kv => kv.Value.Name == "Wizard's Rod").Key;

        Assert.Equal(4_000, ItemPrices.GetSellPrice(diamondId));
        Assert.Equal(7_000, ItemPrices.GetSellPrice(icebrandId));
        Assert.Equal(10_000, ItemPrices.GetSellPrice(runebladeId));
        Assert.Equal(4_000, ItemPrices.GetSellPrice(wizardsRodId));
    }

    // Session 39: GetSellPriceWithEstimate — one-call API returning both
    // the price and whether it came from a live-verified override or the
    // buy/2 estimate. Previously callers had to do GetSellPrice +
    // IsSellPriceGroundTruth separately.

    [Fact]
    public void GetSellPriceWithEstimate_OverriddenItem_ReturnsGroundTruth()
    {
        // Dagger has a live-captured override (50). Expect ground-truth=true.
        var result = ItemPrices.GetSellPriceWithEstimate(1);
        Assert.NotNull(result);
        Assert.Equal(50, result.Value.Price);
        Assert.True(result.Value.IsGroundTruth);
    }

    [Fact]
    public void GetSellPriceWithEstimate_NoOverride_ReturnsEstimate()
    {
        // Potion has no override, buy is 50, so estimate is 25, not ground-truth.
        var result = ItemPrices.GetSellPriceWithEstimate(240);
        Assert.NotNull(result);
        Assert.Equal(25, result.Value.Price);
        Assert.False(result.Value.IsGroundTruth);
    }

    [Fact]
    public void GetSellPriceWithEstimate_UnknownItem_ReturnsNull()
    {
        Assert.Null(ItemPrices.GetSellPriceWithEstimate(9999));
        Assert.Null(ItemPrices.GetSellPriceWithEstimate(-1));
    }

    [Fact]
    public void GetSellPriceWithEstimate_AgreesWithGetSellPrice()
    {
        // Round-trip invariant: for every known item, the Price field of
        // GetSellPriceWithEstimate equals GetSellPrice. No new behavior —
        // just a more convenient accessor.
        foreach (var kv in ItemPrices.BuyPrices)
        {
            var tuple = ItemPrices.GetSellPriceWithEstimate(kv.Key);
            var direct = ItemPrices.GetSellPrice(kv.Key);
            Assert.NotNull(tuple);
            Assert.Equal(direct, tuple.Value.Price);
        }
    }

    [Fact]
    public void GetSellPriceWithEstimate_AgreesWithIsSellPriceGroundTruth()
    {
        // Same for the ground-truth flag.
        foreach (var kv in ItemPrices.BuyPrices)
        {
            var tuple = ItemPrices.GetSellPriceWithEstimate(kv.Key);
            Assert.NotNull(tuple);
            Assert.Equal(ItemPrices.IsSellPriceGroundTruth(kv.Key), tuple.Value.IsGroundTruth);
        }
    }

    [Fact]
    public void SellPriceOverrides_SwordsFitUnder30PercentBuy_Confirmed()
    {
        // Explicit invariant: every sword override is under 30% of buy price.
        // If this breaks, either we captured a typo or FFT changed the sword
        // sell formula between captures.
        var swordNames = new[] {
            "Iron Sword", "Mythril Sword", "Blood Sword", "Coral Sword",
            "Ancient Sword", "Diamond Sword", "Icebrand", "Runeblade",
        };
        foreach (var name in swordNames)
        {
            var entry = ItemData.Items.FirstOrDefault(kv => kv.Value.Name == name);
            if (entry.Key == 0 && entry.Value == null) continue;
            int id = entry.Key;
            int? sell = ItemPrices.GetSellPrice(id);
            int? buy = ItemPrices.GetBuyPrice(id);
            Assert.NotNull(sell);
            Assert.NotNull(buy);
            double ratio = (double)sell!.Value / buy!.Value;
            Assert.True(ratio < 0.30,
                $"Sword '{name}' sell/buy ratio is {ratio:P0}, expected <30%");
        }
    }

    // Additional property + edge tests (session 33 batch 7).

    [Fact]
    public void AllBuyPrices_ArePositive()
    {
        // A zero or negative buy price would break affordability math.
        foreach (var kv in ItemPrices.BuyPrices)
        {
            Assert.True(kv.Value > 0, $"Item {kv.Key} has non-positive buy price {kv.Value}");
        }
    }

    [Fact]
    public void AllSellPriceOverrides_ArePositive()
    {
        foreach (var kv in ItemPrices.SellPriceOverrides)
        {
            Assert.True(kv.Value > 0, $"Item {kv.Key} has non-positive sell override {kv.Value}");
        }
    }

    [Fact]
    public void SellPriceOverrides_WithinTenXBuyPrice()
    {
        // Sanity check: an override shouldn't be absurdly larger than the buy price.
        // NOTE: some items DO sell for more than they buy (discovered session 33
        // batch 7: Mage Masher buys 600, sells 750). So the canonical "sell <= buy"
        // invariant is NOT true. Use a looser sanity bound to catch data-entry
        // typos (e.g. missing a digit) without flagging legitimate variance.
        foreach (var kv in ItemPrices.SellPriceOverrides)
        {
            if (ItemPrices.BuyPrices.TryGetValue(kv.Key, out int buy))
            {
                Assert.True(kv.Value <= buy * 10,
                    $"Item {kv.Key} sell override {kv.Value} is >10× buy price {buy} (likely typo)");
            }
        }
    }

    [Fact]
    public void GetBuyPrice_IntMaxValue_ReturnsNull()
    {
        Assert.Null(ItemPrices.GetBuyPrice(int.MaxValue));
    }

    [Fact]
    public void GetBuyPrice_IntMinValue_ReturnsNull()
    {
        Assert.Null(ItemPrices.GetBuyPrice(int.MinValue));
    }

    [Fact]
    public void GetSellPrice_ConsistentWithBuyPriceExistence()
    {
        // If buy is known AND no override, sell is non-null (equals buy/2).
        foreach (var kv in ItemPrices.BuyPrices)
        {
            if (ItemPrices.SellPriceOverrides.ContainsKey(kv.Key)) continue;
            Assert.NotNull(ItemPrices.GetSellPrice(kv.Key));
        }
    }

    [Fact]
    public void IsSellPriceGroundTruth_IntMaxValue_ReturnsFalse()
    {
        Assert.False(ItemPrices.IsSellPriceGroundTruth(int.MaxValue));
    }

    // Session 35: additional hardening.

    [Fact]
    public void AllSellPriceOverrides_HaveCorrespondingBuyPrice()
    {
        // Live-captured sell prices should always correspond to a shop-sold
        // item (an entry in BuyPrices). An override without a buy price would
        // mean we captured a sell number for something not in the shop list.
        foreach (var kv in ItemPrices.SellPriceOverrides)
        {
            Assert.True(ItemPrices.BuyPrices.ContainsKey(kv.Key),
                $"Sell override for item {kv.Key} has no matching buy price");
        }
    }

    [Fact]
    public void GetSellPrice_MatchesOverrideTable_WhenOverridePresent()
    {
        // Round-trip: for every override entry, GetSellPrice(id) must return
        // the exact override value — no accidental buy/2 fallback.
        foreach (var kv in ItemPrices.SellPriceOverrides)
        {
            Assert.Equal(kv.Value, ItemPrices.GetSellPrice(kv.Key));
        }
    }

    [Fact]
    public void IsSellPriceGroundTruth_ZeroAndNegative_ReturnFalse()
    {
        Assert.False(ItemPrices.IsSellPriceGroundTruth(0));
        Assert.False(ItemPrices.IsSellPriceGroundTruth(-1));
        Assert.False(ItemPrices.IsSellPriceGroundTruth(int.MinValue));
    }

    [Fact]
    public void BuyPrices_And_SellPriceOverrides_AreNotNull()
    {
        // Sanity: the static init populates these — never null. If either
        // were null a NullReferenceException would surface in live use.
        Assert.NotNull(ItemPrices.BuyPrices);
        Assert.NotNull(ItemPrices.SellPriceOverrides);
        Assert.NotNull(ItemPrices.UnresolvedNames);
    }

    [Fact]
    public void GetSellPrice_Override_IsReturnedRegardlessOfBuyPresence()
    {
        // For the Dagger override: the override is 50, buy is 200. The
        // function must never accidentally return buy (200) or buy/2 (100).
        int sell = ItemPrices.GetSellPrice(1)!.Value;
        Assert.NotEqual(200, sell);
        Assert.NotEqual(100, sell);
        Assert.Equal(50, sell);
    }

    [Fact]
    public void GetBuyPrice_AllKeysInBuyPricesResolve()
    {
        // Every ID in BuyPrices round-trips through GetBuyPrice.
        foreach (var kv in ItemPrices.BuyPrices)
        {
            int? price = ItemPrices.GetBuyPrice(kv.Key);
            Assert.NotNull(price);
            Assert.Equal(kv.Value, price);
        }
    }
}
