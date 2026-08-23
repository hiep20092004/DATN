using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.LevelManagement;
using WaterFlow.Framework.Systems.LoadObject;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.IO.Compression;
using UnityEngine.Serialization;

namespace WaterFlow.Framework.Systems.LevelManagement
{
    [CreateAssetMenu(fileName = "LevelDriveService", menuName = "WaterFlow Services/Level Service/Level Drive Service")]
    public class LevelDriveService : LevelService
    {
        [BoxGroup("DRIVE CONFIG")] [SerializeField]
        private LevelService levelServiceFallback;

        [BoxGroup("GOOGLE SHEETS")] [SerializeField]
        private string googleSheetId = "1oo_Wp5MOUnvOAh-C0WNR7edQBpW0uklENg0fAORuJzU";

        [BoxGroup("GOOGLE SHEETS")] [SerializeField]
        private string sheetName = "Sheet1";

        [BoxGroup("GOOGLE SHEETS")] [SerializeField]
        private int linkColumnIndex = 0;

        private string levelsDirectory;
        private string zipFileLink;
        private bool isInitialized = false;
        private int linkRowIndex = 0;

        public async UniTaskVoid DownloadData(int linkRow, Action<bool> onComplete)
        {
            this.linkRowIndex = linkRow;
            try
            {
                Debug.Log("Starting level data download from Google Sheets...");

                // Download the Google Sheet as CSV
                string csvUrl = $"https://docs.google.com/spreadsheets/d/{googleSheetId}/gviz/tq?tqx=out:csv&sheet={sheetName}";

                using (UnityWebRequest request = UnityWebRequest.Get(csvUrl))
                {
                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await UniTask.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string csvData = request.downloadHandler.text;
                        ParseCSVData(csvData);

                        if (!string.IsNullOrEmpty(zipFileLink))
                        {
                            Debug.Log($"Found zip file link: {zipFileLink}");
                            await DownloadAndExtractZipFile(onComplete);
                        }
                        else
                        {
                            Debug.LogError("No zip file link found in Google Sheets");
                            onComplete?.Invoke(false);
                        }
                    }
                    else
                    {
                        Debug.LogError($"Failed to download Google Sheet: {request.error}");
                        onComplete?.Invoke(false);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error downloading level data: {e.Message}");
                onComplete?.Invoke(false);
            }
        }

        private async UniTask DownloadAndExtractZipFile(Action<bool> onComplete)
        {
            try
            {
                Debug.Log($"Downloading zip file from: {zipFileLink}");

                // Convert to direct download link
                string downloadLink = ConvertToDirectDownloadLink(zipFileLink);

                // Download the zip file
                using (UnityWebRequest request = UnityWebRequest.Get(downloadLink))
                {
                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await UniTask.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        byte[] zipData = request.downloadHandler.data;
                        await ExtractZipFile(zipData);
                        Debug.Log("Successfully downloaded and extracted level zip file");
                        isInitialized = true;
                        onComplete?.Invoke(true);
                    }
                    else
                    {
                        Debug.LogError($"Failed to download zip file: {request.error}");
                        onComplete?.Invoke(false);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error downloading/extracting zip file: {e.Message}");
                onComplete?.Invoke(false);
            }
        }

        private async UniTask ExtractZipFile(byte[] zipData)
        {
            try
            {
                // Create levels directory in persistent data path (works on Android and iOS)
                levelsDirectory = Path.Combine(Application.persistentDataPath, "Levels");
                if (!Directory.Exists(levelsDirectory))
                {
                    Directory.CreateDirectory(levelsDirectory);
                }

                Debug.Log($"Extracting zip to: {levelsDirectory}");

                // Extract zip file
                using (MemoryStream zipStream = new MemoryStream(zipData))
                using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.Name.EndsWith(".json"))
                        {
                            string filePath = Path.Combine(levelsDirectory, entry.Name);

                            // Extract the file
                            entry.ExtractToFile(filePath, true);
                            Debug.Log($"Extracted: {entry.Name}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error extracting zip file: {e.Message}");
                throw;
            }
        }

        private void ParseCSVData(string csvData)
        {
            zipFileLink = null;

            string[] lines = csvData.Split('\n');
            //for (int i = 1; i < lines.Length; i++) // Skip header row
            //{

            if (linkRowIndex <= 0 || linkRowIndex > lines.Length) return;

            string line = lines[linkRowIndex - 1].Trim();
            if (string.IsNullOrEmpty(line)) return;

            string[] columns = ParseCSVLine(line);
            if (columns.Length > linkColumnIndex)
            {
                string link = columns[linkColumnIndex].Trim('"');
                if (!string.IsNullOrEmpty(link))
                {
                    zipFileLink = link;
                    //break; // Take the first valid link
                }
            }
            // }
        }

        private string[] ParseCSVLine(string line)
        {
            List<string> result = new List<string>();
            bool inQuotes = false;
            string currentField = "";

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField);
                    currentField = "";
                }
                else
                {
                    currentField += c;
                }
            }

