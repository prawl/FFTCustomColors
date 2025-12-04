namespace FFTColorMod
{
    public class PacExtractor
    {
        // TLDR: Extracts sprite files from FFT PAC archives

        public bool OpenPac(string path)
        {
            // TLDR: Opens a PAC file for extraction
            if (string.IsNullOrEmpty(path)) return false;
            return true;
        }

        public int GetFileCount()
        {
            // TLDR: Returns number of files in the PAC
            return 0;
        }

        public string GetFileName(int index)
        {
            // TLDR: Returns name of file at index
            return null;
        }

        public int GetFileSize(int index)
        {
            // TLDR: Returns size of file at index
            return 0;
        }

        public byte[] ExtractFile(int index)
        {
            // TLDR: Extracts file data at index
            return null;
        }
    }
}