﻿using CSharpGmaReaderLibrary.Exceptions;
using CSharpGmaReaderLibrary.Models;
using CSharpGmaReaderLibrary.Services;
using static CSharpGmaReaderLibrary.Models.Events.ProgressEvents;

namespace CSharpGmaReaderLibrary
{
    public partial class GmaReader : IDisposable
    {
        public event ProgressChanged? e_ReadFilesProgressChanged;
        public event ProgressCompleted? e_ReadFilesCompeted;

        private const string _addonIdEnt = "GMAD";
        private const string _dupIdEnt = "DUP3";
        private const char _addonFormatVersion = (char)3;

        public async Task<AddonInfoModel?> ReadHeaderAsync(string filePath, ReadHeaderOptions? options = null)
        {
#if DEBUG
			Console.WriteLine("\n");
#endif
			filePath = Path.GetFullPath(filePath);
			filePath = await GetReadingFilePathAsync(filePath);
#if DEBUG
			Console.WriteLine(string.Format("[ReadHeaderAsync] Reading file path: {0}", filePath));
#endif
			options = options ?? new ReadHeaderOptions();

            AddonInfoModel? addonInfo = null;
            string? headerCacheFilePath = null;

            if (options.UseCache)
            {
                headerCacheFilePath = CacheService.GetHeaderFileInCachePath(filePath);
#if DEBUG
				Console.WriteLine(string.Format("[ReadHeaderAsync] Read header cache: {0}", headerCacheFilePath));
#endif
				if (headerCacheFilePath == null)
                    return null;

                addonInfo = await ReadCacheFromFileAsync(headerCacheFilePath);
            }

            if (addonInfo == null)
            {
                using (Stream stream = File.OpenRead(filePath))
                {
                    if (stream.Length == 0)
                        throw new GmaReaderException("Attempted to read from empty buffer.");

                    stream.Seek(0, SeekOrigin.Begin);

                    using (var reader = new BinaryReader(stream))
                    {
						//addonInfo = await GetHeaderReaderAsync(reader, filePath, options);
						addonInfo = GetHeaderReader(reader, filePath, options);
					}

                    if (addonInfo == null || string.IsNullOrWhiteSpace(addonInfo.Name) || addonInfo.FormatVersion == 0 || addonInfo.AddonVersion == 0)
						throw new Exception("Failed to read the addon");
                }
#if DEBUG
				Console.WriteLine(string.Format("[ReadHeaderAsync] Complete reading addon info: {0}", Path.GetFileName(filePath)));
#endif
			}

            if (options.UseCache && addonInfo != null && headerCacheFilePath != null && (
                options.RewriteExistsCache || !File.Exists(headerCacheFilePath)
            ))
            {
                await WriteCacheToFileAsync(headerCacheFilePath, addonInfo);
            }
#if DEBUG
            Console.WriteLine("\n");
#endif
			return addonInfo;
        }

        private async Task WriteCacheToFileAsync(string headerCacheFile, AddonInfoModel cacheInfo)
        {
            string? jsonString = null;

            try
            {
                jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(cacheInfo);
            }
            catch { }
            finally
            {
                await Task.Yield();
            }

            if (jsonString != null)
                await File.WriteAllTextAsync(headerCacheFile, jsonString);
        }

        private async Task<AddonInfoModel?> ReadCacheFromFileAsync(string? headerCacheFile)
        {
            AddonInfoModel? cacheInfoResult = null;

            if (headerCacheFile == null || !File.Exists(headerCacheFile))
                return null;

            string? jsonString = await File.ReadAllTextAsync(headerCacheFile);
            if (jsonString != null)
            {
                try
                {
                    AddonInfoModel? cacheInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<AddonInfoModel>(jsonString);
                    if (cacheInfo != null)
                        cacheInfoResult = cacheInfo;
                }
                catch { }
                finally
                {
                    await Task.Yield();
                }
            }

            return cacheInfoResult;
        }

        private async Task<string> GetReadingFilePathAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new GmaReaderException($"File path string is empty");

            if (!File.Exists(filePath))
                throw new GmaReaderException($"File {filePath} not exists");

#if DEBUG
			Console.WriteLine(string.Format("[GetReadingFilePathAsync] Run GetReadingFilePathAsync({0})", filePath));
#endif

			bool isLZMA = Path.GetExtension(filePath) == ".bin";
#if DEBUG
			Console.WriteLine(string.Format("[GetReadingFilePathAsync] File is LZMA: {0}", isLZMA));
#endif
			if (isLZMA)
            {
                string? tempFilePath = CacheService.GetAddonFileInCachePath(filePath);
                if (tempFilePath == null)
                    throw new GmaReaderException($"File {tempFilePath} not exists");

#if DEBUG
				Console.WriteLine(string.Format("[GetReadingFilePathAsync] LZMA file temp path: {0}", tempFilePath));
#endif

				if (!File.Exists(tempFilePath))
                {
					bool isDecode = await DecodeLZMAAsync(filePath, tempFilePath);
                    if (!isDecode || !File.Exists(tempFilePath))
                        throw new GmaReaderException("Failed lazma decode");
                }

                filePath = tempFilePath;
            }

            return filePath;
        }

