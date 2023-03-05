namespace CSharpGmaReaderLibrary.Models
{
    [Serializable]
    public class FileContentModel
    {
        public string? FilePath { get; set; }
        public byte[]? Bytes { get; set; }
    }
}
