namespace CSharpGmaReaderLibrary.Models
{
    public class ReadHeaderOptions
    {
        public bool RewriteExistsCache { get; set; } = false;
        public bool ReadFilesInfo { get; set; } = true;
        public bool ReadCacheSingleTime { get; set; } = true;
        public bool UseCache { get; set; } = true;
    }
}
