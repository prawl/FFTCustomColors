using System.Text.Json.Serialization;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure aggregator that sums equipment-driven stats for a unit on
    /// CharacterStatus. Caller resolves roster u16 equipment IDs to
    /// <see cref="ItemInfo"/> via <see cref="ItemData.GetItem"/> and passes
    /// the results. Aggregator doesn't touch memory itself — keeps the
    /// logic unit-testable without live game state.
    ///
    /// What we surface:
    ///   - TotalHpBonus / TotalMpBonus: sum of equipped helm/body/accessory
    ///     HP/MP bonuses from ItemData. Helm and body are the two big HP
    ///     contributors; accessories add a few.
    ///   - WeaponPower / WeaponRange / WeaponEvade / WeaponElement: stats
    ///     of the right-hand weapon (the "primary" weapon shown on the
    ///     stat panel).
    ///   - LeftHandPower: for dual-wield, the off-hand weapon's WP so
    ///     Claude can reason about the attack pair.
    ///   - ShieldPhysicalEvade / ShieldMagicEvade: for shields, which
    ///     directly modify incoming-hit chance (decision-relevant for
    ///     positioning).
    ///
    /// What we DON'T surface:
    ///   - Move/Jump/Speed/PA/MA: these are job-level×raw-stat computed
    ///     values that require per-job data tables (IC-remaster values
    ///     may differ from PSX wiki per `feedback_wiki_psx_vs_ic.md`).
    ///     Keep memory-read-dependent until a reliable IC reference lands.
    ///   - Effective HP/MP totals: need the formula path for raw stats.
    ///     HoveredUnitArray exposes live HP/MaxHp for the hovered unit
    ///     and the 3 adjacent slots (see HpMpCache) — verbose consumers
    ///     use those values when available.
    /// </summary>
    public record UnitStatsAggregate(
        [property: JsonPropertyName("hpBonus")] int TotalHpBonus,
        [property: JsonPropertyName("mpBonus")] int TotalMpBonus,
        [property: JsonPropertyName("weaponPower"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? WeaponPower,
        [property: JsonPropertyName("weaponRange"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? WeaponRange,
        [property: JsonPropertyName("weaponEvade"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? WeaponEvade,
        [property: JsonPropertyName("weaponElement"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? WeaponElement,
        [property: JsonPropertyName("leftHandPower"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? LeftHandPower,
        [property: JsonPropertyName("shieldPhysicalEvade"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        int ShieldPhysicalEvade,
        [property: JsonPropertyName("shieldMagicEvade"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        int ShieldMagicEvade);

    public static class UnitStatsAggregator
    {
        public static UnitStatsAggregate Aggregate(
            ItemInfo? helm,
            ItemInfo? body,
            ItemInfo? accessory,
            ItemInfo? weapon,
            ItemInfo? leftHand,
            ItemInfo? shield)
        {
            int hp = 0, mp = 0;
            if (helm != null) { hp += helm.HpBonus; mp += helm.MpBonus; }
            if (body != null) { hp += body.HpBonus; mp += body.MpBonus; }
            if (accessory != null) { hp += accessory.HpBonus; mp += accessory.MpBonus; }

            // LeftHand might be a shield (stored in the left-hand slot when no
            // dual wield) — only treat it as a "left hand weapon" if it
            // actually has weapon stats (WP > 0). Shields in the left-hand
            // slot contribute via ShieldPhysicalEvade/MagicEvade instead.
            ItemInfo? actualLeftHandWeapon = (leftHand != null && leftHand.WeaponPower > 0)
                ? leftHand : null;
            ItemInfo? effectiveShield = shield
                ?? (leftHand != null && leftHand.WeaponPower == 0 ? leftHand : null);

            return new UnitStatsAggregate(
                TotalHpBonus: hp,
                TotalMpBonus: mp,
                WeaponPower: weapon?.WeaponPower,
                WeaponRange: weapon?.Range,
                WeaponEvade: weapon?.WeaponEvade,
                WeaponElement: weapon?.Element,
                LeftHandPower: actualLeftHandWeapon?.WeaponPower,
                ShieldPhysicalEvade: effectiveShield?.PhysicalEvade ?? 0,
                ShieldMagicEvade: effectiveShield?.MagicEvade ?? 0);
        }
    }
}
