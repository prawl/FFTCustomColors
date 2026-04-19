namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Tracks which in-game dialogue box the player is currently viewing.
    /// The tracker is a pure serial counter: callers increment it on
    /// advance actions and query it for display. The counter resets to 0
    /// whenever the observed eventId differs from the last-observed one —
    /// including when the player returns to a previously-seen event, since
    /// the bridge can't distinguish "replay from start" from "continuation."
    ///
    /// Not thread-safe. Intended to be owned by <c>CommandWatcher</c>, which
    /// serializes all bridge actions on the command-processing thread.
    /// </summary>
    public class DialogueProgressTracker
    {
        private int _eventId = -1;
        private int _boxIndex = 0;

        /// <summary>
        /// Return the current box index for <paramref name="eventId"/>.
        /// Resets the counter if the event is different from the last
        /// observed one.
        /// </summary>
        public int GetBoxIndex(int eventId)
        {
            if (eventId != _eventId)
            {
                _eventId = eventId;
                _boxIndex = 0;
            }
            return _boxIndex;
        }

        /// <summary>
        /// Increment the counter. If <paramref name="eventId"/> differs from
        /// the currently-tracked event, this is treated as a scene change —
        /// the counter resets to 0 first, then increments to 1 (reflecting
        /// that an advance has been issued on the new event).
        /// </summary>
        public void Advance(int eventId)
        {
            if (eventId != _eventId)
            {
                _eventId = eventId;
                _boxIndex = 0;
            }
            _boxIndex++;
        }
    }
}
