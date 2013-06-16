using System.IO;

namespace GeoIP
{
    public class StreamDbReader : IDbReader
    {
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

        public void Seek(int pos, SeekOrigin origin)
        {
            _fileStream.Seek(pos, origin);
        }

        public void Read(byte[] buffer, int offset, int length)
        {
            _fileStream.Read(buffer, offset, length);
        }

        public object ReadByte()
        {
            return _fileStream.ReadByte();
        }

        public void Close()
        {
            _fileStream.Close();
        }
    }
}