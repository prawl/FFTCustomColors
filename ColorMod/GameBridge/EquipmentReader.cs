namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Decodes equipped items from a roster slot's 7 equipment u16 fields
    /// at +0x0E..+0x1A. Item IDs match the FFTPatcher canonical encoding —
    /// verified 2026-04-14 via Ramza + Kenrick live dumps, every ID matched
    /// ItemData. 0xFF means empty.
    ///
    /// Slot layout:
    ///   +0x0E Helm / +0x10 Body / +0x12 Accessory / +0x14 Right hand /
    ///   +0x16 Left hand (dual-wield) / +0x18 reserved / +0x1A Left shield
    /// </summary>
    public static class EquipmentReader
    {
        public const int HelmOffset = 0x0E;
        public const int BodyOffset = 0x10;
        public const int AccessoryOffset = 0x12;
        public const int RightHandOffset = 0x14;
        public const int LeftHandOffset = 0x16;
        public const int ReservedOffset = 0x18;
        public const int ShieldOffset = 0x1A;

        public class Loadout
        {
            public int? WeaponId;
            public int? LeftHandId;
            public int? ShieldId;
            public int? HelmId;
            public int? BodyId;
            public int? AccessoryId;

            public string? WeaponName;
            public string? LeftHandName;
            public string? ShieldName;
            public string? HelmName;
            public string? BodyName;
            public string? AccessoryName;
        }

        /// <summary>
        /// Build a Loadout from the 7 u16 slot values read sequentially
        /// starting at roster +0x0E. Indices: 0=helm, 1=body, 2=accessory,
        /// 3=right-hand, 4=left-hand, 5=reserved, 6=shield.
        /// </summary>
        public static Loadout FromSlotValues(int[] u16s)
        {
            var lo = new Loadout();
            if (u16s == null || u16s.Length < 7) return lo;

            lo.HelmId      = NonEmpty(u16s[0]);
            lo.BodyId      = NonEmpty(u16s[1]);
            lo.AccessoryId = NonEmpty(u16s[2]);
            lo.WeaponId    = NonEmpty(u16s[3]);
            lo.LeftHandId  = NonEmpty(u16s[4]);
            lo.ShieldId    = NonEmpty(u16s[6]);

            lo.HelmName      = NameOf(lo.HelmId);
            lo.BodyName      = NameOf(lo.BodyId);
            lo.AccessoryName = NameOf(lo.AccessoryId);
            lo.WeaponName    = NameOf(lo.WeaponId);
            lo.LeftHandName  = NameOf(lo.LeftHandId);
            lo.ShieldName    = NameOf(lo.ShieldId);

            return lo;
        }

        private static int? NonEmpty(int v)
        {
            if (v <= 0 || v == 0xFF || v == 0xFFFF) return null;
            return v;
        }

        private static string? NameOf(int? id)
        {
            if (id == null) return null;
            return ItemData.GetItem(id.Value)?.Name;
        }
    }
}
