using System;
using System.IO;

namespace Tests.Helpers
{
    public class TestFileSystem : IDisposable
    {
        public string RootPath { get; }

        public TestFileSystem(string rootPath)
        {
            RootPath = rootPath;
            if (!Directory.Exists(rootPath))
            {
                Directory.CreateDirectory(rootPath);
            }
        }

        public void Dispose()
        {
            // Clean up is handled by the test class
        }
    }
}