using System;

namespace FFTColorCustomizer.Core
{
    /// <summary>
    /// The closed event-verb glossary for every log line the mod emits (the model shared with
    /// the sibling FFT mods, theme-domain verbs instead of battle verbs). docs/LOGGING.md
    /// commits a verb table that must match this enum one-for-one; LogContractTests pins the
    /// two in lockstep. The set is CLOSED: a new subsystem reuses one of these verbs, or the
    /// doc gets amended deliberately; no ad-hoc per-module prefixes.
    /// </summary>
    public enum LogVerb
    {
        Startup,
        Config,
        Theme,
        Sprite,
        Ramza,
        Monster,
        Worldmap,
        Ui,
        Trace,
    }

    /// <summary>Enum member to the literal lowercase bracket token rendered in log lines and
    /// committed in docs/LOGGING.md's verb table.</summary>
    public static class LogVerbToken
    {
        public static string Token(this LogVerb verb) => verb switch
        {
            LogVerb.Startup => "startup",
            LogVerb.Config => "config",
            LogVerb.Theme => "theme",
            LogVerb.Sprite => "sprite",
            LogVerb.Ramza => "ramza",
            LogVerb.Monster => "monster",
            LogVerb.Worldmap => "worldmap",
            LogVerb.Ui => "ui",
            LogVerb.Trace => "trace",
            _ => throw new ArgumentOutOfRangeException(nameof(verb), verb,
                "unmapped LogVerb: add it to both the Token() switch and docs/LOGGING.md's verb table"),
        };
    }
}
