using System.Collections.Generic;
using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Stat-change events: when a unit's Speed / PA / MA changes between
    /// scans (e.g. Speed Surge reaction firing on hit, Tailwind buff
    /// landing), the narrator should emit `> Wilham: Speed +1` events
    /// alongside the existing damaged/status/ko events. Without this,
    /// Speed Surge etc. fire silently and an LLM driver can't notice
    /// the +1 unless they manually re-scan and read Speed.
    ///
    /// Backwards-compatible: stat fields default to 0; UnitSnaps
    /// constructed without them produce no stat events.
    /// </summary>
    public class UnitScanDiffStatChangeTests
    {
        private static UnitScanDiff.UnitSnap Snap(
            string? name, int x = 5, int y = 5, int hp = 100, int maxHp = 100,
            int speed = 0, int pa = 0, int ma = 0)
            => new(name, RosterNameId: 0, Team: 0,
                GridX: x, GridY: y, Hp: hp, MaxHp: maxHp,
                Statuses: null, ClassFingerprint: null,
                Speed: speed, PA: pa, MA: ma);

        [Fact]
        public void NoStatFields_ProducesNoStatEvent()
        {
            // UnitSnap constructed without stat args → fields default 0 →
            // pre/post both 0 → no stat delta. Backwards-compatible.
            var before = new List<UnitScanDiff.UnitSnap> { Snap("Wilham") };
            var after = new List<UnitScanDiff.UnitSnap> { Snap("Wilham") };
            Assert.Empty(UnitScanDiff.Compare(before, after));
        }

        [Fact]
        public void SpeedIncrease_EmitsStatEvent()
        {
            // Speed Surge fires on hit → +1 speed. No HP change, no
            // status change — but the speed delta needs to surface.
            var before = new List<UnitScanDiff.UnitSnap> { Snap("Wilham", speed: 8) };
            var after = new List<UnitScanDiff.UnitSnap> { Snap("Wilham", speed: 9) };
            var events = UnitScanDiff.Compare(before, after);
            var stat = Assert.Single(events);
            Assert.Equal("stat", stat.Kind);
            Assert.NotNull(stat.StatDeltas);
            Assert.Contains("Speed +1", stat.StatDeltas);
        }

        [Fact]
        public void SpeedDecrease_EmitsNegativeDelta()
        {
            // Slow / Speed Save / similar.
            var before = new List<UnitScanDiff.UnitSnap> { Snap("Wilham", speed: 9) };
            var after = new List<UnitScanDiff.UnitSnap> { Snap("Wilham", speed: 7) };
            var events = UnitScanDiff.Compare(before, after);
            var stat = Assert.Single(events);
            Assert.Contains("Speed -2", stat.StatDeltas);
        }

        [Fact]
        public void PaIncrease_EmitsStatEvent()
        {
            // Berserk / Tailwind / Steel buff.
            var before = new List<UnitScanDiff.UnitSnap> { Snap("Ramza", pa: 12) };
            var after = new List<UnitScanDiff.UnitSnap> { Snap("Ramza", pa: 14) };
            var events = UnitScanDiff.Compare(before, after);
            var stat = Assert.Single(events);
            Assert.Contains("PA +2", stat.StatDeltas);
        }

        [Fact]
        public void MaIncrease_EmitsStatEvent()
        {
            // Magick Boost / Magick Save / similar.
            var before = new List<UnitScanDiff.UnitSnap> { Snap("Wizard", ma: 10) };
            var after = new List<UnitScanDiff.UnitSnap> { Snap("Wizard", ma: 11) };
            var events = UnitScanDiff.Compare(before, after);
            var stat = Assert.Single(events);
            Assert.Contains("MA +1", stat.StatDeltas);
        }

        [Fact]
        public void MultipleStatChanges_GroupedInOneEvent()
        {
            // Heroic Speedy Speed (or some big buff) — Speed AND PA up.
            var before = new List<UnitScanDiff.UnitSnap> { Snap("Hero", speed: 8, pa: 12) };
            var after = new List<UnitScanDiff.UnitSnap> { Snap("Hero", speed: 9, pa: 14) };
            var events = UnitScanDiff.Compare(before, after);
            var stat = Assert.Single(events);
            Assert.Equal(2, stat.StatDeltas!.Count);
            Assert.Contains("Speed +1", stat.StatDeltas);
            Assert.Contains("PA +2", stat.StatDeltas);
        }

        [Fact]
        public void StatChangeAlongsideDamage_EmitsBothInOneEvent()
        {
            // Speed Surge fires when hit — both happen in the same diff.
            // Render as a damaged event with stat deltas attached so the
            // narrator can show "Wilham took 30 dmg, Speed +1".
            var before = new List<UnitScanDiff.UnitSnap> { Snap("Wilham", hp: 100, speed: 8) };
            var after = new List<UnitScanDiff.UnitSnap> { Snap("Wilham", hp: 70, speed: 9) };
            var events = UnitScanDiff.Compare(before, after);
            var ev = Assert.Single(events);
            Assert.Equal("damaged", ev.Kind);
            Assert.Equal(100, ev.OldHp);
            Assert.Equal(70, ev.NewHp);
            Assert.NotNull(ev.StatDeltas);
            Assert.Contains("Speed +1", ev.StatDeltas);
        }

        [Fact]
        public void RenderEvent_StatOnly_FormatsCleanly()
        {
            var ev = new UnitScanDiff.ChangeEvent(
                Label: "Wilham", Team: "PLAYER",
                OldXY: null, NewXY: null,
                OldHp: null, NewHp: null,
                StatusesGained: null, StatusesLost: null,
                Kind: "stat",
                StatDeltas: new List<string> { "Speed +1" });
            var rendered = UnitScanDiff.RenderEvent(ev);
            Assert.Contains("Wilham", rendered);
            Assert.Contains("Speed +1", rendered);
        }
    }
}
