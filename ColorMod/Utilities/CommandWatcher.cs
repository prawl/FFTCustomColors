using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FFTColorCustomizer.GameBridge;

namespace FFTColorCustomizer.Utilities
{
    public class CommandWatcher : IDisposable
    {
        private readonly IInputSimulator _inputSimulator;
        private readonly string _bridgeDirectory;
        private readonly string _commandFilePath;
        private readonly string _responseFilePath;

        private FileSystemWatcher? _watcher;
        private string _lastProcessedCommandId = "";
        private readonly object _processingLock = new();
        private bool _disposed;

        // Game bridge components (optional, set after initialization)
        public GameStateReporter? StateReporter { get; set; }
        public MemoryExplorer? Explorer { get; set; }
        public ScreenStateMachine? ScreenMachine { get; set; }

        public CommandWatcher(string modPath, IInputSimulator inputSimulator)
        {
            _inputSimulator = inputSimulator ?? throw new ArgumentNullException(nameof(inputSimulator));
            _bridgeDirectory = Path.Combine(modPath, "claude_bridge");
            _commandFilePath = Path.Combine(_bridgeDirectory, "command.json");
            _responseFilePath = Path.Combine(_bridgeDirectory, "response.json");
        }

        public void Start()
        {
            if (!Directory.Exists(_bridgeDirectory))
                Directory.CreateDirectory(_bridgeDirectory);

            // Clean up stale command file from previous session
            if (File.Exists(_commandFilePath))
            {
                try { File.Delete(_commandFilePath); }
                catch { /* ignore */ }
            }

            _watcher = new FileSystemWatcher(_bridgeDirectory, "command.json");
            _watcher.Changed += OnCommandFileChanged;
            _watcher.Created += OnCommandFileChanged;
            _watcher.Renamed += (s, e) => OnCommandFileChanged(s, e);
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.Size;
            _watcher.EnableRaisingEvents = true;

            // Also start a polling fallback in case FileSystemWatcher misses events
            Task.Run(async () =>
            {
                ModLogger.Log("[CommandBridge] Starting polling fallback (every 1s)");
                while (_watcher != null && !_disposed)
                {
                    try
                    {
                        if (File.Exists(_commandFilePath))
                        {
                            ModLogger.LogDebug("[CommandBridge] Poll detected command.json");
                            ProcessCommandFile();
                        }
                    }
                    catch { /* ignore polling errors */ }
                    await Task.Delay(250);
                }
            });

            ModLogger.Log($"[CommandBridge] Watching for commands at: {_commandFilePath}");
        }

        public void Stop()
        {
            _watcher?.Dispose();
            _watcher = null;
        }

        private void OnCommandFileChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce + process on background thread
            Task.Run(async () =>
            {
                await Task.Delay(50); // Let file write complete
                ProcessCommandFile();
            });
        }

