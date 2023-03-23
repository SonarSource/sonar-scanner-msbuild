using System;
using System.IO;

namespace TestUtilities
{
    public class TempFile : IDisposable
    {
        public string FileName { get; }

        public TempFile()
        {
            FileName = Path.GetRandomFileName();
        }
        public void Dispose()
        {
            if (File.Exists(FileName))
            {
                File.Delete(FileName);
            }
        }
    }
}
