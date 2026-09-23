using System.Collections.Generic;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Adjacent blocks that declare a matching <see cref="BreakableLinkEntry"/>
    /// ({linkId, count}) link into one rigid group and move together. EACH link has its
    /// own counter that ticks down once per board clear and breaks (splitting the group) when it reaches 0.
    /// A block holding any alive link cannot be filled at a gate (<see cref="AllowGateEntered"/>), but it
    /// stays clickable so the whole group can still be dragged.
    ///
    /// State lives on the EDGE, not the group: a single <see cref="LinkEdge"/> is referenced by both
    /// endpoint behaviors so each link decrements exactly once (only its owner ticks), shows exactly one
    /// visual, and both endpoints report fill-locked until that edge breaks.
    /// </summary>
    public sealed class BreakableLinkEffectBehavior : BlockEffectBehavior<BreakableLinkBlockEffectData>
    {
        public sealed class LinkEdge
        {
            public BreakableLinkEffectBehavior Owner;   // ticks the counter + owns the visual (lower index endpoint)
            public BreakableLinkEffectBehavior Other;
            public int LinkId;
            public int RemainingCount;
            public BreakableLinkVisuals Visual;
            public ConnectedBlocks Connection;

            public bool IsAlive => RemainingCount > 0;
        }

        private const float PUNCH_SCALE = 0.3f;
        private const float PUNCH_DURATION = 0.3f;

        private static readonly Vector2Int[] DIRECTIONS = { new(0, 1), new(0, -1), new(1, 0), new(-1, 0) };
        private static int s_nextGroupId;

        [SerializeField] GameObject visualPrefab;

        private readonly List<LinkEdge> links = new();
        private readonly Dictionary<int, int> countByLinkId = new();
        private readonly List<BreakableLinkEntry> dedupedEntries = new();

        private GameObject groupObject;
        private List<BreakableLinkEffectBehavior> componentEffects;
        private List<LevelBlockBehavior> componentBlocks;
        private Transform rootTransform;

        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            BuildDedupedEntries();
        }

        // First-wins de-dup on linkId, so a duplicate id later in the list is ignored when matching.
        private void BuildDedupedEntries()
        {
            dedupedEntries.Clear();
            countByLinkId.Clear();
            if (Data.links == null) return;

            foreach (BreakableLinkEntry entry in Data.links)
            {
                if (entry == null || countByLinkId.ContainsKey(entry.linkId))
                    continue;
                countByLinkId.Add(entry.linkId, entry.count);
                dedupedEntries.Add(entry);
            }
        }

        public override BlockGateState AllowGateEntered()
        {
            return HasAnyAliveLink() ? BlockGateState.Blocked : BlockGateState.Enterable;
        }

        public override bool MoveMultiplyObjects()
        {
            return groupObject && componentEffects is { Count: > 1 };
        }

        public override IReadOnlyList<LevelBlockBehavior> GetLinkedBlocks()
        {
            return componentBlocks ?? base.GetLinkedBlocks();
        }

        public override void OnBlockFullFilledAfterAnimationGlobal(LevelBlockBehavior levelBlockBehavior, BlockColor filledColor)
        {
            if (!gameObject.activeInHierarchy) return;

            List<LinkEdge> broken = null;
            for (int i = 0; i < links.Count; i++)
            {
                LinkEdge edge = links[i];
                // Only the owner ticks each edge → exactly one decrement per edge per clear.
                if (edge.Owner != this || !edge.IsAlive) continue;

                edge.RemainingCount--;
                if (edge.RemainingCount > 0)
                {
                    edge.Visual?.SetCount(edge.RemainingCount);
                    edge.Visual?.PlayPunch(PUNCH_SCALE, PUNCH_DURATION);
                }
                else
                {
                    (broken ??= new List<LinkEdge>()).Add(edge);
                }
            }

            if (broken == null) return;

            List<BreakableLinkEffectBehavior> affected = SnapshotComponent();
            foreach (LinkEdge edge in broken)
            {
                edge.Visual?.Hide(true);
                edge.Visual = null;
                RemoveEdge(edge);
            }

            RebuildAfterTopologyChange(affected, rootTransform);
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            if (links.Count == 0) return;

            List<BreakableLinkEffectBehavior> affected = SnapshotComponent();
            if (!affected.Contains(this)) affected.Add(this);

            // Remove every edge touching this block (no break FX — this is a disable, not a 0-counter break).
            var myEdges = new List<LinkEdge>(links);
            foreach (LinkEdge edge in myEdges)
            {
                edge.Visual?.Hide(false);
                edge.Visual = null;
                edge.RemainingCount = 0;
                RemoveEdge(edge);
            }

            RebuildAfterTopologyChange(affected, rootTransform);
        }

        public override void OnBlockFullAfterAnimationFilled()
        {
            // A linked block is fill-locked, so this normally runs only once all its links have broken.
            DisableEffect();
        }

        public override void OnBlockSplit()
        {
            if (componentEffects.IsNullOrEmpty()) return;
            var snapshot = new List<BreakableLinkEffectBehavior>(componentEffects);
            foreach (BreakableLinkEffectBehavior effect in snapshot)
            {
                if (effect)
                    effect.DisableEffect();
            }
        }

        private bool HasAnyAliveLink()
        {
            for (int i = 0; i < links.Count; i++)
            {
                if (links[i].IsAlive)
                    return true;
            }

            return false;
        }

        private List<BreakableLinkEffectBehavior> SnapshotComponent()
        {
            return componentEffects != null
                ? new List<BreakableLinkEffectBehavior>(componentEffects)
                : new List<BreakableLinkEffectBehavior> { this };
        }

        private static void RemoveEdge(LinkEdge edge)
        {
            if (edge.Owner) edge.Owner.links.Remove(edge);
            if (edge.Other) edge.Other.links.Remove(edge);
        }

        private void SpawnVisualFor(LinkEdge edge)
        {
            if (!visualPrefab) return;

            ConnectedBlocks conn = edge.Connection;
            Vector3 spawnPosition = (conn.PositionA + conn.PositionB) / 2f;

            GameObject visualObject = Instantiate(visualPrefab, spawnPosition, Quaternion.identity);
            visualObject.transform.SetParent(linkedBlock.ModelParentTransform);

            var visual = visualObject.GetComponent<BreakableLinkVisuals>();
            visual.Init(conn, edge.RemainingCount);
            edge.Visual = visual;
        }

        // --- Static link/group formation ------------------------------------------------------------

        /// <summary>
        /// Builds links between adjacent blocks that share a matching {linkId, count}, spawns one visual
        /// per pair, and forms one rigid group per connected component. Called from
        /// <see cref="LevelRepresentation.FormBreakableLinkGroupsForBlocks"/> at spawn (and on release).
        /// </summary>
        public static void FormLinks(List<LevelBlockBehavior> blocks, Transform root)
        {
            if (blocks.IsNullOrEmpty()) return;

            var effects = new List<BreakableLinkEffectBehavior>(blocks.Count);
            foreach (LevelBlockBehavior block in blocks)
            {
                var effect = block.GetEffect<BreakableLinkEffectBehavior>(BlockEffectType.BreakableLink);
                if (effect && effect.IsActive)
                {
                    effect.rootTransform = root;
                    effects.Add(effect);
                }
            }

            for (int i = 0; i < effects.Count; i++)
            {
                BreakableLinkEffectBehavior owner = effects[i];
                for (int j = i + 1; j < effects.Count; j++)
                {
                    BreakableLinkEffectBehavior other = effects[j];
                    if (!TryMatchLink(owner, other, out int linkId, out int count, out Vector2Int cellA, out Vector2Int cellB))
                        continue;

                    var edge = new LinkEdge
                    {
                        Owner = owner,
                        Other = other,
                        LinkId = linkId,
                        RemainingCount = count,
                        Connection = new ConnectedBlocks(owner.linkedBlock, other.linkedBlock, cellA, cellB),
                    };
                    owner.links.Add(edge);
                    other.links.Add(edge);
                    owner.SpawnVisualFor(edge);
                }
            }

            RebuildAfterTopologyChange(effects, root);
        }

        // A pair links iff they share a matching {linkId,count} AND are adjacent. One link per pair.
        private static bool TryMatchLink(BreakableLinkEffectBehavior a, BreakableLinkEffectBehavior b,
            out int linkId, out int count, out Vector2Int cellA, out Vector2Int cellB)
        {
            linkId = 0;
            count = 0;
            cellA = default;
            cellB = default;

            if (!TryGetMatchingEntry(a, b, out linkId, out count))
                return false;

            return TryGetFirstAdjacentCells(a.linkedBlock, b.linkedBlock, out cellA, out cellB);
        }

        private static bool TryGetMatchingEntry(BreakableLinkEffectBehavior a, BreakableLinkEffectBehavior b,
            out int linkId, out int count)
        {
            foreach (BreakableLinkEntry entry in a.dedupedEntries)
            {
                if (b.countByLinkId.TryGetValue(entry.linkId, out int bCount) && bCount == entry.count)
                {
                    linkId = entry.linkId;
                    count = entry.count;
                    return true;
                }
            }

            linkId = 0;
            count = 0;
            return false;
        }

        private static bool TryGetFirstAdjacentCells(LevelBlockBehavior blockA, LevelBlockBehavior blockB,
            out Vector2Int cellA, out Vector2Int cellB)
        {
            Vector2Int[] cellsA = blockA.GetOccupiedCells();
            Vector2Int[] cellsB = blockB.GetOccupiedCells();
            var cellsBSet = new HashSet<Vector2Int>(cellsB);

            for (int i = 0; i < cellsA.Length; i++)
            {
                Vector2Int a = cellsA[i];
                for (int d = 0; d < DIRECTIONS.Length; d++)
                {
                    Vector2Int neighbor = a + DIRECTIONS[d];
                    if (cellsBSet.Contains(neighbor))
                    {
                        cellA = a;
                        cellB = neighbor;
                        return true;
                    }
                }
            }

            cellA = default;
            cellB = default;
            return false;
        }

        /// <summary>
        /// Detaches the affected blocks from their current group(s), then re-forms one rigid group per
        /// connected component (over ALIVE edges). Surviving link visuals are intentionally left untouched
        /// so their counter/FX state persists — only group/rigidbody/parent are rebuilt.
        /// </summary>
        private static void RebuildAfterTopologyChange(List<BreakableLinkEffectBehavior> affected, Transform root)
        {
            if (affected.IsNullOrEmpty()) return;

            // If a block in this group is being dragged right now (e.g. the link breaks mid-drag from a
            // board clear), settle the drag BEFORE we destroy the shared group rigidbody it is driving —
            // otherwise the movement manager keeps driving a freed/destroyed body and flings the blocks.
            ForceReleasePickedIfAffected(affected);

            var oldGroups = new HashSet<GameObject>();
            foreach (BreakableLinkEffectBehavior effect in affected)
            {
                if (effect && effect.groupObject)
                    oldGroups.Add(effect.groupObject);
            }

            foreach (BreakableLinkEffectBehavior effect in affected)
            {
                if (effect)
                    DetachFromGroup(effect, root);
            }

            foreach (GameObject group in oldGroups)
            {
                if (group)
                    Destroy(group);
            }

            List<List<BreakableLinkEffectBehavior>> components = BuildComponents(affected);
            foreach (List<BreakableLinkEffectBehavior> component in components)
            {
                if (component.Count >= 2)
                {
                    FormGroup(component, root);
                }
                else
                {
                    BreakableLinkEffectBehavior single = component[0];
                    single.groupObject = null;
                    single.componentEffects = component;
                    single.componentBlocks = new List<LevelBlockBehavior> { single.linkedBlock };
                }
            }
        }

        // Settle an in-flight drag whose
        // picked block belongs to the group about to be rebuilt, so the shared group rigidbody is never
        // destroyed out from under the movement manager.
        private static void ForceReleasePickedIfAffected(List<BreakableLinkEffectBehavior> affected)
        {
            if (!Application.isPlaying)
                return;

            LevelController levelController = LevelController.Instance;
            BlockMovementManager movementManager = levelController ? levelController.MovementManager : null;
            if (movementManager == null || !movementManager.IsBlockPicked)
                return;

            LevelBlockBehavior pickedBlock = movementManager.BlockBehavior;
            if (!pickedBlock)
                return;

            for (int i = 0; i < affected.Count; i++)
            {
                BreakableLinkEffectBehavior effect = affected[i];
                if (effect && effect.linkedBlock == pickedBlock)
                {
                    levelController.OnObjectReleased(snapToCurrentPosition: true);
                    return;
                }
            }
        }

        private static void DetachFromGroup(BreakableLinkEffectBehavior effect, Transform fallback)
        {
            LevelBlockBehavior block = effect.linkedBlock;
            if (!block) return;

            if (effect.groupObject)
            {
                Transform groupTransform = effect.groupObject.transform;
                if (block.transform.parent == groupTransform)
                {
                    Transform original = block.ConsumeParentBeforeGrouping();
                    if (!original) original = fallback;
                    block.transform.SetParent(original, true);
                }
            }

            block.RestoreIndividualRigidbody();
            effect.groupObject = null;
        }

        private static void FormGroup(List<BreakableLinkEffectBehavior> effects, Transform root)
        {
            GameObject groupGo = new GameObject($"BreakableLinkGroup_{s_nextGroupId++}");
            if (root)
                groupGo.transform.SetParent(root, true);

            foreach (BreakableLinkEffectBehavior effect in effects)
            {
                LevelBlockBehavior block = effect.linkedBlock;
                block.CacheParentBeforeGrouping();
                block.transform.SetParent(groupGo.transform, true);
            }

            Rigidbody groupRb = groupGo.AddComponent<Rigidbody>();
            Rigidbody sourceRb = null;
            foreach (BreakableLinkEffectBehavior effect in effects)
            {
                if (effect.linkedBlock && effect.linkedBlock.BlockRigidbody)
                {
                    sourceRb = effect.linkedBlock.BlockRigidbody;
                    break;
                }
            }
            LevelBlockBehavior.CopyRigidbodySettings(sourceRb, groupRb);

            var blocks = new List<LevelBlockBehavior>(effects.Count);
            foreach (BreakableLinkEffectBehavior effect in effects)
            {
                effect.linkedBlock.SetGroupRigidbody(groupRb);
                blocks.Add(effect.linkedBlock);
            }

            foreach (BreakableLinkEffectBehavior effect in effects)
            {
                effect.groupObject = groupGo;
                effect.componentEffects = effects;
                effect.componentBlocks = blocks;
            }
        }

        private static List<List<BreakableLinkEffectBehavior>> BuildComponents(List<BreakableLinkEffectBehavior> effects)
        {
            var components = new List<List<BreakableLinkEffectBehavior>>();
            var visited = new HashSet<BreakableLinkEffectBehavior>();

            foreach (BreakableLinkEffectBehavior start in effects)
            {
                if (!start || visited.Contains(start))
                    continue;

                var component = new List<BreakableLinkEffectBehavior>();
                var queue = new Queue<BreakableLinkEffectBehavior>();
                queue.Enqueue(start);
                visited.Add(start);

                while (queue.Count > 0)
                {
                    BreakableLinkEffectBehavior current = queue.Dequeue();
                    component.Add(current);

                    foreach (LinkEdge edge in current.links)
                    {
                        if (!edge.IsAlive)
                            continue;

                        BreakableLinkEffectBehavior neighbor = edge.Owner == current ? edge.Other : edge.Owner;
                        if (!neighbor || visited.Contains(neighbor) || !effects.Contains(neighbor))
                            continue;

                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }

                components.Add(component);
            }

            return components;
        }
    }
}
