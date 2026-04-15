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
    }
}
