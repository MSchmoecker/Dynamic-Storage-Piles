using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;

namespace DynamicStoragePiles {
    /// <summary>
    ///     Dynamic piles for vanilla stacks that the asset bundle does not cover. A vanilla stack is a single mesh,
    ///     but its destroy effect holds the single pieces (bars, logs, stones), slightly spread apart for the flying pieces.
    ///     Every piece becomes a fill level, the metal bars are stacked tightly again.
    /// </summary>
    public static class DestroyEffectStacks {
        // Share of the slight turn of every bar in the destroy effect that is kept, so the stack does not look machine-made
        private const float KeptTurn = 0.25f;

        private class Entry {
            public readonly string vanillaStack;
            public readonly string item;
            public readonly int cost;
            public readonly bool stackTightly;

            public Entry(string vanillaStack, string item, int cost, bool stackTightly) {
                this.vanillaStack = vanillaStack;
                this.item = item;
                this.cost = cost;
                this.stackTightly = stackTightly;
            }
        }

        // In the order of the vanilla stacks in the hammer, which is also the order in the build menu
        private static readonly List<Entry> entries = new List<Entry> {
            new Entry("wood_frost_stack", "Frostwood", 10, false),
            new Entry("flint_pile", "Flint", 10, false),
            new Entry("bar_tin_stack", "Tin", 3, true),
            new Entry("bar_copper_stack", "Copper", 3, true),
            new Entry("bar_bronze_stack", "Bronze", 3, true),
            new Entry("bar_iron_stack", "Iron", 3, true),
            new Entry("bar_silver_stack", "Silver", 3, true),
            new Entry("bar_blackmetal_stack", "BlackMetal", 3, true),
            new Entry("bar_flametal_stack", "FlametalNew", 3, true),
            new Entry("bar_gold_stack", "Gold", 3, true),
        };

        public static List<string> vanillaStacks = new List<string>();
        public static List<string> dynamicStacks = new List<string>();

        public static void Init() {
            PrefabManager.OnVanillaPrefabsAvailable += AddPieces;
        }

        private static string DynamicName(string vanillaStack) {
            return $"MS_container_{vanillaStack}";
        }

        private static void AddPieces() {
            PrefabManager.OnVanillaPrefabsAvailable -= AddPieces;

            foreach (Entry entry in entries) {
                // One failing stack must not take the piles after it along
                try {
                    AddPiece(entry);
                } catch (System.Exception e) {
                    Jotunn.Logger.LogWarning($"Could not build the dynamic pile for {entry.vanillaStack}, it falls back to the look of the vanilla stack: {e}");

                    try {
                        AddFallbackPiece(entry);
                    } catch (System.Exception fallbackException) {
                        Jotunn.Logger.LogWarning($"Could not add the fallback pile for {entry.vanillaStack}: {fallbackException}");
                    }
                }
            }
        }

        private static GameObject FindDestroyEffect(GameObject basePrefab) {
            WearNTear wearNTear = basePrefab.GetComponent<WearNTear>();
            EffectList.EffectData[] effects = wearNTear ? wearNTear.m_destroyedEffect.m_effectPrefabs : null;
            return effects?.Select(effect => effect.m_prefab).FirstOrDefault(prefab => prefab && prefab.GetComponentsInChildren<MeshRenderer>(true).Length > 0);
        }

        /// <summary>
        ///     Keeps placed piles and their content when the destroy effect can no longer be used, for example after a game update:
        ///     the pile becomes a plain container that looks like the vanilla stack, without fill levels.
        /// </summary>
        private static void AddFallbackPiece(Entry entry) {
            string vanillaStack = entry.vanillaStack;
            GameObject basePrefab = PrefabManager.Instance.GetPrefab(vanillaStack);

            if (!basePrefab) {
                Jotunn.Logger.LogWarning($"Could not find {vanillaStack}, its dynamic pile is not added");
                return;
            }

            // Only a registered piece counts, not a leftover clone
            if (PieceManager.Instance.GetPiece(DynamicName(vanillaStack)) != null) {
                return;
            }

            // A failed attempt may have left a clone under the real name that Jötunn still knows, so the fallback is cloned under its own name first
            GameObject stack = PrefabManager.Instance.CreateClonedPrefab($"{DynamicName(vanillaStack)}_fallback", basePrefab);
            if (!stack) {
                Jotunn.Logger.LogWarning($"Could not clone {vanillaStack} for its fallback pile");
                return;
            }

            stack.name = DynamicName(vanillaStack);

            string nameToken = basePrefab.GetComponent<Piece>().m_name;

            Container container = stack.AddComponent<Container>();
            container.m_name = nameToken;
            container.m_width = 5;
            container.m_height = 2;
            stack.AddComponent<VisualStack>();

            // The build menu tags of the vanilla stack were copied too; cleared, Jötunn 2.30.1 and later guesses them like for the other piles
            stack.GetComponent<Piece>().m_usage = 0;

            DynamicStoragePiles.Instance.AddCompatPiece(stack, entry.item, entry.cost, true);
            vanillaStacks.Add(vanillaStack);
            dynamicStacks.Add(DynamicName(vanillaStack));
        }

