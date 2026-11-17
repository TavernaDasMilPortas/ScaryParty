using UnityEngine;

namespace ScaryParty.Cutscenes.Editor
{
    public static class CutsceneCompatibilityValidator
    {
        public static void ValidateObjectBinding(ObjectBindingEntry entry, GameObject fbxRoot)
        {
            if (entry == null) return;

            if (entry.useExportedFallback || entry.targetPrefab == null)
            {
                entry.compatibility = BindingCompatibilityStatus.Compatible;
                entry.compatibilityReason = "Using exported FBX fallback.";
                return;
            }

            if (entry.sourceType == "ARMATURE")
            {
                var targetAnim = entry.targetPrefab.GetComponentInChildren<Animator>(true);
                if (targetAnim == null || targetAnim.avatar == null)
                {
                    entry.compatibility = BindingCompatibilityStatus.Incompatible;
                    entry.compatibilityReason = "Target prefab has no Animator/Avatar for armature retargeting.";
                    return;
                }

                if (!targetAnim.avatar.isHuman)
                {
                    entry.compatibility = BindingCompatibilityStatus.NeedsAdjustment;
                    entry.compatibilityReason = "Generic rig: verify bone hierarchy and root bone manually.";
                    return;
                }

                entry.compatibility = BindingCompatibilityStatus.Compatible;
                entry.compatibilityReason = "Humanoid Avatar valid.";
                return;
            }

            // Rigid / Mesh object
            var meshFilter = entry.targetPrefab.GetComponentInChildren<MeshFilter>(true);
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                entry.compatibility = BindingCompatibilityStatus.Incompatible;
                entry.compatibilityReason = "Target prefab has no MeshFilter/sharedMesh.";
                return;
            }

            if (entry.pivotOffset != Vector3.zero || entry.scaleOverride != Vector3.one)
            {
                entry.compatibility = BindingCompatibilityStatus.NeedsAdjustment;
                entry.compatibilityReason = "Rigid wrapper will apply pivot/scale correction.";
            }
            else
            {
                entry.compatibility = BindingCompatibilityStatus.Compatible;
                entry.compatibilityReason = "Rigid mesh compatible.";
            }
        }
    }
}
