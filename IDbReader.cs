using System.IO;

namespace GeoIP
{
    public interface IDbReader
    {
        int Length { get; }
        void Seek(int pos, SeekOrigin origin);
        void Read(byte[] buffer, int offset, int length);
        object ReadByte();
        void Close();
    }
}