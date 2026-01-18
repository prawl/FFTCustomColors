namespace FFTColorCustomizer.Configuration
{
    public class RamzaChapterHslSettings
    {
        public int HueShift { get; set; }
        public int SaturationShift { get; set; }
        public int LightnessShift { get; set; }
        public bool Enabled { get; set; }
    }

    public class RamzaHslSettings
    {
        public RamzaChapterHslSettings Chapter1 { get; set; } = new();
        public RamzaChapterHslSettings Chapter2 { get; set; } = new();
        public RamzaChapterHslSettings Chapter4 { get; set; } = new();

        public void Reset()
        {
            Chapter1 = new RamzaChapterHslSettings();
            Chapter2 = new RamzaChapterHslSettings();
            Chapter4 = new RamzaChapterHslSettings();
        }
    }
}
