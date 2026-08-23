using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Converts legacy LevelData .asset files (flat levelElements) into the current
    /// [SerializeReference] YAML format. Reads and writes only on disk, outside Assets/,
    /// so Unity does not import during conversion.
    /// </summary>
    internal sealed class LegacyLevelDataFolderConverter : EditorWindow
    {
        private const string DefaultSourceFolder =
            @"C:\Users\Admin\Downloads\20260506 - Level New 51 to 100";

        private const string DefaultOutputFolder =
            @"C:\Users\Admin\Downloads\Flow Out - Converted Legacy Levels";

        private const string AssetPattern = "*.asset";
        private const int FilesPerFrame = 8;
        private const int LegacyScanBufferBytes = 8192;

        private string sourceFolder = DefaultSourceFolder;
        private string outputFolder = DefaultOutputFolder;
        private bool overwriteExisting = true;
        private bool recursive = true;
        private Vector2 scrollPos;
        private ConversionSummary summary;
        private string[] files = Array.Empty<string>();
        private int fileIndex;
        private bool isRunning;
        private bool isScanning;
        private int progressId;

        [MenuItem("Tools/Level Data/Legacy Folder Converter")]
        private static void Open()
        {
            var w = GetWindow<LegacyLevelDataFolderConverter>("Legacy Level Converter");
            w.minSize = new Vector2(560f, 360f);
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            if (isRunning || isScanning)
            {
                isRunning = false;
                isScanning = false;
                ClearProgress();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Legacy Level Data Folder Converter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Reads and writes .asset files on disk outside the project Assets folder. " +
                "Only files that still use legacy levelElements are converted. " +
                "Copy the output folder into Assets/ when you are ready to import.",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                FolderField("Source Folder", ref sourceFolder);
                FolderField("Output Folder", ref outputFolder);
                recursive = EditorGUILayout.Toggle("Recursive", recursive);
                overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing Files", overwriteExisting);
            }

            EditorGUILayout.Space(8f);
            bool canRun = Directory.Exists(sourceFolder) && IsValidOutputFolder(outputFolder);
            using (new EditorGUI.DisabledScope(!canRun || isRunning))
            {
                if (GUILayout.Button("Convert Legacy Level Folder", GUILayout.Height(34f)))
                    Begin();
            }

            if (isRunning)
            {
                EditorGUILayout.Space(6f);
                string status = isScanning
                    ? "Scanning legacy files..."
                    : $"{fileIndex} / {files.Length}";
                float p = isScanning || files.Length == 0
                    ? 0f
                    : fileIndex / (float)files.Length;
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 22f), p, status);
                if (GUILayout.Button("Cancel", GUILayout.Height(26f)))
                    Cancel();
            }

            if (!Directory.Exists(sourceFolder))
                EditorGUILayout.HelpBox("Source folder not found.", MessageType.Warning);
            if (!string.IsNullOrWhiteSpace(outputFolder) && !IsValidOutputFolder(outputFolder))
                EditorGUILayout.HelpBox("Output folder path is invalid.", MessageType.Warning);
            else if (IsUnderAssets(outputFolder))
                EditorGUILayout.HelpBox(
                    "Output is inside Assets/ — Unity may reimport on every write. Prefer a folder outside the project.",
                    MessageType.Warning);

            DrawSummary();
        }

        private void DrawSummary()
        {
            if (summary == null) return;
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Last Result", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Found:     {summary.Found}");
            EditorGUILayout.LabelField($"Converted: {summary.Converted}");
            EditorGUILayout.LabelField($"Skipped:   {summary.Skipped}");
            EditorGUILayout.LabelField($"Failed:    {summary.Failed}");
            EditorGUILayout.LabelField($"Elements:  {summary.Elements}");
            foreach (string m in summary.Messages)
                EditorGUILayout.LabelField(m, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        private static void FolderField(string label, ref string folder)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                folder = EditorGUILayout.TextField(label, folder);
                if (!GUILayout.Button("...", GUILayout.Width(32f))) return;
                string start = Directory.Exists(folder) ? folder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string sel = EditorUtility.OpenFolderPanel(label, start, string.Empty);
                if (string.IsNullOrEmpty(sel)) return;
                folder = sel;
            }
        }

        // ─── Conversion lifecycle ────────────────────────────────────────────────

        private void Begin()
        {
            isRunning = true;
            isScanning = true;
            fileIndex = 0;
            files = Array.Empty<string>();
            summary = new ConversionSummary();
            progressId = Progress.Start("Legacy Level Converter", "Scanning...", Progress.Options.Sticky);

            string src = sourceFolder;
            bool scanRecursive = recursive;
            string outDir = Path.GetFullPath(outputFolder);
            Directory.CreateDirectory(outDir);

            Task.Run(() => ScanLegacyFiles(src, scanRecursive))
                .ContinueWith(task =>
                {
                    EditorApplication.delayCall += () => OnScanComplete(task, outDir);
                });
        }

        private void OnScanComplete(Task<List<string>> task, string outDirFull)
        {
            if (!isRunning)
                return;

            if (task.IsFaulted)
            {
                Debug.LogError($"[LegacyLevelDataFolderConverter] Scan failed: {task.Exception?.GetBaseException().Message}");
                Cancel();
                return;
            }

            task.Result.Sort(StringComparer.OrdinalIgnoreCase);
            files = task.Result.ToArray();
            outputFolder = outDirFull;
            summary.Found = files.Length;
            isScanning = false;
            fileIndex = 0;

            Progress.Report(progressId, 0f, $"Converting 0 / {files.Length}");

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Repaint();
        }

        private static List<string> ScanLegacyFiles(string folder, bool scanRecursive)
        {
            string[] allFiles = Directory.GetFiles(folder, AssetPattern,
                scanRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            var legacyOnly = new List<string>(allFiles.Length);
            for (int i = 0; i < allFiles.Length; i++)
            {
                if (FileContainsLegacyLevelElements(allFiles[i]))
                    legacyOnly.Add(allFiles[i]);
            }

            return legacyOnly;
        }

        private static bool FileContainsLegacyLevelElements(string path)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] buffer = new byte[LegacyScanBufferBytes];
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    return false;

                string head = Encoding.UTF8.GetString(buffer, 0, read);
                if (head.Contains("  levelElements:"))
                    return true;
                if (head.Contains("  elements:"))
                    return false;

                using var reader = new StreamReader(path);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("  levelElements:", StringComparison.Ordinal))
                        return true;
                    if (line.StartsWith("  elements:", StringComparison.Ordinal))
                        return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LegacyLevelDataFolderConverter] Could not read '{path}': {ex.Message}");
            }

            return false;
        }

        private void Tick()
        {
            if (!isRunning || isScanning)
                return;

            if (fileIndex >= files.Length)
            {
                Finish();
                return;
            }

            int end = Math.Min(fileIndex + FilesPerFrame, files.Length);
            for (int n = fileIndex; n < end; n++)
            {
                string path = files[n];
                Progress.Report(progressId, n / (float)files.Length,
                    $"Converting {Path.GetFileName(path)} ({n + 1}/{files.Length})");
                ProcessFile(path);
            }

            fileIndex = end;
            Repaint();
        }

        private void Cancel()
        {
            isRunning = false;
            isScanning = false;
            EditorApplication.update -= Tick;
            files = Array.Empty<string>();
            ClearProgress();
            Debug.LogWarning("[LegacyLevelDataFolderConverter] Cancelled.");
        }

        private void Finish()
        {
            isRunning = false;
            isScanning = false;
            EditorApplication.update -= Tick;
            files = Array.Empty<string>();
            ClearProgress();

            string msg =
                $"Converted {summary.Converted}/{summary.Found}\n" +
                $"Skipped: {summary.Skipped}  Failed: {summary.Failed}\n" +
                $"Elements: {summary.Elements}\n\nOutput:\n{outputFolder}";
            EditorUtility.DisplayDialog("Conversion Complete", msg, "OK");
            Debug.Log($"[LegacyLevelDataFolderConverter] {msg}");
        }

        private void ClearProgress()
        {
            if (progressId != 0)
            {
                Progress.Remove(progressId);
                progressId = 0;
            }
        }

        // ─── Per-file logic ──────────────────────────────────────────────────────

        private void ProcessFile(string srcPath)
        {
            try
            {
                string yaml = File.ReadAllText(srcPath);

                string relPath = MakeRelative(sourceFolder, srcPath);
                string dstFull = Path.Combine(
                    Path.GetFullPath(outputFolder),
                    relPath);

                if (!overwriteExisting && File.Exists(dstFull))
                {
                    summary.Skipped++;
                    return;
                }

                string dir = Path.GetDirectoryName(dstFull);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                LegacyAsset level = YamlParser.Parse(yaml);
                string name = string.IsNullOrEmpty(level.Name)
                    ? Path.GetFileNameWithoutExtension(srcPath)
                    : level.Name;

                string newYaml = YamlWriter.Write(level, name);
                File.WriteAllText(dstFull, newYaml, new UTF8Encoding(false));

                summary.Converted++;
                summary.Elements += level.Elements.Count;
            }
            catch (Exception ex)
            {
                summary.Failed++;
                string msg = $"FAIL {Path.GetFileName(srcPath)}: {ex.GetType().Name}: {ex.Message}";
                summary.Log(msg);
                Debug.LogError($"[LegacyLevelDataFolderConverter] {msg}");
            }
        }

        // ─── Path helpers ────────────────────────────────────────────────────────

        private static bool IsValidOutputFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string full = Path.GetFullPath(path);
                string root = Path.GetPathRoot(full);
                return !string.IsNullOrEmpty(root) &&
                       !full.Equals(root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsUnderAssets(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string assets = Path.GetFullPath(Application.dataPath);
                string full = Path.GetFullPath(path);
                return full.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                       full.Equals(assets, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string MakeRelative(string root, string full)
        {
            string nr = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string nf = Path.GetFullPath(full);
            return nf.StartsWith(nr, StringComparison.OrdinalIgnoreCase)
                ? nf.Substring(nr.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : Path.GetFileName(full);
        }

        // ─── Summary ────────────────────────────────────────────────────────────

        private sealed class ConversionSummary
        {
            public int Found, Converted, Skipped, Failed, Elements;
            public readonly List<string> Messages = new List<string>();
            public void Log(string m) { if (Messages.Count < 80) Messages.Add(m); }
        }

        // ─── Legacy data model ───────────────────────────────────────────────────

        private sealed class LegacyAsset
        {
            public string Name;
            public Vector2Int Size;
            public float Duration;
            public LevelType Type;
            public bool UseInRandomizer = true;
            public readonly List<LegacyElem> Elements = new List<LegacyElem>();
        }

        private sealed class LegacyElem
        {
            public ElementType Type;
            public Vector2Int Position;
            public BlockType BlockType;
            public BlockColor BlockColor;
            public int BlockId;
            public bool IsExtendable;
            public readonly List<LegacyBEff> BlockEffects = new List<LegacyBEff>();
            public readonly List<ColorData> GateData = new List<ColorData>();
            public readonly List<LegacyGEff> GateEffects = new List<LegacyGEff>();
            public readonly List<LegacyGenEntry> GeneratorQueue = new List<LegacyGenEntry>();
            public LegacyInteractable InteractableData;
        }

        private sealed class LegacyBEff
        {
            public BlockEffectType Type;
            public bool HorizontalDirection, ShutterIsOpen;
            public BlockColor LayeredBlockColor, SecondDualColor, KeyColor, ScissorColor;
            public int BombDuration, IceTurnsAmount, KeysAmount, CombineGroupId, TntTurn;
            public BlockColor[] RopesColors = Array.Empty<BlockColor>();
        }

        private sealed class LegacyGEff
        {
            public GateEffectType Type;
            public bool IsValveOpened, IsClockwise = true;
            public int IceTurnsAmount;
            public BlockColor LockColor;
        }

        private sealed class LegacyGenEntry
        {
            public int BlockId;
            public BlockType BlockType;
            public BlockColor BlockColor;
            public readonly List<LegacyBEff> BlockEffects = new List<LegacyBEff>();
        }

        private sealed class LegacyInteractable
        {
            public InteractableObjectType Type;
            public BlockColor ObstacleColor;
        }

        // ─── YAML Parser (reads legacy flat format) ──────────────────────────────

        private static class YamlParser
        {
            private static readonly Regex SizeRx =
                new Regex(@"size:\s*\{x:\s*(-?\d+),\s*y:\s*(-?\d+)\}", RegexOptions.Compiled);
            private static readonly Regex PosRx =
                new Regex(@"position:\s*\{x:\s*(-?\d+),\s*y:\s*(-?\d+)\}", RegexOptions.Compiled);

            public static LegacyAsset Parse(string yaml)
            {
                string[] lines = yaml.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                var a = new LegacyAsset();
                int guard = 0;
                int maxSteps = lines.Length * 32;

                for (int i = 0; i < lines.Length; i++)
                {
                    string ln = lines[i];
                    if (ln.StartsWith("  m_Name:", StringComparison.Ordinal))
                        a.Name = Val(ln).Trim();
                    else if (ln.StartsWith("  size:", StringComparison.Ordinal))
                        a.Size = Vec2Int(ln, SizeRx);
                    else if (ln.StartsWith("  duration:", StringComparison.Ordinal))
                        a.Duration = Float(ln);
                    else if (ln.StartsWith("  type:", StringComparison.Ordinal))
                        a.Type = Enum<LevelType>(ln);
                    else if (ln.StartsWith("  useInRandomizer:", StringComparison.Ordinal))
                        a.UseInRandomizer = Bool(ln);
                    else if (ln.StartsWith("  levelElements:", StringComparison.Ordinal))
                    {
                        i++;
                        while (i < lines.Length && !IsLevelElementsSectionEnd(lines[i]))
                        {
                            if (++guard > maxSteps)
                                throw new InvalidOperationException("YAML parse guard exceeded in levelElements.");
                            if (lines[i].StartsWith("  - type:", StringComparison.Ordinal))
                                a.Elements.Add(ParseElem(lines, ref i));
                            else
                                i++;
                        }
                    }
                }

                return a;
            }

            private static LegacyElem ParseElem(string[] lines, ref int idx)
            {
                var e = new LegacyElem { Type = Enum<ElementType>(lines[idx]) };
                idx++;

                while (idx < lines.Length &&
                       !lines[idx].StartsWith("  - type:", StringComparison.Ordinal) &&
                       !IsLevelElementsSectionEnd(lines[idx]))
                {
                    string ln = lines[idx];
                    int lineAt = idx;

                    if (ln.StartsWith("    position:", StringComparison.Ordinal))
                        e.Position = Vec2Int(ln, PosRx);
                    else if (ln.StartsWith("    blockType:", StringComparison.Ordinal))
                        e.BlockType = Enum<BlockType>(ln);
                    else if (ln.StartsWith("    blockColor:", StringComparison.Ordinal))
                        e.BlockColor = Enum<BlockColor>(ln);
                    else if (ln.StartsWith("    blockId:", StringComparison.Ordinal))
                        e.BlockId = Int(ln);
                    else if (ln.StartsWith("    isExtendable:", StringComparison.Ordinal))
                        e.IsExtendable = Bool(ln);
                    else if (ln.StartsWith("    blockEffects:", StringComparison.Ordinal))
                        ParseBEffects(lines, ref idx, e.BlockEffects, "    ");
                    else if (ln.StartsWith("    gateData:", StringComparison.Ordinal))
                        ParseGateData(lines, ref idx, e.GateData);
                    else if (ln.StartsWith("    gateEffects:", StringComparison.Ordinal))
                        ParseGEffects(lines, ref idx, e.GateEffects, "    ");
                    else if (ln.StartsWith("    generatorQueue:", StringComparison.Ordinal))
                        ParseGenQueue(lines, ref idx, e.GeneratorQueue);
                    else if (ln.StartsWith("    interactableObjectData:", StringComparison.Ordinal))
                        e.InteractableData = ParseInteractable(lines, ref idx);

                    if (idx == lineAt)
                        idx++;
                }

                return e;
            }

            private static void ParseBEffects(string[] lines, ref int idx,
                List<LegacyBEff> list, string indent)
            {
                idx++;
                string itemPfx = indent + "- type:";
                while (idx < lines.Length)
                {
                    string ln = lines[idx];
                    if (ln.StartsWith("  - type:", StringComparison.Ordinal) ||
                        IsElemSibling(ln))
                        return;

                    if (ln.StartsWith(itemPfx, StringComparison.Ordinal))
                        list.Add(ParseBEffect(lines, ref idx, indent));
                    else
                        idx++;
                }
            }

            private static LegacyBEff ParseBEffect(string[] lines, ref int idx, string indent)
            {
                var eff = new LegacyBEff { Type = Enum<BlockEffectType>(lines[idx]) };
                idx++;
                string fi = indent + "  ";

                while (idx < lines.Length)
                {
                    string ln = lines[idx];
                    if (ln.StartsWith(indent + "- type:", StringComparison.Ordinal) ||
                        ln.StartsWith("  - type:", StringComparison.Ordinal) ||
                        IsElemSibling(ln))
                        return eff;

                    if (ln.StartsWith(fi + "horizontalDirection:", StringComparison.Ordinal))
                        eff.HorizontalDirection = Bool(ln);
                    else if (ln.StartsWith(fi + "shutterIsOpen:", StringComparison.Ordinal))
                        eff.ShutterIsOpen = Bool(ln);
                    else if (ln.StartsWith(fi + "layeredBlockColor:", StringComparison.Ordinal))
                        eff.LayeredBlockColor = Enum<BlockColor>(ln);
                    else if (ln.StartsWith(fi + "secondDualColor:", StringComparison.Ordinal))
                        eff.SecondDualColor = Enum<BlockColor>(ln);
                    else if (ln.StartsWith(fi + "bombDuration:", StringComparison.Ordinal))
                        eff.BombDuration = Int(ln);
                    else if (ln.StartsWith(fi + "iceTurnsAmount:", StringComparison.Ordinal))
                        eff.IceTurnsAmount = Int(ln);
                    else if (ln.StartsWith(fi + "hiddenTurnsAmount:", StringComparison.Ordinal))
                        eff.IceTurnsAmount = Int(ln);
                    else if (ln.StartsWith(fi + "keysAmount:", StringComparison.Ordinal))
                        eff.KeysAmount = Int(ln);
                    else if (ln.StartsWith(fi + "keyColor:", StringComparison.Ordinal))
                        eff.KeyColor = Enum<BlockColor>(ln);
                    else if (ln.StartsWith(fi + "combineGroupID:", StringComparison.Ordinal))
                        eff.CombineGroupId = Int(ln);
                    else if (ln.StartsWith(fi + "ropesColors:", StringComparison.Ordinal))
                        eff.RopesColors = DecodeRopesColors(Val(ln).Trim());
                    else if (ln.StartsWith(fi + "scissorColor:", StringComparison.Ordinal))
                        eff.ScissorColor = Enum<BlockColor>(ln);
                    else if (ln.StartsWith(fi + "tntTurn:", StringComparison.Ordinal))
                        eff.TntTurn = Int(ln);

                    idx++;
                }

                return eff;
            }

            private static void ParseGateData(string[] lines, ref int idx, List<ColorData> list)
            {
                idx++;
                while (idx < lines.Length)
                {
                    string ln = lines[idx];
                    if (ln.StartsWith("  - type:", StringComparison.Ordinal) ||
                        IsElemSibling(ln))
                        return;

                    if (ln.StartsWith("    - color:", StringComparison.Ordinal))
                    {
                        var cd = new ColorData { color = Enum<BlockColor>(ln) };
                        idx++;
                        if (idx < lines.Length &&
                            lines[idx].StartsWith("      colorCount:", StringComparison.Ordinal))
                            cd.colorCount = Int(lines[idx]);
                        list.Add(cd);
                    }

                    idx++;
                }
            }

            private static void ParseGEffects(string[] lines, ref int idx,
                List<LegacyGEff> list, string indent)
            {
                idx++;
                string itemPfx = indent + "- type:";
                while (idx < lines.Length)
                {
                    string ln = lines[idx];
                    if (ln.StartsWith("  - type:", StringComparison.Ordinal) ||
                        IsElemSibling(ln))
                        return;

                    if (ln.StartsWith(itemPfx, StringComparison.Ordinal))
                        list.Add(ParseGEffect(lines, ref idx, indent));
                    else
                        idx++;
                }
            }

            private static LegacyGEff ParseGEffect(string[] lines, ref int idx, string indent)
            {
                var eff = new LegacyGEff { Type = Enum<GateEffectType>(lines[idx]) };
                idx++;
                string fi = indent + "  ";

                while (idx < lines.Length)
                {
                    string ln = lines[idx];
                    if (ln.StartsWith(indent + "- type:", StringComparison.Ordinal) ||
                        ln.StartsWith("  - type:", StringComparison.Ordinal) ||
                        IsElemSibling(ln))
                        return eff;

                    if (ln.StartsWith(fi + "isValveOpened:", StringComparison.Ordinal))
                        eff.IsValveOpened = Bool(ln);
                    else if (ln.StartsWith(fi + "iceTurnsAmount:", StringComparison.Ordinal))
                        eff.IceTurnsAmount = Int(ln);
                    else if (ln.StartsWith(fi + "lockColor:", StringComparison.Ordinal))
                        eff.LockColor = Enum<BlockColor>(ln);
                    else if (ln.StartsWith(fi + "isClockwise:", StringComparison.Ordinal))
                        eff.IsClockwise = Bool(ln);

                    idx++;
                }

                return eff;
            }

            private static void ParseGenQueue(string[] lines, ref int idx,
                List<LegacyGenEntry> list)
            {
                idx++;
                while (idx < lines.Length)
                {
                    string ln = lines[idx];
                    if (ln.StartsWith("  - type:", StringComparison.Ordinal) ||
                        IsElemSibling(ln))
                        return;

                    if (ln.StartsWith("    - blockId:", StringComparison.Ordinal) ||
                        ln.StartsWith("    - blockType:", StringComparison.Ordinal) ||
                        ln.StartsWith("    - blockColor:", StringComparison.Ordinal))
                        list.Add(ParseGenEntry(lines, ref idx));
                    else
                        idx++;
                }
            }

            private static LegacyGenEntry ParseGenEntry(string[] lines, ref int idx)
            {
                var e = new LegacyGenEntry();
                string first = lines[idx];
                if (first.StartsWith("    - blockId:", StringComparison.Ordinal))
                    e.BlockId = Int(first);
                else if (first.StartsWith("    - blockType:", StringComparison.Ordinal))
                    e.BlockType = Enum<BlockType>(first);
                else if (first.StartsWith("    - blockColor:", StringComparison.Ordinal))
                    e.BlockColor = Enum<BlockColor>(first);
                idx++;

                while (idx < lines.Length)
                {
                    string ln = lines[idx];
                    if (ln.StartsWith("    - ", StringComparison.Ordinal) ||
                        ln.StartsWith("  - type:", StringComparison.Ordinal) ||
                        IsElemSibling(ln))
                        return e;

                    int lineAt = idx;
                    if (ln.StartsWith("      blockId:", StringComparison.Ordinal))
                        e.BlockId = Int(ln);
                    else if (ln.StartsWith("      blockType:", StringComparison.Ordinal))
                        e.BlockType = Enum<BlockType>(ln);
                    else if (ln.StartsWith("      blockColor:", StringComparison.Ordinal))
                        e.BlockColor = Enum<BlockColor>(ln);
                    else if (ln.StartsWith("      blockEffects:", StringComparison.Ordinal))
                        ParseBEffects(lines, ref idx, e.BlockEffects, "      ");

                    if (idx == lineAt)
                        idx++;
                }

                return e;
            }

            private static LegacyInteractable ParseInteractable(string[] lines, ref int idx)
            {
                var d = new LegacyInteractable();
                idx++;
                while (idx < lines.Length)
                {
                    string ln = lines[idx];
                    if (ln.StartsWith("  - type:", StringComparison.Ordinal) ||
                        IsElemSibling(ln))
                        return d;

                    if (ln.StartsWith("      type:", StringComparison.Ordinal))
                        d.Type = Enum<InteractableObjectType>(ln);
                    else if (ln.StartsWith("      obstacleColor:", StringComparison.Ordinal))
                        d.ObstacleColor = Enum<BlockColor>(ln);

                    idx++;
                }

                return d;
            }

            private static bool IsElemSibling(string ln) =>
                ln.StartsWith("    position:", StringComparison.Ordinal) ||
                ln.StartsWith("    blockType:", StringComparison.Ordinal) ||
                ln.StartsWith("    blockColor:", StringComparison.Ordinal) ||
                ln.StartsWith("    blockId:", StringComparison.Ordinal) ||
                ln.StartsWith("    isExtendable:", StringComparison.Ordinal) ||
                ln.StartsWith("    blockEffects:", StringComparison.Ordinal) ||
                ln.StartsWith("    gateData:", StringComparison.Ordinal) ||
                ln.StartsWith("    gateEffects:", StringComparison.Ordinal) ||
                ln.StartsWith("    generatorQueue:", StringComparison.Ordinal) ||
                ln.StartsWith("    interactableObjectData:", StringComparison.Ordinal);

            private static bool IsLevelElementsSectionEnd(string ln) =>
                ln.StartsWith("  useInRandomizer:", StringComparison.Ordinal) ||
                ln.StartsWith("  references:", StringComparison.Ordinal) ||
                ln.StartsWith("  elements:", StringComparison.Ordinal);

            // ── Primitives ──────────────────────────────────────────────────────

            private static string Val(string ln)
            {
                int c = ln.IndexOf(':');
                return c < 0 ? string.Empty : ln.Substring(c + 1);
            }

            private static int Int(string ln)
            {
                string v = Val(ln).Trim();
                return string.IsNullOrEmpty(v) ? 0
                    : int.Parse(v, CultureInfo.InvariantCulture);
            }

            private static float Float(string ln)
            {
                string v = Val(ln).Trim();
                return string.IsNullOrEmpty(v) ? 0f
                    : float.Parse(v, CultureInfo.InvariantCulture);
            }

            private static bool Bool(string ln) =>
                Val(ln).Trim() is string v && (v == "1" ||
                    v.Equals("true", StringComparison.OrdinalIgnoreCase));

            private static TEnum Enum<TEnum>(string ln) where TEnum : struct, System.Enum =>
                (TEnum)System.Enum.ToObject(typeof(TEnum), Int(ln));

            private static Vector2Int Vec2Int(string ln, Regex rx)
            {
                var m = rx.Match(ln);
                return m.Success
                    ? new Vector2Int(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                                     int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture))
                    : default;
            }

            private static BlockColor[] DecodeRopesColors(string hex)
            {
                if (string.IsNullOrEmpty(hex)) return Array.Empty<BlockColor>();
                int count = hex.Length / 8;
                var arr = new BlockColor[count];
                for (int i = 0; i < count; i++)
                {
                    string c = hex.Substring(i * 8, 8);
                    int v = Convert.ToInt32(c.Substring(0, 2), 16) |
                            Convert.ToInt32(c.Substring(2, 2), 16) << 8 |
                            Convert.ToInt32(c.Substring(4, 2), 16) << 16 |
                            Convert.ToInt32(c.Substring(6, 2), 16) << 24;
                    arr[i] = (BlockColor)v;
                }
                return arr;
            }
        }

        // ─── YAML Writer (generates new SerializeReference format) ───────────────

        private static class YamlWriter
        {
            private const string ScriptGuid = "6aafc1ca0b84426881bccef3317c660a";
            private const string Ns = "WaterFlow.Game";
            private const string Asm = "Assembly-CSharp";

            public static string Write(LegacyAsset level, string name)
            {
                // Phase 1: assign RIDs (Unity uses large unique ids per asset)
                long nextRid = CreateRidSeed();
                int count = level.Elements.Count;

                var elemRids = new long[count];
                for (int i = 0; i < count; i++) elemRids[i] = nextRid++;

                var bRids = new List<long>[count];
                var gRids = new List<long>[count];
                var genRids = new List<List<long>>[count];

                for (int i = 0; i < count; i++)
                {
                    var el = level.Elements[i];
                    bRids[i] = new List<long>();
                    foreach (var e in el.BlockEffects)
                        if (BEffClass(e.Type) != null) bRids[i].Add(nextRid++);

                    gRids[i] = new List<long>();
                    foreach (var e in el.GateEffects)
                        if (GEffClass(e.Type) != null) gRids[i].Add(nextRid++);

                    genRids[i] = new List<List<long>>();
                    foreach (var entry in el.GeneratorQueue)
                    {
                        var er = new List<long>();
                        foreach (var e in entry.BlockEffects)
                            if (BEffClass(e.Type) != null) er.Add(nextRid++);
                        genRids[i].Add(er);
                    }
                }

                // Phase 2: write YAML
                var sb = new StringBuilder(count * 200 + 512);

                sb.Append("%YAML 1.1\n");
                sb.Append("%TAG !u! tag:unity3d.com,2011:\n");
                sb.Append("--- !u!114 &11400000\n");
                sb.Append("MonoBehaviour:\n");
                sb.Append("  m_ObjectHideFlags: 0\n");
                sb.Append("  m_CorrespondingSourceObject: {fileID: 0}\n");
                sb.Append("  m_PrefabInstance: {fileID: 0}\n");
                sb.Append("  m_PrefabAsset: {fileID: 0}\n");
                sb.Append("  m_GameObject: {fileID: 0}\n");
                sb.Append("  m_Enabled: 1\n");
                sb.Append("  m_EditorHideFlags: 0\n");
                sb.Append($"  m_Script: {{fileID: 11500000, guid: {ScriptGuid}, type: 3}}\n");
                sb.Append($"  m_Name: {name}\n");
                sb.Append("  m_EditorClassIdentifier: Assembly-CSharp::WaterFlow.Game.LevelData\n");
                sb.Append($"  size: {{x: {level.Size.x}, y: {level.Size.y}}}\n");
                sb.Append($"  duration: {level.Duration.ToString("G", CultureInfo.InvariantCulture)}\n");
                sb.Append($"  type: {(int)level.Type}\n");

                if (count == 0)
                {
                    sb.Append("  elements: []\n");
                }
                else
                {
                    sb.Append("  elements:\n");
                    foreach (long rid in elemRids)
                        sb.Append($"  - rid: {rid}\n");
                }

                sb.Append($"  useInRandomizer: {(level.UseInRandomizer ? 1 : 0)}\n");
                sb.Append("  references:\n");
                sb.Append("    version: 2\n");
                sb.Append("    RefIds:\n");
                sb.Append("    - rid: -2\n");
                sb.Append("      type: {class: , ns: , asm: }\n");

                // Elements
                for (int i = 0; i < count; i++)
                    WriteElem(sb, level.Elements[i], elemRids[i], bRids[i], gRids[i], genRids[i]);

                // Effects (after all elements)
                for (int i = 0; i < count; i++)
                {
                    var el = level.Elements[i];
                    int bi = 0;
                    foreach (var e in el.BlockEffects)
                    {
                        string cls = BEffClass(e.Type);
                        if (cls == null) continue;
                        WriteBEff(sb, bRids[i][bi++], cls, e);
                    }

                    int gi = 0;
                    foreach (var e in el.GateEffects)
                    {
                        string cls = GEffClass(e.Type);
                        if (cls == null) continue;
                        WriteGEff(sb, gRids[i][gi++], cls, e);
                    }

                    for (int j = 0; j < el.GeneratorQueue.Count; j++)
                    {
                        int ei = 0;
                        foreach (var e in el.GeneratorQueue[j].BlockEffects)
                        {
                            string cls = BEffClass(e.Type);
                            if (cls == null) continue;
                            WriteBEff(sb, genRids[i][j][ei++], cls, e);
                        }
                    }
                }

                return sb.ToString();
            }

            private static void WriteElem(StringBuilder sb, LegacyElem el, long rid,
                List<long> bR, List<long> gR, List<List<long>> genR)
            {
                string cls = ElemClass(el.Type);
                sb.Append($"    - rid: {rid}\n");
                sb.Append($"      type: {{class: {cls}, ns: {Ns}, asm: {Asm}}}\n");
                sb.Append("      data:\n");
                sb.Append($"        position: {{x: {el.Position.x}, y: {el.Position.y}}}\n");
                sb.Append($"        blockId: {el.BlockId}\n");

                switch (el.Type)
                {
                    case ElementType.Border:
                        sb.Append($"        isExtendable: {(el.IsExtendable ? 1 : 0)}\n");
                        break;

                    case ElementType.Block:
                        sb.Append($"        blockType: {(int)el.BlockType}\n");
                        sb.Append($"        blockColor: {(int)el.BlockColor}\n");
                        WriteRidArr(sb, "        blockEffects", bR);
                        break;

                    case ElementType.Gate:
                        WriteColorArr(sb, el.GateData);
                        WriteRidArr(sb, "        gateEffects", gR);
                        break;

                    case ElementType.Generator:
                        WriteGenQueue(sb, el.GeneratorQueue, genR);
                        break;

                    case ElementType.InteractableObject:
                        WriteInteractable(sb, el.InteractableData);
                        break;
                }
            }

            private static void WriteRidArr(StringBuilder sb, string field, List<long> rids)
            {
                if (rids.Count == 0) { sb.Append($"{field}: []\n"); return; }
                sb.Append($"{field}:\n");
                int indent = 0;
                while (indent < field.Length && field[indent] == ' ') indent++;
                string pad = field.Substring(0, indent);
                foreach (long r in rids) sb.Append($"{pad}- rid: {r}\n");
            }

            private static void WriteColorArr(StringBuilder sb, List<ColorData> data)
            {
                if (data == null || data.Count == 0)
                {
                    sb.Append("        gateData: []\n");
                    return;
                }
                sb.Append("        gateData:\n");
                foreach (var cd in data)
                {
                    sb.Append($"        - color: {(int)cd.color}\n");
                    sb.Append($"          colorCount: {cd.colorCount}\n");
                }
            }

            private static void WriteGenQueue(StringBuilder sb,
                List<LegacyGenEntry> queue, List<List<long>> entryRids)
            {
                if (queue == null || queue.Count == 0)
                {
                    sb.Append("        generatorQueue: []\n");
                    return;
                }
                sb.Append("        generatorQueue:\n");
                for (int j = 0; j < queue.Count; j++)
                {
                    var entry = queue[j];
                    sb.Append($"        - blockId: {entry.BlockId}\n");
                    sb.Append($"          blockType: {(int)entry.BlockType}\n");
                    sb.Append($"          blockColor: {(int)entry.BlockColor}\n");

                    var rids = j < entryRids.Count ? entryRids[j] : new List<long>();
                    if (rids.Count == 0)
                    {
                        sb.Append("          blockEffects: []\n");
                    }
                    else
                    {
                        sb.Append("          blockEffects:\n");
                        foreach (long r in rids) sb.Append($"          - rid: {r}\n");
                    }
                }
            }

            private static void WriteInteractable(StringBuilder sb, LegacyInteractable d)
            {
                sb.Append("        interactableObjectData:\n");
                int t = d != null ? (int)d.Type : 0;
                int c = d != null ? (int)d.ObstacleColor : 0;
                sb.Append($"          type: {t}\n");
                sb.Append($"          obstacleColor: {c}\n");
            }

            private static void WriteBEff(StringBuilder sb, long rid, string cls, LegacyBEff e)
            {
                sb.Append($"    - rid: {rid}\n");
                sb.Append($"      type: {{class: {cls}, ns: {Ns}, asm: {Asm}}}\n");

                switch (e.Type)
                {
                    case BlockEffectType.Ice:
                        sb.Append("      data:\n");
                        sb.Append($"        iceTurnsAmount: {e.IceTurnsAmount}\n");
                        break;
                    case BlockEffectType.Hidden:
                        sb.Append("      data:\n");
                        sb.Append($"        hiddenTurnsAmount: {e.IceTurnsAmount}\n");
                        break;
                    case BlockEffectType.Bomb:
                        sb.Append("      data:\n");
                        sb.Append($"        bombDuration: {e.BombDuration}\n");
                        break;
                    case BlockEffectType.Layered:
                        sb.Append("      data:\n");
                        sb.Append($"        layeredBlockColor: {(int)e.LayeredBlockColor}\n");
                        break;
                    case BlockEffectType.FixedDirection:
                        sb.Append("      data:\n");
                        sb.Append($"        horizontalDirection: {(e.HorizontalDirection ? 1 : 0)}\n");
                        break;
                    case BlockEffectType.Dual:
                        sb.Append("      data:\n");
                        sb.Append($"        secondDualColor: {(int)e.SecondDualColor}\n");
                        break;
                    case BlockEffectType.Shutter:
                        sb.Append("      data:\n");
                        sb.Append($"        shutterIsOpen: {(e.ShutterIsOpen ? 1 : 0)}\n");
                        break;
                    case BlockEffectType.Chain:
                        sb.Append("      data:\n");
                        sb.Append($"        keysAmount: {e.KeysAmount}\n");
                        break;
                    case BlockEffectType.KeyChain:
                    case BlockEffectType.Blocked:
                        sb.Append("      data: \n");
                        break;
                    case BlockEffectType.KeyColor:
                        sb.Append("      data:\n");
                        sb.Append($"        keyColor: {(int)e.KeyColor}\n");
                        break;
                    case BlockEffectType.Combines:
                        sb.Append("      data:\n");
                        sb.Append($"        combineGroupID: {e.CombineGroupId}\n");
                        break;
                    case BlockEffectType.Ropes:
                        sb.Append("      data:\n");
                        sb.Append($"        ropesColors: {EncodeRopes(e.RopesColors)}\n");
                        break;
                    case BlockEffectType.Scissor:
                        sb.Append("      data:\n");
                        sb.Append($"        scissorColor: {(int)e.ScissorColor}\n");
                        break;
                    case BlockEffectType.Tnt:
                        sb.Append("      data:\n");
                        sb.Append($"        tntTurn: {e.TntTurn}\n");
                        break;
                    default:
                        sb.Append("      data: \n");
                        break;
                }
            }

            private static void WriteGEff(StringBuilder sb, long rid, string cls, LegacyGEff e)
            {
                sb.Append($"    - rid: {rid}\n");
                sb.Append($"      type: {{class: {cls}, ns: {Ns}, asm: {Asm}}}\n");

                switch (e.Type)
                {
                    case GateEffectType.IceGate:
                        sb.Append("      data:\n");
                        sb.Append($"        iceTurnsAmount: {e.IceTurnsAmount}\n");
                        break;
                    case GateEffectType.Valve:
                        sb.Append("      data:\n");
                        sb.Append($"        isValveOpened: {(e.IsValveOpened ? 1 : 0)}\n");
                        break;
                    case GateEffectType.LockedColor:
                        sb.Append("      data:\n");
                        sb.Append($"        lockColor: {(int)e.LockColor}\n");
                        break;
                    case GateEffectType.MovingLock:
                        sb.Append("      data:\n");
                        sb.Append($"        isClockwise: {(e.IsClockwise ? 1 : 0)}\n");
                        break;
                    default:
                        sb.Append("      data: \n");
                        break;
                }
            }

            // ── Class name maps ─────────────────────────────────────────────────

            private static string ElemClass(ElementType t)
            {
                switch (t)
                {
                    case ElementType.Empty:             return "EmptyLevelElementData";
                    case ElementType.InnerTile:         return "InnerTileLevelElementData";
                    case ElementType.Obstacle:          return "ObstacleLevelElementData";
                    case ElementType.Border:            return "BorderLevelElementData";
                    case ElementType.Block:             return "BlockLevelElementData";
                    case ElementType.Gate:              return "GateLevelElementData";
                    case ElementType.Generator:         return "GeneratorLevelElementData";
                    case ElementType.InteractableObject:return "InteractableObjectLevelElementData";
                    default:                            return "EmptyLevelElementData";
                }
            }

            private static string BEffClass(BlockEffectType t)
            {
                switch (t)
                {
                    case BlockEffectType.Ice:           return "IceBlockEffectData";
                    case BlockEffectType.Hidden:        return "HiddenBlockEffectData";
                    case BlockEffectType.Bomb:          return "BombBlockEffectData";
                    case BlockEffectType.Layered:       return "LayeredBlockEffectData";
                    case BlockEffectType.FixedDirection:return "FixedDirectionBlockEffectData";
                    case BlockEffectType.Dual:          return "DualBlockEffectData";
                    case BlockEffectType.Shutter:       return "ShutterBlockEffectData";
                    case BlockEffectType.Chain:         return "ChainBlockEffectData";
                    case BlockEffectType.KeyChain:      return "KeyChainBlockEffectData";
                    case BlockEffectType.KeyColor:      return "KeyColorBlockEffectData";
                    case BlockEffectType.Combines:      return "CombinesBlockEffectData";
                    case BlockEffectType.Ropes:         return "RopesBlockEffectData";
                    case BlockEffectType.Scissor:       return "ScissorBlockEffectData";
                    case BlockEffectType.Tnt:           return "TntBlockEffectData";
                    case BlockEffectType.Blocked:       return "BlockedBlockEffectData";
                    default:                            return null;
                }
            }

            private static string GEffClass(GateEffectType t)
            {
                switch (t)
                {
                    case GateEffectType.IceGate:        return "IceGateEffectData";
                    case GateEffectType.Valve:      return "ValveGateEffectData";
                    case GateEffectType.LockedColor:return "LockedColorGateEffectData";
                    case GateEffectType.MovingLock: return "MovingLockGateEffectData";
                    default:                        return null;
                }
            }

            // ── Helpers ─────────────────────────────────────────────────────────

            private static long CreateRidSeed()
            {
                long ticks = DateTime.UtcNow.Ticks;
                return 266889289000000000L + (ticks % 99_999_999L) * 1000L;
            }

            private static string EncodeRopes(BlockColor[] colors)
            {
                if (colors == null || colors.Length == 0) return string.Empty;
                var sb = new StringBuilder(colors.Length * 8);
                foreach (var c in colors)
                {
                    uint v = (uint)(int)c;
                    sb.Append((v & 0xFF).ToString("x2"));
                    sb.Append(((v >> 8) & 0xFF).ToString("x2"));
                    sb.Append(((v >> 16) & 0xFF).ToString("x2"));
                    sb.Append(((v >> 24) & 0xFF).ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
