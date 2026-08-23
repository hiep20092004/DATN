using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Stores precomputed, per-level lookup data that is built before effects are created.
    /// Gate/Block/Interactable systems can query this cache at runtime.
    /// </summary>
    public sealed class LevelRuntimePrecomputedCache
    {
        private List<int> gatePerimeterOrder;
        private readonly Dictionary<int, int> gatePerimeterIndexByBlockId = new Dictionary<int, int>();
        private readonly Dictionary<int, GateBehavior> gateByBlockId = new Dictionary<int, GateBehavior>();

        public string LastError { get; private set; }

        public void Build(LevelElementData[] levelElements)
        {
            LastError = null;
            gatePerimeterOrder = null;
            gatePerimeterIndexByBlockId.Clear();
            gateByBlockId.Clear();

            if (!GatePerimeterOrdering.TryComputeGateBlockIdOrder(levelElements, out List<int> order, out string err))
            {
                LastError = err;
                return;
            }

            gatePerimeterOrder = order;
            for (int i = 0; i < gatePerimeterOrder.Count; i++)
            {
                int blockId = gatePerimeterOrder[i];
                gatePerimeterIndexByBlockId.TryAdd(blockId, i);
            }
        }

        public void BindGates(IReadOnlyList<GateBehavior> gates)
        {
            gateByBlockId.Clear();
            if (gates == null)
                return;

            for (int i = 0; i < gates.Count; i++)
            {
                GateBehavior gate = gates[i];
                if (!gate)
                    continue;

                int gateBlockId = gate.Data?.LevelElementData?.BlockId ?? 0;
                if (gateBlockId == 0)
                    continue;

                gateByBlockId.TryAdd(gateBlockId, gate);
            }
        }

        public bool TryGetNextGateInPerimeterOrder(int currentGateBlockId, bool isClockwise, out GateBehavior nextGate)
        {
            nextGate = null;

            if (gatePerimeterOrder == null || gatePerimeterOrder.Count == 0)
                return false;

            if (gatePerimeterOrder.Count == 1)
            {
                int onlyId = gatePerimeterOrder[0];
                return gateByBlockId.TryGetValue(onlyId, out nextGate);
            }

            if (!gatePerimeterIndexByBlockId.TryGetValue(currentGateBlockId, out int idx))
                return false;

            int delta = isClockwise ? 1 : -1;
            int count = gatePerimeterOrder.Count;
            int nextIdx = (idx + delta + count) % count;
            int nextId = gatePerimeterOrder[nextIdx];

            return gateByBlockId.TryGetValue(nextId, out nextGate);
        }
    }
}