        private void ProcessCommandFile()
        {
            lock (_processingLock)
            {
                try
                {
                    var command = ReadCommandFile();
                    if (command == null) return;

                    if (command.Id == _lastProcessedCommandId)
                    {
                        ModLogger.LogDebug("[CommandBridge] Skipping duplicate command: " + command.Id);
                        return;
                    }

                    ModLogger.Log($"[CommandBridge] Processing command {command.Id}: {command.Description}");
                    var response = ExecuteCommand(command);
                    WriteResponse(response);

                    _lastProcessedCommandId = command.Id;

                    // Rename processed file to prevent re-processing
                    try
                    {
                        var processedPath = Path.Combine(_bridgeDirectory, "command.processed.json");
                        if (File.Exists(processedPath)) File.Delete(processedPath);
                        File.Move(_commandFilePath, processedPath);
                    }
                    catch { /* ignore rename failures */ }
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"[CommandBridge] Error processing command: {ex.Message}");
                    WriteResponse(new CommandResponse
                    {
                        Id = "unknown",
                        Status = "error",
                        Error = ex.Message,
                        ProcessedAt = DateTime.UtcNow.ToString("o")
                    });
                }
            }
        }

        private CommandRequest? ReadCommandFile()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    var json = File.ReadAllText(_commandFilePath);
                    return JsonSerializer.Deserialize<CommandRequest>(json);
                }
                catch (IOException) when (attempt < 2)
                {
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"[CommandBridge] Failed to read command file: {ex.Message}");
                    return null;
                }
            }
            return null;
        }

        private CommandResponse ExecuteCommand(CommandRequest command)
        {
            // Route action commands (dump_unit, report_state, etc.)
            if (!string.IsNullOrEmpty(command.Action))
                return ExecuteAction(command);

            return ExecuteKeyCommand(command);
        }

        private CommandResponse ExecuteAction(CommandRequest command)
        {
            var response = new CommandResponse
            {
                Id = command.Id,
                ProcessedAt = DateTime.UtcNow.ToString("o"),
                GameWindowFound = true
            };

            try
            {
                switch (command.Action)
                {
                    case "dump_unit":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        Explorer.DumpUnitToFile(command.Slot);
                        response.Status = "completed";
                        break;

                    case "dump_all":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        Explorer.DumpAllActiveUnits();
                        response.Status = "completed";
                        break;

                    case "report_state":
                        if (StateReporter == null) { response.Status = "failed"; response.Error = "State reporter not initialized"; break; }
                        StateReporter.ReportNow();
                        response.Status = "completed";
                        break;

                    case "search_memory":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        Explorer.SearchMemoryForUInt16((ushort)command.SearchValue, command.SearchLabel ?? "search");
                        response.Status = "completed";
                        break;

                    case "search_near":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        Explorer.SearchNearAddress(Explorer.Scanner.UnitDataBase, 0x200000, (ushort)command.SearchValue, command.SearchLabel ?? "near");
                        response.Status = "completed";
                        break;

                    case "snapshot":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        Explorer.TakeSnapshot(command.SearchLabel ?? "default");
                        response.Status = "completed";
                        break;

                    case "diff":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        Explorer.DiffSnapshots(command.FromLabel ?? "before", command.ToLabel ?? "after", command.SearchLabel ?? "result");
                        response.Status = "completed";
                        break;

                    case "read_address":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (string.IsNullOrEmpty(command.Address)) { response.Status = "failed"; response.Error = "Address required"; break; }
                        var addr = Convert.ToInt64(command.Address, 16);
                        var size = Math.Clamp(command.ReadSize, 1, 4);
                        var result2 = Explorer.ReadAbsolute((nint)addr, size);
                        if (result2 == null) { response.Status = "failed"; response.Error = $"Failed to read {size} bytes at 0x{addr:X}"; break; }
                        response.ReadResult = new ReadResult
                        {
                            Address = $"0x{addr:X}",
                            Size = size,
                            Value = result2.Value.value,
                            Hex = $"0x{result2.Value.value:X}",
                            RawBytes = BitConverter.ToString(result2.Value.raw).Replace("-", " ")
                        };
                        response.Status = "completed";
                        break;

                    case "read_block":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (string.IsNullOrEmpty(command.Address)) { response.Status = "failed"; response.Error = "Address required"; break; }
                        var blockAddr = Convert.ToInt64(command.Address, 16);
                        var blockSize = Math.Clamp(command.BlockSize, 1, 4096);
                        var blockHex = Explorer.ReadBlock((nint)blockAddr, blockSize);
                        if (blockHex == null) { response.Status = "failed"; response.Error = $"Failed to read {blockSize} bytes at 0x{blockAddr:X}"; break; }
                        response.BlockData = blockHex;
                        response.Status = "completed";
                        break;

                    case "sequence":
                        return ExecuteSequence(command);

                    case "set_screen":
                        if (ScreenMachine == null) { response.Status = "failed"; response.Error = "Screen state machine not initialized"; break; }
                        var screenName = command.SearchLabel ?? "unknown";
                        if (Enum.TryParse<GameScreen>(screenName, ignoreCase: true, out var screen))
                        {
                            ScreenMachine.SetScreen(screen);
                            response.Status = "completed";
                        }
                        else
                        {
                            response.Status = "failed";
                            response.Error = $"Unknown screen: {screenName}. Valid: {string.Join(", ", Enum.GetNames<GameScreen>())}";
                        }
                        break;

                    default:
                        response.Status = "failed";
                        response.Error = $"Unknown action: {command.Action}";
                        break;
                }
            }
            catch (Exception ex)
            {
                response.Status = "error";
                response.Error = ex.Message;
            }

            // Embed state in all action responses
            var actionState = StateReporter?.GetCurrentState();
            if (actionState != null)
            {
                actionState.ScreenState = ScreenMachine?.GetScreenState();
                response.GameState = actionState;
            }

            return response;
        }

        private CommandResponse ExecuteKeyCommand(CommandRequest command)
        {
            var response = new CommandResponse
            {
                Id = command.Id,
                ProcessedAt = DateTime.UtcNow.ToString("o"),
                KeyResults = new List<KeyResult>()
            };

            IntPtr gameWindow = Process.GetCurrentProcess().MainWindowHandle;
            response.GameWindowFound = gameWindow != IntPtr.Zero;

            if (!response.GameWindowFound)
            {
                response.Status = "failed";
                response.Error = "Could not find game window handle";
                return response;
            }

            int successCount = 0;
            for (int i = 0; i < command.Keys.Count; i++)
            {
                var key = command.Keys[i];
                bool success = _inputSimulator.SendKeyPressToWindow(gameWindow, key.Vk);

                response.KeyResults.Add(new KeyResult { Vk = key.Vk, Success = success });
                if (success) successCount++;

                ModLogger.LogDebug($"[CommandBridge] Key {key.Name ?? key.Vk.ToString()} (0x{key.Vk:X2}): {(success ? "OK" : "FAIL")}");

                if (success)
                    ScreenMachine?.OnKeyPressed(key.Vk);

                if (key.HoldMs > 0)
                    Thread.Sleep(key.HoldMs);

                if (i < command.Keys.Count - 1 && command.DelayBetweenMs > 0)
                    Thread.Sleep(command.DelayBetweenMs);
            }

            response.KeysProcessed = successCount;
            response.Status = successCount == command.Keys.Count ? "completed"
                            : successCount > 0 ? "partial"
                            : "failed";

            // Embed state in response so Claude reads one file instead of two
            var state = StateReporter?.GetCurrentState();
            if (state != null)
            {
                state.ScreenState = ScreenMachine?.GetScreenState();
                response.GameState = state;
            }
            StateReporter?.ReportNow();

            ModLogger.Log($"[CommandBridge] Command {command.Id} finished: {response.Status} ({successCount}/{command.Keys.Count} keys)");
            return response;
        }

        private CommandResponse ExecuteSequence(CommandRequest command)
        {
            var response = new CommandResponse
            {
                Id = command.Id,
                ProcessedAt = DateTime.UtcNow.ToString("o"),
                GameWindowFound = true,
                Sequence = new SequenceResult
                {
                    TotalSteps = command.Steps?.Count ?? 0
                }
            };

            IntPtr gameWindow = Process.GetCurrentProcess().MainWindowHandle;
            response.GameWindowFound = gameWindow != IntPtr.Zero;

            if (!response.GameWindowFound)
            {
                response.Status = "failed";
                response.Error = "Could not find game window handle";
                return response;
            }

            if (command.Steps == null || command.Steps.Count == 0)
            {
                response.Status = "completed";
                return response;
            }

            for (int s = 0; s < command.Steps.Count; s++)
            {
                var step = command.Steps[s];
                var stepResult = new StepResult
                {
                    Index = s,
                    Description = step.Description
                };

                // Send keys for this step
                int keysOk = 0;
                for (int k = 0; k < step.Keys.Count; k++)
                {
                    var key = step.Keys[k];
                    bool success = _inputSimulator.SendKeyPressToWindow(gameWindow, key.Vk);
                    if (success)
                    {
                        keysOk++;
                        ScreenMachine?.OnKeyPressed(key.Vk);
                    }

                    if (key.HoldMs > 0)
                        Thread.Sleep(key.HoldMs);

                    if (k < step.Keys.Count - 1 && command.DelayBetweenMs > 0)
                        Thread.Sleep(command.DelayBetweenMs);
                }

                stepResult.KeysProcessed = keysOk;

                // Wait after keys (minimum 100ms for game to process)
                int waitMs = Math.Max(step.WaitMs, 100);
                Thread.Sleep(waitMs);

                // Read memory address if requested
                if (!string.IsNullOrEmpty(step.ReadAddress) && Explorer != null)
                {
                    var readAddr = Convert.ToInt64(step.ReadAddress, 16);
                    var readSz = Math.Clamp(step.ReadSize, 1, 4);
                    var readVal = Explorer.ReadAbsolute((nint)readAddr, readSz);
                    if (readVal != null)
                    {
                        stepResult.ReadResult = new ReadResult
                        {
                            Address = $"0x{readAddr:X}",
                            Size = readSz,
                            Value = readVal.Value.value,
                            Hex = $"0x{readVal.Value.value:X}",
                            RawBytes = BitConverter.ToString(readVal.Value.raw).Replace("-", " ")
                        };
                    }
                }

                // Check assertions if present
                if (step.Assert != null)
                {
                    var failure = CheckAssertions(step.Assert, s);
                    if (failure != null)
                    {
                        stepResult.Status = "assertion_failed";
                        response.Sequence.StepResults.Add(stepResult);
                        response.Sequence.StepsCompleted = s;
                        response.Sequence.FailedAssertion = failure;
                        response.Status = "assertion_failed";

                        // Embed state at failure point
                        var failState = StateReporter?.GetCurrentState();
                        if (failState != null)
                        {
                            failState.ScreenState = ScreenMachine?.GetScreenState();
                            response.GameState = failState;
                        }

                        ModLogger.Log($"[CommandBridge] Sequence stopped at step {s}: {failure.Field} expected={failure.Expected} actual={failure.Actual}");
                        return response;
                    }
                }

                stepResult.Status = "completed";
                response.Sequence.StepResults.Add(stepResult);
                response.Sequence.StepsCompleted = s + 1;
            }

            response.Status = "completed";

            // Embed final state
            var finalState = StateReporter?.GetCurrentState();
            if (finalState != null)
            {
                finalState.ScreenState = ScreenMachine?.GetScreenState();
                response.GameState = finalState;
            }

            ModLogger.Log($"[CommandBridge] Sequence completed: {response.Sequence.StepsCompleted}/{response.Sequence.TotalSteps} steps");
            return response;
        }

        private AssertionFailure? CheckAssertions(SequenceAssert assert, int stepIndex)
        {
            // Check screen assertion against state machine
            if (assert.Screen != null && ScreenMachine != null)
            {
                var actualScreen = ScreenMachine.CurrentScreen.ToString();
                if (!string.Equals(actualScreen, assert.Screen, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssertionFailure
                    {
                        StepIndex = stepIndex,
                        Field = "screen",
                        Expected = assert.Screen,
                        Actual = actualScreen
                    };
                }
            }

            // Check cursor index from memory
            if (assert.CursorIndex != null && StateReporter != null)
            {
                var state = StateReporter.GetCurrentState();
                var actualCursor = state.UI?.CursorIndex ?? -1;
                if (actualCursor != assert.CursorIndex.Value)
                {
                    return new AssertionFailure
                    {
                        StepIndex = stepIndex,
                        Field = "cursorIndex",
                        Expected = assert.CursorIndex.Value.ToString(),
                        Actual = actualCursor.ToString()
                    };
                }
            }

            // Check tab assertion against state machine
            if (assert.Tab != null && ScreenMachine != null)
            {
                var screenState = ScreenMachine.GetScreenState();
                var actualTab = screenState.Tab ?? "";
                if (!string.Equals(actualTab, assert.Tab, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssertionFailure
                    {
                        StepIndex = stepIndex,
                        Field = "tab",
                        Expected = assert.Tab,
                        Actual = actualTab
                    };
                }
            }

            // Check sidebar index against state machine
            if (assert.SidebarIndex != null && ScreenMachine != null)
            {
                var screenState = ScreenMachine.GetScreenState();
                var actualSidebar = screenState.SidebarIndex ?? -1;
                if (actualSidebar != assert.SidebarIndex.Value)
                {
                    return new AssertionFailure
                    {
                        StepIndex = stepIndex,
                        Field = "sidebarIndex",
                        Expected = assert.SidebarIndex.Value.ToString(),
                        Actual = actualSidebar.ToString()
                    };
                }
            }

            return null;
        }

        private void WriteResponse(CommandResponse response)
        {
            try
            {
                var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_responseFilePath, json);
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[CommandBridge] Failed to write response: {ex.Message}");
            }
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
