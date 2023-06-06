using System.Security.Cryptography;

namespace CSharpGmaReaderLibrary.Services
{
    public class CacheService
    {
		/*
        public static async Task<string?> GetAddonFileInCachePathAsync(string filePath)
        {
#if DEBUG
			Console.WriteLine(string.Format("[CacheService] Run GetAddonFileInCachePathAsync({0})", Path.GetFileName(filePath)));
#endif
			string? fileName = await CalculateFileMD5HashAsync(filePath);
            if (fileName == null) return null;
            return Path.Combine(GetCacheDirectoryPath("gma"), fileName + ".gma");
        }
        */

		public static string? GetHeaderFileInCachePath(string filePath)
		{
#if DEBUG
			Console.WriteLine(string.Format("[CacheService] Run GetHeaderFileInCachePathAsync({0})", Path.GetFileName(filePath)));
#endif
			using (MD5 md5 = MD5.Create())
			{
				string inputValue = Path.GetFileName(Path.GetDirectoryName(filePath)) + Path.GetFileNameWithoutExtension(filePath);
#if DEBUG
				Console.WriteLine(string.Format("[CacheService][GetHeaderFileInCachePath] inputValue: {0}", inputValue));
#endif
				byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(inputValue);
				byte[] hashBytes = md5.ComputeHash(inputBytes);
				string hashString = Convert.ToHexString(hashBytes);
				DateTime fileLastWriteTime = File.GetLastWriteTime(filePath);
				string lastWriteTimeString = fileLastWriteTime.ToString("yyyyMMddHHmmssffff");
				return Path.Combine(GetCacheDirectoryPath("headers/files/"), hashString + "_" + lastWriteTimeString + ".json");
			}
		}

		/*
        public static async Task<string?> GetHeaderFileInCachePathAsync(string filePath)
        {
#if DEBUG
			Console.WriteLine(string.Format("[CacheService] Run GetHeaderFileInCachePathAsync({0})", Path.GetFileName(filePath)));
#endif

			string? fileName = await CalculateFileMD5HashAsync(filePath);
            if (fileName == null) return null;
            return Path.Combine(GetCacheDirectoryPath("headers/files/"), fileName + ".json");
		}
        */

		public static string? GetAddonFileInCachePath(string filePath)
		{
#if DEBUG
			Console.WriteLine(string.Format("[CacheService] Run GetAddonFileInCachePath({0})", Path.GetFileName(filePath)));
#endif
			using (MD5 md5 = MD5.Create())
			{
				string inputValue = Path.GetFileName(Path.GetDirectoryName(filePath)) + Path.GetFileNameWithoutExtension(filePath);
#if DEBUG
				Console.WriteLine(string.Format("[CacheService][GetAddonFileInCachePath] inputValue: {0}", inputValue));
#endif
				byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(inputValue);
				byte[] hashBytes = md5.ComputeHash(inputBytes);
				string hashString = Convert.ToHexString(hashBytes);
				DateTime fileLastWriteTime = File.GetLastWriteTime(filePath);
				string lastWriteTimeString = fileLastWriteTime.ToString("yyyyMMddHHmmssffff");
				return Path.Combine(GetCacheDirectoryPath("gma"), hashString + "_" + lastWriteTimeString + ".gma");
			}
		}

		/*
        public static string? GetAddonFileInCachePath(string filePath)
        {
#if DEBUG
			Console.WriteLine(string.Format("[CacheService] Run GetAddonFileInCachePath({0})", Path.GetFileName(filePath)));
#endif
			string? fileName = CalculateFileMD5Hash(filePath);
            if (fileName == null) return null;
            return Path.Combine(GetCacheDirectoryPath("gma"), fileName + ".gma");
        }
        */

		public static string GetHeaderSingleFileInCachePath()
        {
#if DEBUG
			Console.WriteLine(string.Format("[CacheService] Run GetHeaderSingleFileInCachePath()"));
#endif
			return Path.Combine(GetCacheDirectoryPath("headers"), "cache.json");
        }

        public static async Task<string?> CalculateFileMD5HashAsync(string filePath)
        {
#if DEBUG
			Console.WriteLine(string.Format("[CacheService] Run CalculateFileMD5HashAsync({0})", Path.GetFileName(filePath)));
#endif
			byte[]? fileHashBytes = null;

            using (MD5 md5 = MD5.Create())
                using (FileStream stream = File.OpenRead(filePath))
                    fileHashBytes = await md5.ComputeHashAsync(stream);

            return GetBytesHashString(fileHashBytes);
        }

        public static string? CalculateFileMD5Hash(string filePath)
        {
#if DEBUG
			Console.WriteLine(string.Format("[CacheService] Run CalculateFileMD5Hash({0})", Path.GetFileName(filePath)));
#endif
			byte[]? fileHashBytes = null;

            using (MD5 md5 = MD5.Create())
                using (FileStream stream = File.OpenRead(filePath))
                    fileHashBytes = md5.ComputeHash(stream);

            return GetBytesHashString(fileHashBytes);
        }

        private static string? GetBytesHashString(byte[]? fileHashBytes)
        {
#if DEBUG
			Console.WriteLine(string.Format("[CacheService] Run GetBytesHashString({0})", fileHashBytes == null ? 0 : fileHashBytes.Length));
#endif
			if (fileHashBytes == null) return null;
            return BitConverter.ToString(fileHashBytes).Replace("-", "").ToLowerInvariant();
        }

        private static string GetCacheDirectoryPath(string directoryName)
        {
#if DEBUG
			Console.WriteLine(string.Format("[CacheService] Run GetCacheDirectoryPath({0})", Path.GetFileName(directoryName)));
#endif
			string cacheDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache/" + directoryName);

            if (!Directory.Exists(cacheDirectory))
                Directory.CreateDirectory(cacheDirectory);

            return cacheDirectory;
        }
    }
}
