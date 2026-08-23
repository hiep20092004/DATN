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
using System.Text.RegularExpressions;
using UnityEngine.Serialization;

namespace WaterFlow.Framework.Systems.LevelManagement
{
    [CreateAssetMenu(fileName = "LevelDriveServiceFolder", menuName = "WaterFlow Services/Level Service/Level Drive Service Folder")]
    public class LevelDriveServiceFolder : LevelService
    {
        protected static readonly JsonSerializerSettings Settings = new() { TypeNameHandling = TypeNameHandling.Auto };

        [BoxGroup("DRIVE CONFIG")] [SerializeField]
        private LevelService levelServiceFallback;

        [BoxGroup("GOOGLE DRIVE FOLDER")] [SerializeField]
        private string driveFolderId = "1mmFs5iomYipkqNDgOjJZ7gikAMOwpfwf";

        [BoxGroup("GOOGLE DRIVE FOLDER")] [SerializeField]
        private string folderName = "Levels";

        [BoxGroup("GOOGLE SHEET CONFIG")] [SerializeField]
        private string googleSheetId = "1GSELr26XpnIOwMfkQaiBO4RXVv-BLpQI37hiJiBLqro";

        [BoxGroup("GOOGLE SHEET CONFIG")] [SerializeField]
        private string sheetName = "Sheet1";


        [BoxGroup("FOLDER SELECTION")] [SerializeField]
        private string selectedSubfolder = "Hue Anh"; // Default subfolder

        private Dictionary<string, string> subfolderIdMap = new Dictionary<string, string>();
        private Dictionary<string, Dictionary<string, string>> folderFileIdMap = new Dictionary<string, Dictionary<string, string>>();

        private bool isInitialized = false;

        // private string lastLevelDataDownloaded;
        private LevelDataFramework levelDataCache;

        public async UniTask DownloadData(Action<bool> onComplete)
        {
            try
            {
                Debug.Log("Starting subfolder discovery from Google Sheet...");

                // Get subfolder information from Google Sheet
                await GetSubfolderInfoFromSheet();

                if (subfolderIdMap.Count > 0)
                {
                    Debug.Log($"Found {subfolderIdMap.Count} subfolders: {string.Join(", ", subfolderIdMap.Keys)}");
                    isInitialized = true;
                    onComplete?.Invoke(true);
                }
                else
                {
                    Debug.LogError("No subfolders found in the Google Sheet");
                    onComplete?.Invoke(false);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error discovering subfolders: {e.Message}");
                onComplete?.Invoke(false);
            }
        }

        private async UniTask GetSubfolderInfoFromSheet()
        {
            try
            {
                subfolderIdMap.Clear();

                // Get CSV data from Google Sheet
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
                        string csvContent = request.downloadHandler.text;
                        ParseSubfolderInfoFromCSV(csvContent);
                    }
                    else
                    {
                        Debug.LogError($"Failed to get Google Sheet data: {request.error}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error getting subfolder info from sheet: {e.Message}");
                throw;
            }
        }

        private void ParseSubfolderInfoFromCSV(string csvContent)
        {
            Debug.Log($"Parsing CSV content length: {csvContent.Length}");

            try
            {
                // Split CSV into lines
                string[] lines = csvContent.Split('\n');

                // Skip header row
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    // Parse CSV line (simple comma split, handle quoted values)
                    string[] columns = ParseCSVLine(line);

                    if (columns.Length >= 2)
                    {
                        string folderName = columns[0].Trim();
                        string folderLink = columns[1].Trim();

                        if (!string.IsNullOrEmpty(folderName) && !string.IsNullOrEmpty(folderLink))
                        {
                            // Extract folder ID from Google Drive link
                            string folderId = ExtractFolderIdFromLink(folderLink);

                            if (!string.IsNullOrEmpty(folderId))
                            {
                                Debug.Log($"Found subfolder: {folderName} with ID: {folderId}");
                                subfolderIdMap[folderName] = folderId;
                            }
                            else
                            {
                                Debug.LogWarning($"Could not extract folder ID from link: {folderLink}");
                            }
                        }
                    }
                }

                Debug.Log($"Total subfolders found: {subfolderIdMap.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing CSV: {e.Message}");
            }
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

        private string ExtractFolderIdFromLink(string folderLink)
        {
            // Extract folder ID from Google Drive link
            // Example: https://drive.google.com/drive/folders/1RQDDq6FLgBjgbpijcOxEatP4pIeZ9PB7?usp=drive_link
            string pattern = @"/folders/([a-zA-Z0-9_-]+)";
            var match = Regex.Match(folderLink, pattern);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
        }


        private string ExtractFileName(string beforeContent, string afterContent)
        {
            // Look for common patterns that might contain the filename
            string[] patterns =
            {
                @"data-tooltip=""([^""]+\.json)""",
                @"aria-label=""([^""]+\.json)""",
                @"title=""([^""]+\.json)""",
                @">([a-zA-Z0-9_-]+\.json)<",
                @"""([^""]+\.json)""",
                @"([a-zA-Z0-9_-]+\.json)"
            };

            foreach (string pattern in patterns)
            {
                var matches = Regex.Matches(beforeContent + afterContent, pattern);
                foreach (Match match in matches)
                {
                    string fileName = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(fileName) && fileName.EndsWith(".json"))
                    {
                        Debug.Log($"Extracted filename: {fileName}");
                        return fileName;
                    }
                }
            }

            return null;
        }

