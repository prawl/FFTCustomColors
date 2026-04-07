using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Periodically reads game state from memory and writes state.json
    /// so Claude can understand what's happening in the game.
    /// </summary>
    public class GameStateReporter : IDisposable
    {
        private readonly GameMemoryScanner _scanner;
        private readonly string _stateFilePath;
        private CancellationTokenSource? _cts;
        private bool _disposed;

        public GameStateReporter(GameMemoryScanner scanner, string bridgeDirectory)
        {
            _scanner = scanner;
            _stateFilePath = Path.Combine(bridgeDirectory, "state.json");
        }

        public void Start(int intervalMs = 2000)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Run(async () =>
            {
                ModLogger.Log($"[StateReporter] Started (interval: {intervalMs}ms)");
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        ReportNow();
                    }
                    catch (Exception ex)
                    {
                        ModLogger.LogError($"[StateReporter] Error: {ex.Message}");
                    }
                    await Task.Delay(intervalMs, token);
                }
            }, _cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public GameState GetCurrentState()
        {
            var state = new GameState
            {
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            if (!_scanner.IsInitialized)
            {
                state.ScanStatus = "not_initialized";
                return state;
            }

            state.ScanStatus = "ok";
            state.UnitDataBaseAddress = $"0x{_scanner.UnitDataBase:X}";

            int activeCount = 0;
            for (int i = 0; i < GameMemoryScanner.MaxUnitSlots; i++)
            {
                if (!_scanner.IsUnitActive(i)) continue;

                activeCount++;
                var reactionId = _scanner.ReadUnitByte(i, GameMemoryScanner.OffsetReactionAbility);
                var supportId = _scanner.ReadUnitByte(i, GameMemoryScanner.OffsetSupportAbility);
                var movementId = _scanner.ReadUnitByte(i, GameMemoryScanner.OffsetMovementAbility);

                var unit = new UnitState
                {
                    Slot = i,
                    SpriteSet = _scanner.ReadUnitByte(i, GameMemoryScanner.OffsetSpriteSet),
                    UnitIndex = _scanner.ReadUnitByte(i, GameMemoryScanner.OffsetUnitIndex),
                    Job = _scanner.ReadUnitByte(i, GameMemoryScanner.OffsetJob),
                    Experience = _scanner.ReadUnitByte(i, GameMemoryScanner.OffsetExperience),
                    Level = _scanner.ReadUnitByte(i, GameMemoryScanner.OffsetLevel),
                    Brave = _scanner.ReadUnitByte(i, GameMemoryScanner.OffsetBrave),
                    Faith = _scanner.ReadUnitByte(i, GameMemoryScanner.OffsetFaith),
                    NameId = _scanner.ReadUnitUInt16(i, GameMemoryScanner.OffsetNameId),
                    JobName = GetJobName(_scanner.ReadUnitByte(i, GameMemoryScanner.OffsetJob)),
                    GridPosition = _scanner.ReadUnitByte(i, GameMemoryScanner.OffsetDisplayOrder),
                    SecondaryAbility = _scanner.ReadUnitByte(i, GameMemoryScanner.OffsetSecondaryAbility),
                    ReactionAbility = reactionId,
                    ReactionAbilityName = AbilityData.ReactionAbilities.TryGetValue(reactionId, out var ra) ? ra.Name : null,
                    SupportAbility = supportId,
                    SupportAbilityName = AbilityData.SupportAbilities.TryGetValue(supportId, out var sa) ? sa.Name : null,
                    MovementAbility = movementId,
                    MovementAbilityName = AbilityData.MovementAbilities.TryGetValue(movementId, out var ma) ? ma.Name : null
                };

                state.Units.Add(unit);
            }

            state.ActiveUnitCount = activeCount;

            // Read UI state buffer
            state.UI = new UIState
            {
                CursorIndex = _scanner.ReadByte(GameMemoryScanner.UIStateBufferAddress + GameMemoryScanner.UIOffsetCursorIndex),
                SelectedHp = _scanner.ReadUInt16(GameMemoryScanner.UIStateBufferAddress + GameMemoryScanner.UIOffsetHp),
                SelectedMaxHp = _scanner.ReadUInt16(GameMemoryScanner.UIStateBufferAddress + GameMemoryScanner.UIOffsetMaxHp),
                SelectedMp = _scanner.ReadByte(GameMemoryScanner.UIStateBufferAddress + GameMemoryScanner.UIOffsetMp),
                SelectedMaxMp = _scanner.ReadByte(GameMemoryScanner.UIStateBufferAddress + GameMemoryScanner.UIOffsetMaxMp),
                SelectedJob = _scanner.ReadByte(GameMemoryScanner.UIStateBufferAddress + GameMemoryScanner.UIOffsetJob),
                SelectedBrave = _scanner.ReadByte(GameMemoryScanner.UIStateBufferAddress + GameMemoryScanner.UIOffsetBrave),
                SelectedFaith = _scanner.ReadByte(GameMemoryScanner.UIStateBufferAddress + GameMemoryScanner.UIOffsetFaith)
            };

            return state;
        }

        public void ReportNow()
        {
            try
            {
                var state = GetCurrentState();
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_stateFilePath, json);
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[StateReporter] Failed to write state: {ex.Message}");
            }
        }

        internal static string? GetJobName(int jobId)
        {
            return jobId switch
            {
                // Generic jobs (male/female pairs)
                // Job grid Row 0: Squire, Chemist, Knight, Archer, Monk, White Mage
                // Job grid Row 1: Black Mage, Time Mage, Summoner, Thief, Orator, Mystic, Geomancer
                // Job grid Row 2: Dragoon, Samurai, Ninja, Arithmetician, Bard, Mime
                0x01 or 0x02 => "Chemist",
                0x03 or 0x04 => "Knight",
                0x05 or 0x06 => "Archer",
                0x07 or 0x08 => "Monk",
                0x09 or 0x0A => "White Mage",
                0x0B or 0x0C => "Black Mage",
                0x0D or 0x0E => "Time Mage",
                0x0F or 0x10 => "Summoner",
                0x11 or 0x12 => "Thief",
                0x13 or 0x14 => "Orator",
                0x15 or 0x16 => "Mystic",
                0x17 or 0x18 => "Geomancer",
                0x19 or 0x1A => "Dragoon",
                0x1B or 0x1C => "Samurai",
                0x1D or 0x1E => "Ninja",
                0x1F or 0x20 => "Arithmetician",
                0x21 => "Bard",
                0x22 => "Dancer",
                0x23 or 0x24 => "Mime",

                0x4A => "Squire",
                0x4B => "Squire",

                // Story character unique jobs
                0xA0 => "Gallant Knight (Ramza Ch4)",
                0x4C => "Holy Knight (Agrias)",
                0x4D => "Holy Knight (Delita Ch1)",
                0x5E => "Thunder God (Orlandeau)",
                0x5B => "Templar (Beowulf)",
                0x5F => "Sword Saint (Cidolfus)",
                0xA2 => "Sky Pirate (Balthier)",
                0xA3 => "Game Hunter (Luso)",
                _ => null
            };
        }


        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }
}
