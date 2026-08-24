using System;
using System.Collections.Generic;
using System.Drawing;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.ThemeEditor
{
    /// <summary>
    /// Drives the "Animate" checkbox's walk-cycle preview: owns the WinForms timer, the current
    /// phase, and a frame cache per pane (Modified / Original). The panel hands this class four
    /// lambdas so it never needs to know about controls directly, it just calls back with a
    /// bitmap to paint. See CC-36.
    /// </summary>
    internal sealed class WalkPreviewAnimator : IDisposable
    {
        private readonly Func<PaletteModifier> _getModified;
        private readonly Func<PaletteModifier> _getOriginal;
        private readonly Func<int> _getDirection;
        private readonly Action<Bitmap> _paintModified;
        private readonly Action<Bitmap> _paintOriginal;
        private readonly System.Windows.Forms.Timer _timer;

        public bool IsRunning { get; private set; }
        internal int Phase { get; private set; }
        internal List<Bitmap> ModifiedFrames { get; private set; }
        internal List<Bitmap> OriginalFrames { get; private set; }

        /// <summary>Test seam: the timer's current interval, so tests can pin the variable
        /// per-beat timing (<see cref="WalkCycle.BeatDurationsMs"/>) without reaching into a
        /// private field.</summary>
        internal int TimerIntervalMs => _timer.Interval;

        public WalkPreviewAnimator(
            Func<PaletteModifier> getModified,
            Func<PaletteModifier> getOriginal,
            Func<int> getDirection,
            Action<Bitmap> paintModified,
            Action<Bitmap> paintOriginal)
        {
            _getModified = getModified;
            _getOriginal = getOriginal;
            _getDirection = getDirection;
            _paintModified = paintModified;
            _paintOriginal = paintOriginal;

            _timer = new System.Windows.Forms.Timer();
            _timer.Tick += (s, e) => AdvanceOnce();
        }

        public void Start()
        {
            IsRunning = true;
            Phase = 0;
            BuildModifiedCache();
            BuildOriginalCache();
            _timer.Interval = WalkCycle.BeatDurationsMs[Phase];
            _timer.Start();
            PaintPhase();
        }

        public void Stop()
        {
            _timer.Stop();
            IsRunning = false;
            Phase = 0;
            DisposeModifiedCache();
            DisposeOriginalCache();
            // Does NOT repaint: the panel repaints the static pose after calling Stop.
        }

        /// <summary>
        /// Rebuilds only the modified-pane cache (a slider drag mid-animation); the original
        /// pane's colors haven't changed, so its frames stay valid.
        /// </summary>
        public void InvalidateModifiedFrames()
        {
            DisposeModifiedCache();
            if (IsRunning)
            {
                BuildModifiedCache();
                PaintModifiedAtCurrentPhase();
            }
        }

        /// <summary>Rebuilds BOTH caches (direction/template changes affect both panes).</summary>
        public void InvalidateFrames()
        {
            DisposeModifiedCache();
            DisposeOriginalCache();
            if (IsRunning)
            {
                BuildModifiedCache();
                BuildOriginalCache();
                PaintPhase();
            }
        }

        internal void AdvanceOnce()
        {
            if (!IsRunning)
                return;

            Phase = (Phase + 1) % WalkCycle.BeatCount;
            _timer.Interval = WalkCycle.BeatDurationsMs[Phase];
            PaintPhase();
        }

        /// <summary>
        /// Repaints both panes at the current phase if the animator is running and has usable
        /// frames. Returns false when !IsRunning or the modified cache is null/empty (unsupported
        /// template): the caller falls through to the static paint in that case.
        /// </summary>
        public bool RepaintIfRunning()
        {
            if (!IsRunning || ModifiedFrames == null || ModifiedFrames.Count == 0)
                return false;

            PaintPhase();
            return true;
        }

        private void BuildModifiedCache()
        {
            ModifiedFrames = _getModified()?.GetWalkPreviewFrames(_getDirection());
        }

        private void BuildOriginalCache()
        {
            OriginalFrames = _getOriginal()?.GetWalkPreviewFrames(_getDirection());
        }

        private void DisposeModifiedCache()
        {
            DisposeFrames(ModifiedFrames);
            ModifiedFrames = null;
        }

        private void DisposeOriginalCache()
        {
            DisposeFrames(OriginalFrames);
            OriginalFrames = null;
        }

        private static void DisposeFrames(List<Bitmap> frames)
        {
            if (frames == null)
                return;
            foreach (var frame in frames)
                frame?.Dispose();
        }

        private void PaintPhase()
        {
            PaintModifiedAtCurrentPhase();
            // Painting the hidden Original pane is harmless and keeps it in step for when
            // Compare is toggled on.
            if (OriginalFrames != null && Phase < OriginalFrames.Count)
                _paintOriginal(OriginalFrames[Phase]);
        }

        private void PaintModifiedAtCurrentPhase()
        {
            if (ModifiedFrames != null && Phase < ModifiedFrames.Count)
                _paintModified(ModifiedFrames[Phase]);
        }

        public void Dispose()
        {
            Stop();
            _timer.Dispose();
        }
    }
}