        private async UniTask<bool> DownloadSingleFile<T>(string folderName, string fileName, string fileId) where T : LevelDataFramework
        {
            try
            {
                string downloadUrl = $"https://drive.google.com/uc?export=download&id={fileId}";

                Debug.Log($"Downloading {folderName}/{fileName} from {downloadUrl}");

                using (UnityWebRequest request = UnityWebRequest.Get(downloadUrl))
                {
                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await UniTask.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string jsonContent = request.downloadHandler.text;

                        // Validate that it's actually JSON
                        try
                        {
                            levelDataCache = JsonConvert.DeserializeObject<T>(jsonContent);
                            //File.WriteAllText(filePath, jsonContent);
                            Debug.Log($"Successfully downloaded: {folderName}/{fileName}");
                            return true;
                        }
                        catch (JsonException)
                        {
                            Debug.LogWarning($"Downloaded content for {folderName}/{fileName} is not valid JSON");
                            return false;
                        }
                    }
                    else
                    {
                        Debug.LogError($"Failed to download {folderName}/{fileName}: {request.error}");
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error downloading {folderName}/{fileName}: {e.Message}");
                return false;
            }
        }

        public override T GetLevelData<T>(int level, GameMode gameMode, bool force = false, bool loop = true, int category = 0)
        {
            return GetLevel<T>(level, gameMode, category);
        }

        protected override T GetLevel<T>(int level, GameMode gameMode, int category)
        {
            if (levelDataCache != null && levelDataCache.level == level && levelDataCache.gameMode == gameMode && levelDataCache.category == category)
            {
                Debug.Log($"Loaded level {level} from {selectedSubfolder} folder");
                return levelDataCache as T;
            }

            // Fallback to cover service if all else fails
            if (levelServiceFallback != null)
            {
                var fallbackLevelData = levelServiceFallback.GetLevelData<T>(level, gameMode, true, false, category);
                if (fallbackLevelData != null)
                {
                    Debug.Log($"Loaded level {level} from cover service");
                    return fallbackLevelData;
                }
            }

            Debug.LogError($"Failed to load level {level} from {selectedSubfolder} folder and cover service");
            return null;
        }

        public override void SaveLevel<T>(T levelData)
        {
            // Use async save but don't await it since this is a synchronous method
            levelServiceFallback.SaveLevel(levelData);
        }

        // Helper method to check if the service is ready
        public bool IsReady()
        {
            return isInitialized;
        }


        // Helper method to refresh the folder contents
        public async UniTaskVoid RefreshFolderContents(Action<bool> onComplete)
        {
            subfolderIdMap.Clear();
            folderFileIdMap.Clear();
            isInitialized = false;
            await DownloadData(onComplete);
        }

        // Method to manually add a subfolder (useful for testing or if sheet is not accessible)
        public void AddSubfolderManually(string folderName, string folderLink)
        {
            string folderId = ExtractFolderIdFromLink(folderLink);
            if (!string.IsNullOrEmpty(folderId))
            {
                subfolderIdMap[folderName] = folderId;
                Debug.Log($"Manually added subfolder: {folderName} with ID: {folderId}");
            }
            else
            {
                Debug.LogError($"Could not extract folder ID from link: {folderLink}");
            }
        }

        // Method to manually set file IDs for specific levels (most reliable approach)
        private Dictionary<string, Dictionary<int, string>> manualFileIdMap = new Dictionary<string, Dictionary<int, string>>();

        public void SetFileIdForLevel(string subfolderName, int level, string fileId)
        {
            if (!manualFileIdMap.ContainsKey(subfolderName))
            {
                manualFileIdMap[subfolderName] = new Dictionary<int, string>();
            }

            manualFileIdMap[subfolderName][level] = fileId;
            Debug.Log($"Manually set file ID for {subfolderName}/level {level}: {fileId}");
        }

        public void SetFileIdsForSubfolder(string subfolderName, Dictionary<int, string> levelFileIds)
        {
            manualFileIdMap[subfolderName] = new Dictionary<int, string>(levelFileIds);
            Debug.Log($"Manually set {levelFileIds.Count} file IDs for subfolder: {subfolderName}");
        }

        // Method to get level data from a specific subfolder (on-demand download)
        public async UniTask<bool> GetLevelInSubfolder<T>(string subfolder, int level, GameMode gameMode) where T : LevelDataFramework
        {
            try
            {
                // Check if subfolder exists
                if (!subfolderIdMap.ContainsKey(subfolder))
                {
                    Debug.LogError($"Subfolder {subfolder} not found. Available subfolders: {string.Join(", ", subfolderIdMap.Keys)}");
                    return false;
                }

                // Download the specific level file
                bool success = await DownloadLevelFromSubfolder<T>(subfolder, level);
                if (success)
                {
                    Debug.Log($"Downloaded and loaded level {level} from {subfolder} folder");
                }

                return success;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to get level {level} from {subfolder} folder: {e.Message}");
                return false;
            }
        }

        // Method to download a specific level from a subfolder
        private async UniTask<bool> DownloadLevelFromSubfolder<T>(string subfolder, int level) where T : LevelDataFramework
        {
            try
            {
                // Get the subfolder ID
                string subfolderId = subfolderIdMap[subfolder];

                // Try multiple methods to find the file ID
                string fileId = null;

                // Method 0: Check manually set file IDs (most reliable)
                if (manualFileIdMap.ContainsKey(subfolder) && manualFileIdMap[subfolder].ContainsKey(level))
                {
                    fileId = manualFileIdMap[subfolder][level];
                    Debug.Log($"Using manually set file ID for {subfolder}/level {level}: {fileId}");
                }


                // Method 1: Parse HTML content (fallback)
                if (string.IsNullOrEmpty(fileId))
                {
                    fileId = await FindFileIdFromHTML(subfolderId, $"{level}.json");
                }

                // Method 2: Try direct download with common patterns (last resort)
                if (string.IsNullOrEmpty(fileId))
                {
                    fileId = await TryDirectDownload(subfolderId, $"{level}.json");
                }

                if (!string.IsNullOrEmpty(fileId))
                {
                    return await DownloadSingleFile<T>(subfolder, $"{level}.json", fileId);
                }
                else
                {
                    Debug.LogError($"File {level}.json not found in subfolder {subfolder}");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error downloading level {level} from {subfolder}: {e.Message}");
                return false;
            }
        }


        private async UniTask<string> FindFileIdFromHTML(string subfolderId, string fileName)
        {
            try
            {
                string subfolderUrl = $"https://drive.google.com/drive/folders/{subfolderId}";

                using (UnityWebRequest request = UnityWebRequest.Get(subfolderUrl))
                {
                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await UniTask.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string htmlContent = request.downloadHandler.text;
                        return FindFileIdInSubfolder(htmlContent, fileName);
                    }
                    else
                    {
                        Debug.LogError($"Failed to get subfolder contents: {request.error}");
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error getting HTML content: {e.Message}");
                return null;
            }
        }

        private async UniTask<string> TryDirectDownload(string subfolderId, string fileName)
        {
            // Try some common file ID patterns that might work
            // This is a fallback method when HTML parsing fails
            string[] commonPatterns =
            {
                $"{subfolderId}_1", // Sometimes files have similar IDs to folders
                $"{subfolderId}1",
                $"{subfolderId}_file",
                // Add more patterns as needed
            };

            foreach (string pattern in commonPatterns)
            {
                try
                {
                    string testUrl = $"https://drive.google.com/uc?export=download&id={pattern}";
                    using (UnityWebRequest request = UnityWebRequest.Get(testUrl))
                    {
                        var operation = request.SendWebRequest();
                        while (!operation.isDone)
                        {
                            await UniTask.Yield();
                        }

                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            string content = request.downloadHandler.text;
                            // Check if it's valid JSON
                            try
                            {
                                JsonConvert.DeserializeObject(content);
                                Debug.Log($"Found working file ID: {pattern}");
                                return pattern;
                            }
                            catch (JsonException)
                            {
                                // Not valid JSON, try next pattern
                                continue;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to test pattern {pattern}: {e.Message}");
                    continue;
                }
            }

            return null;
        }

        // Method to find file ID in subfolder HTML content
        private string FindFileIdInSubfolder(string htmlContent, string fileName)
        {
            Debug.Log($"Searching for file: {fileName} in HTML content length: {htmlContent.Length}");

            // Method 1: Look for the specific file name in the HTML structure and extract the data-id
            string specificPattern = $@"data-tooltip=""[^""]*{fileName}[^""]*""[^>]*>{fileName}<";
            var specificMatch = Regex.Match(htmlContent, specificPattern);
            if (specificMatch.Success)
            {
                Debug.Log($"Found specific HTML structure for {fileName}");
                // Now look for the data-id attribute in the parent elements
                int matchIndex = specificMatch.Index;
                string searchArea = htmlContent.Substring(Math.Max(0, matchIndex - 2000), 4000);

                // Look for data-id attribute
                string dataIdPattern = @"data-id=""([a-zA-Z0-9_-]+)""";
                var dataIdMatches = Regex.Matches(searchArea, dataIdPattern);

                if (dataIdMatches.Count > 0)
                {
                    // Get the closest data-id to our file name
                    string fileId = dataIdMatches[dataIdMatches.Count - 1].Groups[1].Value;
                    Debug.Log($"Found file {fileName} with data-id: {fileId} using specific pattern");
                    return fileId;
                }
            }

            // Method 2: Look for the file name and extract the nearest data-id
            string fileNamePattern = $@">{fileName}<";
            var fileNameMatches = Regex.Matches(htmlContent, fileNamePattern);

            foreach (Match fileNameMatch in fileNameMatches)
            {
                int matchIndex = fileNameMatch.Index;
                string searchArea = htmlContent.Substring(Math.Max(0, matchIndex - 1000), 2000);

                // Look for data-id attribute in the search area
                string dataIdPattern = @"data-id=""([a-zA-Z0-9_-]+)""";
                var dataIdMatches = Regex.Matches(searchArea, dataIdPattern);

                if (dataIdMatches.Count > 0)
                {
                    string fileId = dataIdMatches[dataIdMatches.Count - 1].Groups[1].Value;
                    Debug.Log($"Found file {fileName} with data-id: {fileId} using fileName pattern");
                    return fileId;
                }
            }

            // Method 3: Fallback - Look for file IDs in the HTML content (old method)
            string pattern = @"/file/d/([a-zA-Z0-9_-]+)";
            var matches = Regex.Matches(htmlContent, pattern);

            Debug.Log($"Found {matches.Count} file IDs in HTML");

            foreach (Match match in matches)
            {
                string fileId = match.Groups[1].Value;

                // Look for the filename near the file ID
                int startIndex = htmlContent.IndexOf(fileId);
                if (startIndex > 0)
                {
                    string beforeFileId = htmlContent.Substring(Math.Max(0, startIndex - 500), 500);
                    string afterFileId = htmlContent.Substring(startIndex, 500);

                    string foundFileName = ExtractFileName(beforeFileId, afterFileId);

                    if (foundFileName == fileName)
                    {
                        Debug.Log($"Found file {fileName} with ID: {fileId}");
                        return fileId;
                    }
                    else if (!string.IsNullOrEmpty(foundFileName))
                    {
                        Debug.Log($"Found different file: {foundFileName} with ID: {fileId}");
                    }
                }
            }

            // Method 4: Look for any JSON files and log them for debugging
            string jsonPattern = @"([a-zA-Z0-9_-]+\.json)";
            var jsonMatches = Regex.Matches(htmlContent, jsonPattern);

            Debug.Log($"Found {jsonMatches.Count} JSON files in HTML:");
            foreach (Match match in jsonMatches)
            {
                Debug.Log($"  - {match.Value}");
            }

            return null;
        }

        // Method to switch the active subfolder
        public void SetActiveSubfolder(string subfolderName)
        {
            if (subfolderIdMap.ContainsKey(subfolderName))
            {
                selectedSubfolder = subfolderName;
                Debug.Log($"Switched to subfolder: {selectedSubfolder}");
            }
            else
            {
                Debug.LogError($"Subfolder {subfolderName} is not available. Available folders: {string.Join(", ", subfolderIdMap.Keys)}");
            }
        }

        // Method to get the currently active subfolder
        public string GetActiveSubfolder()
        {
            return selectedSubfolder;
        }

        // Method to get all available subfolders
        public List<string> GetAvailableSubfolders()
        {
            return new List<string>(subfolderIdMap.Keys);
        }


        // Method to check if a subfolder exists (without downloading)
        public bool HasSubfolder(string subfolderName)
        {
            return subfolderIdMap.ContainsKey(subfolderName);
        }

        // Method to manually set subfolder names if automatic extraction fails
        public void SetSubfolderName(string subfolderId, string folderName)
        {
            subfolderIdMap[folderName] = subfolderId;
            Debug.Log($"Manually set subfolder name: {folderName} for ID: {subfolderId}");
        }

        // Method to manually set multiple subfolder names
        public void SetSubfolderNames(Dictionary<string, string> folderNameMap)
        {
            foreach (var kvp in folderNameMap)
            {
                string folderName = kvp.Key;
                string subfolderId = kvp.Value;
                subfolderIdMap[folderName] = subfolderId;
                Debug.Log($"Manually set subfolder name: {folderName} for ID: {subfolderId}");
            }
        }

        // Method to rename a subfolder
        public void RenameSubfolder(string oldName, string newName)
        {
            if (subfolderIdMap.ContainsKey(oldName))
            {
                string subfolderId = subfolderIdMap[oldName];
                subfolderIdMap.Remove(oldName);
                subfolderIdMap[newName] = subfolderId;
                Debug.Log($"Renamed subfolder from '{oldName}' to '{newName}' for ID: {subfolderId}");
            }
            else
            {
                Debug.LogError($"Subfolder '{oldName}' not found for renaming");
            }
        }

        // Method to get all discovered subfolder IDs (for debugging)
        public Dictionary<string, string> GetDiscoveredSubfolderIds()
        {
            return new Dictionary<string, string>(subfolderIdMap);
        }

        // Method to clear and re-discover subfolders
        public async UniTaskVoid RediscoverSubfolders(Action<bool> onComplete)
        {
            subfolderIdMap.Clear();
            isInitialized = false;
            await DownloadData(onComplete);
        }
    }
}