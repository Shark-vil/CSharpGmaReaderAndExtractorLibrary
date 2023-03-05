using System.Text;

namespace CSharpGmaReaderLibrary
{
    public static class BinaryReaderExtensions
    {
        public static string ReadNullTerminatedString(this BinaryReader br)
        {
            List<byte> bytes = new List<byte>();
            byte read;

            while ((read = br.ReadByte()) != 0x00)
                bytes.Add(read);

            return (bytes.Count > 0 ? Encoding.UTF8.GetString(bytes.ToArray()) : "");
        }
    }
}
