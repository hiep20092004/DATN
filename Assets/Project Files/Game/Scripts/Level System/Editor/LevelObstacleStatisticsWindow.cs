using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    public class LevelObstacleStatisticsWindow : EditorWindow
    {
        private enum MainTab
        {
            LevelOverview = 0,
            EffectSearch = 1,
            EffectCombos = 2
        }

        private enum SearchCategory
        {
            BlockEffect = 0,
            BlockEffectCombo = 1,
            GateEffect = 2,
            Generator = 3,
            InteractableObject = 4
        }

        private enum ComboCategory
        {
            All = 0,
            Block = 1,
            Gate = 2
        }

        private sealed class BlockWithEffects
        {
            public int BlockId;
            public BlockType BlockType;
            public BlockColor BlockColor;
            public BlockEffectData[] Effects;
        }

        private sealed class GateWithEffects
        {
            public int BlockId;
            public GateEffectData[] Effects;
        }

        private sealed class GeneratorEntry
        {
            public int BlockId;
            public BlockType BlockType;
            public BlockColor BlockColor;
            public BlockEffectData[] Effects;
        }

        private sealed class LevelEffectSummary
        {
            public LevelData Level;
            public int LevelIndex;
            public readonly List<BlockWithEffects> BlocksWithEffects = new List<BlockWithEffects>();
            public readonly List<GateWithEffects> GatesWithEffects = new List<GateWithEffects>();
            public readonly List<GeneratorEntry> GeneratorBlocks = new List<GeneratorEntry>();
            public readonly Dictionary<InteractableObjectType, int> InteractableTypeCounts = new Dictionary<InteractableObjectType, int>();
            public int GeneratorCellCount;

            public int TotalObstacleEntries
            {
                get
                {
                    int interactableCount = 0;
                    foreach (KeyValuePair<InteractableObjectType, int> pair in InteractableTypeCounts)
                        interactableCount += pair.Value;

                    return BlocksWithEffects.Count + GatesWithEffects.Count + GeneratorBlocks.Count + interactableCount;
                }
            }

            public bool IsClean => TotalObstacleEntries == 0;

            public string GetDisplayLabel()
            {
                string levelType = Level != null ? Level.Type.ToString() : "-";
                string size = Level != null ? $"{Level.Size.x}x{Level.Size.y}" : "-";
                return $"Level {LevelIndex + 1} - {levelType} {size}";
            }
        }

        private sealed class SearchOccurrence
        {
            public string ContainerLabel;
            public int MatchCount;
            public readonly List<string> CoEffects = new List<string>();
        }

        private sealed class SearchLevelResult
        {
            public LevelEffectSummary Summary;
            public int TotalMatches;
            public readonly List<SearchOccurrence> Occurrences = new List<SearchOccurrence>();
        }

        private sealed class ComboLevelHit
        {
            public LevelEffectSummary Summary;
            public int Count;
        }

        private sealed class EffectComboEntry
        {
            public string Key;
            public bool IsBlockCombo;
            public string ComboLabel;
            public int OccurrenceCount;
            public readonly Dictionary<int, ComboLevelHit> LevelHits = new Dictionary<int, ComboLevelHit>();

            public int LevelCount => LevelHits.Count;

            public string GetDisplayLabel()
            {
                string source = IsBlockCombo ? "Block" : "Gate";
                return $"{source}: {ComboLabel}";
            }
        }

        private const string WindowTitle = "Level Obstacle Stats";
        private const string LevelsFieldName = "levels";

        private LevelDatabase levelDatabase;
        private MainTab mainTab;
        private Vector2 overviewScroll;
        private Vector2 searchScroll;
        private Vector2 comboScroll;
        private string levelNameFilter = string.Empty;
        private bool hideCleanLevels;

        private SearchCategory searchCategory;
        private BlockEffectType searchBlockEffect;
        private BlockEffectType searchSecondaryBlockEffect = BlockEffectType.ContainerBox;
        private GateEffectType searchGateEffect;
        private InteractableObjectType searchInteractableType = InteractableObjectType.ColorObstacle;
        private readonly List<SearchLevelResult> searchResults = new List<SearchLevelResult>();
        private int totalSearchOccurrences;
        private bool didRunSearch;

        private readonly List<LevelEffectSummary> levelSummaries = new List<LevelEffectSummary>();
        private readonly Dictionary<int, bool> overviewFoldouts = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> searchFoldouts = new Dictionary<int, bool>();
        private readonly List<EffectComboEntry> comboEntries = new List<EffectComboEntry>();
        private readonly Dictionary<string, EffectComboEntry> comboEntriesByKey = new Dictionary<string, EffectComboEntry>();
        private readonly Dictionary<string, bool> comboFoldouts = new Dictionary<string, bool>();
        private ComboCategory comboCategory;
        private string comboSearchQuery = string.Empty;

        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle subSectionStyle;
        private bool stylesInitialized;

        [MenuItem("Tools/Level Extension/Level Obstacle Statistics")]
        public static void ShowWindow()
        {
            LevelObstacleStatisticsWindow window = GetWindow<LevelObstacleStatisticsWindow>(WindowTitle);
            window.minSize = new Vector2(760f, 580f);
            window.Show();
        }

        private void OnEnable()
        {
            TryFindLevelDatabase();
            BuildCache();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawHeaderBar();

            if (levelDatabase == null)
            {
                EditorGUILayout.HelpBox("Please assign a LevelDatabase asset.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6f);
            mainTab = (MainTab)GUILayout.Toolbar((int)mainTab, new[] { "Level Overview", "Effect Search", "Effect Combos" });
            EditorGUILayout.Space(8f);

            switch (mainTab)
            {
                case MainTab.LevelOverview:
                    DrawOverviewTab();
                    break;
                case MainTab.EffectSearch:
                    DrawSearchTab();
                    break;
                case MainTab.EffectCombos:
                    DrawCombosTab();
                    break;
            }
        }

        private void DrawHeaderBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();
            levelDatabase = (LevelDatabase)EditorGUILayout.ObjectField("Level Database", levelDatabase, typeof(LevelDatabase), false);
            if (EditorGUI.EndChangeCheck())
                BuildCache();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(100f)))
                BuildCache();

            GUILayout.Label(
                $"Levels: {levelSummaries.Count}",
                EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawOverviewTab()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            levelNameFilter = EditorGUILayout.TextField("Filter by name", levelNameFilter);
            hideCleanLevels = EditorGUILayout.ToggleLeft("Hide clean levels", hideCleanLevels, GUILayout.Width(130f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            overviewScroll = EditorGUILayout.BeginScrollView(overviewScroll);

            List<LevelEffectSummary> activeList = levelSummaries;

            int shown = 0;
            for (int i = 0; i < activeList.Count; i++)
            {
                LevelEffectSummary summary = activeList[i];
                if (!PassOverviewFilters(summary))
                    continue;

                shown++;
                DrawOverviewCard(summary);
            }

            if (shown == 0)
                EditorGUILayout.HelpBox("No levels match current filter.", MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        private bool PassOverviewFilters(LevelEffectSummary summary)
        {
            if (summary == null || summary.Level == null)
                return false;

            if (hideCleanLevels && summary.IsClean)
                return false;

            if (string.IsNullOrEmpty(levelNameFilter))
                return true;

            return summary.Level.name.IndexOf(levelNameFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawOverviewCard(LevelEffectSummary summary)
        {
            int key = summary.Level.GetInstanceID();
            bool isExpanded = overviewFoldouts.ContainsKey(key) && overviewFoldouts[key];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            bool nextExpanded = EditorGUILayout.Foldout(
                isExpanded,
                $"{summary.GetDisplayLabel()} ({summary.TotalObstacleEntries} entries)",
                true);
            if (nextExpanded != isExpanded)
                overviewFoldouts[key] = nextExpanded;

            if (GUILayout.Button("Ping", GUILayout.Width(52f)))
            {
                Selection.activeObject = summary.Level;
                EditorGUIUtility.PingObject(summary.Level);
            }

            if (GUILayout.Button("Open", GUILayout.Width(52f)))
                OpenLevelInLevelEditor(summary);

            EditorGUILayout.EndHorizontal();

            if (nextExpanded)
            {
                DrawOverviewEffectCombinationSummary(summary);
                DrawBlockEffectsSection(summary);
                DrawGateEffectsSection(summary);
                DrawGeneratorSection(summary);
                DrawInteractableSection(summary);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBlockEffectsSection(LevelEffectSummary summary)
        {
            if (summary.BlocksWithEffects.Count == 0)
                return;

            EditorGUILayout.LabelField("Block Effects", subSectionStyle);
            for (int i = 0; i < summary.BlocksWithEffects.Count; i++)
            {
                BlockWithEffects block = summary.BlocksWithEffects[i];
                EditorGUILayout.BeginVertical(EditorStyles.textArea);
                EditorGUILayout.LabelField(
                    $"Block #{block.BlockId} - {block.BlockColor}/{block.BlockType}",
                    EditorStyles.boldLabel);
                DrawBlockEffectsList(block.Effects);
                EditorGUILayout.EndVertical();
            }
        }

        private static void DrawOverviewEffectCombinationSummary(LevelEffectSummary summary)
        {
            Dictionary<BlockEffectType, int> blockEffectCounters = new Dictionary<BlockEffectType, int>();
            for (int i = 0; i < summary.BlocksWithEffects.Count; i++)
                CountBlockEffects(summary.BlocksWithEffects[i].Effects, blockEffectCounters);
            for (int i = 0; i < summary.GeneratorBlocks.Count; i++)
                CountBlockEffects(summary.GeneratorBlocks[i].Effects, blockEffectCounters);

            if (blockEffectCounters.Count > 0)
            {
                EditorGUILayout.LabelField("Block Effect Summary", EditorStyles.boldLabel);
                foreach (KeyValuePair<BlockEffectType, int> pair in blockEffectCounters)
                    EditorGUILayout.LabelField($"- {pair.Key}: {pair.Value}", EditorStyles.miniLabel);
            }
        }

        private void DrawGateEffectsSection(LevelEffectSummary summary)
        {
            if (summary.GatesWithEffects.Count == 0)
                return;

            EditorGUILayout.LabelField("Gate Effects", subSectionStyle);
            for (int i = 0; i < summary.GatesWithEffects.Count; i++)
            {
                GateWithEffects gate = summary.GatesWithEffects[i];
                EditorGUILayout.BeginVertical(EditorStyles.textArea);
                EditorGUILayout.LabelField($"Gate #{gate.BlockId}", EditorStyles.boldLabel);
                DrawGateEffectsList(gate.Effects);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawGeneratorSection(LevelEffectSummary summary)
        {
            if (summary.GeneratorCellCount == 0)
                return;

            EditorGUILayout.LabelField("Generator", subSectionStyle);
            EditorGUILayout.LabelField($"Generator cells: {summary.GeneratorCellCount}", EditorStyles.miniLabel);

            if (summary.GeneratorBlocks.Count == 0)
            {
                EditorGUILayout.LabelField("No generator entries with block effects.", EditorStyles.miniLabel);
                return;
            }

            for (int i = 0; i < summary.GeneratorBlocks.Count; i++)
            {
                GeneratorEntry entry = summary.GeneratorBlocks[i];
                EditorGUILayout.BeginVertical(EditorStyles.textArea);
                EditorGUILayout.LabelField(
                    $"Queue Block #{entry.BlockId} - {entry.BlockColor}/{entry.BlockType}",
                    EditorStyles.boldLabel);
                DrawBlockEffectsList(entry.Effects);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawInteractableSection(LevelEffectSummary summary)
        {
            if (summary.InteractableTypeCounts.Count == 0)
                return;

            EditorGUILayout.LabelField("Interactable Objects", subSectionStyle);
            foreach (KeyValuePair<InteractableObjectType, int> pair in summary.InteractableTypeCounts)
                EditorGUILayout.LabelField($"- {pair.Key}: {pair.Value}", EditorStyles.miniLabel);
        }

        private void DrawSearchTab()
        {
            EditorGUILayout.LabelField("Effect Search", titleStyle);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            searchCategory = (SearchCategory)EditorGUILayout.EnumPopup("Category", searchCategory);
            switch (searchCategory)
            {
                case SearchCategory.BlockEffect:
                    searchBlockEffect = (BlockEffectType)EditorGUILayout.EnumPopup("Effect", searchBlockEffect);
                    break;
                case SearchCategory.BlockEffectCombo:
                    searchBlockEffect = (BlockEffectType)EditorGUILayout.EnumPopup("Effect A", searchBlockEffect);
                    searchSecondaryBlockEffect = (BlockEffectType)EditorGUILayout.EnumPopup("Effect B", searchSecondaryBlockEffect);
                    break;
                case SearchCategory.GateEffect:
                    searchGateEffect = (GateEffectType)EditorGUILayout.EnumPopup("Effect", searchGateEffect);
                    break;
                case SearchCategory.InteractableObject:
                    searchInteractableType = (InteractableObjectType)EditorGUILayout.EnumPopup("Type", searchInteractableType);
                    break;
            }

            bool comboSelectionValid =
                searchCategory != SearchCategory.BlockEffectCombo || searchBlockEffect != searchSecondaryBlockEffect;

            if (!comboSelectionValid)
                EditorGUILayout.HelpBox("Effect A and Effect B must be different.", MessageType.Warning);

            EditorGUI.BeginDisabledGroup(!comboSelectionValid);
            if (GUILayout.Button("Search", GUILayout.Height(26f)))
                PerformSearch();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8f);

            searchScroll = EditorGUILayout.BeginScrollView(searchScroll);
            DrawSearchResults();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSearchResults()
        {
            if (!didRunSearch)
            {
                EditorGUILayout.HelpBox("Select category/effect then click Search.", MessageType.Info);
                return;
            }

            if (searchResults.Count == 0)
            {
                EditorGUILayout.HelpBox("No result found.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField(
                $"Found in {searchResults.Count} levels ({totalSearchOccurrences} total occurrences)",
                sectionStyle);

            if (searchCategory == SearchCategory.BlockEffectCombo)
            {
                EditorGUILayout.LabelField(
                    $"Combo: {searchBlockEffect} + {searchSecondaryBlockEffect}",
                    EditorStyles.miniBoldLabel);
            }

            for (int i = 0; i < searchResults.Count; i++)
            {
                SearchLevelResult result = searchResults[i];
                LevelEffectSummary summary = result.Summary;
                if (summary == null || summary.Level == null)
                    continue;

                int key = summary.Level.GetInstanceID();
                bool expanded = searchFoldouts.ContainsKey(key) && searchFoldouts[key];

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                bool nextExpanded = EditorGUILayout.Foldout(
                    expanded,
                    $"{summary.GetDisplayLabel()} - {result.TotalMatches} matches",
                    true);
                if (nextExpanded != expanded)
                    searchFoldouts[key] = nextExpanded;

                if (GUILayout.Button("Open", GUILayout.Width(52f)))
                    OpenLevelInLevelEditor(summary);

                if (GUILayout.Button("Ping", GUILayout.Width(52f)))
                {
                    Selection.activeObject = summary.Level;
                    EditorGUIUtility.PingObject(summary.Level);
                }
                EditorGUILayout.EndHorizontal();

                if (nextExpanded)
                {
                    for (int j = 0; j < result.Occurrences.Count; j++)
                    {
                        SearchOccurrence occurrence = result.Occurrences[j];
                        EditorGUILayout.BeginVertical(EditorStyles.textArea);
                        EditorGUILayout.LabelField($"{occurrence.ContainerLabel} (x{occurrence.MatchCount})", EditorStyles.boldLabel);

                        if (occurrence.CoEffects.Count > 0)
                            EditorGUILayout.LabelField("Co-effects: " + string.Join(", ", occurrence.CoEffects), EditorStyles.miniLabel);
                        else
                            EditorGUILayout.LabelField("Co-effects: none", EditorStyles.miniLabel);

                        EditorGUILayout.EndVertical();
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawCombosTab()
        {
            EditorGUILayout.LabelField("All Effect Combos", titleStyle);
            comboCategory = (ComboCategory)EditorGUILayout.EnumPopup("Combo Source", comboCategory);
            comboSearchQuery = EditorGUILayout.TextField("Search", comboSearchQuery);

            int visibleCount = 0;
            comboScroll = EditorGUILayout.BeginScrollView(comboScroll);
            for (int i = 0; i < comboEntries.Count; i++)
            {
                EffectComboEntry combo = comboEntries[i];
                if (!PassComboCategory(combo) || !PassComboSearch(combo))
                    continue;

                visibleCount++;
                DrawComboEntry(combo);
            }

            if (visibleCount == 0)
                EditorGUILayout.HelpBox("No combo found with current filter.", MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        private bool PassComboCategory(EffectComboEntry combo)
        {
            switch (comboCategory)
            {
                case ComboCategory.Block:
                    return combo.IsBlockCombo;
                case ComboCategory.Gate:
                    return !combo.IsBlockCombo;
                default:
                    return true;
            }
        }

        private bool PassComboSearch(EffectComboEntry combo)
        {
            if (string.IsNullOrEmpty(comboSearchQuery))
                return true;

            string query = comboSearchQuery.Trim();
            if (query.Length == 0)
                return true;

            if (combo.GetDisplayLabel().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            foreach (KeyValuePair<int, ComboLevelHit> pair in combo.LevelHits)
            {
                ComboLevelHit hit = pair.Value;
                if (hit == null || hit.Summary == null || hit.Summary.Level == null)
                    continue;

                if (hit.Summary.GetDisplayLabel().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private void DrawComboEntry(EffectComboEntry combo)
        {
            bool expanded = !comboFoldouts.ContainsKey(combo.Key) || comboFoldouts[combo.Key];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            bool nextExpanded = EditorGUILayout.Foldout(
                expanded,
                $"{combo.GetDisplayLabel()} | levels: {combo.LevelCount} | occurrences: {combo.OccurrenceCount}",
                true);
            if (nextExpanded != expanded)
                comboFoldouts[combo.Key] = nextExpanded;

            if (nextExpanded)
            {
                foreach (KeyValuePair<int, ComboLevelHit> pair in combo.LevelHits)
                {
                    ComboLevelHit hit = pair.Value;
                    if (hit == null || hit.Summary == null || hit.Summary.Level == null)
                        continue;

                    EditorGUILayout.BeginHorizontal(EditorStyles.textArea);
                    EditorGUILayout.LabelField(
                        $"{hit.Summary.GetDisplayLabel()} (x{hit.Count})",
                        EditorStyles.boldLabel);

                    if (GUILayout.Button("Open", GUILayout.Width(52f)))
                        OpenLevelInLevelEditor(hit.Summary);

                    if (GUILayout.Button("Ping", GUILayout.Width(52f)))
                    {
                        Selection.activeObject = hit.Summary.Level;
                        EditorGUIUtility.PingObject(hit.Summary.Level);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void PerformSearch()
        {
            didRunSearch = true;
            totalSearchOccurrences = 0;
            searchResults.Clear();

            AddSearchResults(levelSummaries);
        }

        private void AddSearchResults(List<LevelEffectSummary> summaries)
        {
            for (int i = 0; i < summaries.Count; i++)
            {
                LevelEffectSummary summary = summaries[i];
                if (summary == null || summary.Level == null)
                    continue;

                SearchLevelResult result = new SearchLevelResult { Summary = summary };
                FillSearchResult(summary, result);

                if (result.TotalMatches <= 0)
                    continue;

                searchResults.Add(result);
                totalSearchOccurrences += result.TotalMatches;
            }
        }

        private void FillSearchResult(LevelEffectSummary summary, SearchLevelResult result)
        {
            switch (searchCategory)
            {
                case SearchCategory.BlockEffect:
                    CollectBlockEffectMatches(summary, result);
                    break;
                case SearchCategory.BlockEffectCombo:
                    CollectBlockEffectComboMatches(summary, result);
                    break;
                case SearchCategory.GateEffect:
                    CollectGateEffectMatches(summary, result);
                    break;
                case SearchCategory.Generator:
                    CollectGeneratorMatches(summary, result);
                    break;
                case SearchCategory.InteractableObject:
                    CollectInteractableMatches(summary, result);
                    break;
            }
        }

        private void CollectBlockEffectMatches(LevelEffectSummary summary, SearchLevelResult result)
        {
            for (int i = 0; i < summary.BlocksWithEffects.Count; i++)
            {
                BlockWithEffects block = summary.BlocksWithEffects[i];
                AddBlockEffectOccurrence(
                    block.Effects,
                    $"Block #{block.BlockId} - {block.BlockColor}/{block.BlockType}",
                    result);
            }

            for (int i = 0; i < summary.GeneratorBlocks.Count; i++)
            {
                GeneratorEntry entry = summary.GeneratorBlocks[i];
                AddBlockEffectOccurrence(
                    entry.Effects,
                    $"Generator Queue #{entry.BlockId} - {entry.BlockColor}/{entry.BlockType}",
                    result);
            }
        }

        private void AddBlockEffectOccurrence(BlockEffectData[] effects, string containerLabel, SearchLevelResult result)
        {
            if (effects == null || effects.Length == 0)
                return;

            int matchCount = 0;
            List<string> coEffects = new List<string>();
            for (int i = 0; i < effects.Length; i++)
            {
                BlockEffectData effect = effects[i];
                if (effect == null)
                    continue;

                if (effect.Type == searchBlockEffect)
                {
                    matchCount++;
                    continue;
                }

                string label = FormatBlockEffect(effect);
                if (!coEffects.Contains(label))
                    coEffects.Add(label);
            }

            if (matchCount <= 0)
                return;

            SearchOccurrence occurrence = new SearchOccurrence
            {
                ContainerLabel = containerLabel,
                MatchCount = matchCount
            };

            occurrence.CoEffects.AddRange(coEffects);
            result.Occurrences.Add(occurrence);
            result.TotalMatches += matchCount;
        }

        private void CollectGateEffectMatches(LevelEffectSummary summary, SearchLevelResult result)
        {
            for (int i = 0; i < summary.GatesWithEffects.Count; i++)
            {
                GateWithEffects gate = summary.GatesWithEffects[i];
                if (gate.Effects == null || gate.Effects.Length == 0)
                    continue;

                int matchCount = 0;
                List<string> coEffects = new List<string>();
                for (int j = 0; j < gate.Effects.Length; j++)
                {
                    GateEffectData effect = gate.Effects[j];
                    if (effect == null)
                        continue;

                    if (effect.Type == searchGateEffect)
                    {
                        matchCount++;
                        continue;
                    }

                    string label = FormatGateEffect(effect);
                    if (!coEffects.Contains(label))
                        coEffects.Add(label);
                }

                if (matchCount <= 0)
                    continue;

                SearchOccurrence occurrence = new SearchOccurrence
                {
                    ContainerLabel = $"Gate #{gate.BlockId}",
                    MatchCount = matchCount
                };
                occurrence.CoEffects.AddRange(coEffects);
                result.Occurrences.Add(occurrence);
                result.TotalMatches += matchCount;
            }
        }

        private void CollectGeneratorMatches(LevelEffectSummary summary, SearchLevelResult result)
        {
            if (summary.GeneratorCellCount <= 0)
                return;

            SearchOccurrence occurrence = new SearchOccurrence
            {
                ContainerLabel = "Generator Cells",
                MatchCount = summary.GeneratorCellCount
            };

            result.Occurrences.Add(occurrence);
            result.TotalMatches += summary.GeneratorCellCount;
        }

        private void CollectInteractableMatches(LevelEffectSummary summary, SearchLevelResult result)
        {
            int count;
            if (!summary.InteractableTypeCounts.TryGetValue(searchInteractableType, out count) || count <= 0)
                return;

            SearchOccurrence occurrence = new SearchOccurrence
            {
                ContainerLabel = $"Interactable {searchInteractableType}",
                MatchCount = count
            };

            result.Occurrences.Add(occurrence);
            result.TotalMatches += count;
        }

        private void CollectBlockEffectComboMatches(LevelEffectSummary summary, SearchLevelResult result)
        {
            if (searchBlockEffect == searchSecondaryBlockEffect)
                return;

            for (int i = 0; i < summary.BlocksWithEffects.Count; i++)
            {
                BlockWithEffects block = summary.BlocksWithEffects[i];
                AddBlockEffectComboOccurrence(
                    block.Effects,
                    $"Block #{block.BlockId} - {block.BlockColor}/{block.BlockType}",
                    result);
            }

            for (int i = 0; i < summary.GeneratorBlocks.Count; i++)
            {
                GeneratorEntry entry = summary.GeneratorBlocks[i];
                AddBlockEffectComboOccurrence(
                    entry.Effects,
                    $"Generator Queue #{entry.BlockId} - {entry.BlockColor}/{entry.BlockType}",
                    result);
            }
        }

        private void AddBlockEffectComboOccurrence(BlockEffectData[] effects, string containerLabel, SearchLevelResult result)
        {
            if (effects == null || effects.Length == 0)
                return;

            int firstCount = 0;
            int secondCount = 0;
            List<string> coEffects = new List<string>();

            for (int i = 0; i < effects.Length; i++)
            {
                BlockEffectData effect = effects[i];
                if (effect == null)
                    continue;

                if (effect.Type == searchBlockEffect)
                {
                    firstCount++;
                    continue;
                }

                if (effect.Type == searchSecondaryBlockEffect)
                {
                    secondCount++;
                    continue;
                }

                string formatted = FormatBlockEffect(effect);
                if (!coEffects.Contains(formatted))
                    coEffects.Add(formatted);
            }

            if (firstCount <= 0 || secondCount <= 0)
                return;

            int matchCount = Mathf.Min(firstCount, secondCount);
            SearchOccurrence occurrence = new SearchOccurrence
            {
                ContainerLabel = containerLabel,
                MatchCount = matchCount
            };

            occurrence.CoEffects.AddRange(coEffects);
            result.Occurrences.Add(occurrence);
            result.TotalMatches += matchCount;
        }

        private void BuildCache()
        {
            levelSummaries.Clear();
            overviewFoldouts.Clear();
            searchFoldouts.Clear();
            comboFoldouts.Clear();
            searchResults.Clear();
            comboEntries.Clear();
            comboEntriesByKey.Clear();
            didRunSearch = false;
            totalSearchOccurrences = 0;

            if (levelDatabase == null)
                return;

            LevelData[] levels = GetMainLevels(levelDatabase);
            for (int i = 0; i < levels.Length; i++)
            {
                LevelData level = levels[i];
                if (level == null)
                    continue;

                levelSummaries.Add(BuildSummary(level, i));
            }


            RebuildComboEntries();
        }

        private void RebuildComboEntries()
        {
            BuildCombosForList(levelSummaries);

            comboEntries.Sort((left, right) =>
            {
                int occurrenceCompare = right.OccurrenceCount.CompareTo(left.OccurrenceCount);
                if (occurrenceCompare != 0)
                    return occurrenceCompare;
                return string.Compare(left.GetDisplayLabel(), right.GetDisplayLabel(), StringComparison.Ordinal);
            });
        }

        private void BuildCombosForList(List<LevelEffectSummary> summaries)
        {
            for (int i = 0; i < summaries.Count; i++)
            {
                LevelEffectSummary summary = summaries[i];
                if (summary == null || summary.Level == null)
                    continue;

                for (int blockIndex = 0; blockIndex < summary.BlocksWithEffects.Count; blockIndex++)
                    RegisterBlockCombos(summary, summary.BlocksWithEffects[blockIndex].Effects);

                for (int generatorIndex = 0; generatorIndex < summary.GeneratorBlocks.Count; generatorIndex++)
                    RegisterBlockCombos(summary, summary.GeneratorBlocks[generatorIndex].Effects);

                for (int gateIndex = 0; gateIndex < summary.GatesWithEffects.Count; gateIndex++)
                    RegisterGateCombos(summary, summary.GatesWithEffects[gateIndex].Effects);
            }
        }

        private void RegisterBlockCombos(LevelEffectSummary summary, BlockEffectData[] effects)
        {
            if (effects == null || effects.Length < 2)
                return;

            HashSet<BlockEffectType> unique = new HashSet<BlockEffectType>();
            for (int i = 0; i < effects.Length; i++)
            {
                BlockEffectData effect = effects[i];
                if (effect != null)
                    unique.Add(effect.Type);
            }

            if (unique.Count < 2)
                return;

            List<int> orderedIds = new List<int>(unique.Count);
            foreach (BlockEffectType type in unique)
                orderedIds.Add((int)type);
            orderedIds.Sort();

            string key = BuildComboKey("B:", orderedIds);
            string label = BuildBlockComboLabel(orderedIds);
            EffectComboEntry entry = GetOrCreateComboEntry(key, true, label);
            RegisterComboHit(entry, summary);
        }

        private void RegisterGateCombos(LevelEffectSummary summary, GateEffectData[] effects)
        {
            if (effects == null || effects.Length < 2)
                return;

            HashSet<GateEffectType> unique = new HashSet<GateEffectType>();
            for (int i = 0; i < effects.Length; i++)
            {
                GateEffectData effect = effects[i];
                if (effect != null)
                    unique.Add(effect.Type);
            }

            if (unique.Count < 2)
                return;

            List<int> orderedIds = new List<int>(unique.Count);
            foreach (GateEffectType type in unique)
                orderedIds.Add((int)type);
            orderedIds.Sort();

            string key = BuildComboKey("G:", orderedIds);
            string label = BuildGateComboLabel(orderedIds);
            EffectComboEntry entry = GetOrCreateComboEntry(key, false, label);
            RegisterComboHit(entry, summary);
        }

        private static string BuildComboKey(string prefix, List<int> orderedIds)
        {
            return prefix + string.Join("+", orderedIds);
        }

        private static string BuildBlockComboLabel(List<int> orderedIds)
        {
            string[] labels = new string[orderedIds.Count];
            for (int i = 0; i < orderedIds.Count; i++)
                labels[i] = ((BlockEffectType)orderedIds[i]).ToString();
            return string.Join(" + ", labels);
        }

        private static string BuildGateComboLabel(List<int> orderedIds)
        {
            string[] labels = new string[orderedIds.Count];
            for (int i = 0; i < orderedIds.Count; i++)
                labels[i] = ((GateEffectType)orderedIds[i]).ToString();
            return string.Join(" + ", labels);
        }

        private EffectComboEntry GetOrCreateComboEntry(string key, bool isBlockCombo, string comboLabel)
        {
            EffectComboEntry entry;
            if (comboEntriesByKey.TryGetValue(key, out entry))
                return entry;

            entry = new EffectComboEntry
            {
                Key = key,
                IsBlockCombo = isBlockCombo,
                ComboLabel = comboLabel
            };

            comboEntriesByKey[key] = entry;
            comboEntries.Add(entry);
            return entry;
        }

        private static void RegisterComboHit(EffectComboEntry entry, LevelEffectSummary summary)
        {
            if (entry == null || summary == null || summary.Level == null)
                return;

            entry.OccurrenceCount++;
            int levelId = summary.Level.GetInstanceID();

            ComboLevelHit levelHit;
            if (!entry.LevelHits.TryGetValue(levelId, out levelHit))
            {
                levelHit = new ComboLevelHit
                {
                    Summary = summary,
                    Count = 0
                };
                entry.LevelHits[levelId] = levelHit;
            }

            levelHit.Count++;
        }

        private static LevelData[] GetMainLevels(LevelDatabase database)
        {
            if (database == null)
                return Array.Empty<LevelData>();

            FieldInfo levelsField = typeof(LevelDatabase).GetField(LevelsFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (levelsField == null)
                return Array.Empty<LevelData>();

            LevelData[] levels = levelsField.GetValue(database) as LevelData[];
            return levels ?? Array.Empty<LevelData>();
        }

        private static LevelEffectSummary BuildSummary(LevelData level, int levelIndex)
        {
            LevelEffectSummary summary = new LevelEffectSummary
            {
                Level = level,
                LevelIndex = levelIndex
            };

            LevelElementData[] elements = level.Elements;
            if (elements == null)
                return summary;

            for (int i = 0; i < elements.Length; i++)
            {
                LevelElementData element = elements[i];
                if (element == null)
                    continue;

                switch (element)
                {
                    case BlockLevelElementData block:
                        if (HasAnyEffect(block.BlockEffects))
                        {
                            summary.BlocksWithEffects.Add(new BlockWithEffects
                            {
                                BlockId = block.BlockId,
                                BlockType = block.BlockType,
                                BlockColor = block.BlockColor,
                                Effects = block.BlockEffects
                            });
                        }
                        break;

                    case GateLevelElementData gate:
                        if (HasAnyEffect(gate.GateEffects))
                        {
                            summary.GatesWithEffects.Add(new GateWithEffects
                            {
                                BlockId = gate.BlockId,
                                Effects = gate.GateEffects
                            });
                        }
                        break;

                    case GeneratorLevelElementData generator:
                        summary.GeneratorCellCount++;
                        if (generator.GeneratorQueue == null)
                            break;

                        for (int queueIndex = 0; queueIndex < generator.GeneratorQueue.Count; queueIndex++)
                        {
                            GeneratorBlockEntry queueEntry = generator.GeneratorQueue[queueIndex];
                            if (queueEntry == null || !HasAnyEffect(queueEntry.BlockEffects))
                                continue;

                            summary.GeneratorBlocks.Add(new GeneratorEntry
                            {
                                BlockId = queueEntry.BlockId,
                                BlockType = queueEntry.BlockType,
                                BlockColor = queueEntry.BlockColor,
                                Effects = queueEntry.BlockEffects
                            });
                        }
                        break;

                    case InteractableObjectLevelElementData interactable:
                        InteractableObjectData data = interactable.InteractableObjectData;
                        if (data == null || data.Type == InteractableObjectType.None)
                            break;

                        if (!summary.InteractableTypeCounts.ContainsKey(data.Type))
                            summary.InteractableTypeCounts[data.Type] = 0;
                        summary.InteractableTypeCounts[data.Type]++;
                        break;
                }
            }

            return summary;
        }

        private static bool HasAnyEffect(BlockEffectData[] effects)
        {
            if (effects == null || effects.Length == 0)
                return false;

            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null)
                    return true;
            }

            return false;
        }

        private static bool HasAnyEffect(GateEffectData[] effects)
        {
            if (effects == null || effects.Length == 0)
                return false;

            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null)
                    return true;
            }

            return false;
        }

        private static void DrawBlockEffectsList(BlockEffectData[] effects)
        {
            if (effects == null || effects.Length == 0)
            {
                EditorGUILayout.LabelField("- None", EditorStyles.miniLabel);
                return;
            }

            for (int i = 0; i < effects.Length; i++)
            {
                BlockEffectData effect = effects[i];
                if (effect == null)
                    continue;

                EditorGUILayout.LabelField("- " + FormatBlockEffect(effect), EditorStyles.miniLabel);
            }
        }

        private static void DrawGateEffectsList(GateEffectData[] effects)
        {
            if (effects == null || effects.Length == 0)
            {
                EditorGUILayout.LabelField("- None", EditorStyles.miniLabel);
                return;
            }

            for (int i = 0; i < effects.Length; i++)
            {
                GateEffectData effect = effects[i];
                if (effect == null)
                    continue;

                EditorGUILayout.LabelField("- " + FormatGateEffect(effect), EditorStyles.miniLabel);
            }
        }

        private static void CountBlockEffects(BlockEffectData[] effects, Dictionary<BlockEffectType, int> counters)
        {
            if (effects == null)
                return;

            for (int i = 0; i < effects.Length; i++)
            {
                BlockEffectData effect = effects[i];
                if (effect == null)
                    continue;

                if (!counters.ContainsKey(effect.Type))
                    counters[effect.Type] = 0;
                counters[effect.Type]++;
            }
        }

        private static string FormatBlockEffect(BlockEffectData effect)
        {
            switch (effect)
            {
                case IceBlockEffectData ice:
                    return $"Ice (turns: {ice.iceTurnsAmount})";
                case HiddenBlockEffectData:
                    return "Hidden";
                case BombBlockEffectData bomb:
                    return $"Bomb (duration: {bomb.bombDuration})";
                case LayeredBlockEffectData layered:
                    return $"Layered (color: {layered.layeredBlockColor})";
                case FixedDirectionBlockEffectData direction:
                    return $"FixedDirection ({(direction.horizontalDirection ? "Horizontal" : "Vertical")})";
                case DualBlockEffectData dual:
                    return $"Dual (second color: {dual.secondDualColor})";
                case ShutterBlockEffectData shutter:
                    return $"Shutter ({(shutter.shutterIsOpen ? "Open" : "Closed")})";
                case ChainBlockEffectData chain:
                    return $"Chain (keys: {chain.keysAmount})";
                case KeyChainBlockEffectData:
                    return "KeyChain";
                case KeyColorBlockEffectData keyColor:
                    return $"KeyColor ({keyColor.keyColor})";
                case CombinesBlockEffectData combine:
                    return $"Combines (group: {combine.combineGroupID})";
                case RopesBlockEffectData ropes:
                    return $"Ropes ({FormatBlockColorArray(ropes.ropesColors)})";
                case ScissorBlockEffectData scissor:
                    return $"Scissor ({scissor.scissorColor})";
                case TntBlockEffectData tnt:
                    return $"Tnt (turn: {tnt.tntTurn})";
                case BlockedBlockEffectData:
                    return "Blocked";
                case TimeCapsuleBlockEffectData timeCapsule:
                    return $"TimeCapsule (bonus: {timeCapsule.timeBonus})";
                case ContainerBoxBlockEffectData box:
                    return $"ContainerBox (id: {box.containerBoxID}, clear: {box.clearCount})";
                case ContainerMoveBoxBlockEffectData moveBox:
                    return $"ContainerMoveBox (id: {moveBox.containerBoxID}, clear: {moveBox.clearCount})";
                case ContainerColorBoxBlockEffectData colorBox:
                    return $"ContainerColorBox (id: {colorBox.containerBoxID}, color: {colorBox.colorCount}, clear: {colorBox.clearCount})";
                default:
                    return effect.Type.ToString();
            }
        }

        private static string FormatBlockColorArray(BlockColor[] colors)
        {
            if (colors == null || colors.Length == 0)
                return "none";

            string[] colorStrings = new string[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                colorStrings[i] = colors[i].ToString();

            return string.Join(", ", colorStrings);
        }

        private static string FormatGateEffect(GateEffectData effect)
        {
            switch (effect)
            {
                case IceGateEffectData ice:
                    return $"IceGate (turns: {ice.iceTurnsAmount})";
                case ValveGateEffectData valve:
                    return $"Valve ({(valve.isValveOpened ? "Opened" : "Closed")})";
                case LockedColorGateEffectData locked:
                    return $"LockedColor ({locked.lockColor})";
                case MovingLockGateEffectData moving:
                    return $"MovingLock ({(moving.isClockwise ? "Clockwise" : "CounterClockwise")})";
                default:
                    return effect.Type.ToString();
            }
        }

        private static void OpenLevelInLevelEditor(LevelEffectSummary summary)
        {
            if (summary == null || summary.Level == null)
                return;

            EditorApplication.ExecuteMenuItem("Tools/Level Editor");
            LevelEditorBase editorWindow = LevelEditorBase.Instance;
            if (editorWindow == null)
            {
                Debug.LogWarning("Cannot open level in Level Editor window.");
                return;
            }

            // Preferred path: deferred jump that keeps the list/grid UI in sync.
            if (editorWindow is LevelEditorWindow levelEditor)
            {
                levelEditor.JumpToLevel(summary.Level, summary.LevelIndex);
                Selection.activeObject = summary.Level;
                EditorGUIUtility.PingObject(summary.Level);
                return;
            }

            // Fallback for any other LevelEditorBase implementation.
            editorWindow.OpenLevel(summary.Level, summary.LevelIndex);
            Selection.activeObject = summary.Level;
            EditorGUIUtility.PingObject(summary.Level);
        }

        private void TryFindLevelDatabase()
        {
            if (levelDatabase != null)
                return;

            string[] guids = AssetDatabase.FindAssets("t:LevelDatabase");
            if (guids == null || guids.Length == 0)
                return;

            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            levelDatabase = AssetDatabase.LoadAssetAtPath<LevelDatabase>(assetPath);
        }

        private void EnsureStyles()
        {
            if (stylesInitialized)
                return;

            titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13
            };

            sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };

            subSectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11
            };

            stylesInitialized = true;
        }
    }
}
