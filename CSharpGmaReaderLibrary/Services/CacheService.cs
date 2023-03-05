using System.Security.Cryptography;

namespace CSharpGmaReaderLibrary.Services
{
    public class CacheService
    {
        public static async Task<string?> GetAddonFileInCachePath(string filePath)
        {
            string? fileName = await CalculateFileMD5Hash(filePath);
            if (fileName == null) return null;
            return Path.Combine(GetCacheDirectoryPath("gma"), fileName + ".gma");
        }

        public static async Task<string?> GetHeaderFileInCachePath(string filePath)
        {
            string? fileName = await CalculateFileMD5Hash(filePath);
            if (fileName == null) return null;
            return Path.Combine(GetCacheDirectoryPath("headers"), fileName + ".json");
        }

        public static async Task<string?> CalculateFileMD5Hash(string filePath)
        {
            byte[]? fileHashBytes = null;

            using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(filePath))
                    fileHashBytes = await md5.ComputeHashAsync(stream);

            if (fileHashBytes == null)
                return null;

            return BitConverter.ToString(fileHashBytes).Replace("-", "").ToLowerInvariant();
        }

        private static string GetCacheDirectoryPath(string directoryName)
        {
            string cacheDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache/" + directoryName);

            if (!Directory.Exists(cacheDirectory))
                Directory.CreateDirectory(cacheDirectory);

            return cacheDirectory;
        }
    }
}
