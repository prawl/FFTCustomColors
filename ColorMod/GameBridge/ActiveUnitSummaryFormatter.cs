namespace FFTColorCustomizer.GameBridge
{
    public static class ActiveUnitSummaryFormatter
    {
        public static string Format(
            string? name, string? jobName, int x, int y, int hp, int maxHp,
            string? weaponTag = null,
            string? disabledTag = null)
        {
            string banner;
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(jobName))
                banner = $"{name}({jobName})";
            else if (!string.IsNullOrEmpty(name))
                banner = name!;
            else if (!string.IsNullOrEmpty(jobName))
                banner = $"({jobName})";
            else
                return "";

            var result = $"{banner} ({x},{y})";
            if (maxHp > 0)
                result += $" HP={hp}/{maxHp}";
            if (!string.IsNullOrEmpty(weaponTag))
                result += $" [{weaponTag}]";
            // Disabled tag (e.g. "DontAct(no act)") goes last so it sits at
            // the end of the active-unit banner where the existing
            // acted/moved tags also live in fft.sh.
            if (!string.IsNullOrEmpty(disabledTag))
                result += $" {disabledTag}";
            return result;
        }
    }
}
