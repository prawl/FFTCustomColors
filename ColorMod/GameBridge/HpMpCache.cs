using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Per-unit HP/MaxHp/Mp/MaxMp store keyed by (slotIndex, equipmentSignature).
    /// Disk-backed at <c>claude_bridge/hp_mp_cache.json</c>.
    ///
    /// Why this exists: the HoveredUnitArray only populates HP/MP for ~4 roster
    /// slots near the cursor. Without a cache, <c>screen -v</c> on PartyMenu can
    /// only surface HP/MP for those few hovered-adjacent units. As the player
    /// moves the cursor through the party, each hover observation cached here
    /// gives the next <c>screen</c> call a ground-truth HP/MP for any unit
    /// previously seen — fills the gap without implementing a from-scratch FFT
    /// HP/MP formula (which would need per-job-level history the game doesn't
    /// expose and PSX wiki mults that may differ from the IC remaster).
    ///
    /// Equipment signature: the 7 equipment u16s read from roster +0x0E..+0x1A.
    /// When equipment changes, the cached MaxHp/MaxMp are stale (bonuses
    /// differ), so <see cref="Get"/> returns null until a fresh observation
    /// re-populates the entry under the new signature.
    /// </summary>
    public class HpMpCache
    {
        public class Entry
        {
            public int Hp { get; set; }
            public int MaxHp { get; set; }
            public int Mp { get; set; }
            public int MaxMp { get; set; }
            public int[] Equipment { get; set; } = new int[7];
        }

        private readonly string _filePath;
        private Dictionary<int, Entry> _entries = new();

        public HpMpCache(string bridgeDirectory)
        {
            _filePath = Path.Combine(bridgeDirectory, "hp_mp_cache.json");
            Load();
        }

        public HpMpCache.Entry? Get(int slotIndex, int[] equipment)
        {
            if (!_entries.TryGetValue(slotIndex, out var entry)) return null;
            if (!EquipmentEquals(entry.Equipment, equipment)) return null;
            return entry;
        }

        public void Set(int slotIndex, int[] equipment, int hp, int maxHp, int mp, int maxMp)
        {
            var copy = new int[7];
            for (int i = 0; i < 7 && i < equipment.Length; i++) copy[i] = equipment[i];
            _entries[slotIndex] = new Entry
            {
                Hp = hp, MaxHp = maxHp, Mp = mp, MaxMp = maxMp,
                Equipment = copy,
            };
            Save();
        }

        private static bool EquipmentEquals(int[] a, int[] b)
        {
            if (a == null || b == null) return false;
            int len = a.Length < b.Length ? a.Length : b.Length;
            for (int i = 0; i < len; i++) if (a[i] != b[i]) return false;
            if (a.Length != b.Length)
            {
                var longer = a.Length > b.Length ? a : b;
                for (int i = len; i < longer.Length; i++) if (longer[i] != 0) return false;
            }
            return true;
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return;
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<int, Entry>>(json);
                if (loaded != null) _entries = loaded;
            }
            catch
            {
                _entries = new Dictionary<int, Entry>();
            }
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_entries);
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }
    }
}
