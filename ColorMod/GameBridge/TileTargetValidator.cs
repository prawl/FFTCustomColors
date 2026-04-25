namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Validate target tile coords for battle_move / battle_attack /
    /// battle_ability up front. Default CommandRequest field values
    /// (LocationId=-1, UnitIndex=0) appear when the caller sends wrong
    /// JSON keys like <c>{"x":8,"y":11}</c> instead of
    /// <c>{"locationId":8,"unitIndex":11}</c>. Without this validator,
    /// the nav loop runs on the bogus coords and emits a confusing
    /// downstream error like <c>Cursor miss: at (0,0) expected (-1,0)</c>
    /// — surfaces the wrong layer.
    /// </summary>
    public static class TileTargetValidator
    {
        public static string? Validate(int x, int y, string actionName)
        {
            if (x < 0 || y < 0 || x > 31 || y > 31)
            {
                return $"{actionName}: invalid target tile ({x},{y}). " +
                       "Pass tile coords as 'locationId' (x) and 'unitIndex' (y) JSON fields. " +
                       "Use the fft.sh helper instead of raw JSON to avoid this: e.g. `battle_attack 8 11`.";
            }
            return null;
        }
    }
}
