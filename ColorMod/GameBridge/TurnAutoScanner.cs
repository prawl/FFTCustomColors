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
        public bool ShouldAutoScan(string screenName)
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
            return !_scannedThisTurn;
        }

        /// <summary>
        /// Mark that the scan has been completed for this turn.
        /// </summary>
        public void MarkScanned()
        {
            _scannedThisTurn = true;
        }
    }
}
