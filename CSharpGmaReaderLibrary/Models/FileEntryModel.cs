using Newtonsoft.Json;

namespace CSharpGmaReaderLibrary.Models
{
    [Serializable]
    public struct FileEntryModel
    {
        [JsonProperty("path")]
        public string? Path { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("crc")]
        public uint CRC { get; set; }

        [JsonProperty("file_number")]
        public uint FileNumber { get; set; }

        [JsonProperty("offset")]
        public long Offset { get; set; }
    }
}
