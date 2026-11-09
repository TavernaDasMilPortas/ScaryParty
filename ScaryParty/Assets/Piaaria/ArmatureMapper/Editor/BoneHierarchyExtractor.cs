using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ScaryParty.ArmatureMapper
{
    public static class BoneHierarchyExtractor
    {
        public static void ExtractHierarchy(GameObject root, ArmatureMap map)
        {
            map.hierarchy.Clear();
            map.skinnedMeshes.Clear();

            int boneCount = 0;
            int componentCount = 0;

            // Start recursive extraction
            BoneNode rootNode = ProcessTransform(root.transform, root.transform, 0, ref boneCount, ref componentCount);
            map.hierarchy.Add(rootNode);

            // Extract SkinnedMeshRenderers
            SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in renderers)
            {
                SkinnedMeshInfo info = new SkinnedMeshInfo();
                info.meshName = smr.sharedMesh != null ? smr.sharedMesh.name : "None";
                
                if (smr.sharedMaterials != null)
                {
                    foreach (var mat in smr.sharedMaterials)
                    {
                        info.materialNames.Add(mat != null ? mat.name : "None");
                    }
                }

                if (smr.bones != null)
                {
                    foreach (var bone in smr.bones)
                    {
                        info.boneNames.Add(bone != null ? bone.name : "None");
                    }
                }

                info.rootBoneName = smr.rootBone != null ? smr.rootBone.name : "None";
                info.boundingBox = smr.localBounds;

                if (smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
                {
                    for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                    {
                        info.blendShapeNames.Add(smr.sharedMesh.GetBlendShapeName(i));
                    }
                }

                map.skinnedMeshes.Add(info);
            }

            map.prefabInfo.totalBoneCount = boneCount;
            map.prefabInfo.totalComponentCount = componentCount;
        }

        private static BoneNode ProcessTransform(Transform current, Transform root, int depth, ref int boneCount, ref int componentCount)
        {
            boneCount++;
            BoneNode node = new BoneNode();
            node.name = current.name;
            node.path = AnimationUtility.CalculateTransformPath(current, root);
            node.localPosition = current.localPosition;
            node.localRotation = current.localEulerAngles;
            node.localScale = current.localScale;
            node.worldPosition = current.position;
            node.worldRotation = current.eulerAngles;
            node.depth = depth;

            // Extract components
            Component[] components = current.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp != null)
                {
                    node.components.Add(comp.GetType().Name);
                    componentCount++;
                }
            }

            // Recursively process children
            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                node.children.Add(ProcessTransform(child, root, depth + 1, ref boneCount, ref componentCount));
            }

            return node;
        }
    }
}