            result.Add(currentField);
            return result.ToArray();
        }

        public override T GetLevelData<T>(int level, GameMode gameMode, bool force = false, bool loop = true, int category = 0)
        {
            return GetLevel<T>(level, gameMode, category);
        }

        protected override T GetLevel<T>(int level, GameMode gameMode, int category)
        {
            try
            {
                // Try to load from extracted zip files first
                if (isInitialized && !string.IsNullOrEmpty(levelsDirectory))
                {
                    string levelFilePath = category == 0
                        ? Path.Combine(levelsDirectory, $"{level}.json")
                        : Path.Combine(levelsDirectory, $"{level}.{category}.json");
                    if (File.Exists(levelFilePath))
                    {
                        string jsonData = File.ReadAllText(levelFilePath);
                        var levelData = JsonConvert.DeserializeObject<T>(jsonData, Settings);
                        if (levelData != null)
                        {
                            levelData.category = category;
                            Debug.Log($"Loaded level {level} from extracted files");
                            return levelData;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load level {level} from extracted files: {e.Message}");
            }

            // Fallback to cover service if all else fails
            if (levelServiceFallback != null)
            {
                var coverLevelData = levelServiceFallback.GetLevelData<T>(level, gameMode, true, false, category);
                if (coverLevelData != null)
                {
                    Debug.Log($"Loaded level {level} from cover service");
                    return coverLevelData;
                }
            }

            Debug.LogError($"Failed to load level {level} from extracted files, local storage, and cover service");
            return null;
        }

        private string ConvertToDirectDownloadLink(string driveLink)
        {
            // Extract file ID from Google Drive sharing link
            string fileId = "";

            if (driveLink.Contains("/d/"))
            {
                int startIndex = driveLink.IndexOf("/d/") + 3;
                int endIndex = driveLink.IndexOf("/", startIndex);
                if (endIndex == -1) endIndex = driveLink.Length;
                fileId = driveLink.Substring(startIndex, endIndex - startIndex);
            }
            else if (driveLink.Contains("id="))
            {
                int startIndex = driveLink.IndexOf("id=") + 3;
                int endIndex = driveLink.IndexOf("&", startIndex);
                if (endIndex == -1) endIndex = driveLink.Length;
                fileId = driveLink.Substring(startIndex, endIndex - startIndex);
            }

            if (!string.IsNullOrEmpty(fileId))
            {
                return $"https://drive.google.com/uc?export=download&id={fileId}";
            }

            return driveLink; // Return original if conversion fails
        }

        public override void SaveLevel<T>(T levelData)
        {
            // Use async save but don't await it since this is a synchronous method
            levelServiceFallback.SaveLevel(levelData);
        }

        // Helper method to check if the service is ready
        public bool IsReady()
        {
            return isInitialized && !string.IsNullOrEmpty(levelsDirectory) && Directory.Exists(levelsDirectory);
        }

        // Helper method to get the levels directory path
        public string GetLevelsDirectory()
        {
            return levelsDirectory;
        }
    }
}