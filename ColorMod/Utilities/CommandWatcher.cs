using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        public BattleTracker? BattleTracker { get; set; }
        private NavigationActions? _navActions;
        private MapLoader? _mapLoader;

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
                ModLogger.Log("[CommandBridge] Starting polling fallback (every 50ms)");
                while (_watcher != null && !_disposed)
                {
                    try
                    {
                        if (File.Exists(_commandFilePath))
                        {
                            ProcessCommandFile();
                        }
                    }
                    catch { /* ignore polling errors */ }
                    await Task.Delay(50);
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
            Task.Run(() => ProcessCommandFile());
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
                    response.Screen ??= DetectScreenSettled();
                    response.Battle ??= BattleTracker?.Update();
                    if (response.Screen != null)
                        response.ValidPaths ??= NavigationPaths.GetPaths(response.Screen);
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
                    var errorResponse = new CommandResponse
                    {
                        Id = "unknown",
                        Status = "error",
                        Error = ex.Message,
                        ProcessedAt = DateTime.UtcNow.ToString("o")
                    };
                    errorResponse.Screen = DetectScreenSettled();
                    if (errorResponse.Screen != null)
                        errorResponse.ValidPaths = NavigationPaths.GetPaths(errorResponse.Screen);
                    WriteResponse(errorResponse);
                }
            }
        }

        private CommandRequest? ReadCommandFile()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (!File.Exists(_commandFilePath)) return null;
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

                    case "mark_blocked":
                        if (command.LocationId >= 0 && command.UnitIndex >= 0)
                        {
                            MarkTileBlocked(command.LocationId, command.UnitIndex);
                            response.Status = "completed";
                        }
                        else
                        {
                            response.Status = "failed";
                            response.Error = "locationId (gridX) and unitIndex (gridY) required";
                        }
                        break;

                    case "set_map":
                        if (command.LocationId >= 0)
                        {
                            EnsureMapLoader();
                            var map = _mapLoader?.LoadMap(command.LocationId);
                            if (map != null)
                            {
                                response.Status = "completed";
                                response.Error = $"Loaded MAP{command.LocationId:D3}: {map.Width}x{map.Height}";
                                ClearBlockedTiles();
                            }
                            else
                            {
                                response.Status = "failed";
                                response.Error = $"Failed to load MAP{command.LocationId:D3}";
                            }
                        }
                        else
                        {
                            response.Status = "failed";
                            response.Error = "locationId (map number) required, e.g. 74 for MAP074";
                        }
                        break;

                    case "scan_move":
                    case "auto_move":
                        return ExecuteNavAction(command);


                    case "heap_snapshot":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        Explorer.TakeHeapSnapshot(command.SearchLabel ?? "default");
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

                    case "write_address":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (string.IsNullOrEmpty(command.Address)) { response.Status = "failed"; response.Error = "Address required"; break; }
                        var writeAddr = Convert.ToInt64(command.Address, 16);
                        var writeVal = (byte)command.ReadSize; // reuse ReadSize field for the value to write
                        Explorer.Scanner.WriteByte((nint)writeAddr, writeVal);
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

                    case "search_bytes":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (string.IsNullOrEmpty(command.Pattern)) { response.Status = "failed"; response.Error = "Pattern required (hex string, e.g. '080B00')"; break; }
                        try
                        {
                            // Parse hex string to byte array
                            var hexClean = command.Pattern.Replace(" ", "").Replace("-", "");
                            var patternBytes = new byte[hexClean.Length / 2];
                            for (int i = 0; i < patternBytes.Length; i++)
                                patternBytes[i] = Convert.ToByte(hexClean.Substring(i * 2, 2), 16);

                            var matches = Explorer.SearchBytesInAllMemory(patternBytes, 100);

                            // Write results to file
                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine($"Byte pattern search: {command.Pattern} ({patternBytes.Length} bytes)");
                            sb.AppendLine($"Searched at: {DateTime.UtcNow:O}");
                            sb.AppendLine($"Found {matches.Count} matches");
                            sb.AppendLine();
                            var unitBase = Explorer.Scanner.UnitDataBase;
                            foreach (var (matchAddr, ctx) in matches)
                            {
                                long dist = Math.Abs((long)matchAddr - (long)unitBase);
                                string proximity = dist < 0x200000 ? " ** NEAR **" : "";
                                sb.AppendLine($"  0x{matchAddr:X}{proximity}");
                                sb.AppendLine($"    {ctx}");
                            }
                            var searchPath = System.IO.Path.Combine(_bridgeDirectory, $"search_bytes_{command.SearchLabel ?? "result"}.txt");
                            System.IO.File.WriteAllText(searchPath, sb.ToString());

                            // Also put summary in response
                            response.ReadResult = new ReadResult
                            {
                                Address = command.Pattern,
                                Size = patternBytes.Length,
                                Value = matches.Count,
                                Hex = $"{matches.Count} matches",
                                RawBytes = matches.Count > 0 ? $"0x{matches[0].address:X}" : "none"
                            };
                            response.Status = "completed";
                        }
                        catch (Exception ex)
                        {
                            response.Status = "error";
                            response.Error = $"Pattern parse error: {ex.Message}";
                        }
                        break;

                    case "write_byte":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (string.IsNullOrEmpty(command.Address)) { response.Status = "failed"; response.Error = "Address required"; break; }
                        try
                        {
                            nint wAddr = (nint)Convert.ToInt64(command.Address.Replace("0x", ""), 16);
                            byte wVal = (byte)command.SearchValue;
                            Explorer.Scanner.WriteByte(wAddr, wVal);
                            // Read back to confirm
                            byte readBack = Explorer.Scanner.ReadByte(wAddr);
                            response.ReadResult = new ReadResult { Address = command.Address, Size = 1, Value = readBack, Hex = $"0x{readBack:X2}" };
                            response.Status = "completed";
                        }
                        catch (Exception ex) { response.Status = "error"; response.Error = ex.Message; }
                        break;

                    case "batch_read":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (command.Addresses == null || command.Addresses.Count == 0) { response.Status = "failed"; response.Error = "Addresses required"; break; }
                        var batchReads = new (nint address, int size)[command.Addresses.Count];
                        for (int i = 0; i < command.Addresses.Count; i++)
                        {
                            batchReads[i] = ((nint)Convert.ToInt64(command.Addresses[i].Addr, 16), Math.Clamp(command.Addresses[i].Size, 1, 4));
                        }
                        var batchValues = Explorer.ReadMultiple(batchReads);
                        response.Reads = new List<BatchReadResult>();
                        for (int i = 0; i < batchValues.Length; i++)
                        {
                            response.Reads.Add(new BatchReadResult
                            {
                                Label = command.Addresses[i].Label,
                                Addr = command.Addresses[i].Addr,
                                Val = batchValues[i],
                                Hex = $"0x{batchValues[i]:X}"
                            });
                        }
                        response.Status = "completed";
                        break;

                    case "sequence":
                        return ExecuteSequence(command);

                    case "path":
                        return ExecuteValidPath(command);

                    case "battle_wait":
                    case "navigate":
                    case "travel":
                    case "confirm_attack":
                    case "move_to":
                    case "scan_units":
                    case "test_c_hold":
                    case "get_arrows":
                    case "move_grid":
                        return ExecuteNavAction(command);

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

            return response;
        }

        private CommandResponse ExecuteValidPath(CommandRequest command)
        {
            var response = new CommandResponse
            {
                Id = command.Id,
                ProcessedAt = DateTime.UtcNow.ToString("o"),
                GameWindowFound = true
            };

            var pathName = command.To;
            if (string.IsNullOrEmpty(pathName))
            {
                response.Status = "failed";
                response.Error = "Missing 'to' field — specify the validPath name (e.g. \"Flee\", \"PartyMenu\")";
                return response;
            }

            // Detect current screen and look up the path
            var screen = DetectScreen();
            if (screen == null)
            {
                response.Status = "failed";
                response.Error = "Could not detect current screen";
                return response;
            }

            var paths = NavigationPaths.GetPaths(screen);
            if (paths == null || !paths.TryGetValue(pathName, out var path))
            {
                var available = paths != null ? string.Join(", ", paths.Keys) : "none";
                response.Status = "failed";
                response.Error = $"No path '{pathName}' on screen '{screen.Name}'. Available: {available}";
                return response;
            }

            // If the path specifies a high-level action, delegate to it
            if (!string.IsNullOrEmpty(path.Action))
            {
                command.Action = path.Action;
                if (path.LocationId != 0) command.LocationId = path.LocationId;
                return ExecuteNavAction(command);
            }

            // Otherwise execute as a key command with the path's wait conditions
            if (path.Keys == null || path.Keys.Length == 0)
            {
                response.Status = "failed";
                response.Error = $"Path '{pathName}' has no keys or action";
                return response;
            }

            // Convert PathEntry keys to CommandRequest keys and execute
            command.Keys = new System.Collections.Generic.List<KeyCommand>();
            foreach (var k in path.Keys)
                command.Keys.Add(new KeyCommand { Vk = k.Vk, Name = k.Name });
            command.WaitForScreen = path.WaitForScreen;
            command.WaitUntilScreenNot = path.WaitUntilScreenNot;
            if (path.WaitTimeoutMs > 0) command.WaitTimeoutMs = path.WaitTimeoutMs;
            command.Action = null; // Clear action so ExecuteKeyCommand runs

            return ExecuteKeyCommand(command);
        }

        private CommandResponse ExecuteNavAction(CommandRequest command)
        {
            if (Explorer == null)
                return new CommandResponse { Id = command.Id, Status = "failed", Error = "Memory explorer not initialized", ProcessedAt = DateTime.UtcNow.ToString("o") };

            if (_navActions == null)
            {
                _navActions = new NavigationActions(_inputSimulator, Explorer, DetectScreen);
                _navActions.BattleTracker = BattleTracker;
            }
            EnsureMapLoader();
            _navActions._mapLoader = _mapLoader;
            return _navActions.Execute(command);
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

            // If keys succeeded and a wait condition is specified, poll until satisfied
            if (response.Status != "failed")
            {
                var waitResult = WaitForCondition(
                    command.WaitForScreen, command.WaitUntilScreenNot,
                    command.WaitForChange, command.WaitTimeoutMs);
                if (waitResult.screen != null)
                    response.Screen = waitResult.screen;
                if (waitResult.timedOut)
                {
                    response.Status = "completed_timeout";
                    ModLogger.Log($"[CommandBridge] Command {command.Id} keys OK but wait timed out after {command.WaitTimeoutMs}ms");
                }
            }

            ModLogger.Log($"[CommandBridge] Command {command.Id} finished: {response.Status} ({successCount}/{command.Keys.Count} keys)");
            return response;
        }

        /// <summary>
        /// Polls game state at ~5ms intervals until the requested condition is met or timeout.
        /// Returns the final detected screen and whether the wait timed out.
        /// Call TakePreSnapshot before sending keys if using waitForChange.
        /// </summary>
        private (DetectedScreen? screen, bool timedOut) WaitForCondition(
            string? waitForScreen, string? waitUntilScreenNot,
            List<string>? waitForChange, int waitTimeoutMs)
        {
            bool hasWait = waitForScreen != null
                        || waitUntilScreenNot != null
                        || (waitForChange != null && waitForChange.Count > 0);

            if (!hasWait || Explorer == null)
                return (null, false);

            int timeoutMs = Math.Clamp(waitTimeoutMs, 50, 10000);
            var sw = Stopwatch.StartNew();

            // Snapshot pre-wait values for WaitForChange
            long[]? preValues = null;
            (nint address, int size)[]? changeAddresses = null;
            if (waitForChange != null && waitForChange.Count > 0)
            {
                changeAddresses = new (nint, int)[waitForChange.Count];
                for (int i = 0; i < waitForChange.Count; i++)
                    changeAddresses[i] = ((nint)Convert.ToInt64(waitForChange[i], 16), 1);
                preValues = Explorer.ReadMultiple(changeAddresses);
            }

            DetectedScreen? lastScreen = null;

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                Thread.Sleep(5);
                lastScreen = DetectScreen();
                if (lastScreen == null) continue;

                if (waitForScreen != null)
                {
                    if (string.Equals(lastScreen.Name, waitForScreen, StringComparison.OrdinalIgnoreCase))
                        return (lastScreen, false);
                    continue;
                }

                if (waitUntilScreenNot != null)
                {
                    if (!string.Equals(lastScreen.Name, waitUntilScreenNot, StringComparison.OrdinalIgnoreCase))
                    {
                        // Screen changed — now settle: wait for 10 consecutive matching
                        // reads at 100ms intervals (1 second stable). Animations and
                        // transient states (Battle_Acting during attacks) can persist
                        // for several hundred milliseconds.
                        string newName = lastScreen.Name;
                        int stableCount = 0;
                        while (sw.ElapsedMilliseconds < timeoutMs)
                        {
                            Thread.Sleep(100);
                            var settled = DetectScreen();
                            if (settled == null) { stableCount = 0; continue; }
                            if (settled.Name == newName)
                            {
                                stableCount++;
                                lastScreen = settled;
                                if (stableCount >= 10)
                                    return (settled, false);
                            }
                            else if (string.Equals(settled.Name, waitUntilScreenNot, StringComparison.OrdinalIgnoreCase))
                            {
                                // Reverted back — was a transient blip, keep waiting
                                break;
                            }
                            else
                            {
                                // Changed to something else — restart settle
                                newName = settled.Name;
                                lastScreen = settled;
                                stableCount = 0;
                            }
                        }
                    }
                    continue;
                }

                if (changeAddresses != null && preValues != null)
                {
                    var currentValues = Explorer.ReadMultiple(changeAddresses);
                    for (int i = 0; i < preValues.Length; i++)
                    {
                        if (currentValues[i] != preValues[i])
                            return (lastScreen, false);
                    }
                }
            }

            return (lastScreen, true);
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

                // Smart wait: use condition-based polling if specified, else fall back to fixed sleep
                bool stepHasWait = step.WaitForScreen != null
                                || step.WaitUntilScreenNot != null
                                || (step.WaitForChange != null && step.WaitForChange.Count > 0);
                if (stepHasWait)
                {
                    var stepWait = WaitForCondition(
                        step.WaitForScreen, step.WaitUntilScreenNot,
                        step.WaitForChange, step.WaitTimeoutMs);
                    if (stepWait.timedOut)
                    {
                        stepResult.Status = "timeout";
                        ModLogger.Log($"[CommandBridge] Sequence step {s} wait timed out");
                    }
                }
                else if (step.WaitMs > 0)
                {
                    Thread.Sleep(step.WaitMs);
                }

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

        // Screen detection address table — indices match the ReadMultiple call below
        private static readonly (nint address, int size)[] ScreenAddresses =
        {
            ((nint)0x140D3A41E, 1),  // 0: partyFlag
            ((nint)0x140D4A264, 1),  // 1: uiFlag
            ((nint)0x14077D208, 1),  // 2: location
            ((nint)0x140787A22, 1),  // 3: hover
            ((nint)0x1407FC620, 1),  // 4: menuCursor (battle action menu: 0=Move,1=Abilities,2=Wait,3=Status,4=AutoBattle)
            ((nint)0x140900824, 1),  // 5: encA
            ((nint)0x140900828, 1),  // 6: encB
            ((nint)0x14077D2A2, 2),  // 7: battleTeam
            ((nint)0x14077CA8C, 1),  // 8: battleAct
            ((nint)0x14077CA9C, 1),  // 9: battleMov
            ((nint)0x14077D2A4, 2),  // 10: battleId
            ((nint)0x14077D2AC, 2),  // 11: battleHp
            ((nint)0x14077CA30, 4),  // 12: unitSlot0
            ((nint)0x14077CA54, 4),  // 13: unitSlot9
            ((nint)0x140C64A5C, 1),  // 14: pauseFlag
            ((nint)0x14077CA5C, 1),  // 15: moveMode (255=selecting tile, 0=not)
            ((nint)0x140900650, 1),  // 16: battleMode (3=action menu, 2=move, 0=game over/cutscene)
            ((nint)0x14077C970, 1),  // 17: cameraRotation (incrementing counter, mod 4 = current rotation 0-3)
        };

        /// <summary>
        /// Polls DetectScreen until two consecutive reads return the same screen name,
        /// ensuring the game UI has settled after a transition. Waits up to 1s.
        /// </summary>
        private DetectedScreen? DetectScreenSettled()
        {
            var first = DetectScreen();
            if (first == null) return null;

            var sw = Stopwatch.StartNew();
            string lastName = first.Name;
            DetectedScreen? last = first;
            int stableCount = 0;

            while (sw.ElapsedMilliseconds < 1000)
            {
                Thread.Sleep(50);
                var current = DetectScreen();
                if (current == null) continue;

                if (current.Name == lastName)
                {
                    stableCount++;
                    last = current;
                    if (stableCount >= 3) // 3 consecutive matches (~150ms stable)
                        return current;
                }
                else
                {
                    lastName = current.Name;
                    last = current;
                    stableCount = 0;
                }
            }

            return last;
        }

        private void EnsureMapLoader()
        {
            if (_mapLoader != null) return;
            // Look for map JSON files in claude_bridge/maps/ directory
            var mapsDir = Path.Combine(_bridgeDirectory, "maps");
            if (!Directory.Exists(mapsDir))
                Directory.CreateDirectory(mapsDir);
            _mapLoader = new MapLoader(mapsDir);
        }

        /// <summary>
        // ===== Movement Tile Validity via BFS + Learn-by-Doing Cache =====
        // BFS gives 100% recall (all valid tiles + ~12 false positives from impassable terrain).
        // When a move fails (tile is tree/obstacle), cache it as blocked for the rest of the battle.
        // This self-corrects: after 1-2 failed moves, the tile list becomes exact.
        private const long AddrTerrainGrid = 0x140C65000;
        private const int TerrainEntrySize = 7;
        private const int TerrainGridCols = 9;
        private const int TerrainGridRows = 8;
        private const int TerrainEntryCount = TerrainGridCols * TerrainGridRows; // 72

        private readonly HashSet<(int, int)> _blockedTiles = new();
        private int _lastWorldMapLocation = -1;
        private bool _battleMapAutoLoaded = false;

        private static readonly Dictionary<int, string> LocationNames = new()
        {
            {0, "Orbonne Monastery"}, {1, "Gariland"}, {2, "Mandalia Plains"},
            {3, "Eagrose Castle"}, {4, "Zeklaus Desert"}, {5, "Thieves Fort"},
            {6, "Lenalia Plateau"}, {7, "Windmill Hut"}, {8, "Fort Besselat"},
            {9, "Dorter Trade City"}, {10, "Araguay Woods"}, {11, "Zirekile Falls"},
            {12, "Zaland Fort City"}, {13, "Bariaus Hill"}, {14, "Tchigolith Fenlands"},
            {15, "Bariaus Valley"}, {16, "Lionel Castle"}, {17, "Goug Machine City"},
            {18, "Warjilis Trade City"}, {19, "Golgollada Gallows"}, {20, "Lesalia Imperial Capital"},
            {21, "Riovanes Castle"}, {22, "Yardrow Fort City"}, {23, "Walled City of Yardrow"},
            {24, "Dugeura Pass"}, {25, "Bervenia Free City"}, {26, "Siedge Weald"},
            {27, "Mount Germinas"}, {28, "Zeklaus Desert"}, {29, "Lenalia Plateau"},
            {30, "Lake Poescas"}, {31, "Dorvauldar Marsh"}, {32, "Grogh Heights"},
            {33, "Beddha Sandwaste"}, {34, "Finnath Creek"}, {35, "Balias Tor"},
            {36, "Balias Swale"}, {37, "Balias Tor"}, {38, "Mount Bervenia"},
            {39, "Zeltennia Castle"}, {40, "Limberry Castle"}, {41, "Igros Castle"},
            {42, "Mullonde"},
        };

        private static string? GetLocationName(int locationId)
        {
            return LocationNames.TryGetValue(locationId, out var name) ? name : null;
        }

        private string? _lastLocationPath;

        private string GetLastLocationPath()
        {
            if (_lastLocationPath != null) return _lastLocationPath;
            // Save in the maps directory (survives deploys), not bridge directory
            EnsureMapLoader();
            var mapsDir = _mapLoader != null ? _mapLoader.MapDataDir : _bridgeDirectory;
            _lastLocationPath = Path.Combine(mapsDir, "last_location.txt");
            return _lastLocationPath;
        }

        private void SaveLastLocation(int locationId)
        {
            _lastWorldMapLocation = locationId;
            try { File.WriteAllText(GetLastLocationPath(), locationId.ToString()); }
            catch { /* best effort */ }
        }

        private int LoadLastLocation()
        {
            try
            {
                var path = GetLastLocationPath();
                if (File.Exists(path))
                    return int.Parse(File.ReadAllText(path).Trim());
            }
            catch { }
            return -1;
        }

        /// <summary>
        /// Mark a grid tile as blocked (impassable). Called when a move attempt fails.
        /// Persists for the duration of the battle.
        /// </summary>
        public void MarkTileBlocked(int gridX, int gridY)
        {
            _blockedTiles.Add((gridX, gridY));
            ModLogger.Log($"[Tiles] Marked ({gridX},{gridY}) as blocked. Total blocked: {_blockedTiles.Count}");
        }

        /// <summary>
        /// Clear blocked tile cache (call when entering a new battle).
        /// </summary>
        public void ClearBlockedTiles()
        {
            _blockedTiles.Clear();
            _battleMapAutoLoaded = false;
        }

        /// <summary>
        /// Reads cursor grid position and computes valid movement tiles during Battle_Moving.
        /// Uses BFS with terrain heights + blocked tile cache for accuracy.
        /// </summary>
        private void PopulateBattleTileData(DetectedScreen screen)
        {
            if (Explorer == null) return;

            try
            {
                // Read cursor position from grid addresses
                var cursorXResult = Explorer.ReadAbsolute((nint)0x140C64A54, 1);
                var cursorYResult = Explorer.ReadAbsolute((nint)0x140C6496C, 1);
                if (cursorXResult != null) screen.CursorX = (int)cursorXResult.Value.value;
                if (cursorYResult != null) screen.CursorY = (int)cursorYResult.Value.value;

                if (screen.Name != "Battle_Moving")
                    return;

                // Auto-load map from location ID if not already loaded
                EnsureMapLoader();
                if (_lastWorldMapLocation < 0)
                    _lastWorldMapLocation = LoadLastLocation();
                if (!_battleMapAutoLoaded && _lastWorldMapLocation >= 0 && _mapLoader != null)
                {
                    var autoMap = _mapLoader.LoadMapForLocation(_lastWorldMapLocation);
                    if (autoMap != null)
                    {
                        ModLogger.Log($"[Tiles] Auto-loaded MAP{autoMap.MapNumber:D3} for location {_lastWorldMapLocation}");
                        ClearBlockedTiles();
                    }
                    _battleMapAutoLoaded = true;
                }

                // Read Move/Jump stats from UI buffer
                var moveResult = Explorer.ReadAbsolute((nint)0x1407AC7E4, 1);
                var jumpResult = Explorer.ReadAbsolute((nint)0x1407AC7E6, 1);
                int moveStat = moveResult != null ? (int)moveResult.Value.value : 4;
                int jumpStat = jumpResult != null ? (int)jumpResult.Value.value : 3;

                // Read unit's grid position
                var gxResult = Explorer.ReadAbsolute((nint)0x140C64A54, 1);
                var gyResult = Explorer.ReadAbsolute((nint)0x140C6496C, 1);
                int unitGX = gxResult != null ? (int)gxResult.Value.value : 0;
                int unitGY = gyResult != null ? (int)gyResult.Value.value : 0;

                // === Try JSON map data first (exact terrain) ===
                var mapData = _mapLoader?.CurrentMap;
                if (mapData != null)
                {
                    // Get enemy positions from last scan (if available)
                    var enemyPositions = _navActions?.GetEnemyPositions();

                    var validTiles = ComputeValidTilesFromMap(mapData, unitGX, unitGY, moveStat, jumpStat, enemyPositions);
                    screen.Tiles = validTiles;
                    ModLogger.Log($"[Tiles] MapBFS (MAP{mapData.MapNumber:D3}): {validTiles.Count} tiles (blocked: {_blockedTiles.Count}, enemies: {enemyPositions?.Count ?? 0}). " +
                        $"Unit=({unitGX},{unitGY}), Move={moveStat}, Jump={jumpStat}");
                    return;
                }

                // === Fallback: memory terrain grid (approximate) ===
                byte[]? terrainData = null;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var td = Explorer.Scanner.ReadBytes((nint)AddrTerrainGrid, TerrainEntryCount * TerrainEntrySize);
                    if (td.Length >= TerrainEntryCount * TerrainEntrySize)
                    {
                        int unitEntry = unitGY * TerrainGridCols + (unitGX + 1);
                        if (unitEntry >= 0 && unitEntry < TerrainEntryCount)
                        {
                            int mOff = unitEntry * TerrainEntrySize;
                            if (td[mOff + 3] == 0x1F && td[mOff + 4] == 0x1F && td[mOff + 5] == 0x1F)
                            {
                                terrainData = td;
                                break;
                            }
                        }
                    }
                    Thread.Sleep(5);
                }

                if (terrainData != null)
                {
                    var validTiles = ComputeValidTilesBFS(terrainData, unitGX, unitGY, moveStat, jumpStat);
                    screen.Tiles = validTiles;
                    ModLogger.Log($"[Tiles] MemBFS: {validTiles.Count} tiles (blocked cache: {_blockedTiles.Count}). " +
                        $"Unit=({unitGX},{unitGY}), Move={moveStat}, Jump={jumpStat}");
                    return;
                }

                // === Fallback: original tile path list ===
                var cursorIdx = Explorer.ReadAbsolute((nint)0x140C64E7C, 1);
                int idx = cursorIdx != null ? (int)cursorIdx.Value.value : 0;

                var tileData = Explorer.Scanner.ReadBytes((nint)0x140C66315, 350);
                if (tileData.Length < 7) return;

                var pathTiles = new List<TilePosition>();
                for (int i = 0; i < tileData.Length - 6; i += 7)
                {
                    int x = tileData[i];
                    int y = tileData[i + 1];
                    int flag = tileData[i + 3];

                    if (flag == 0) break;
                    if (x > 30 || y > 30) break;

                    pathTiles.Add(new TilePosition { X = x, Y = y });
                }

                if (idx >= 0 && idx < pathTiles.Count)
                {
                    screen.CursorX = pathTiles[idx].X;
                    screen.CursorY = pathTiles[idx].Y;
                }

                var seen = new HashSet<(int, int)>();
                var uniqueTiles = new List<TilePosition>();
                foreach (var t in pathTiles)
                {
                    if (seen.Add((t.X, t.Y)))
                        uniqueTiles.Add(t);
                }
                screen.Tiles = uniqueTiles;
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[CommandBridge] PopulateBattleTileData error: {ex.Message}");
            }
        }

        /// <summary>
        /// BFS computation of valid movement tiles using terrain height data.
        /// Uses min(b0,b1) as tile height, checks |height_diff| ≤ jump per step.
        /// Excludes tiles in the blocked cache (learned from failed move attempts).
        /// </summary>
        private List<TilePosition> ComputeValidTilesBFS(byte[] terrainData, int unitGX, int unitGY, int moveStat, int jumpStat)
        {
            int GetHeight(int gx, int gy)
            {
                if (gx < -1 || gx > 7 || gy < 0 || gy > 7) return -1;
                int idx = gy * TerrainGridCols + (gx + 1);
                if (idx < 0 || idx >= TerrainEntryCount) return -1;
                int off = idx * TerrainEntrySize;
                int b0 = terrainData[off];
                int b1 = terrainData[off + 1];
                if (terrainData[off + 3] == 0x1F && terrainData[off + 4] == 0x1F && terrainData[off + 5] == 0x1F)
                    return 0;
                return Math.Min(b0, b1);
            }

            var visited = new Dictionary<(int, int), int>();
            var queue = new Queue<(int x, int y, int cost)>();
            queue.Enqueue((unitGX, unitGY, 0));
            visited[(unitGX, unitGY)] = 0;

            int[][] dirs = { new[]{1,0}, new[]{-1,0}, new[]{0,1}, new[]{0,-1} };

            while (queue.Count > 0)
            {
                var (x, y, cost) = queue.Dequeue();
                if (cost >= moveStat) continue;

                foreach (var d in dirs)
                {
                    int nx = x + d[0], ny = y + d[1];
                    if (nx < 0 || ny < 0 || ny > 7) continue;

                    int nh = GetHeight(nx, ny);
                    if (nh < 0) continue;

                    // Skip tiles known to be blocked (trees, obstacles — learned from failed moves)
                    if (_blockedTiles.Contains((nx, ny))) continue;

                    int ch = GetHeight(x, y);
                    if (Math.Abs(nh - ch) > jumpStat) continue;

                    int newCost = cost + 1;
                    if (!visited.ContainsKey((nx, ny)) || visited[(nx, ny)] > newCost)
                    {
                        visited[(nx, ny)] = newCost;
                        queue.Enqueue((nx, ny, newCost));
                    }
                }
            }

            var allTiles = visited
                .OrderBy(kv => kv.Value)
                .ThenBy(kv => Math.Abs(kv.Key.Item1 - unitGX) + Math.Abs(kv.Key.Item2 - unitGY))
                .Select(kv => new TilePosition { X = kv.Key.Item1, Y = kv.Key.Item2 })
                .ToList();

            return allTiles;
        }

        /// <summary>
        /// BFS using JSON map data for exact terrain heights and passability.
        /// Grid coords = map tile coords (identity mapping).
        /// Height formula: display = height + slope_height / 2.0
        /// Jump check uses display heights. All terrain costs 1 move point.
        /// Enemy-occupied tiles block movement (can't move through or onto them).
        /// </summary>
        private List<TilePosition> ComputeValidTilesFromMap(MapData map, int unitGX, int unitGY, int moveStat, int jumpStat, HashSet<(int, int)>? enemyPositions = null)
        {
            double GetDisplayHeight(int x, int y)
            {
                if (!map.InBounds(x, y)) return -1;
                var t = map.Tiles[x, y];
                return t.Height + t.SlopeHeight / 2.0;
            }

            var visited = new Dictionary<(int, int), int>();
            var queue = new Queue<(int x, int y, int cost)>();

            if (!map.InBounds(unitGX, unitGY)) return new List<TilePosition>();

            queue.Enqueue((unitGX, unitGY, 0));
            visited[(unitGX, unitGY)] = 0;

            int[][] dirs = { new[]{1,0}, new[]{-1,0}, new[]{0,1}, new[]{0,-1} };

            while (queue.Count > 0)
            {
                var (x, y, cost) = queue.Dequeue();
                if (cost >= moveStat) continue;

                double ch = GetDisplayHeight(x, y);

                foreach (var d in dirs)
                {
                    int nx = x + d[0], ny = y + d[1];

                    if (!map.InBounds(nx, ny)) continue;
                    if (!map.IsWalkable(nx, ny)) continue;
                    if (_blockedTiles.Contains((nx, ny))) continue;
                    if (enemyPositions != null && enemyPositions.Contains((nx, ny))) continue;

                    double nh = GetDisplayHeight(nx, ny);
                    if (nh < 0 || ch < 0) continue;

                    if (Math.Abs(nh - ch) > jumpStat) continue;

                    int newCost = cost + 1;
                    if (!visited.ContainsKey((nx, ny)) || visited[(nx, ny)] > newCost)
                    {
                        visited[(nx, ny)] = newCost;
                        queue.Enqueue((nx, ny, newCost));
                    }
                }
            }

            // Exclude the starting tile (unit's own position)
            visited.Remove((unitGX, unitGY));

            return visited
                .OrderBy(kv => kv.Value)
                .ThenBy(kv => Math.Abs(kv.Key.Item1 - unitGX) + Math.Abs(kv.Key.Item2 - unitGY))
                .Select(kv => new TilePosition { X = kv.Key.Item1, Y = kv.Key.Item2 })
                .ToList();
        }

        private bool IsPartySubScreen()
        {
            if (ScreenMachine == null) return false;
            return ScreenMachine.CurrentScreen is
                GameScreen.PartyMenu or
                GameScreen.CharacterStatus or
                GameScreen.EquipmentScreen or
                GameScreen.EquipmentItemList or
                GameScreen.JobScreen or
                GameScreen.JobActionMenu or
                GameScreen.JobChangeConfirmation;
        }

        private DetectedScreen? DetectScreen()
        {
            if (Explorer == null) return null;

            try
            {
                var v = Explorer.ReadMultiple(ScreenAddresses);

                var screen = new DetectedScreen
                {
                    Location = (int)v[2],
                    Hover = (int)v[3],
                    MenuCursor = (int)v[4],
                    UiPresent = (int)v[1],
                    BattleTeam = (int)v[7],
                    BattleActed = (int)v[8],
                    BattleMoved = (int)v[9],
                    BattleUnitId = (int)v[10],
                    BattleUnitHp = (int)v[11],
                    CameraRotation = (int)(v[17] - 1 + 4) % 4,
                };

                // Track world map location for auto map loading (persists to disk)
                if (screen.Location >= 0 && screen.Location <= 42 && screen.Location != _lastWorldMapLocation)
                    SaveLastLocation(screen.Location);

                screen.LocationName = GetLocationName(screen.Location);

                int party = (int)v[0];
                int ui = (int)v[1];
                int paused = (int)v[14];
                int moveMode = (int)v[15];
                int eA = (int)v[5];
                int eB = (int)v[6];
                long slot0 = v[12];
                long slot9 = v[13];
                // Battle detection: slot0==255 && slot9==0xFFFFFFFF.
                // These slots stay stale after crash/reload, so also require that
                // we're NOT clearly on the world map (party=0, ui=0, valid location).
                bool validWorldLocation = screen.Location >= 0 && screen.Location <= 42;
                bool clearlyOnWorldMap = validWorldLocation && party == 0 && ui == 0;
                bool inBattle = (slot0 == 255 && slot9 == 0xFFFFFFFF && !clearlyOnWorldMap);

                int battleMode = (int)v[16];

                if (inBattle && paused == 1 && battleMode == 0)
                    screen.Name = "GameOver";
                else if (inBattle && paused == 1)
                    screen.Name = "Battle_Paused";
                else if (inBattle && moveMode == 255 && screen.BattleActed == 0)
                    screen.Name = "Battle_Moving";
                else if (inBattle && moveMode == 255 && screen.BattleActed == 1)
                    screen.Name = "Battle_Targeting";
                else if (inBattle && screen.BattleTeam == 0 && screen.BattleActed == 0 && screen.BattleMoved == 0)
                    screen.Name = "Battle_MyTurn";
                else if (inBattle && screen.BattleTeam == 0 && (screen.BattleActed == 1 || screen.BattleMoved == 1))
                    screen.Name = "Battle_Acting";
                else if (inBattle)
                    screen.Name = "Battle";
                else if (screen.Location == 255 || screen.Location < 0)
                    screen.Name = "TitleScreen";
                else if (eA != eB)
                    screen.Name = "EncounterDialog";
                else if (!inBattle && IsPartySubScreen())
                {
                    // State machine says we're in a party sub-screen.
                    // Trust it — memory flags (party/ui) are unreliable in sub-screens.
                    screen.Name = ScreenMachine!.CurrentScreen switch
                    {
                        GameScreen.CharacterStatus => "CharacterStatus",
                        GameScreen.EquipmentScreen => "EquipmentScreen",
                        GameScreen.EquipmentItemList => "EquipmentItemList",
                        GameScreen.JobScreen => "JobScreen",
                        GameScreen.JobActionMenu => "JobActionMenu",
                        GameScreen.JobChangeConfirmation => "JobChangeConfirmation",
                        _ => "PartyMenu"
                    };
                }
                else if (party == 1)
                    screen.Name = "PartyMenu";
                else if (party == 0 && ui == 1)
                    screen.Name = "TravelList";
                else if (party == 0 && ui == 0)
                    screen.Name = "WorldMap";
                else
                    screen.Name = "Unknown";

                // Populate cursor tile and available tiles for battle sub-states
                if (screen.Name == "Battle_Moving" || screen.Name == "Battle_Targeting")
                    PopulateBattleTileData(screen);

                // Sync state machine with memory-detected top-level screens.
                // This ensures the state machine stays in sync even after restarts
                // or when it drifts from reality.
                if (ScreenMachine != null)
                {
                    var expected = screen.Name switch
                    {
                        "WorldMap" => GameScreen.WorldMap,
                        "TitleScreen" => GameScreen.TitleScreen,
                        "PartyMenu" => GameScreen.PartyMenu,
                        "TravelList" => GameScreen.Unknown,
                        "EncounterDialog" => GameScreen.Unknown,
                        "Battle_MyTurn" or "Battle_Moving" or "Battle_Targeting" or "Battle_Acting" or "Battle_Paused" or "Battle" or "GameOver" => GameScreen.Unknown,
                        _ => (GameScreen?)null
                    };
                    if (expected.HasValue && ScreenMachine.CurrentScreen != expected.Value)
                        ScreenMachine.SetScreen(expected.Value);
                }

                return screen;
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[CommandBridge] DetectScreen error: {ex.Message}");
                return null;
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
