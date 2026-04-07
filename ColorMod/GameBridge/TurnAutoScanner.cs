namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Tracks battle turn state to determine when auto-scanning should occur.
    /// Auto-scan triggers once per friendly turn (Battle_MyTurn), resets when
    /// the turn ends (any non-MyTurn battle state or leaving battle).
    /// </summary>
    public class BattleTurnTracker
    {
        private bool _scannedThisTurn;
        private bool _wasMyTurn;

        /// <summary>
        /// Returns true if an auto-scan should be triggered for the given screen state.
        /// Only returns true on the first Battle_MyTurn detection after a turn transition.
        /// </summary>
        /// <summary>
        /// Returns true if an auto-scan should be triggered.
        /// Only returns true for player-controlled units (team 0) on Battle_MyTurn.
        /// </summary>
        public bool ShouldAutoScan(string screenName, int team = 0)
        {
            bool isMyTurn = screenName == "Battle_MyTurn";

            // Transitioned away from my turn — reset for next turn
            if (_wasMyTurn && !isMyTurn)
            {
                _scannedThisTurn = false;
                _wasMyTurn = false;
            }

            // Not in battle or not my turn — don't scan
            if (!isMyTurn)
                return false;

            _wasMyTurn = true;

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
        public void ResetForNewTurn()
        {
            _scannedThisTurn = false;
            _wasMyTurn = false;
        }
    }
}
