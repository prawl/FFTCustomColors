using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Tracks per-unit and per-battle statistics across the entire playthrough.
    /// Persisted to lifetime_stats.json in the bridge directory.
    ///
    /// Hooks fire from:
    ///   - battle_move postAction (tiles moved)
    ///   - battle_ability postAction (damage via HP diff)
    ///   - scan_move (detect HP changes from enemy attacks between turns)
    ///   - battle_wait (turn count)
    ///   - BattleVictory detection (finalize battle stats)
    ///
    /// NOT YET WIRED — this is the data model and persistence layer.
    /// Hook integration is a separate step after the tracker is validated.
    /// </summary>
    public class BattleStatTracker
    {
        private string? _savePath;

        public LifetimeStats Lifetime { get; set; } = new();
        public BattleStats? CurrentBattle { get; set; }

        public void SetSavePath(string bridgeDir)
        {
            _savePath = Path.Combine(bridgeDir, "lifetime_stats.json");
        }

        // =====================================================================
        // Battle lifecycle
        // =====================================================================

        public void StartBattle(string location)
        {
            CurrentBattle = new BattleStats
            {
                Location = location,
                StartedAt = DateTime.UtcNow.ToString("o"),
            };
        }

        /// <summary>
        /// S58: milestones crossed in the most recent EndBattle call. Populated
        /// by EndBattle by diffing lifetime-stats before and after the
        /// per-unit merge. Consumed by the battle-summary renderer so
        /// milestone callouts surface alongside the MVP line.
        /// </summary>
        public List<string> RecentMilestones { get; private set; } = new();

        public void EndBattle(bool won)
        {
            // Idempotent: once a battle's rolled up into Lifetime, keep
            // CurrentBattle frozen for `stats battle` rendering but ignore
            // further EndBattle calls. Protects against stray lifecycle
            // flickers (e.g. Victory → Desertion) double-counting.
            if (CurrentBattle == null || CurrentBattle.EndedAt != null) return;
            CurrentBattle.Won = won;
            CurrentBattle.EndedAt = DateTime.UtcNow.ToString("o");
            Lifetime.TotalBattles++;
            if (won) Lifetime.BattlesWon++;
            else Lifetime.BattlesLost++;

            // S58: snapshot per-unit lifetime stats BEFORE the merge so
            // MilestoneDetector can diff before/after and emit callouts.
            var beforeSnapshot = SnapshotLifetime();

            // Determine MVP (session 47: extracted to MvpSelector for dedicated coverage).
            var mvp = MvpSelector.Select(CurrentBattle.Units);
            CurrentBattle.Mvp = mvp;

            // Merge into lifetime
            foreach (var (name, battle) in CurrentBattle.Units)
            {
                if (!Lifetime.Units.TryGetValue(name, out var lifetime))
                {
                    lifetime = new UnitLifetimeStats { Name = name };
                    Lifetime.Units[name] = lifetime;
                }
                lifetime.TotalBattles++;
                lifetime.TotalDamageDealt += battle.DamageDealt;
                lifetime.TotalDamageReceived += battle.DamageReceived;
                lifetime.TotalHealingDealt += battle.HealingDealt;
                lifetime.TotalKills += battle.Kills;
                lifetime.TotalTimesKOd += battle.TimesKOd;
                lifetime.TotalTimesRaised += battle.TimesRaised;
                lifetime.TotalTilesMoved += battle.TilesMoved;
                lifetime.TotalTurns += battle.Turns;
                if (name == mvp) lifetime.MvpCount++;

                // Merge ability usage
                foreach (var (ability, count) in battle.AbilityUsage)
                {
                    if (!lifetime.AbilityUsage.ContainsKey(ability))
                        lifetime.AbilityUsage[ability] = 0;
                    lifetime.AbilityUsage[ability] += count;
                }
            }

            // S58: detect milestones crossed by this battle's merge.
            RecentMilestones = MilestoneDetector.DetectAll(beforeSnapshot, Lifetime);

            Save();
        }

        /// <summary>
        /// S58: deep-copy per-unit lifetime stats for milestone diffing.
        /// Only the fields MilestoneDetector reads are copied.
        /// </summary>
        private LifetimeStats SnapshotLifetime()
        {
            var snap = new LifetimeStats
            {
                TotalBattles = Lifetime.TotalBattles,
                BattlesWon = Lifetime.BattlesWon,
                BattlesLost = Lifetime.BattlesLost,
            };
            foreach (var (name, u) in Lifetime.Units)
            {
                snap.Units[name] = new UnitLifetimeStats
                {
                    Name = u.Name,
                    TotalBattles = u.TotalBattles,
                    TotalDamageDealt = u.TotalDamageDealt,
                    TotalKills = u.TotalKills,
                };
            }
            return snap;
        }

        // =====================================================================
        // Event hooks (called during battle)
        // =====================================================================

        public void OnDamageDealt(string attacker, string target, int damage, string? ability = null)
        {
            if (CurrentBattle == null) return;
            GetOrCreate(attacker).DamageDealt += damage;
            GetOrCreate(target).DamageReceived += damage;
            if (ability != null) IncrementAbility(attacker, ability);

            // Track fastest kill candidate
            // (caller should call OnKill separately if target died)
        }

        public void OnHeal(string healer, string target, int amount, string? ability = null)
        {
            if (CurrentBattle == null) return;
            GetOrCreate(healer).HealingDealt += amount;
            if (ability != null) IncrementAbility(healer, ability);
        }

        public void OnKill(string attacker, string target)
        {
            if (CurrentBattle == null) return;
            GetOrCreate(attacker).Kills++;
            GetOrCreate(target).TimesKOd++;
        }

        public void OnRaise(string healer, string target)
        {
            if (CurrentBattle == null) return;
            GetOrCreate(target).TimesRaised++;
        }

        public void OnMove(string unit, int distance)
        {
            if (CurrentBattle == null) return;
            GetOrCreate(unit).TilesMoved += distance;
        }

        public void OnTurnTaken(string unit)
        {
            if (CurrentBattle == null) return;
            GetOrCreate(unit).Turns++;
            CurrentBattle.TotalTurns++;
        }

        public void OnAbilityUsed(string unit, string ability)
        {
            if (CurrentBattle == null) return;
            IncrementAbility(unit, ability);
        }

        public void OnHpLow(string unit, int hp, int maxHp)
        {
            if (CurrentBattle == null) return;
            var stats = GetOrCreate(unit);
            if (hp < stats.LowestHp || stats.LowestHp == 0)
            {
                stats.LowestHp = hp;
                stats.LowestHpMaxHp = maxHp;
            }
        }

        // =====================================================================
        // Rendering
        // =====================================================================

        /// <summary>
        /// Generate a post-battle summary string for display.
        /// </summary>
        public string RenderBattleSummary()
        {
            if (CurrentBattle == null) return "";
            var b = CurrentBattle;
            var lines = new List<string>();

            lines.Add($"═══ BATTLE {(b.Won ? "COMPLETE" : "LOST")} — {b.Location} ({b.TotalTurns} turns) ═══");

            if (b.Mvp != null && b.Units.TryGetValue(b.Mvp, out var mvpStats))
            {
                lines.Add($"  MVP: {b.Mvp}");
                lines.Add($"    • {mvpStats.Kills} kills, {mvpStats.DamageDealt} dmg dealt, {mvpStats.DamageReceived} dmg taken");
            }
            lines.Add("");

            foreach (var (name, stats) in b.Units.OrderByDescending(u => u.Value.DamageDealt))
            {
                if (name == b.Mvp) continue;
                var parts = new List<string>();
                if (stats.Kills > 0) parts.Add($"{stats.Kills} kill{(stats.Kills > 1 ? "s" : "")}");
                parts.Add($"{stats.DamageDealt} dmg");
                if (stats.HealingDealt > 0) parts.Add($"{stats.HealingDealt} healed");
                if (stats.TilesMoved > 0) parts.Add($"moved {stats.TilesMoved} tiles");
                if (stats.TimesKOd > 0) parts.Add($"KO'd {stats.TimesKOd}x");
                lines.Add($"  {name}: {string.Join(", ", parts)}");
            }

            // Closest call
            var closestCall = b.Units
                .Where(u => u.Value.LowestHp > 0 && u.Value.LowestHpMaxHp > 0)
                .OrderBy(u => (double)u.Value.LowestHp / u.Value.LowestHpMaxHp)
                .FirstOrDefault();
            if (closestCall.Key != null)
            {
                lines.Add($"  Closest call: {closestCall.Key} at {closestCall.Value.LowestHp}/{closestCall.Value.LowestHpMaxHp} HP");
            }

            // S58: milestone callouts from the last EndBattle diff.
            if (RecentMilestones.Count > 0)
            {
                lines.Add("");
                foreach (var m in RecentMilestones)
                    lines.Add("  " + m);
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Generate a lifetime stats summary string.
        /// </summary>
        public string RenderLifetimeSummary()
        {
            var lines = new List<string>();
            lines.Add($"═══ LIFETIME STATS ({Lifetime.TotalBattles} battles: {Lifetime.BattlesWon}W/{Lifetime.BattlesLost}L) ═══");

            foreach (var (name, stats) in Lifetime.Units.OrderByDescending(u => u.Value.TotalDamageDealt))
            {
                var mostUsed = stats.AbilityUsage.Count > 0
                    ? stats.AbilityUsage.OrderByDescending(a => a.Value).First()
                    : default;
                var parts = new List<string>
                {
                    $"{stats.TotalBattles} battles",
                    $"{stats.TotalDamageDealt} dmg",
                    $"{stats.TotalKills} kills",
                };
                if (stats.TotalHealingDealt > 0) parts.Add($"{stats.TotalHealingDealt} healed");
                if (stats.MvpCount > 0) parts.Add($"MVP {stats.MvpCount}x");
                if (stats.TotalTimesKOd > 0) parts.Add($"KO'd {stats.TotalTimesKOd}x");
                lines.Add($"  {name}: {string.Join(" | ", parts)}");
                if (mostUsed.Key != null)
                    lines.Add($"    most used: {mostUsed.Key} ({mostUsed.Value}x)");
            }

            return string.Join("\n", lines);
        }

        // =====================================================================
        // Persistence
        // =====================================================================

        public void Save()
        {
            if (_savePath == null) return;
            try
            {
                var json = JsonSerializer.Serialize(Lifetime, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                });
                File.WriteAllText(_savePath, json);
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[BattleStatTracker] Save failed: {ex.Message}");
            }
        }

        public void Load()
        {
            if (_savePath == null || !File.Exists(_savePath)) return;
            try
            {
                var json = File.ReadAllText(_savePath);
                Lifetime = JsonSerializer.Deserialize<LifetimeStats>(json) ?? new LifetimeStats();
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[BattleStatTracker] Load failed: {ex.Message}");
                Lifetime = new LifetimeStats();
            }
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private UnitBattleStats GetOrCreate(string unitName)
        {
            if (!CurrentBattle!.Units.TryGetValue(unitName, out var stats))
            {
                stats = new UnitBattleStats();
                CurrentBattle.Units[unitName] = stats;
            }
            return stats;
        }

        private void IncrementAbility(string unit, string ability)
        {
            var stats = GetOrCreate(unit);
            if (!stats.AbilityUsage.ContainsKey(ability))
                stats.AbilityUsage[ability] = 0;
            stats.AbilityUsage[ability]++;
        }
    }

    // =====================================================================
    // Data models
    // =====================================================================

    public class LifetimeStats
    {
        [JsonPropertyName("totalBattles")]
        public int TotalBattles { get; set; }

        [JsonPropertyName("battlesWon")]
        public int BattlesWon { get; set; }

        [JsonPropertyName("battlesLost")]
        public int BattlesLost { get; set; }

        [JsonPropertyName("units")]
        public Dictionary<string, UnitLifetimeStats> Units { get; set; } = new();
    }

    public class UnitLifetimeStats
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("totalBattles")]
        public int TotalBattles { get; set; }

        [JsonPropertyName("totalDamageDealt")]
        public int TotalDamageDealt { get; set; }

        [JsonPropertyName("totalDamageReceived")]
        public int TotalDamageReceived { get; set; }

        [JsonPropertyName("totalHealingDealt")]
        public int TotalHealingDealt { get; set; }

        [JsonPropertyName("totalKills")]
        public int TotalKills { get; set; }

        [JsonPropertyName("totalTimesKOd")]
        public int TotalTimesKOd { get; set; }

        [JsonPropertyName("totalTimesRaised")]
        public int TotalTimesRaised { get; set; }

        [JsonPropertyName("totalTilesMoved")]
        public int TotalTilesMoved { get; set; }

        [JsonPropertyName("totalTurns")]
        public int TotalTurns { get; set; }

        [JsonPropertyName("mvpCount")]
        public int MvpCount { get; set; }

        [JsonPropertyName("abilityUsage")]
        public Dictionary<string, int> AbilityUsage { get; set; } = new();
    }

    public class BattleStats
    {
        [JsonPropertyName("location")]
        public string Location { get; set; } = "";

        [JsonPropertyName("startedAt")]
        public string StartedAt { get; set; } = "";

        [JsonPropertyName("endedAt")]
        public string? EndedAt { get; set; }

        [JsonPropertyName("won")]
        public bool Won { get; set; }

        [JsonPropertyName("totalTurns")]
        public int TotalTurns { get; set; }

        [JsonPropertyName("mvp")]
        public string? Mvp { get; set; }

        [JsonPropertyName("units")]
        public Dictionary<string, UnitBattleStats> Units { get; set; } = new();
    }

    public class UnitBattleStats
    {
        [JsonPropertyName("damageDealt")]
        public int DamageDealt { get; set; }

        [JsonPropertyName("damageReceived")]
        public int DamageReceived { get; set; }

        [JsonPropertyName("healingDealt")]
        public int HealingDealt { get; set; }

        [JsonPropertyName("kills")]
        public int Kills { get; set; }

        [JsonPropertyName("timesKOd")]
        public int TimesKOd { get; set; }

        [JsonPropertyName("timesRaised")]
        public int TimesRaised { get; set; }

        [JsonPropertyName("tilesMoved")]
        public int TilesMoved { get; set; }

        [JsonPropertyName("turns")]
        public int Turns { get; set; }

        [JsonPropertyName("lowestHp")]
        public int LowestHp { get; set; }

        [JsonPropertyName("lowestHpMaxHp")]
        public int LowestHpMaxHp { get; set; }

        [JsonPropertyName("abilityUsage")]
        public Dictionary<string, int> AbilityUsage { get; set; } = new();
    }
}
