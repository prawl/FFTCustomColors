using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Tracks battle turn state to determine when auto-scanning should occur.
    /// Auto-scan triggers once per friendly turn (BattleMyTurn), resets when
    /// the turn ends (any non-MyTurn battle state or leaving battle).
    /// Also caches the scan response so subsequent scan_move calls within
    /// the same turn return instantly without re-scanning.
    /// </summary>
    public class BattleTurnTracker
    {
        private bool _scannedThisTurn;
        private int _lastUnitX = -1;
        private int _lastUnitY = -1;
        private bool _wasMyTurn;
        private int _lastUnitId = -1;
        private int _lastUnitHp = -1;

        /// <summary>
        /// Returns true if an auto-scan should be triggered for the given screen state.
        /// Only returns true on the first BattleMyTurn detection after a turn transition.
        /// </summary>
        /// <summary>
        /// Returns true if an auto-scan should be triggered.
        /// Only returns true for player-controlled units (team 0) on BattleMyTurn.
        /// </summary>
        public bool ShouldAutoScan(
            string screenName, int team = 0, int unitId = -1, int unitHp = -1,
            int unitX = -1, int unitY = -1)
        {
            bool isMyTurn = screenName == "BattleMyTurn";

            // Only reset when truly transitioning to another unit's turn,
            // not when visiting submenus (BattleAbilities, BattleMoving, etc.)
            // which are still part of the same turn.
            bool isOtherUnitsTurn = screenName == "BattleEnemiesTurn"
                || screenName == "BattleAlliesTurn";
            bool leftBattle = !screenName.StartsWith("Battle");

            if (_wasMyTurn && (isOtherUnitsTurn || leftBattle))
            {
                _scannedThisTurn = false;
                _wasMyTurn = false;
                _lastUnitId = -1;
                _lastUnitHp = -1;
                _lastUnitX = -1;
                _lastUnitY = -1;
            }

            // Detect unit change without intermediate enemy/ally turn.
            // battleUnitId at 0x14077D2A4 is unreliable (same value for multiple units).
            // Use HP AND position as change signals — both differ between units even
            // when they're at full health. Position is the most reliable: when the turn
            // switches from unit A at (5,3) to unit B at (8,4), position always changes.
            if (isMyTurn && _wasMyTurn && _scannedThisTurn)
            {
                bool unitChanged = (unitId >= 0 && _lastUnitId >= 0 && unitId != _lastUnitId)
                    || (unitHp >= 0 && _lastUnitHp >= 0 && unitHp != _lastUnitHp)
                    || (unitX >= 0 && _lastUnitX >= 0 && (unitX != _lastUnitX || unitY != _lastUnitY));
                if (unitChanged)
                {
                    _scannedThisTurn = false;
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
            if (unitX >= 0)
            {
                _lastUnitX = unitX;
                _lastUnitY = unitY;
            }

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
        /// Returns true if scanning is allowed on the given screen.
        /// </summary>
        public static bool CanScan(string screenName)
        {
            return screenName == "BattleMyTurn"
                || screenName == "BattleMoving"
                || screenName == "BattleAttacking"
                || screenName == "BattleCasting"
                || screenName == "BattleAbilities"
                || screenName == "BattleWaiting"
                || screenName == "BattlePaused";
        }

        public void ResetForNewTurn()
        {
            _scannedThisTurn = false;
            _wasMyTurn = false;
            _lastUnitId = -1;
            _lastUnitHp = -1;
            _lastUnitX = -1;
            _lastUnitY = -1;
        }
    }
}
