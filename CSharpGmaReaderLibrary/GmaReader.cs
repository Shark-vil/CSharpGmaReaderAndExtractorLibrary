using CSharpGmaReaderLibrary.Exceptions;
using CSharpGmaReaderLibrary.Models;
using CSharpGmaReaderLibrary.Services;
using static CSharpGmaReaderLibrary.Models.Events.ProgressEvents;

namespace CSharpGmaReaderLibrary
{
    public class GmaReader : IDisposable
    {
        public event ProgressChanged? e_ReadFilesProgressChanged;
        public event ProgressCompleted? e_ReadFilesCompeted;

        private const string _addonIdEnt = "GMAD";
        private const string _dupIdEnt = "DUP3";
        private const char _addonFormatVersion = (char)3;

        private static object _lock = new object();
        private AddonInfoCacheModel? _cacheObject = null;

        public async Task<AddonInfoModel?> ReadHeaderAsync(string filePath, ReadHeaderOptions? options = null)
        {
            filePath = await GetReadingFilePathAsync(filePath);

            AddonInfoModel? addonInfo = null;
            string? headerCacheFile = null;

            options = options ?? new ReadHeaderOptions();

            if (!options.ReadCacheSingleTime || _cacheObject == null)
            {
                headerCacheFile = await CacheService.GetHeaderFileInCachePath(filePath);
                if (headerCacheFile == null || !File.Exists(headerCacheFile))
                {
                    _cacheObject = new AddonInfoCacheModel();
                }
                else
                {
                    string? addonFileHash = await CacheService.CalculateFileMD5Hash(filePath);
                    string jsonString = await File.ReadAllTextAsync(headerCacheFile);
                    try
                    {
                        AddonInfoCacheModel? cacheInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<AddonInfoCacheModel>(jsonString);
                        if (cacheInfo != null)
                        {
                            _cacheObject = cacheInfo;
                            addonInfo = _cacheObject.Cache
                                .Where(x => x.AddonFileHash == addonFileHash)
                                .FirstOrDefault();
                        }
                    }
                    catch { }
                }
            }

            if (addonInfo == null)
            {
                using (Stream stream = File.OpenRead(filePath))
                {
                    if (stream.Length == 0)
                        throw new GmaReaderException("Attempted to read from empty buffer.");

                    stream.Seek(0, SeekOrigin.Begin);
                    await Task.Yield();

                    using (var reader = new BinaryReader(stream))
                        addonInfo = await GetHeaderReader(reader, filePath, options);
                }

                if (_cacheObject != null && addonInfo != null && headerCacheFile != null)
                {
                    _cacheObject.Cache.Add(addonInfo);

                    try
                    {
                        string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(_cacheObject);
                        lock (_lock)
                        {
                            File.WriteAllText(headerCacheFile, jsonString);
                        }
                    }
                    catch { }
                }
            }

            return addonInfo;
        }

        private async Task<string> GetReadingFilePathAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new GmaReaderException($"File path string is empty");

            if (!File.Exists(filePath))
                throw new GmaReaderException($"File {filePath} not exists");

            bool isLZMA = Path.GetExtension(filePath) == ".bin";
            if (isLZMA)
            {
                string? tempFilePath = await CacheService.GetAddonFileInCachePath(filePath);
                if (tempFilePath == null)
                    throw new GmaReaderException($"File {tempFilePath} not exists");

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

        private async Task<AddonInfoModel?> GetHeaderReader(BinaryReader reader, string filePath, ReadHeaderOptions options)
        {
            char[] gmadCharTag = reader.ReadChars(_addonIdEnt.Length);
            string gmadStringTag = string.Join(string.Empty, gmadCharTag);

            if (gmadStringTag == _dupIdEnt)
                return null;

            if (gmadStringTag != _addonIdEnt)
                throw new GmaReaderException($"Header mismatch: {gmadStringTag} - {filePath}");

            char formatVersion = reader.ReadChar();
            await Task.Yield();

            if (formatVersion > _addonFormatVersion)
                throw new GmaReaderException($"Can't parse version {formatVersion} addons: {filePath}");

            ulong steamid64 = reader.ReadUInt64();
            await Task.Yield();

            DateTime timestamp = new DateTime(1970, 1, 1, 0, 0, 0)
                .ToLocalTime()
                .AddSeconds((double)reader.ReadInt64());
            await Task.Yield();

            string content = string.Empty;
            if (formatVersion > 1)
            {
                content = reader.ReadNullTerminatedString();
                await Task.Yield();

                while (content != string.Empty)
                {
                    content = reader.ReadNullTerminatedString();
                    await Task.Yield();
                }
            }

            string name = reader.ReadNullTerminatedString();
            await Task.Yield();

            string description = reader.ReadNullTerminatedString();
            await Task.Yield();

            string author = reader.ReadNullTerminatedString();
            await Task.Yield();

            int addonVersion = reader.ReadInt32();
            await Task.Yield();

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
                    await Task.Yield();

                    entry.Size = reader.ReadInt64();
                    await Task.Yield();

                    entry.CRC = reader.ReadUInt32();
                    await Task.Yield();

                    entry.Offset = offset;
                    entry.FileNumber = (uint)fileNumber;

                    indexesFiles.Add(entry);
                    await Task.Yield();

                    offset += (int)entry.Size;
                }

                fileBlock = (ulong)reader.BaseStream.Position;
            }

            string? fileHash = await CacheService.CalculateFileMD5Hash(filePath);
            await Task.Yield();

            if (fileHash == null)
                throw new GmaReaderException($"Make md5 hash failed: {filePath}");

            var addonInfo = new AddonInfoModel
            {
                Name = name,
                AddonFileHash = fileHash,
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
                await Task.Yield();

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
                            await Task.Yield();
                            await stream.ReadAsync(read_buffer, 0, (int)entry.Size);

                            await tempBuffer.WriteAsync(read_buffer, 0, read_buffer.Length);
                            await Task.Yield();

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
            try
            {
                using (Stream input = File.OpenRead(fileInputPath))
                {
                    input.Seek(0, SeekOrigin.Begin);
                    await Task.Yield();

                    // Read the decoder properties
                    byte[] properties = new byte[5];
                    await input.ReadAsync(properties, 0, 5);

                    // Read in the decompress file size.
                    byte[] fileLengthBytes = new byte[8];
                    await input.ReadAsync(fileLengthBytes, 0, 8);
                    long fileLength = BitConverter.ToInt64(fileLengthBytes, 0);

                    var lzmaCoder = new SevenZip.Compression.LZMA.Decoder();
                    lzmaCoder.SetDecoderProperties(properties);
                    await Task.Yield();

                    using (FileStream output = new FileStream(fileOutputPath, FileMode.Create))
                    {
                        lzmaCoder.Code(input, output, input.Length, fileLength, null);
                        await Task.Yield();
                        await output.FlushAsync();
                        await Task.Yield();
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
            _cacheObject = null;
        }
    }
}