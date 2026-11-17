using UnityEngine;

namespace ScaryParty.Cutscenes
{
    /// <summary>
    /// Converts Blender Z-up right-handed coordinates to Unity Y-up left-handed coordinates,
    /// matching Unity's FBX import of Blender's default (-Z forward, Y up) export:
    /// (x_b, y_b, z_b) -> (-x_b, z_b, -y_b).
    /// </summary>
    public static class CutsceneCoordinateConverter
    {
        public static Vector3 BlenderPositionToUnity(float[] pos)
        {
            if (pos == null || pos.Length < 3) return Vector3.zero;
            return new Vector3(-pos[0], pos[2], -pos[1]);
        }

        public static Vector3 BlenderDirectionToUnity(Vector3 dir)
        {
            return new Vector3(-dir.x, dir.z, -dir.y);
        }

        public static Quaternion BlenderCameraRotationToUnity(float[] xyzw)
        {
            if (xyzw == null || xyzw.Length < 4) return Quaternion.identity;

            // Blender camera looks down local -Z with local +Y as up.
            Quaternion qBlender = new Quaternion(xyzw[0], xyzw[1], xyzw[2], xyzw[3]);
            Vector3 forwardBlender = qBlender * new Vector3(0f, 0f, -1f);
            Vector3 upBlender = qBlender * new Vector3(0f, 1f, 0f);

            Vector3 forwardUnity = BlenderDirectionToUnity(forwardBlender);
            Vector3 upUnity = BlenderDirectionToUnity(upBlender);

            if (forwardUnity.sqrMagnitude < 1e-8f)
                return Quaternion.identity;

            return Quaternion.LookRotation(forwardUnity.normalized, upUnity.normalized);
        }
    }
}
