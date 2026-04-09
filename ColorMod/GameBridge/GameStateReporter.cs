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
            // Delegate to CharacterData which handles both IC remaster roster IDs and PSX IDs
            return CharacterData.GetJobName(jobId);
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
