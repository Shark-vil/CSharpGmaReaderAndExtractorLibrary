using Newtonsoft.Json;

namespace CSharpGmaReaderLibrary.Models
{
    [Serializable]
    public class AddonInfoModel
    {
        [JsonIgnore]
        public int Id { get; set; }

        [JsonProperty("addon_file_Hash")]
        public string? AddonFileHash { get; set; }

        [JsonProperty("source_path")]
        public string? SourcePath { get; set; }

        [JsonProperty("format_version")]
        public char FormatVersion { get; set; }

        [JsonProperty("steamid64")]
        public ulong SteamID64 { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("content")]
        public string? Content { get; set; }
            
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("author")]
        public string? Author { get; set; }

        [JsonProperty("addon_version")]
        public int AddonVersion { get; set; }

        [JsonProperty("file_block")]
        public ulong FileBlock { get; set; }

        [JsonProperty("indexes_files")]
        public IList<FileEntryModel>? IndexesFiles { get; set; } = null;
    }
}
