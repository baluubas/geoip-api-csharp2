using System.IO;

namespace GeoIP
{
    public class CachedDbReader : StreamDbReader
    {
        public CachedDbReader(string file) : base(ToMemoryStream(file))
        {
        }
        
        public CachedDbReader(Stream stream) : base(ToMemoryStream(stream))
        {
        }
  
        public static Stream ToMemoryStream(string file)
        {
            using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                return ToMemoryStream(fileStream);
            }
        }
        
        public static Stream ToMemoryStream(Stream stream)
        {
            var buffer = new byte[stream.Length];
            stream.Read(buffer, 0, (int)stream.Length);
            return new MemoryStream(buffer);
        }
    }
}
