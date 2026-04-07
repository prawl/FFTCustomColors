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
        public EventScriptLookup? ScriptLookup { get; set; }
        private NavigationActions? _navActions;
        private MapLoader? _mapLoader;
        private readonly BattleTurnTracker _turnTracker = new();

        /// <summary>
        /// When true, game actions must go through validPaths. Raw key presses and
        /// actions not in the current screen's validPaths are blocked.
        /// Info actions (scan_move, screen, memory reads) are always allowed.
        /// </summary>
        public bool StrictMode { get; set; } = false;

        /// <summary>
        /// When true, any command that sends keys or game actions is blocked unless
        /// the previous command was a state query (no-op key press, read, or infrastructure action).
        /// Forces Claude to check state before every action.
        /// </summary>
        public bool RequireStateCheck { get; set; } = false;
        private bool _lastCommandWasQuery = false;

        // Actions that are always allowed regardless of strict mode (info/infrastructure)
        private static readonly HashSet<string> InfrastructureActions = new()
        {
            "scan_move", "scan_units", "set_map", "report_state",
            "read_address", "read_block", "batch_read",
            "mark_blocked", "snapshot", "heap_snapshot", "diff",
            "search_bytes", "search_all", "search_memory", "search_near",
            "dump_unit", "dump_all", "write_address", "set_strict", "set_map",
            "read_dialogue", "write_byte"
        };

        // Named game actions allowed in strict mode (from fft.sh helpers)
        private static readonly HashSet<string> AllowedGameActions = new()
        {
            "path", "battle_wait", "battle_flee", "battle_attack",
            "move_grid", "travel_to", "navigate", "auto_move", "get_arrows"
        };

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

                    // Only a zero-key no-op command counts as a state check (the "screen" helper)
                    bool isScreenQuery = (command.Keys != null && command.Keys.Count == 0)
                        && command.Action == null;

                    // Enforce: must call screen before any game command
                    if (RequireStateCheck && !isScreenQuery && !_lastCommandWasQuery)
                    {
                        var blocked = new CommandResponse
                        {
                            Id = command.Id,
                            Status = "blocked",
                            Error = "[STATE CHECK REQUIRED] Call 'screen' before sending game commands.",
                            ProcessedAt = DateTime.UtcNow.ToString("o"),
                            GameWindowFound = true
                        };
                        blocked.Screen = DetectScreenSettled();
                        WriteResponse(blocked);
                        _lastProcessedCommandId = command.Id;
                        return;
                    }

                    _lastCommandWasQuery = isScreenQuery;

                    var response = ExecuteCommand(command);
                    response.Screen ??= DetectScreenSettled();

                    // Auto-scan units when a new friendly turn starts
                    // TODO: Re-enable once scan_move marks turn as scanned to prevent double-scan
                    // if (response.Screen != null && _turnTracker.ShouldAutoScan(response.Screen.Name))
                    // {
                    //     ...
                    // }

                    response.Battle ??= BattleTracker?.Update();
                    // Populate map info on battle state from MapLoader
                    if (response.Battle != null && _mapLoader != null)
                    {
                        response.Battle.MapId = _mapLoader.CurrentMapNumber;
                        response.Battle.MapName = MapLoader.GetMapName(_mapLoader.CurrentMapNumber)
                            ?? response.Screen?.LocationName;
                    }
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
            if (StrictMode)
            {
                // In strict mode, only allow:
                //   1. Infrastructure actions (scan_units, read_address, etc.)
                //   2. Named game actions from fft.sh helpers (path, battle_wait, etc.)
                //   3. No-op state queries (empty keys, no action) — e.g. screen command
                // Block everything else (raw keys, sequence, unknown actions).
                bool isInfra = !string.IsNullOrEmpty(command.Action) && InfrastructureActions.Contains(command.Action);
                bool isGameAction = !string.IsNullOrEmpty(command.Action) && AllowedGameActions.Contains(command.Action);
                bool isNoOp = string.IsNullOrEmpty(command.Action) && (command.Keys == null || command.Keys.Count == 0);

                if (!isInfra && !isGameAction && !isNoOp)
                {
                    var screen = DetectScreen();
                    var paths = screen != null ? NavigationPaths.GetPaths(screen) : null;
                    var available = paths != null ? string.Join(", ", paths.Keys) : "none";
                    string reason = string.IsNullOrEmpty(command.Action)
                        ? "Raw key presses are not allowed"
                        : $"Action '{command.Action}' is not allowed";
                    return new CommandResponse
                    {
                        Id = command.Id,
                        Status = "blocked",
                        Error = $"[STRICT MODE] {reason}. Use the fft.sh helper commands: path, battle_wait, battle_attack, move_grid, scan_units, etc. Current screen: {screen?.Name}. ValidPaths: {available}",
                        ProcessedAt = DateTime.UtcNow.ToString("o"),
                        GameWindowFound = true,
                        Screen = screen,
                        ValidPaths = paths
                    };
                }
            }

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

                    case "read_dialogue":
                        if (ScriptLookup == null) { response.Status = "failed"; response.Error = "Script lookup not initialized"; break; }
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        {
                            var evtRead = Explorer.ReadAbsolute((nint)0x14077CA94, 2);
                            int evtId = evtRead.HasValue ? (int)evtRead.Value.value : 0;
                            var script = ScriptLookup.GetFormattedScript(evtId);
                            if (script != null)
                            {
                                response.Dialogue = script;
                                response.Status = "completed";
                            }
                            else
                            {
                                response.Status = "failed";
                                response.Error = $"No script found for eventId={evtId}";
                            }
                        }
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
                                _battleMapAutoLoaded = true;
                                // Cache this as the random encounter map for the current location
                                if (_lastWorldMapLocation >= 0)
                                    SaveRandomEncounterMap(_lastWorldMapLocation, command.LocationId);
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

                    case "set_strict":
                        StrictMode = command.LocationId != 0; // locationId=1 → on, 0 → off
                        response.Status = "completed";
                        response.Error = $"Strict mode: {(StrictMode ? "ON — game actions must use validPaths" : "OFF — all actions allowed")}";
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

                    case "search_all":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (string.IsNullOrEmpty(command.Pattern)) { response.Status = "failed"; response.Error = "Pattern required (hex string, e.g. '080B00')"; break; }
                        try
                        {
                            var hexAll = command.Pattern.Replace(" ", "").Replace("-", "");
                            var patternAll = new byte[hexAll.Length / 2];
                            for (int i = 0; i < patternAll.Length; i++)
                                patternAll[i] = Convert.ToByte(hexAll.Substring(i * 2, 2), 16);

                            var allMatches = Explorer.SearchBytesAllRegions(patternAll, 100);

                            var sbAll = new System.Text.StringBuilder();
                            sbAll.AppendLine($"Byte pattern search (ALL regions): {command.Pattern} ({patternAll.Length} bytes)");
                            sbAll.AppendLine($"Searched at: {DateTime.UtcNow:O}");
                            sbAll.AppendLine($"Found {allMatches.Count} matches");
                            sbAll.AppendLine();
                            var unitBaseAll = Explorer.Scanner.UnitDataBase;
                            foreach (var (matchAddr, ctx) in allMatches)
                            {
                                long dist = Math.Abs((long)matchAddr - (long)unitBaseAll);
                                string proximity = dist < 0x200000 ? " ** NEAR **" : "";
                                sbAll.AppendLine($"  0x{matchAddr:X}{proximity}");
                                sbAll.AppendLine($"    {ctx}");
                            }
                            var searchAllPath = System.IO.Path.Combine(_bridgeDirectory, $"search_all_{command.SearchLabel ?? "result"}.txt");
                            System.IO.File.WriteAllText(searchAllPath, sbAll.ToString());

                            response.ReadResult = new ReadResult
                            {
                                Address = command.Pattern,
                                Size = patternAll.Length,
                                Value = allMatches.Count,
                                Hex = $"{allMatches.Count} matches (all regions)",
                                RawBytes = allMatches.Count > 0 ? $"0x{allMatches[0].address:X}" : "none"
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
                    case "battle_flee":
                    case "battle_attack":
                    case "navigate":
                    case "travel_to":
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

        private void EnsureNavActions()
        {
            if (_navActions == null && Explorer != null)
            {
                _navActions = new NavigationActions(_inputSimulator, Explorer, DetectScreen);
                _navActions.BattleTracker = BattleTracker;
            }
            EnsureMapLoader();
            if (_navActions != null)
                _navActions._mapLoader = _mapLoader;
        }

        private CommandResponse ExecuteNavAction(CommandRequest command)
        {
            if (Explorer == null)
                return new CommandResponse { Id = command.Id, Status = "failed", Error = "Memory explorer not initialized", ProcessedAt = DateTime.UtcNow.ToString("o") };

            EnsureNavActions();
            return _navActions!.Execute(command);
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
            ((nint)0x14077CA5C, 1),  // 15: moveMode (VOLATILE/unused — was 255=selecting tile, replaced by battleMode[16]==2)
            ((nint)0x140900650, 1),  // 16: battleMode (3=action menu, 2=move, 0=game over/cutscene)
            ((nint)0x14077C970, 1),  // 17: cameraRotation (incrementing counter, mod 4 = current rotation 0-3)
            ((nint)0x140D3A10C, 1),  // 18: gameOverFlag (1=game over, 0=normal)
            ((nint)0x14077CA94, 2),  // 19: eventId (event file number during cutscenes, nameId during battle)
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
            // Settlements (0-14) — verified in-game 2026-04-06
            {0, "Royal City of Lesalia"}, {1, "Riovanes Castle"}, {2, "Eagrose Castle"},
            {3, "Lionel Castle"}, {4, "Limberry Castle"}, {5, "Zeltennia Castle"},
            {6, "Magick City of Gariland"}, {7, "Walled City of Yardrow"}, {8, "Mining Town of Gollund"},
            {9, "Merchant City of Dorter"}, {10, "Castled City of Zaland"}, {11, "Clockwork City of Goug"},
            {12, "Port City of Warjilis"}, {13, "Free City of Bervenia"}, {14, "Trade City of Sal Ghidos"},
            // Miscellaneous (15-23) — verified in-game 2026-04-06
            {15, "Ziekden Fortress"}, {16, "Mullonde"}, {17, "Brigands' Den"},
            {18, "Orbonne Monastery"}, {19, "Golgollada Gallows"}, {20, "unused"},
            {21, "Fort Besselat"}, {22, "Midlight's Deep"}, {23, "Nelveska Temple"},
            // Battlegrounds (24-42) — verified in-game 2026-04-06
            {24, "Mandalia Plain"}, {25, "Fovoham Windflats"}, {26, "The Siedge Weald"},
            {27, "Mount Bervenia"}, {28, "Zeklaus Desert"}, {29, "Lenalian Plateau"},
            {30, "Tchigolith Fenlands"}, {31, "The Yuguewood"}, {32, "Araguay Woods"},
            {33, "Grogh Heights"}, {34, "Beddha Sandwaste"}, {35, "Zeirchele Falls"},
            {36, "Dorvauldar Marsh"}, {37, "Balias Tor"}, {38, "Dugeura Pass"},
            {39, "Balias Swale"}, {40, "Finnath Creek"}, {41, "Lake Poescas"},
            {42, "Mount Germinas"},
        };

        private static string? GetLocationName(int locationId)
        {
            return LocationNames.TryGetValue(locationId, out var name) ? name : null;
        }

        private string? _lastLocationPath;

        private string GetLastLocationPath()
        {
            if (_lastLocationPath != null) return _lastLocationPath;
            // Save in bridge directory (claude_bridge/last_location.txt)
            _lastLocationPath = Path.Combine(_bridgeDirectory, "last_location.txt");
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

        private void SaveRandomEncounterMap(int locationId, int mapNumber)
        {
            try
            {
                EnsureMapLoader();
                var dir = _mapLoader?.MapDataDir ?? _bridgeDirectory;
                var path = Path.Combine(dir, "..", "random_encounter_maps.json");
                if (!File.Exists(path))
                    path = Path.Combine(dir, "random_encounter_maps.json");

                var lookup = new Dictionary<string, object>();
                if (File.Exists(path))
                {
                    var existing = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(File.ReadAllText(path));
                    if (existing != null)
                        foreach (var kv in existing)
                            if (kv.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                                lookup[kv.Key] = kv.Value.GetInt32();
                            else if (kv.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                                lookup[kv.Key] = kv.Value.GetString()!;
                }
                lookup[locationId.ToString()] = mapNumber;
                var json = System.Text.Json.JsonSerializer.Serialize(lookup, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                ModLogger.Log($"[Map] Saved random encounter map: location {locationId} → MAP{mapNumber:D3}");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[Map] Failed to save random encounter map: {ex.Message}");
            }
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
            _mapLoader?.ClearRejections();
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

                // Read Move/Jump base stats from UI buffer, then add movement ability bonus.
                // UI buffer shows BASE values (e.g., 4), not effective (e.g., 7 with Move+3).
                var moveResult = Explorer.ReadAbsolute((nint)0x1407AC7E4, 1);
                var jumpResult = Explorer.ReadAbsolute((nint)0x1407AC7E6, 1);
                int moveStat = moveResult != null ? (int)moveResult.Value.value : 4;
                int jumpStat = jumpResult != null ? (int)jumpResult.Value.value : 3;

                // Apply movement ability bonus (e.g., Move+1=0xE6, Move+2=0xE7, Move+3=0xE8)
                var battleState = BattleTracker?.Update();
                int movementAbilityId = battleState?.ActiveUnit?.MovementAbility ?? 0;
                if (movementAbilityId == 0xE6) moveStat += 1;      // Move+1
                else if (movementAbilityId == 0xE7) moveStat += 2; // Move+2
                else if (movementAbilityId == 0xE8) moveStat += 3; // Move+3
                else if (movementAbilityId == 0xEB) jumpStat += 1; // Jump+1
                else if (movementAbilityId == 0xEC) jumpStat += 2; // Jump+2
                else if (movementAbilityId == 0xED) jumpStat += 3; // Jump+3

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
                    UI = (int)v[4] switch
                    {
                        0 => "Move",
                        1 => "Abilities",
                        2 => "Wait",
                        3 => "Status",
                        4 => "AutoBattle",
                        _ => null
                    },
                };

                // Save raw location before overriding — needed for title screen detection
                int rawLocation = screen.Location;

                // During battle (location=255), use last known world map location
                if (screen.Location == 255)
                {
                    if (_lastWorldMapLocation < 0)
                        _lastWorldMapLocation = LoadLastLocation();
                    if (_lastWorldMapLocation >= 0)
                        screen.Location = _lastWorldMapLocation;
                }
                screen.LocationName = GetLocationName(screen.Location);

                int party = (int)v[0];
                int ui = (int)v[1];
                int paused = (int)v[14];
                int moveMode = (int)v[15]; // 255=tile selection active, 0=not
                int eA = (int)v[5];
                int eB = (int)v[6];
                long slot0 = v[12];
                long slot9 = v[13];
                int battleMode = (int)v[16];

                // Battle detection: use RAW location (not overridden by last_location.txt).
                // Title screen has rawLocation=255, battleMode=255, slot0=0xFFFFFFFF.
                // World map has rawLocation 0-42, battleMode=0.
                int gameOverFlag = (int)v[18];
                int eventId = (int)v[19];
                bool inBattle = (slot0 == 255 && slot9 == 0xFFFFFFFF);

                screen.Name = GameBridge.ScreenDetectionLogic.Detect(
                    party, ui, rawLocation, slot0, slot9,
                    battleMode, moveMode, paused, gameOverFlag,
                    screen.BattleTeam, screen.BattleActed, screen.BattleMoved,
                    eA, eB, !inBattle && IsPartySubScreen(), eventId);

                if (screen.Name == "Cutscene")
                    screen.EventId = eventId;

                // Resolve party sub-screen to specific screen via state machine
                if (screen.Name == "PartySubScreen")
                {
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

                // Track world map location for auto map loading (persists to disk).
                // Uses rawLocation (before battle override) to avoid the stale-check bug
                // where screen.Location == _lastWorldMapLocation after override → never saves.
                if (GameBridge.LocationSaveLogic.ShouldSave(rawLocation, screen.Name, _lastWorldMapLocation))
                    SaveLastLocation(rawLocation);

                // Reset map auto-load flag when not in battle
                if (!inBattle && _battleMapAutoLoaded)
                    _battleMapAutoLoaded = false;

                // Auto-load map when first entering battle (any battle screen)
                if (inBattle && !_battleMapAutoLoaded)
                {
                    EnsureMapLoader();
                    if (_lastWorldMapLocation < 0)
                        _lastWorldMapLocation = LoadLastLocation();

                    // Try location-based lookup (fast, validated later by scan_move)
                    if (_lastWorldMapLocation >= 0 && _mapLoader != null)
                    {
                        var autoMap = _mapLoader.LoadMapForLocation(_lastWorldMapLocation);
                        if (autoMap != null)
                            ModLogger.Log($"[Map] Auto-loaded MAP{autoMap.MapNumber:D3} for location {_lastWorldMapLocation} (will validate on scan_move)");
                    }

                    _battleMapAutoLoaded = true;
                }

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