		//private async Task<AddonInfoModel?> GetHeaderReaderAsync(BinaryReader reader, string filePath, ReadHeaderOptions options)
		private AddonInfoModel? GetHeaderReader(BinaryReader reader, string filePath, ReadHeaderOptions options)
		{
#if DEBUG
			Console.WriteLine(string.Format("[GetHeaderReaderAsync] Run GetHeaderReaderAsync({0})", filePath));
#endif
			char[] gmadCharTag = reader.ReadChars(_addonIdEnt.Length);
            string gmadStringTag = string.Join(string.Empty, gmadCharTag);

            if (gmadStringTag == _dupIdEnt)
                return null;

            if (gmadStringTag != _addonIdEnt)
                throw new GmaReaderException($"Header mismatch: {gmadStringTag} - {filePath}");

            char formatVersion = reader.ReadChar();

            if (formatVersion > _addonFormatVersion)
                throw new GmaReaderException($"Can't parse version {formatVersion} addons: {filePath}");

            ulong steamid64 = reader.ReadUInt64();

            DateTime timestamp = new DateTime(1970, 1, 1, 0, 0, 0)
                .ToLocalTime()
                .AddSeconds((double)reader.ReadInt64());

            string content = string.Empty;
            if (formatVersion > 1)
            {
                content = reader.ReadNullTerminatedString();

                while (content != string.Empty)
                    content = reader.ReadNullTerminatedString();
            }

            string name = reader.ReadNullTerminatedString();
            string description = reader.ReadNullTerminatedString();
            string author = reader.ReadNullTerminatedString();
            int addonVersion = reader.ReadInt32();

            int fileNumber = 0;
            int offset = 0;
            var indexesFiles = new List<FileEntryModel>();
            ulong fileBlock = 0;

            if (options.ReadFilesInfo)
            {
                while (reader.ReadInt32() != 0)
                {
                    fileNumber++;

                    var entry = new FileEntryModel();
                    entry.Path = reader.ReadNullTerminatedString();
                    entry.Size = reader.ReadInt64();
                    entry.CRC = reader.ReadUInt32();
                    entry.Offset = offset;
                    entry.FileNumber = (uint)fileNumber;

                    indexesFiles.Add(entry);

                    offset += (int)entry.Size;
                }

                fileBlock = (ulong)reader.BaseStream.Position;
            }

            //string? fileHash = await CacheService.CalculateFileMD5HashAsync(filePath);

            //if (fileHash == null)
            //    throw new GmaReaderException($"Make md5 hash failed: {filePath}");

            var addonInfo = new AddonInfoModel
            {
                Name = name,
                //AddonFileHash = fileHash,
                Description = description,
                Author = author,
                AddonVersion = addonVersion,
                Content = content,
                FormatVersion = formatVersion,
                SteamID64 = steamid64,
                Timestamp = timestamp,
                FileBlock = fileBlock,
                IndexesFiles = indexesFiles,
                SourcePath = filePath,
            };

            return addonInfo;
        }

        public async Task ReadFileContentAsync(string filePath, Func<FileContentModel, Task> handler, ReadFileContentOptions? options = null)
        {
            options = options ?? new ReadFileContentOptions();
            AddonInfoModel? addonInfo = options.AddonInfo ?? await ReadHeaderAsync(filePath, options.HeaderOptions);

            if (addonInfo == null)
                throw new NullReferenceException();

            if (addonInfo.IndexesFiles == null)
                throw new NullReferenceException();

            var files = new List<FileContentModel>();

            using (Stream stream = File.OpenRead(filePath))
            {
                if (stream.Length == 0)
                    throw new GmaReaderException("Attempted to read from empty buffer.");

                stream.Seek(0, SeekOrigin.Begin);

                int count = addonInfo.IndexesFiles.Count();
                if (count != 0)
                {
                    double percentRelationship = (double)100 / (double)count;

                    for (int index = 0; index < count; index++)
                    {
                        FileEntryModel entry = addonInfo.IndexesFiles.ElementAt(index);

                        using (var tempBuffer = new MemoryStream())
                        {
                            byte[] read_buffer = new byte[(long)entry.Size];

                            stream.Seek((long)addonInfo.FileBlock + (long)entry.Offset, SeekOrigin.Begin);

                            await stream.ReadAsync(read_buffer, 0, (int)entry.Size);
                            await tempBuffer.WriteAsync(read_buffer, 0, read_buffer.Length);

                            var fileInfo = new FileContentModel
                            {
                                FilePath = entry.Path,
                                Bytes = tempBuffer.ToArray(),
                            };

                            await handler.Invoke(fileInfo);
                        }

                        double currentPercent = percentRelationship * (index + 1);
                        e_ReadFilesProgressChanged?.Invoke(this, (int)currentPercent);
                    }
                }
            }

            e_ReadFilesCompeted?.Invoke(this);
        }

        private async Task<bool> DecodeLZMAAsync(string fileInputPath, string fileOutputPath)
        {
#if DEBUG
			Console.WriteLine(string.Format("[DecodeLZMAAsync] Start LZMA decode: {0} -> {1}", fileInputPath, fileOutputPath));
#endif
			try
			{
                using (Stream input = File.OpenRead(fileInputPath))
                {
                    input.Seek(0, SeekOrigin.Begin);

                    byte[] properties = new byte[5];
                    await input.ReadAsync(properties, 0, 5);

                    byte[] fileLengthBytes = new byte[8];
                    await input.ReadAsync(fileLengthBytes, 0, 8);
                    long fileLength = BitConverter.ToInt64(fileLengthBytes, 0);

                    var lzmaCoder = new SevenZip.Compression.LZMA.Decoder();
                    lzmaCoder.SetDecoderProperties(properties);

                    using (FileStream output = new FileStream(fileOutputPath, FileMode.Create))
                    {
                        lzmaCoder.Code(input, output, input.Length, fileLength, null);
                        await output.FlushAsync();
                    }
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            e_ReadFilesProgressChanged = null;
            e_ReadFilesCompeted = null;
        }
    }
}