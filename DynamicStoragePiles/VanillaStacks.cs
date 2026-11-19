using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;

namespace DynamicStoragePiles {
    /// <summary>
    ///     Dynamic piles built at runtime from vanilla assets, so they need no meshes or prefabs
    ///     in the asset bundle: either from a vanilla stack whose pieces are separate objects,
    ///     or by laying copies of an item's own model in a criss-cross pile.
    /// </summary>
    public static class VanillaStacks {
        public static void Init() {
            LocalizationManager.Instance.GetLocalization().AddTranslation("English", new Dictionary<string, string> {
                { "piece_charredbonestack", "Charred Bone Stack" },
            });

            PrefabManager.OnVanillaPrefabsAvailable += AddPiles;
        }

        private static void AddPiles() {
            PrefabManager.OnVanillaPrefabsAvailable -= AddPiles;

            AddPile("wood_frost_stack", "vfx_wood_frost_stack_destroyed", "MS_container_frostwood_stack");
            AddCrissCrossPile("MS_container_charredbone_stack", "CharredBone", "bone_stack", "$piece_charredbonestack", 8, 4, 1337);
        }

        /// <param name="vanillaStackName">Vanilla stack piece to copy the layout, name and material from.</param>
        /// <param name="vfxName">Its destroyed effect, which holds the stack's pieces as separate objects.</param>
        /// <param name="newPrefabName">Prefab name of the new pile. Saved in the world, so never change it.</param>
        private static void AddPile(string vanillaStackName, string vfxName, string newPrefabName) {
            GameObject vanillaStack = PrefabManager.Instance.GetPrefab(vanillaStackName);
            GameObject vfx = vanillaStack ? PieceHelper.FindDestroyVFX(vanillaStack, vfxName) : null;
            Piece vanillaPiece = vanillaStack ? vanillaStack.GetComponent<Piece>() : null;

            if (!vfx || !vanillaPiece || vanillaPiece.m_resources.Length == 0 || !vanillaPiece.m_resources[0].m_resItem) {
                Jotunn.Logger.LogWarning($"Could not find vanilla {vanillaStackName} / {vfxName}, skipping {newPrefabName}");
                return;
            }

            string craftItem = vanillaPiece.m_resources[0].m_resItem.name;

            GameObject stack = PrefabManager.Instance.CreateClonedPrefab(newPrefabName, vfx);
            stack.layer = vanillaStack.layer;

            // The destroyed effect's pieces sit lower than in the real stack (it's positioned for
            // the explosion), so move each one to where the matching piece is in the vanilla stack.
            Transform layout = vanillaStack.transform.Find("New/objects");
            foreach (Transform part in stack.transform) {
                Transform original = layout ? layout.Find(part.name) : null;

                if (!original) {
                    Jotunn.Logger.LogWarning($"{newPrefabName}: no '{part.name}' in {vanillaStackName}, keeping its effect position");
                    continue;
                }

                part.localPosition = vanillaStack.transform.InverseTransformPoint(original.position);
                part.localRotation = Quaternion.Inverse(vanillaStack.transform.rotation) * original.rotation;
            }

            stack = PieceHelper.PreparePiecePrefab(stack, vanillaStack, vanillaPiece.m_name);
            stack.name = newPrefabName;

            // The copied WearNTear points at the vanilla prefab's New/Worn/Broken objects. Left as is,
            // damaging this pile would toggle those on the vanilla prefab. Null disables the swap.
            WearNTear wearNTear = stack.GetComponent<WearNTear>();
            wearNTear.m_new = null;
            wearNTear.m_worn = null;
            wearNTear.m_broken = null;

            VisualStack visualStack = stack.GetComponent<VisualStack>();
            visualStack.stackMeshes = OrderByRows(visualStack.stackMeshes, 0.1f);

            DynamicStoragePiles.dynamicStacks.Add(newPrefabName);
            DynamicStoragePiles.Instance.AddCompatPiece(stack, craftItem, 10, true);
        }

        /// <summary>
        ///     A pile of copies of an item's own model, laid like the bone pile: <paramref name="layers"/>
        ///     layers of <paramref name="perLayer"/> pieces, each layer at right angles to the one below.
        ///     Sizes come from the item's model, so every layer rests exactly on the one below.
        /// </summary>
        /// <param name="newPrefabName">Prefab name of the new pile. Saved in the world, so never change it.</param>
        /// <param name="itemName">Item stored in the pile, whose dropped model is stacked.</param>
        /// <param name="basePieceName">Vanilla piece to copy the Piece / WearNTear settings from.</param>
        /// <param name="nameToken">Localization token for the pile's name.</param>
        /// <param name="seed">Seed for the small random turns and shifts that make it look hand-stacked.</param>
        private static void AddCrissCrossPile(string newPrefabName, string itemName, string basePieceName, string nameToken,
                                              int layers, int perLayer, int seed) {
            GameObject item = PrefabManager.Instance.GetPrefab(itemName);
            GameObject basePiece = PrefabManager.Instance.GetPrefab(basePieceName);
            MeshFilter itemMesh = item ? item.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.sharedMesh) : null;
            MeshRenderer itemRenderer = itemMesh ? itemMesh.GetComponent<MeshRenderer>() : null;

            if (!itemRenderer || !basePiece || !basePiece.GetComponent<Piece>() || !basePiece.GetComponent<WearNTear>()) {
                Jotunn.Logger.LogWarning($"Could not find {itemName}'s model or {basePieceName}, skipping {newPrefabName}");
                return;
            }

