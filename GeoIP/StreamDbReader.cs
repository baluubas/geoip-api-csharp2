using System.IO;

namespace GeoIP
{
    public class StreamDbReader : IDbReader
    {
        private static readonly object pointerLock = new object();
        private readonly Stream _fileStream;

        public StreamDbReader(string dbPath) : this(new FileStream(dbPath, FileMode.Open, FileAccess.Read))
        {
        } 

        public StreamDbReader(Stream fileStream)
        {
            _fileStream = fileStream;
            Length = (int)fileStream.Length;
        }

        public int Length { get; private set; }

        public byte[] Read(int position, int count)
        {
            var buffer = new byte[count];
            lock (pointerLock)
            {
                _fileStream.Seek(position, SeekOrigin.Begin);
                _fileStream.Read(buffer, 0, count);
            }
  
            return buffer;
        }

        public byte ReadByte(int position)
        {
            return Read(position, 1)[0];
        }

        public void Close()
        {
            _fileStream.Close();
        }
    }
}