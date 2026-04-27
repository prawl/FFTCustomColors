namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure planner for direct-cursor writes. Validates the request
    /// (cursor name known, target index in range, optional screen
    /// guard), and emits one of three plans:
    ///   - <c>Skip</c>: target already equals current value, no-op.
    ///   - <c>Write</c>: emit a single-byte write to the registered address.
    ///   - <c>Reject</c>: invalid request, error message in <c>Reason</c>.
    /// </summary>
    public static class DirectCursorPlanner
    {
        public enum PlanKind { Skip, Write, Reject }

        public record Plan(
            PlanKind Kind,
            long Address,
            byte Value,
            string CursorName,
            string? Reason);

        public static Plan PlanWrite(
            string cursorName,
            int targetIndex,
            byte? currentValue,
            string? currentScreen)
        {
            var entry = DirectCursorRegistry.Get(cursorName);
            if (entry == null)
            {
                return new Plan(PlanKind.Reject, 0, 0, cursorName,
                    $"Unknown cursor '{cursorName}'. Known: [{string.Join(", ", DirectCursorRegistry.Names)}]");
            }

            if (targetIndex < entry.MinIndex || targetIndex > entry.MaxIndex)
            {
                return new Plan(PlanKind.Reject, entry.Address, 0, cursorName,
                    $"Index {targetIndex} out of range [{entry.MinIndex}..{entry.MaxIndex}] for '{cursorName}'");
            }

            if (entry.RequiredScreen != null
                && currentScreen != null
                && currentScreen != entry.RequiredScreen)
            {
                return new Plan(PlanKind.Reject, entry.Address, 0, cursorName,
                    $"Cursor '{cursorName}' requires screen '{entry.RequiredScreen}', got '{currentScreen}'");
            }

            byte targetByte = (byte)targetIndex;
            if (currentValue.HasValue && currentValue.Value == targetByte)
            {
                return new Plan(PlanKind.Skip, entry.Address, targetByte, cursorName,
                    $"Already at index {targetIndex}");
            }

            return new Plan(PlanKind.Write, entry.Address, targetByte, cursorName, null);
        }
    }
}
