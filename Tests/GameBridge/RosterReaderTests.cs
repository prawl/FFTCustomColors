using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

public class RosterReaderTests
{
    [Fact]
    public void IsEmptySlot_LevelZero_IsEmpty()
    {
        var f = new RosterReader.RawSlotFields { Level = 0, UnitIndex = 0 };
        Assert.True(RosterReader.IsEmptySlot(f));
    }

    [Fact]
    public void IsEmptySlot_TemplateWithUnitIndex0xFF_IsEmpty()
    {
        // Verified 2026-04-14 live: iterating slots 0..49 and filtering
        // unitIndex == 0xFF yields exactly the 16 displayed party members.
        // Slots past the active roster carry unitIndex=0xFF with non-zero
        // level — these are monster/generic TEMPLATES the engine keeps
        // behind the roster (Treant, duplicate Agrias/Mustadio, spare
        // Construct 8) and must NOT be surfaced.
        var template = new RosterReader.RawSlotFields
        {
            Level = 98,
            UnitIndex = 0xFF,
            SpriteSet = 0x48,   // Treant
            NameId = 0x48,
            Job = 0x48,
        };
        Assert.True(RosterReader.IsEmptySlot(template));
    }

    [Fact]
    public void IsEmptySlot_GenericActiveUnit_IsNotEmpty()
    {
        var kenrick = new RosterReader.RawSlotFields
        {
            Level = 99,
            UnitIndex = 1,
            NameId = 298,
            Job = 76,
        };
        Assert.False(RosterReader.IsEmptySlot(kenrick));
    }

    [Fact]
    public void ResolveJobName_StoryCharacter_OnCanonicalClass_ReturnsStoryJob()
    {
        // Mustadio on his canonical class: the roster +0x02 byte equals his
        // nameId (22), so we use StoryCharacterJob[22] = "Machinist".
        Assert.Equal("Machinist", RosterReader.ResolveJobName(nameId: 22, job: 22));
    }

    [Fact]
    public void ResolveJobName_StoryCharacter_ChangedJob_ReturnsCurrentJob()
    {
        // Mustadio reassigned to Knight: +0x02 becomes 76 (roster Knight),
        // no longer equals his nameId. We return the current job name.
        Assert.Equal("Knight", RosterReader.ResolveJobName(nameId: 22, job: 76));
    }

    [Fact]
    public void ResolveJobName_GenericInRosterRange_UsesRosterJobDict()
    {
        // Kenrick job=76 (0x4C) → roster dict "Knight". nameId=298 is not a
        // story character so story lookup falls through.
        Assert.Equal("Knight", RosterReader.ResolveJobName(nameId: 298, job: 76));
    }

    [Fact]
    public void ResolveJobName_RamzaWithGallantKnight_ResolvesToGallantKnight()
    {
        // Ramza: nameId=1 is not in StoryCharacterJob, so falls through to
        // roster dict where job=3 → "Gallant Knight" (his unique variant).
        Assert.Equal("Gallant Knight", RosterReader.ResolveJobName(nameId: 1, job: 3));
    }

    [Fact]
    public void ResolveJobName_ZeroJob_ReturnsNull()
    {
        Assert.Null(RosterReader.ResolveJobName(nameId: 0, job: 0));
    }

    // --- DisplayOrder sort semantics (roster +0x122) ---
    //
    // The game writes a byte at roster +0x122 per slot that holds the unit's
    // position in the PartyMenu Units grid under the current Sort option
    // (default: Time Recruited). When we sort the ReadAll() output by
    // DisplayOrder, the list should match the grid order — NOT slot order.
    // Slot 11 (Mustadio) sorts before slot 6 (Reis) under Time Recruited
    // because Mustadio was recruited earlier in the story. This test proves
    // the sort doesn't accidentally re-use slot order.

    [Fact]
    public void SortByDisplayOrder_PutsEarlierRecruitsFirst_EvenWhenSlotIndexLarger()
    {
        var reis     = new RosterReader.RosterSlot { SlotIndex = 6,  DisplayOrder = 12, Name = "Reis" };
        var mustadio = new RosterReader.RosterSlot { SlotIndex = 11, DisplayOrder = 4,  Name = "Mustadio" };
        var ramza    = new RosterReader.RosterSlot { SlotIndex = 0,  DisplayOrder = 0,  Name = "Ramza" };
        var list = new System.Collections.Generic.List<RosterReader.RosterSlot> { reis, mustadio, ramza };
        list.Sort((a, b) => a.DisplayOrder.CompareTo(b.DisplayOrder));
        Assert.Equal("Ramza",    list[0].Name);
        Assert.Equal("Mustadio", list[1].Name);
        Assert.Equal("Reis",     list[2].Name);
    }

    [Fact]
    public void DisplayOrder_IdentifiesHoveredUnit_ByGridPosition()
    {
        // Given a 5-col grid and cursor at (r1, c2), the hovered unit is
        // whoever has DisplayOrder == 1*5 + 2 == 7. This mirrors how
        // CommandWatcher resolves grid.hoveredName for the `ui=<name>`
        // label.
        var slots = new System.Collections.Generic.List<RosterReader.RosterSlot>
        {
            new() { SlotIndex = 0,  DisplayOrder = 0,  Name = "Ramza" },
            new() { SlotIndex = 11, DisplayOrder = 4,  Name = "Mustadio" },
            new() { SlotIndex = 12, DisplayOrder = 5,  Name = "Agrias" },
            new() { SlotIndex = 18, DisplayOrder = 6,  Name = "Rapha" },
            new() { SlotIndex = 19, DisplayOrder = 7,  Name = "Marach" },
        };
        const int cols = 5, cursorRow = 1, cursorCol = 2;
        int gridIdx = cursorRow * cols + cursorCol;
        var hovered = slots.Find(s => s.DisplayOrder == gridIdx);
        Assert.NotNull(hovered);
        Assert.Equal("Marach", hovered!.Name);
    }
}
