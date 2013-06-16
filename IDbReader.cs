using System.IO;

namespace GeoIP
{
    /// <summary>
    /// An abstraction of the database file. Implementation must be thread-safe
    /// </summary>
    public interface IDbReader
    {
        int Length { get; }
        byte[] Read(int position, int count);
        byte ReadByte(int position);
        void Close();
    }
}