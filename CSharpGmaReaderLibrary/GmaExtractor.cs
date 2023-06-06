using CSharpGmaReaderLibrary.Models;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using static CSharpGmaReaderLibrary.Models.Events.ProgressEvents;

namespace CSharpGmaReaderLibrary
{
    public class GmaExtractor
    {
        public delegate void ExtractFileCompleted(string filePath);

        public event ProgressChanged? e_ExtractFilesProgressChanged;
        public event ProgressCompleted? e_ExtractFilesProgressCompeted;
        public event ExtractFileCompleted? e_ExtractFileCompleted;

        private string _extractFolderPath;

        public GmaExtractor(string extractFolderPath)
        {
			_extractFolderPath = extractFolderPath;
		}

        private async Task<string?> ExtractFileHandlerAsync(FileContentModel addonFile, ExtractFileOptions? options)
        {
            string? fileDirectoryPath = Path.GetDirectoryName(addonFile.FilePath);
            if (fileDirectoryPath == null) return null;

            if (addonFile.FilePath == null)
                throw new NullReferenceException();

            options = options ?? new ExtractFileOptions();

            string pasteDirectoryPath = Path.Combine(_extractFolderPath, fileDirectoryPath);
            string pasteFilePath = Path.Combine(_extractFolderPath, addonFile.FilePath);

			if (!Directory.Exists(pasteDirectoryPath))
                Directory.CreateDirectory(pasteDirectoryPath);

            if (addonFile.Bytes != null && (!File.Exists(pasteFilePath) || options.RewriteExistsFiles))
                await File.WriteAllBytesAsync(pasteFilePath, addonFile.Bytes);

            return pasteFilePath;
        }

        public async Task ExtractFileAsync(FileContentModel addonFile, ExtractFileOptions? options = null)
        {
            string? pasteFilePath = await ExtractFileHandlerAsync(addonFile, options);
            if (pasteFilePath == null) return;

            e_ExtractFileCompleted?.Invoke(pasteFilePath);
            e_ExtractFilesProgressChanged?.Invoke(this, 100);
            e_ExtractFilesProgressCompeted?.Invoke(this);
        }

        public async Task ExtractFilesListAsync(IList<FileContentModel> addonFiles, ExtractFileOptions? options = null)
        {
            int count = addonFiles.Count;
            if (count == 0) return;

            double percentRelationship = (double)100 / (double)count;

            for (int index = 0; index < count; index++)
            {
                FileContentModel addonFile = addonFiles[index];

                string? pasteFilePath = await ExtractFileHandlerAsync(addonFile, options);
                if (pasteFilePath == null) return;

                e_ExtractFileCompleted?.Invoke(pasteFilePath);

                double currentPercent = percentRelationship * (index + 1);
                e_ExtractFilesProgressChanged?.Invoke(this, (int)currentPercent);
            }

            e_ExtractFilesProgressCompeted?.Invoke(this);
        }

        public async Task<string?> MakeDescriptionFile(AddonInfoModel addonInfo)
        {
            if (string.IsNullOrWhiteSpace(addonInfo.Description))
                return null;
         
            bool isJsonDescriptionContent;
            try
            {
                var tempValue = System.Text.Json.Nodes.JsonNode.Parse(addonInfo.Description);
                isJsonDescriptionContent = true;
            }
            catch
            {
                isJsonDescriptionContent = false;
            }

            string outputFilePath;

            if (isJsonDescriptionContent)
                outputFilePath = Path.Combine(_extractFolderPath, "description.json");
            else
                outputFilePath = Path.Combine(_extractFolderPath, "description.txt");

            await File.WriteAllTextAsync(outputFilePath, addonInfo.Description);

			return outputFilePath;
        }
    }
}
