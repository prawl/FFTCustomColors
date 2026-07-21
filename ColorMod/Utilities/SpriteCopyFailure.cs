namespace FFTColorCustomizer.Utilities
{
    public enum SpriteCopyFailureKind
    {
        Locked,
        AccessDenied
    }

    /// <summary>
    /// Truthful user-facing text for a failed sprite copy (CC-13). The old messages were
    /// DEBUG-tier and promised "theme will be applied via path redirection", a mechanism
    /// that does not run in production; the change was silently dropped. These compose the
    /// honest warning every catch site logs instead.
    /// </summary>
    public static class SpriteCopyFailure
    {
        public static string Compose(string spriteName, SpriteCopyFailureKind kind)
        {
            return kind switch
            {
                SpriteCopyFailureKind.Locked =>
                    $"Sprite {spriteName} is locked by another process; this change was NOT applied. " +
                    "Restart the game to release the file, then save the theme again.",
                _ =>
                    $"Access denied writing {spriteName}; this change was NOT applied. " +
                    "Check that the mod folder is not read-only (Program Files installs may need admin), then save again.",
            };
        }
    }
}
