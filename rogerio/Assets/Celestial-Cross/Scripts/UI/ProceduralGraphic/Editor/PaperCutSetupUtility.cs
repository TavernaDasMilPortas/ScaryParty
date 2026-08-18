using UnityEngine;
using UnityEditor;
using ProceduralGraphicSystem;

namespace ProceduralGraphicSystem.Editor
{
    public class PaperCutSetupUtility : EditorWindow
    {
        private GameObject _targetObject;
        private Color _borderColor = Color.white;
        private float _borderExpansion = 0.15f;
        private float _jitterAmount = 0.03f;

        [MenuItem("Celestial Cross/Tools/Paper Cut Border Setup")]
        public static void ShowWindow()
        {
            GetWindow<PaperCutSetupUtility>("Paper Cut Setup");
        }

        private void OnGUI()
        {
            GUILayout.Label("Configurar Paper Cut em Unidades (Inimigos/Bases)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _targetObject = (GameObject)EditorGUILayout.ObjectField("Target Unit / Base", _targetObject, typeof(GameObject), true);
            _borderColor = EditorGUILayout.ColorField("Border Color", _borderColor);
            _borderExpansion = EditorGUILayout.Slider("Border Expansion", _borderExpansion, 0f, 1f);
            _jitterAmount = EditorGUILayout.Slider("Jitter Amount", _jitterAmount, 0f, 0.2f);

            EditorGUILayout.Space();

            if (GUILayout.Button("Setup Paper Cut Border", GUILayout.Height(40)))
            {
                SetupPaperCutBorder();
            }
        }

        private void SetupPaperCutBorder()
        {
            if (_targetObject == null)
            {
                EditorUtility.DisplayDialog("Erro", "Por favor, selecione um GameObject alvo.", "OK");
                return;
            }

            bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(_targetObject);
            string assetPath = "";
            GameObject rootObject = _targetObject;

            if (isPrefabAsset)
            {
                assetPath = AssetDatabase.GetAssetPath(_targetObject);
                rootObject = PrefabUtility.LoadPrefabContents(assetPath);
            }

            SpriteRenderer sr = rootObject.GetComponentInChildren<SpriteRenderer>(true);
            if (sr == null)
            {
                EditorUtility.DisplayDialog("Erro", "Nenhum SpriteRenderer encontrado no GameObject ou em seus filhos.", "OK");
                if (isPrefabAsset) PrefabUtility.UnloadPrefabContents(rootObject);
                return;
            }

            // Verifica se já existe
            Transform existing = sr.transform.Find("TurnBorder_PaperCut");
            if (existing != null)
            {
                if (EditorUtility.DisplayDialog("Aviso", "Já existe um objeto 'TurnBorder_PaperCut' como filho deste SpriteRenderer. Deseja recriá-lo?", "Sim", "Não"))
                {
                    if (isPrefabAsset)
                        DestroyImmediate(existing.gameObject);
                    else
                        Undo.DestroyObjectImmediate(existing.gameObject);
                }
                else
                {
                    if (isPrefabAsset) PrefabUtility.UnloadPrefabContents(rootObject);
                    return;
                }
            }

            // Cria o objeto base
            GameObject borderObj = new GameObject("TurnBorder_PaperCut");
            if (!isPrefabAsset) Undo.RegisterCreatedObjectUndo(borderObj, "Create Paper Cut Border");
            borderObj.transform.SetParent(sr.transform, false);

            // Adiciona e configura o Canvas World Space
            Canvas canvas = borderObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingLayerID = sr.sortingLayerID;
            canvas.sortingOrder = sr.sortingOrder - 1; // Para ficar atrás do sprite original

            // Ajusta o RectTransform
            RectTransform rect = borderObj.GetComponent<RectTransform>();
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            if (sr.sprite != null)
            {
                // Aumenta o tamanho para acomodar a borda expandida
                float sizeMult = 1f + (_borderExpansion * 2f);
                rect.sizeDelta = new Vector2(sr.sprite.bounds.size.x * sizeMult, sr.sprite.bounds.size.y * sizeMult);
            }
            else
            {
                rect.sizeDelta = new Vector2(5f, 5f);
            }

            // Adiciona o ProceduralGraphic (adicionado pelo RequireComponent mas vamos garantir)
            ProceduralGraphic graphic = borderObj.GetComponent<ProceduralGraphic>();
            if (graphic == null) graphic = borderObj.AddComponent<ProceduralGraphic>();
            graphic.color = _borderColor;

            // Adiciona o gerador
            PaperCutBorderGenerator generator = borderObj.GetComponent<PaperCutBorderGenerator>();
            if (generator == null) generator = borderObj.AddComponent<PaperCutBorderGenerator>();
            
            generator.targetSpriteRenderer = sr;
            generator.borderExpansion = _borderExpansion;
            generator.jitterAmount = _jitterAmount;

            // Inicia desativado como pedido
            borderObj.SetActive(false);

            if (isPrefabAsset)
            {
                PrefabUtility.SaveAsPrefabAsset(rootObject, assetPath);
                PrefabUtility.UnloadPrefabContents(rootObject);
                Debug.Log($"[PaperCutSetup] Borda criada com sucesso no PREFAB {assetPath}!");
            }
            else
            {
                // Marca a cena/prefab como modificado
                EditorUtility.SetDirty(_targetObject);
                Selection.activeGameObject = borderObj;
                Debug.Log($"[PaperCutSetup] Borda criada com sucesso em {sr.gameObject.name} na Cena!");
            }
        }
    }
}
