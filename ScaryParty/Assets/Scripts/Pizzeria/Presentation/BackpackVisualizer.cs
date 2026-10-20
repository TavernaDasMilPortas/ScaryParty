using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Player;

namespace ScaryParty.Pizzeria.Presentation
{
    public class BackpackVisualizer : MonoBehaviour
    {
        private Transform _backpackAnchor;
        private GameObject _bagVisual;
        private readonly List<GameObject> _boxVisuals = new List<GameObject>();
        private NetworkObject _netObj;

        private float _refreshTimer = 0f;
        private const float RefreshInterval = 0.25f;

        private void Awake()
        {
            _netObj = GetComponent<NetworkObject>();
        }

        private void Start()
        {
            SetupBackpackMount();
        }

        private bool IsOwnerPlayer()
        {
            if (_netObj != null) return _netObj.IsOwner;
            return true;
        }

        private ulong GetOwnerClientId()
        {
            if (_netObj != null) return _netObj.OwnerClientId;
            if (NetworkManager.Singleton != null) return NetworkManager.Singleton.LocalClientId;
            return 0;
        }

        private void SetupBackpackMount()
        {
            if (_backpackAnchor != null) return;

            var anim = GetComponentInChildren<Animator>();
            Transform spine = null;
            if (anim != null)
            {
                spine = anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine);
            }

            if (spine == null) spine = transform;

            var mountGo = new GameObject("BackpackMount");
            mountGo.transform.SetParent(spine, false);
            // Posicionar atrás do peito/coluna
            mountGo.transform.localPosition = new Vector3(0f, 0.05f, -0.22f);
            mountGo.transform.localRotation = Quaternion.identity;
            _backpackAnchor = mountGo.transform;

            // Criar a bolsa térmica vermelha
            _bagVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _bagVisual.name = "ThermalDeliveryBag";
            _bagVisual.transform.SetParent(_backpackAnchor, false);
            _bagVisual.transform.localPosition = Vector3.zero;
            _bagVisual.transform.localScale = new Vector3(0.48f, 0.42f, 0.38f);

            var col = _bagVisual.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var renderer = _bagVisual.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = new Color(0.85f, 0.15f, 0.15f); // Vermelho térmico de delivery
                renderer.sharedMaterial = mat;
            }

            // Se for o jogador local em 1ª pessoa, esconder para não atrapalhar o campo de visão
            if (IsOwnerPlayer())
            {
                SetLayerRecursively(_bagVisual, LayerMask.NameToLayer("ThirdPersonOnly") != -1 ? LayerMask.NameToLayer("ThirdPersonOnly") : 0);
            }
        }

        private void Update()
        {
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = RefreshInterval;

            UpdateBoxVisuals();
        }

        private void UpdateBoxVisuals()
        {
            if (_backpackAnchor == null) SetupBackpackMount();
            if (_backpackAnchor == null) return;

            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || netState.Items == null) return;

            var backpackItems = new List<NetItemDto>();
            ulong myId = GetOwnerClientId();
            var ctrl = GetComponent<BackpackController>();
            ulong bpId = ctrl != null && ctrl.backpackId != 0 ? ctrl.backpackId : (myId + 1);

            for (int i = 0; i < netState.Items.Count; i++)
            {
                var it = netState.Items[i];
                if (it.LocationType == (byte)LocationType.Backpack && (it.HolderId == bpId || it.HolderId == myId))
                {
                    backpackItems.Add(it);
                }
            }

            // Assegurar quantidade de caixas visuais
            while (_boxVisuals.Count < backpackItems.Count)
            {
                var boxGo = CreateVisualBox(_boxVisuals.Count);
                _boxVisuals.Add(boxGo);
            }

            // Atualizar e posicionar caixas
            for (int i = 0; i < _boxVisuals.Count; i++)
            {
                if (i < backpackItems.Count)
                {
                    _boxVisuals[i].SetActive(true);
                    _boxVisuals[i].transform.localPosition = new Vector3(0f, -0.12f + (i * 0.11f), 0f);

                    var labelTmp = _boxVisuals[i].GetComponentInChildren<TextMeshPro>();
                    if (labelTmp != null)
                    {
                        int dest = backpackItems[i].LabelDestinationId;
                        labelTmp.text = dest >= 0 ? $"<b>{AddressFormat.Display(dest)}</b>" : "(Sem etiqueta)";
                    }
                }
                else
                {
                    _boxVisuals[i].SetActive(false);
                }
            }
        }

        private GameObject CreateVisualBox(int index)
        {
            var boxGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boxGo.name = $"BackpackBox_{index}";
            boxGo.transform.SetParent(_backpackAnchor, false);
            boxGo.transform.localScale = new Vector3(0.42f, 0.10f, 0.42f);

            var col = boxGo.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = boxGo.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = new Color(0.72f, 0.52f, 0.35f); // Papelão
                rend.sharedMaterial = mat;
            }

            // Etiqueta traseira
            var labelGo = new GameObject("BoxBackLabel");
            labelGo.transform.SetParent(boxGo.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.52f);
            labelGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            labelGo.transform.localScale = Vector3.one * 0.08f;

            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 2.2f;
            tmp.color = Color.black;
            var rect = labelGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(3f, 1.5f);

            if (IsOwnerPlayer())
            {
                SetLayerRecursively(boxGo, LayerMask.NameToLayer("ThirdPersonOnly") != -1 ? LayerMask.NameToLayer("ThirdPersonOnly") : 0);
            }

            return boxGo;
        }

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null || layer < 0) return;
            obj.layer = layer;
            foreach (Transform c in obj.transform)
            {
                SetLayerRecursively(c.gameObject, layer);
            }
        }

        private void OnDestroy()
        {
            if (_backpackAnchor != null)
            {
                Destroy(_backpackAnchor.gameObject);
            }
        }
    }
}
