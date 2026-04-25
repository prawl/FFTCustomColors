using System;
using System.IO;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Move a malformed command.json out of the way so the watcher
    /// loop doesn't re-read it forever. Without this, a single bad
    /// write (e.g. a shell helper that wrote a raw command name like
    /// <c>screen</c> instead of JSON) jams the bridge for the rest of
    /// the session — every poll re-reads the bad file, re-logs the
    /// parse error, and never advances.
    ///
    /// Quarantined files are renamed to
    /// <c>command.json.bad-{UTC-timestamp}[-N]</c> so the bad payload
    /// is preserved for inspection while the bridge unblocks.
    /// </summary>
    public static class CommandFileQuarantine
    {
        public static string? Quarantine(string commandFilePath, DateTime utcNow)
        {
            if (!File.Exists(commandFilePath)) return null;

            var dir = Path.GetDirectoryName(commandFilePath) ?? ".";
            var baseStamp = utcNow.ToString("yyyyMMddTHHmmssZ");
            var basename = $"{Path.GetFileName(commandFilePath)}.bad-{baseStamp}";

            var dest = Path.Combine(dir, basename);
            int counter = 1;
            while (File.Exists(dest))
            {
                dest = Path.Combine(dir, $"{basename}-{counter}");
                counter++;
            }

            File.Move(commandFilePath, dest);
            return dest;
        }
    }
}