        private static void AddPiece(Entry entry) {
            string vanillaStack = entry.vanillaStack;
            GameObject basePrefab = PrefabManager.Instance.GetPrefab(vanillaStack);
            GameObject vfx = basePrefab ? FindDestroyEffect(basePrefab) : null;

            if (!basePrefab) {
                Jotunn.Logger.LogWarning($"Could not find {vanillaStack}, its dynamic pile is not added");
                return;
            }

            if (!vfx) {
                Jotunn.Logger.LogWarning($"Could not find the destroy effect of {vanillaStack}, its dynamic pile falls back to the look of the vanilla stack");
                AddFallbackPiece(entry);
                return;
            }

            GameObject stack = PrefabManager.Instance.CreateClonedPrefab(DynamicName(vanillaStack), vfx);

            // The dust of the destroy effect would puff up whenever the pile loads; nested systems are already gone with their parent and the root is the pile itself
            foreach (ParticleSystem particles in stack.GetComponentsInChildren<ParticleSystem>(true)) {
                if (particles && particles.gameObject != stack) {
                    Object.DestroyImmediate(particles.gameObject);
                }
            }

            // Physics and timers of the effect would move or remove the pieces of a static pile, joints go first as they need a Rigidbody
            foreach (Joint joint in stack.GetComponentsInChildren<Joint>(true)) {
                Object.DestroyImmediate(joint);
            }

            foreach (Rigidbody body in stack.GetComponentsInChildren<Rigidbody>(true)) {
                Object.DestroyImmediate(body);
            }

            foreach (TimedDestruction timer in stack.GetComponentsInChildren<TimedDestruction>(true)) {
                Object.DestroyImmediate(timer);
            }

            stack = PieceHelper.PreparePiecePrefab(stack, basePrefab, basePrefab.GetComponent<Piece>().m_name);

            // The build menu tags of the vanilla stack were copied too; cleared, Jötunn 2.30.1 and later guesses them like for the other piles
            stack.GetComponent<Piece>().m_usage = 0;

            // The copied WearNTear still points to the wear states of the vanilla stack and would switch them on damage
            WearNTear wearNTear = stack.GetComponent<WearNTear>();
            wearNTear.m_new = null;
            wearNTear.m_worn = null;
            wearNTear.m_broken = null;
            wearNTear.m_wet = null;
            wearNTear.m_fragmentRoots = null;

            // The single parts of the effect are not on the piece layer, so placing and hovering would not find them
            foreach (Transform child in stack.GetComponentsInChildren<Transform>(true)) {
                child.gameObject.layer = basePrefab.layer;
            }

            VisualStack visualStack = stack.GetComponent<VisualStack>();

            if (entry.stackTightly) {
                StackTightly(visualStack);
            } else {
                FitIntoVanillaStack(visualStack, basePrefab, stack.transform);

                // The pile fills from the bottom
                visualStack.stackMeshes = visualStack.stackMeshes.OrderBy(piece => Center(piece).y).ToList();
            }

            // Rendered like the other piles, so the pile can be told apart from the vanilla stack in the build menu
            DynamicStoragePiles.Instance.AddCompatPiece(stack, entry.item, entry.cost, true);

            // Listed only once added, so the recipe setting never hides a vanilla stack that has no dynamic pile
            vanillaStacks.Add(vanillaStack);
            dynamicStacks.Add(DynamicName(vanillaStack));
        }