            Mesh mesh = itemMesh.sharedMesh;
            Vector3 scale = itemMesh.transform.lossyScale;   // the dropped item's size (CharredBone: 1.3)
            Vector3 size = Vector3.Scale(mesh.bounds.size, scale);

            // Lay the model with its longest side along X and its thinnest side up, whatever
            // axes the model itself uses.
            int longAxis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            int upAxis = Enumerable.Range(0, 3).Where(a => a != longAxis).OrderBy(a => size[a]).First();
            int sideAxis = 3 - longAxis - upAxis;
            Vector3 longDir = Vector3.zero, upDir = Vector3.zero;
            longDir[longAxis] = 1f;
            upDir[upAxis] = 1f;
            Quaternion modelToFlat = Quaternion.Inverse(Quaternion.LookRotation(Vector3.Cross(longDir, upDir), upDir));
            Vector3 modelCenter = Vector3.Scale(mesh.bounds.center, scale);

            float length = size[longAxis];
            float height = size[upAxis];
            float width = size[sideAxis];
            // Outer pieces of a layer sit under the ends of the layer above.
            float spread = Mathf.Max(0f, length - width);

            // CreateEmptyPrefab isn't empty: it's a cube (pink in Valheim) with a collider, and its
            // bool only controls the ZNetView. Left in, the cube becomes the pile's first "piece",
            // which VisualStack always shows while hiding every bone, and the empty pile has nothing
            // to aim at. Strip the cube so only the bones are left.
            GameObject stack = PrefabManager.Instance.CreateEmptyPrefab(newPrefabName, true);
            Object.DestroyImmediate(stack.GetComponent<Collider>());
            Object.DestroyImmediate(stack.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(stack.GetComponent<MeshFilter>());
            stack.layer = basePiece.layer;

            System.Random random = new System.Random(seed);
            float Jitter(float max) => (float)(random.NextDouble() * 2 - 1) * max;

            // Created bottom layer first, so this is also the fill order.
            for (int layer = 0; layer < layers; layer++) {
                Quaternion layerTurn = Quaternion.Euler(0f, layer % 2 == 0 ? 0f : 90f, 0f);

                for (int i = 0; i < perLayer; i++) {
                    float across = perLayer > 1 ? -spread / 2f + spread * i / (perLayer - 1) : 0f;
                    Vector3 center = layerTurn * new Vector3(Jitter(0.03f), height / 2f + layer * height, across);

                    // A small turn, and some pieces flipped end for end, so it isn't machine-perfect.
                    // Both turn around the vertical axis only, so the layer height never changes.
                    Quaternion turn = Quaternion.Euler(0f, Jitter(3f) + (random.Next(2) == 0 ? 0f : 180f), 0f);
                    Quaternion rotation = layerTurn * turn * modelToFlat;

                    GameObject piece = new GameObject($"{itemName}_{layer}_{i}");
                    piece.layer = basePiece.layer;
                    piece.transform.SetParent(stack.transform, false);
                    piece.transform.localScale = scale;
                    piece.transform.localRotation = rotation;
                    piece.transform.localPosition = center - rotation * modelCenter;
                    piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                    piece.AddComponent<MeshRenderer>().sharedMaterials = itemRenderer.sharedMaterials;
                }
            }

            stack = PieceHelper.PreparePiecePrefab(stack, basePiece, nameToken);
            stack.name = newPrefabName;
            stack.GetComponent<Piece>().m_name = nameToken;

            WearNTear wearNTear = stack.GetComponent<WearNTear>();
            wearNTear.m_new = null;
            wearNTear.m_worn = null;
            wearNTear.m_broken = null;
            // The base piece's break effect scatters its own pieces (white bones for bone_stack).
            // Keep its sounds and dust, drop the scattering part. New list: the old one is shared.
            wearNTear.m_destroyedEffect = new EffectList {
                m_effectPrefabs = wearNTear.m_destroyedEffect.m_effectPrefabs
                    .Where(e => !e.m_prefab || !e.m_prefab.GetComponent<Gibber>())
                    .ToArray(),
            };

            DynamicStoragePiles.dynamicStacks.Add(newPrefabName);
            DynamicStoragePiles.Instance.AddCompatPiece(stack, itemName, 10, true);
        }

        /// <summary>
        ///     Fill order: bottom row first, each row from one side to the other.
        ///     The pieces in a row aren't at exactly the same height (the frost stack's bottom row
        ///     varies by 3 cm), so a plain height sort jumps around inside a row. Instead, a piece
        ///     starts a new row when it sits more than <paramref name="rowTolerance"/> above the
        ///     lowest piece of the current row.
        /// </summary>
        private static List<Transform> OrderByRows(List<Transform> pieces, float rowTolerance) {
            List<List<Transform>> rows = new List<List<Transform>>();

            foreach (Transform piece in pieces.OrderBy(t => t.localPosition.y)) {
                List<Transform> row = rows.LastOrDefault();

                if (row == null || piece.localPosition.y - row[0].localPosition.y > rowTolerance) {
                    row = new List<Transform>();
                    rows.Add(row);
                }

                row.Add(piece);
            }

            return rows.SelectMany(row => row.OrderBy(t => t.localPosition.x)).ToList();
        }
    }
}
