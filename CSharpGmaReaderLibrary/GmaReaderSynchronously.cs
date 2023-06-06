using CSharpGmaReaderLibrary.Exceptions;
using CSharpGmaReaderLibrary.Models;
using CSharpGmaReaderLibrary.Services;

namespace CSharpGmaReaderLibrary
{
    /*
    public partial class GmaReader
    {
        private static object _lockerSynchronously = new object();
        //private AddonInfoCacheModel? _cacheObjectSynchronously = null;
        private string _headerCacheFilePathSynchronously = CacheService.GetHeaderSingleFileInCachePath();

        public AddonInfoModel? ReadHeader(string filePath, ReadHeaderOptions? options = null)
        {
            filePath = GetReadingFilePath(filePath);

            AddonInfoModel? addonInfo = null;
            string? addonFileHash = CacheService.CalculateFileMD5Hash(filePath);

            if (addonFileHash == null)
                return null;

            options = options ?? new ReadHeaderOptions();

            //if (!options.ReadCacheSingleTime || _cacheObjectSynchronously == null)
            //{
            //    _cacheObjectSynchronously = ReadCacheFromFile(_headerCacheFilePathSynchronously);
            //    addonInfo = _cacheObjectSynchronously.Cache
            //        .Where(x => x.AddonFileHash == addonFileHash)
            //        .FirstOrDefault();
            //}

            if (addonInfo == null)
            {
                using (Stream stream = File.OpenRead(filePath))
                {
                    if (stream.Length == 0)
                        throw new GmaReaderException("Attempted to read from empty buffer.");

                    stream.Seek(0, SeekOrigin.Begin);

                    using (var reader = new BinaryReader(stream))
                        addonInfo = GetHeaderReader(reader, filePath, options);
                }
            }

            //if (
            //    _cacheObjectSynchronously != null && 
            //    addonInfo != null && 
            //    _headerCacheFilePathSynchronously != null && 
            //    (options.RewriteExistsCache || !_cacheObjectSynchronously.Cache.Exists(x => x.AddonFileHash == addonFileHash && x.Timestamp == addonInfo.Timestamp))
            //)
            //{
            //    lock (_lockerSynchronously)
            //    {
            //        _cacheObjectSynchronously.Cache.Add(addonInfo);
            //    }

            //    WriteCacheToFile(_headerCacheFilePathSynchronously, _cacheObjectSynchronously);
            //}

            return addonInfo;
        }

        private void WriteCacheToFile(string headerCacheFile, AddonInfoCacheModel cacheInfo)
        {
            string? jsonString = null;

            try
            {
                jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(cacheInfo);
            }
            catch { }

            if (jsonString != null)
            {
                lock (_lockerSynchronously)
                {
                    File.WriteAllText(headerCacheFile, jsonString);
                }
            }
        }

        private AddonInfoCacheModel ReadCacheFromFile(string? headerCacheFile)
        {
            var cacheInfoResult = new AddonInfoCacheModel();

            if (headerCacheFile == null || !File.Exists(headerCacheFile))
                return cacheInfoResult;

            string? jsonString = null;
            lock (_lockerSynchronously)
            {
                jsonString = File.ReadAllText(headerCacheFile);
            }

            if (jsonString != null)
            {
                try
                {
                    AddonInfoCacheModel? cacheInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<AddonInfoCacheModel>(jsonString);
                    if (cacheInfo != null)
                        cacheInfoResult = cacheInfo;
                }
                catch { }
            }

            return cacheInfoResult;
        }

        private string GetReadingFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new GmaReaderException($"File path string is empty");

            if (!File.Exists(filePath))
                throw new GmaReaderException($"File {filePath} not exists");

            bool isLZMA = Path.GetExtension(filePath) == ".bin";
            if (isLZMA)
            {
                string? tempFilePath = CacheService.GetAddonFileInCachePath(filePath);
                if (tempFilePath == null)
                    throw new GmaReaderException($"File {tempFilePath} not exists");

                if (!File.Exists(tempFilePath))
                {
                    bool isDecode = DecodeLZMAA(filePath, tempFilePath);
                    if (!isDecode || !File.Exists(tempFilePath))
                        throw new GmaReaderException("Failed lazma decode");
                }

                filePath = tempFilePath;
            }

            return filePath;
        }

        private AddonInfoModel? GetHeaderReader(BinaryReader reader, string filePath, ReadHeaderOptions options)
        {
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
                {
                    content = reader.ReadNullTerminatedString();
                }
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

            string? fileHash = CacheService.CalculateFileMD5Hash(filePath);

            if (fileHash == null)
                throw new GmaReaderException($"Make md5 hash failed: {filePath}");

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

        public void ReadFileContent(string filePath, Func<FileContentModel, Task> handler, ReadFileContentOptions? options = null)
        {
            options = options ?? new ReadFileContentOptions();
            AddonInfoModel? addonInfo = options.AddonInfo ?? ReadHeader(filePath, options.HeaderOptions);

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
                            stream.Read(read_buffer, 0, (int)entry.Size);
                            tempBuffer.Write(read_buffer, 0, read_buffer.Length);

                            var fileInfo = new FileContentModel
                            {
                                FilePath = entry.Path,
                                Bytes = tempBuffer.ToArray(),
                            };

                            handler.Invoke(fileInfo);
                        }

                        double currentPercent = percentRelationship * (index + 1);
                        e_ReadFilesProgressChanged?.Invoke(this, (int)currentPercent);
                    }
                }
            }

            e_ReadFilesCompeted?.Invoke(this);
        }

        private bool DecodeLZMAA(string fileInputPath, string fileOutputPath)
        {
            try
            {
                using (Stream input = File.OpenRead(fileInputPath))
                {
                    input.Seek(0, SeekOrigin.Begin);

                    byte[] properties = new byte[5];
                    input.Read(properties, 0, 5);

                    byte[] fileLengthBytes = new byte[8];
                    input.Read(fileLengthBytes, 0, 8);
                    long fileLength = BitConverter.ToInt64(fileLengthBytes, 0);

                    var lzmaCoder = new SevenZip.Compression.LZMA.Decoder();
                    lzmaCoder.SetDecoderProperties(properties);

                    using (FileStream output = new FileStream(fileOutputPath, FileMode.Create))
                    {
                        lzmaCoder.Code(input, output, input.Length, fileLength, null);
                        output.Flush();
                    }
                }
            }
            catch
            {
                return false;
            }

            return true;
        }
    }
    */
}