        // The destroy effect starts slightly burst, so the pieces are pulled together to the size of the vanilla stack
        private static void FitIntoVanillaStack(VisualStack visualStack, GameObject basePrefab, Transform root) {
            WearNTear baseWear = basePrefab.GetComponent<WearNTear>();
            Transform vanillaRoot = baseWear && baseWear.m_new ? baseWear.m_new.transform : basePrefab.transform;
            Bounds vanilla = new Bounds();
            bool hasVanilla = false;

            foreach (MeshFilter filter in vanillaRoot.GetComponentsInChildren<MeshFilter>(true).Where(filter => filter.sharedMesh)) {
                Encapsulate(ref vanilla, ref hasVanilla, filter, basePrefab.transform);
            }

            Bounds effect = new Bounds();
            bool hasEffect = false;

            foreach (Transform piece in visualStack.stackMeshes) {
                Encapsulate(ref effect, ref hasEffect, piece.GetComponent<MeshFilter>(), root);
            }

            List<Vector3> centers = visualStack.stackMeshes.Select(Center).ToList();
            Vector3 min = centers.Aggregate(Vector3.Min);
            Vector3 max = centers.Aggregate(Vector3.Max);
            Vector3 span = max - min;
            Vector3 scale = Vector3.one;

            for (int axis = 0; axis < 3; axis++) {
                if (span[axis] > 0.0001f) {
                    scale[axis] = Mathf.Clamp((vanilla.size[axis] - (effect.size[axis] - span[axis])) / span[axis], 0.01f, 1f);
                }
            }

            // The lowest piece keeps its height above the ground, so it does not sink in
            Vector3 anchorEffect = new Vector3(effect.center.x, min.y, effect.center.z);
            Vector3 anchorVanilla = new Vector3(vanilla.center.x, vanilla.min.y + min.y - effect.min.y, vanilla.center.z);

            for (int i = 0; i < centers.Count; i++) {
                Vector3 target = anchorVanilla + Vector3.Scale(centers[i] - anchorEffect, scale);
                visualStack.stackMeshes[i].localPosition += target - centers[i];
            }
        }

        private static void Encapsulate(ref Bounds bounds, ref bool hasBounds, MeshFilter filter, Transform space) {
            Bounds local = filter.sharedMesh.bounds;

            for (int i = 0; i < 8; i++) {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 point = space.InverseTransformPoint(filter.transform.TransformPoint(corner));

                if (hasBounds) {
                    bounds.Encapsulate(point);
                } else {
                    bounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
            }
        }

        /// <summary>
        ///     Puts every layer of bars right onto the one below with its bars side by side, like the vanilla stack.
        ///     Every bar snaps to a quarter turn but keeps a bit of its turn, and the fill levels go bottom to top.
        /// </summary>
        private static void StackTightly(VisualStack visualStack) {
            List<Transform> bars = visualStack.stackMeshes.OrderBy(bar => Center(bar).y).ToList();
            Mesh mesh = bars[0].GetComponent<MeshFilter>().sharedMesh;
            Vector3 size = Vector3.Scale(mesh.bounds.size, bars[0].localScale);
            bool longAlongZ = size.z >= size.x;
            float width = Mathf.Min(size.x, size.z);
            List<Transform> stacked = new List<Transform>();
            int layer = 0;

            while (stacked.Count < bars.Count) {
                // A layer is every bar less than half a bar height above its lowest one
                float bottom = Center(bars[stacked.Count]).y;
                List<Transform> row = bars.Skip(stacked.Count).TakeWhile(bar => Center(bar).y - bottom < size.y / 2f).ToList();
                float turn = Mathf.Round(row[0].localEulerAngles.y / 90f) * 90f;
                bool sideBySideInX = (Mathf.RoundToInt(turn / 90f) % 2 == 0) == longAlongZ;
                row = row.OrderBy(bar => sideBySideInX ? Center(bar).x : Center(bar).z).ToList();

                for (int i = 0; i < row.Count; i++) {
                    float side = (i - (row.Count - 1) / 2f) * width;
                    float height = (layer + 0.5f) * size.y;
                    Vector3 target = sideBySideInX ? new Vector3(side, height, 0f) : new Vector3(0f, height, side);
                    float yaw = row[i].localEulerAngles.y;
                    float snapped = Mathf.Round(yaw / 90f) * 90f;
                    row[i].localRotation = Quaternion.Euler(0f, snapped + Mathf.DeltaAngle(snapped, yaw) * KeptTurn, 0f);
                    row[i].localPosition = target - row[i].localRotation * Vector3.Scale(mesh.bounds.center, row[i].localScale);
                }

                stacked.AddRange(row);
                layer++;
            }

            visualStack.stackMeshes = stacked;
        }

        private static Vector3 Center(Transform bar) {
            Mesh mesh = bar.GetComponent<MeshFilter>().sharedMesh;
            return bar.localPosition + bar.localRotation * Vector3.Scale(mesh.bounds.center, bar.localScale);
        }
    }
}
