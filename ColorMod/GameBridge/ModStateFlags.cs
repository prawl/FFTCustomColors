using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Disk-backed dictionary of named integer flags the mod can set
    /// and read across bridge sessions. Backed by
    /// <c>claude_bridge/mod_state.json</c> — load on construction, save
    /// on any mutation.
    ///
    /// Use-cases (session 24 viability exploration):
    /// - Sticky UI caches: remember the last-observed party-menu Tab
    ///   between detection calls so we can use it as a fallback hint
    ///   when the natural tab flags are both 0.
    /// - Session-scoped counters: "how many chain-call open_eqa
    ///   transitions has Claude done this session".
    /// - Diagnostic toggles: turn logging verbosity on/off without
    ///   rebuilding the mod.
    ///
    /// NOT a replacement for memory discriminators — flags reflect
    /// what the mod already knows (whatever we decide to write).
    /// They can't discover state from cold.
    ///
    /// Thread-safety: writes and reads are not synchronized. The
    /// CommandWatcher processes commands serially on one thread, so
    /// as long as all callers go through CommandWatcher, there's no
    /// contention. Don't use this from a background worker.
    /// </summary>
    public class ModStateFlags
    {
        private readonly string _filePath;
        private Dictionary<string, int> _flags = new();

        public ModStateFlags(string bridgeDirectory)
        {
            _filePath = Path.Combine(bridgeDirectory, "mod_state.json");
            Load();
        }

        /// <summary>Returns the flag's value, or null if unset.</summary>
        public int? Get(string name) =>
            _flags.TryGetValue(name, out int v) ? v : (int?)null;

        /// <summary>Sets a flag and immediately flushes to disk.</summary>
        public void Set(string name, int value)
        {
            _flags[name] = value;
            Save();
        }

        /// <summary>Removes a flag and flushes to disk.</summary>
        public void Clear(string name)
        {
            if (_flags.Remove(name))
                Save();
        }

        /// <summary>Removes all flags and flushes to disk.</summary>
        public void ClearAll()
        {
            _flags.Clear();
            Save();
        }

        /// <summary>Returns a snapshot of all current flag name→value pairs.</summary>
        public IReadOnlyDictionary<string, int> Snapshot() =>
            new Dictionary<string, int>(_flags);

        private void Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return;
                var json = File.ReadAllText(_filePath);
                var parsed = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                if (parsed != null) _flags = parsed;
            }
            catch (System.Exception ex)
            {
                ModLogger.LogError($"[ModStateFlags] Load failed: {ex.Message} — starting with empty flags");
                _flags = new Dictionary<string, int>();
            }
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_flags, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch (System.Exception ex)
            {
                ModLogger.LogError($"[ModStateFlags] Save failed: {ex.Message}");
            }
        }
    }
}
