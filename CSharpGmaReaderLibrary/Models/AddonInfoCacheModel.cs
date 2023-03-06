namespace CSharpGmaReaderLibrary.Models
{
    [Serializable]
    public class AddonInfoCacheModel
    {
        public List<AddonInfoModel> Cache { get; set; } = new List<AddonInfoModel>();
    }
}
