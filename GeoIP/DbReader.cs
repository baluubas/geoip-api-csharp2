using System.IO;

namespace GeoIP
{
    public class CachedDbReader : StreamDbReader
    {
        public CachedDbReader(string file) : base(ToMemoryStream(file))
        {
        }
  
        public static Stream ToMemoryStream(string file)
        {
            using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var buffer = new byte[fileStream.Length];
                fileStream.Read(buffer, 0, (int)fileStream.Length);
                return new MemoryStream(buffer);
            }
        }
    }
}
