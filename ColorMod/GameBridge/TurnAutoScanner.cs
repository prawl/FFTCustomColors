using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Tracks battle turn state to determine when auto-scanning should occur.
    /// Auto-scan triggers once per friendly turn (Battle_MyTurn), resets when
    /// the turn ends (any non-MyTurn battle state or leaving battle).
    /// Also caches the scan response so subsequent scan_move calls within
    /// the same turn return instantly without re-scanning.
    /// </summary>
    public class BattleTurnTracker
    {
        private bool _scannedThisTurn;
        private bool _wasMyTurn;
        private int _lastUnitId = -1;
        private int _lastUnitHp = -1;

        /// <summary>Cached scan response for this turn. Null if not yet scanned.</summary>
        public CommandResponse? CachedScanResponse { get; private set; }

        /// <summary>True if a scan response is cached for this turn.</summary>
        public bool HasCachedScan => CachedScanResponse != null;

        /// <summary>Cache a scan response for reuse within this turn.</summary>
        public void CacheScanResponse(CommandResponse response)
        {
            CachedScanResponse = response;
        }

        /// <summary>
        /// Returns true if an auto-scan should be triggered for the given screen state.
        /// Only returns true on the first Battle_MyTurn detection after a turn transition.
        /// </summary>
        /// <summary>
        /// Returns true if an auto-scan should be triggered.
        /// Only returns true for player-controlled units (team 0) on Battle_MyTurn.
        /// </summary>
        public bool ShouldAutoScan(string screenName, int team = 0, int unitId = -1, int unitHp = -1)
        {
            bool isMyTurn = screenName == "Battle_MyTurn";

            // Only reset when truly transitioning to another unit's turn,
            // not when visiting submenus (Battle_Abilities, Battle_Moving, etc.)
            // which are still part of the same turn.
            bool isOtherUnitsTurn = screenName == "Battle_EnemiesTurn"
                || screenName == "Battle_AlliesTurn";
            bool leftBattle = !screenName.StartsWith("Battle");

            if (_wasMyTurn && (isOtherUnitsTurn || leftBattle))
            {
                _scannedThisTurn = false;
                _wasMyTurn = false;
                _lastUnitId = -1;
                _lastUnitHp = -1;
                CachedScanResponse = null;
            }

            // Detect unit change without intermediate enemy/ally turn.
            // battleUnitId at 0x14077D2A4 is unreliable (same value for multiple units),
            // so also check HP which differs between units.
            if (isMyTurn && _wasMyTurn && _scannedThisTurn)
            {
                bool unitChanged = (unitId >= 0 && _lastUnitId >= 0 && unitId != _lastUnitId)
                    || (unitHp >= 0 && _lastUnitHp >= 0 && unitHp != _lastUnitHp);
                if (unitChanged)
                {
                    _scannedThisTurn = false;
                    CachedScanResponse = null;
                }
            }

            // Not in battle or not my turn — don't scan
            if (!isMyTurn)
                return false;

            _wasMyTurn = true;
            if (unitId >= 0)
                _lastUnitId = unitId;
            if (unitHp >= 0)
                _lastUnitHp = unitHp;

            // Only auto-scan for player-controlled units (team 0)
            if (team != 0)
                return false;

            return !_scannedThisTurn;
        }

        /// <summary>
        /// Mark that the scan has been completed for this turn.
        /// </summary>
        public void MarkScanned()
        {
            _scannedThisTurn = true;
        }

        /// <summary>
        /// Reset the tracker for a new turn. Call this when battle_wait or similar
        /// actions complete, since they bypass the normal screen transition detection
        /// (the tracker never sees the intermediate enemy/ally phases).
        /// </summary>
        /// <summary>
        /// Clear the cached scan response without resetting the turn state.
        /// Call after game actions that change battlefield state (battle_attack,
        /// battle_ability, battle_move) so the next scan_move gets fresh data.
        /// </summary>
        /// <summary>
        /// Returns true if scanning is allowed on the given screen. Allowed during
        /// any state where the player has cursor control — not during animations,
        /// enemy turns, or post-action transitions. Kept in sync with
        /// NavigationActions.ScanMove's allowedStates so both gates agree.
        /// </summary>
        public static bool CanScan(string screenName)
        {
            return screenName == "Battle_MyTurn"
                || screenName == "Battle_Moving"
                || screenName == "Battle_Attacking"
                || screenName == "Battle_Casting"
                || screenName == "Battle_Abilities"
                || screenName == "Battle_Waiting"
                || screenName == "Battle_Paused";
        }

        public void InvalidateCache()
        {
            CachedScanResponse = null;
        }

        public void ResetForNewTurn()
        {
            _scannedThisTurn = false;
            _wasMyTurn = false;
            _lastUnitId = -1;
            _lastUnitHp = -1;
            CachedScanResponse = null;
        }
    }
}
