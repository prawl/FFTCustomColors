using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class JobGridLayoutTests
    {
        [Fact]
        public void Ramza_Row0_HasGallantKnightAt00()
        {
            // AC2: Ramza defaults to Gallant Knight (grid pos 0,0).
            Assert.Equal("Gallant Knight",
                JobGridLayout.GetClassAt(JobGridLayout.CharacterKind.Ramza, 0, 0));
        }

        [Fact]
        public void GenericMale_Row0_HasSquireAt00()
        {
            // AC3: Generics default to Squire at (0,0).
            Assert.Equal("Squire",
                JobGridLayout.GetClassAt(JobGridLayout.CharacterKind.GenericMale, 0, 0));
        }

        [Fact]
        public void GenericMale_Row2_LastCellIsBard()
        {
            // AC7: Bard is male-only. Last cell of row 2 is Bard for males.
            // (Grid is 6/7/6 → row 2 last col index = 5.)
            int rowWidth = JobGridLayout.GetRowWidth(JobGridLayout.CharacterKind.GenericMale, 2);
            Assert.Equal("Bard",
                JobGridLayout.GetClassAt(JobGridLayout.CharacterKind.GenericMale, 2, rowWidth - 2));
        }

        [Fact]
        public void GenericFemale_Row2_HasDancerWhereMalesHaveBard()
        {
            // AC8: Dancer is female-only — same cell as Bard for males.
            int rowWidth = JobGridLayout.GetRowWidth(JobGridLayout.CharacterKind.GenericFemale, 2);
            Assert.Equal("Dancer",
                JobGridLayout.GetClassAt(JobGridLayout.CharacterKind.GenericFemale, 2, rowWidth - 2));
        }

        [Fact]
        public void Ramza_RowWidths_Are_6_7_6()
        {
            // Verified live 2026-04-15: Ramza Ch4 JobSelection is 6/7/6.
            Assert.Equal(6, JobGridLayout.GetRowWidth(JobGridLayout.CharacterKind.Ramza, 0));
            Assert.Equal(7, JobGridLayout.GetRowWidth(JobGridLayout.CharacterKind.Ramza, 1));
            Assert.Equal(6, JobGridLayout.GetRowWidth(JobGridLayout.CharacterKind.Ramza, 2));
        }

        [Fact]
        public void Ramza_Row1_SeventhCellIsGeomancer()
        {
            // Live-verified: 6 Rights from Black Mage lands on Geomancer (col 6).
            Assert.Equal("Geomancer",
                JobGridLayout.GetClassAt(JobGridLayout.CharacterKind.Ramza, 1, 6));
        }

        [Fact]
        public void Ramza_Row2_HasMimeAtLastCell()
        {
            // Live-verified: Mime is the last cell of row 2 (col 5).
            Assert.Equal("Mime",
                JobGridLayout.GetClassAt(JobGridLayout.CharacterKind.Ramza, 2, 5));
        }

        [Fact]
        public void Ramza_TotalCells_Is_19()
        {
            Assert.Equal(19, JobGridLayout.TotalCells(JobGridLayout.CharacterKind.Ramza));
        }

        [Fact]
        public void GetClassAt_OutOfRange_ReturnsNull()
        {
            Assert.Null(JobGridLayout.GetClassAt(JobGridLayout.CharacterKind.Ramza, -1, 0));
            Assert.Null(JobGridLayout.GetClassAt(JobGridLayout.CharacterKind.Ramza, 99, 0));
            Assert.Null(JobGridLayout.GetClassAt(JobGridLayout.CharacterKind.Ramza, 0, 99));
            // Row 0 has only 6 cols, so col 6 is out of range
            Assert.Null(JobGridLayout.GetClassAt(JobGridLayout.CharacterKind.Ramza, 0, 6));
        }

        [Theory]
        [InlineData(0, 0, 0)]   // Gallant Knight
        [InlineData(5, 0, 5)]   // White Mage (row 0 last)
        [InlineData(6, 1, 0)]   // Black Mage (row 1 first)
        [InlineData(12, 1, 6)]  // Geomancer (row 1 7th)
        [InlineData(13, 2, 0)]  // Dragoon (row 2 first)
        [InlineData(18, 2, 5)]  // Mime (row 2 last)
        public void Ramza_IndexToRowCol_FlatLinear(int index, int expectedRow, int expectedCol)
        {
            var rc = JobGridLayout.IndexToRowCol(JobGridLayout.CharacterKind.Ramza, index);
            Assert.NotNull(rc);
            Assert.Equal(expectedRow, rc!.Value.Row);
            Assert.Equal(expectedCol, rc.Value.Col);
        }

        [Fact]
        public void Ramza_IndexToRowCol_OutOfRange_ReturnsNull()
        {
            Assert.Null(JobGridLayout.IndexToRowCol(JobGridLayout.CharacterKind.Ramza, -1));
            Assert.Null(JobGridLayout.IndexToRowCol(JobGridLayout.CharacterKind.Ramza, 19));
            Assert.Null(JobGridLayout.IndexToRowCol(JobGridLayout.CharacterKind.Ramza, 100));
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(0, 5, 5)]
        [InlineData(1, 0, 6)]
        [InlineData(1, 6, 12)]
        [InlineData(2, 0, 13)]
        [InlineData(2, 5, 18)]
        public void Ramza_RowColToIndex_FlatLinear(int row, int col, int expectedIndex)
        {
            Assert.Equal(expectedIndex,
                JobGridLayout.RowColToIndex(JobGridLayout.CharacterKind.Ramza, row, col));
        }

        [Fact]
        public void Ramza_RowColToIndex_OutOfRange_ReturnsNegativeOne()
        {
            // Row 0 only has 6 cols
            Assert.Equal(-1, JobGridLayout.RowColToIndex(JobGridLayout.CharacterKind.Ramza, 0, 6));
            Assert.Equal(-1, JobGridLayout.RowColToIndex(JobGridLayout.CharacterKind.Ramza, 99, 0));
        }

        [Fact]
        public void EnumerateCells_Ramza_Returns19Cells()
        {
            int count = 0;
            foreach (var _ in JobGridLayout.EnumerateCells(JobGridLayout.CharacterKind.Ramza))
                count++;
            Assert.Equal(19, count);
        }

        [Fact]
        public void EnumerateCells_OrderedRowMajor()
        {
            var cells = System.Linq.Enumerable.ToList(
                JobGridLayout.EnumerateCells(JobGridLayout.CharacterKind.Ramza));
            Assert.Equal("Gallant Knight", cells[0].ClassName);
            Assert.Equal("Chemist", cells[1].ClassName);
            Assert.Equal("Geomancer", cells[12].ClassName);
            Assert.Equal("Mime", cells[18].ClassName);
        }

        // --- ForUnit API (story-character + gender aware) ---

        [Theory]
        [InlineData("Ramza", "Gallant Knight")]
        [InlineData("Agrias", "Holy Knight")]
        [InlineData("Mustadio", "Machinist")]
        [InlineData("Orlandeau", "Thunder God")]
        [InlineData("Cloud", "Soldier")]
        [InlineData("Rapha", "Skyseer")]
        [InlineData("Marach", "Netherseer")]
        [InlineData("Beowulf", "Templar")]
        [InlineData("Construct 8", "Steel Giant")]
        [InlineData("Reis", "Dragonkin")]
        [InlineData("Meliadoul", "Divine Knight")]
        public void ForUnit_StoryCharacter_HasUniqueClassAt00(string unitName, string expectedClass)
        {
            // Live-verified 2026-04-15 for Ramza (Gallant Knight) + Agrias
            // (Holy Knight). Other entries inferred from the roster job
            // ID → name table and the rule that story units' starting
            // job is their unique class.
            var layout = JobGridLayout.ForUnit(unitName, isFemale: false);
            Assert.Equal(expectedClass, layout.GetClassAt(0, 0));
        }

        [Fact]
        public void ForUnit_Generic_HasSquireAt00()
        {
            var layout = JobGridLayout.ForUnit(null, isFemale: false);
            Assert.Equal("Squire", layout.GetClassAt(0, 0));
        }

        [Fact]
        public void ForUnit_UnknownName_FallsBackToSquire()
        {
            // An unrecognized unit name (e.g. a user-renamed generic)
            // should use the generic Squire slot, not a story class.
            var layout = JobGridLayout.ForUnit("Kenrick", isFemale: false);
            Assert.Equal("Squire", layout.GetClassAt(0, 0));
        }

        [Fact]
        public void ForUnit_Male_HasBardAt_Row2_Col4()
        {
            var layout = JobGridLayout.ForUnit(null, isFemale: false);
            Assert.Equal("Bard", layout.GetClassAt(2, 4));
        }

        [Fact]
        public void ForUnit_Female_HasDancerAt_Row2_Col4()
        {
            var layout = JobGridLayout.ForUnit(null, isFemale: true);
            Assert.Equal("Dancer", layout.GetClassAt(2, 4));
        }

        [Fact]
        public void ForUnit_Agrias_Female_HolyKnight_Dancer()
        {
            // Live-verified 2026-04-15: Agrias is female, grid shows Holy
            // Knight at (0,0) and Dancer at (2,4).
            var layout = JobGridLayout.ForUnit("Agrias", isFemale: true);
            Assert.Equal("Holy Knight", layout.GetClassAt(0, 0));
            Assert.Equal("Dancer", layout.GetClassAt(2, 4));
        }

        [Fact]
        public void ForUnit_Ramza_Male_GallantKnight_Bard()
        {
            var layout = JobGridLayout.ForUnit("Ramza", isFemale: false);
            Assert.Equal("Gallant Knight", layout.GetClassAt(0, 0));
            Assert.Equal("Bard", layout.GetClassAt(2, 4));
        }

        [Fact]
        public void ForUnit_RowWidths_Are_6_7_6()
        {
            var layout = JobGridLayout.ForUnit("Ramza", isFemale: false);
            Assert.Equal(6, layout.GetRowWidth(0));
            Assert.Equal(7, layout.GetRowWidth(1));
            Assert.Equal(6, layout.GetRowWidth(2));
            Assert.Equal(19, layout.TotalCells);
        }

        [Fact]
        public void ForUnit_IndexToRowCol_Agrias_6_MapsToBlackMage()
        {
            // Flat index 6 = row 1 col 0 (first cell of row 1).
            var layout = JobGridLayout.ForUnit("Agrias", isFemale: true);
            var rc = layout.IndexToRowCol(6);
            Assert.NotNull(rc);
            Assert.Equal(1, rc!.Value.Row);
            Assert.Equal(0, rc.Value.Col);
            Assert.Equal("Black Mage", layout.GetClassAt(rc.Value.Row, rc.Value.Col));
        }

        [Theory]
        [InlineData(0x01, false)] // Chemist male
        [InlineData(0x02, true)]  // Chemist female
        [InlineData(0x03, false)] // Knight male
        [InlineData(0x04, true)]  // Knight female
        [InlineData(0x21, false)] // Bard (male only)
        [InlineData(0x22, true)]  // Dancer (female only)
        [InlineData(0x4A, true)]  // Squire female (even = female)
        [InlineData(0x4B, false)] // Squire male (odd = male)
        [InlineData(0xA4, true)]  // Dark Knight female
        [InlineData(0xA5, false)] // Dark Knight male
        [InlineData(0x4C, false)] // Holy Knight (story) — out of generic range
        [InlineData(0xFF, false)] // Unknown/out of range
        public void IsGenericFemale_ParityRule(int jobId, bool expected)
        {
            Assert.Equal(expected, JobGridLayout.IsGenericFemale(jobId));
        }
    }
}
