using UnityEngine;
using StarterAssets;
using Unity.Netcode;

namespace ScaryParty.Player
{
    public class FirstPersonCameraController : NetworkBehaviour
    {
        public static FirstPersonCameraController LocalInstance { get; private set; }

        public Transform LeftHandAnchor { get; private set; }
        public Transform RightHandAnchor { get; private set; }

        private ThirdPersonController _tpc;
        private Transform _headBone;
        private CharacterController _cc;

        private Vector3 _leftAnchorBasePos = new Vector3(-0.35f, -0.25f, 0.4f);
        private Vector3 _rightAnchorBasePos = new Vector3(0.35f, -0.25f, 0.4f);
        
        private float _bobTimer = 0f;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            LocalInstance = this;

            _tpc = GetComponent<ThirdPersonController>();
            if (_tpc != null)
            {
                _tpc.TopClamp = 89.0f;
                _tpc.BottomClamp = -89.0f;
            }
            _cc = GetComponent<CharacterController>();
            
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                _headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            }

            // Create Anchors parented to the main camera
            var camSetup = GetComponent<PlayerCameraSetup>();
            if (camSetup != null && camSetup.mainCamera != null)
            {
                var camTransform = camSetup.mainCamera.transform;
                
                var leftGo = new GameObject("LeftHandAnchor");
                leftGo.transform.SetParent(camTransform);
                LeftHandAnchor = leftGo.transform;

                var rightGo = new GameObject("RightHandAnchor");
                rightGo.transform.SetParent(camTransform);
                RightHandAnchor = rightGo.transform;
            }
        }

        private void LateUpdate()
        {
            if (_tpc != null && _tpc.CinemachineCameraTarget != null && _headBone != null)
            {
                // Place the camera target slightly in front of the head to avoid clipping
                _tpc.CinemachineCameraTarget.transform.position = _headBone.position + _headBone.forward * 0.15f;

                // Sync main camera perfectly with the Target (bypassing Cinemachine)
                var camSetup = GetComponent<PlayerCameraSetup>();
                if (camSetup != null && camSetup.mainCamera != null)
                {
                    camSetup.mainCamera.transform.position = _tpc.CinemachineCameraTarget.transform.position;
                    camSetup.mainCamera.transform.rotation = _tpc.CinemachineCameraTarget.transform.rotation;
                }
            }

            UpdateSwayAndBob();
        }

        private void UpdateSwayAndBob()
        {
            if (LeftHandAnchor == null || RightHandAnchor == null || _cc == null) return;

            float speed = new Vector3(_cc.velocity.x, 0, _cc.velocity.z).magnitude;
            
            // Sway based on look input
            var starterAssetsInputs = GetComponent<StarterAssetsInputs>();
            float lookX = starterAssetsInputs != null ? starterAssetsInputs.look.x : 0f;
            float lookY = starterAssetsInputs != null ? starterAssetsInputs.look.y : 0f;

            float swayAmount = 0.02f;
            float maxSway = 0.05f;
            
            Vector3 swayOffset = new Vector3(
                Mathf.Clamp(-lookX * swayAmount, -maxSway, maxSway),
                Mathf.Clamp(-lookY * swayAmount, -maxSway, maxSway),
                0
            );

            // Bob based on movement
            float bobSpeed = 10f;
            float bobAmount = 0.02f;

            if (speed > 0.1f)
            {
                _bobTimer += Time.deltaTime * bobSpeed;
                float bobY = Mathf.Sin(_bobTimer) * bobAmount;
                float bobX = Mathf.Cos(_bobTimer * 0.5f) * bobAmount;
                swayOffset += new Vector3(bobX, bobY, 0);
            }
            else
            {
                _bobTimer = 0f;
            }

            // Smoothly move anchors
            LeftHandAnchor.localPosition = Vector3.Lerp(LeftHandAnchor.localPosition, _leftAnchorBasePos + swayOffset, Time.deltaTime * 5f);
            RightHandAnchor.localPosition = Vector3.Lerp(RightHandAnchor.localPosition, _rightAnchorBasePos + swayOffset, Time.deltaTime * 5f);

            // Also slight rotation sway
            Quaternion swayRotation = Quaternion.Euler(-lookY * 0.5f, lookX * 0.5f, lookX * 0.5f);
            LeftHandAnchor.localRotation = Quaternion.Slerp(LeftHandAnchor.localRotation, swayRotation, Time.deltaTime * 5f);
            RightHandAnchor.localRotation = Quaternion.Slerp(RightHandAnchor.localRotation, swayRotation, Time.deltaTime * 5f);
        }
    }
}
