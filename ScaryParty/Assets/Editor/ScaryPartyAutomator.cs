using UnityEngine;
using UnityEditor;

public class ScaryPartyAutomator
{
    [MenuItem("ScaryParty/Aplicar Setup de Rede e Cores")]
    public static void ApplySetup()
    {
        // 1. Encontra e adiciona AutoStartNetwork no NetworkManager
        GameObject networkManagerGO = GameObject.Find("NetworkManager");
        if (networkManagerGO != null)
        {
            if (networkManagerGO.GetComponent<AutoStartNetwork>() == null)
            {
                networkManagerGO.AddComponent<AutoStartNetwork>();
                Debug.Log("AutoStartNetwork adicionado ao NetworkManager na cena.");
            }
        }
        else
        {
            Debug.LogWarning("NetworkManager não encontrado na cena. Tem certeza que está na cena correta?");
        }

        // 2. Encontra o prefab do PlayerArmature para aplicar o material e o script de cor
        string[] prefabGuids = AssetDatabase.FindAssets("PlayerArmature t:Prefab");
        if (prefabGuids.Length > 0)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[0]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                bool modified = false;

                // Adiciona PlayerColorManager
                if (prefab.GetComponent<PlayerColorManager>() == null)
                {
                    prefab.AddComponent<PlayerColorManager>();
                    modified = true;
                    Debug.Log("PlayerColorManager adicionado ao prefab PlayerArmature.");
                }

                // Encontra o Material 'Toon Example'
                string[] matGuids = AssetDatabase.FindAssets("Toon Example t:Material");
                Material toonMat = null;
                if (matGuids.Length > 0)
                {
                    string matPath = AssetDatabase.GUIDToAssetPath(matGuids[0]);
                    toonMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                }

                if (toonMat != null)
                {
                    Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                    {
                        Material[] mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] != toonMat)
                            {
                                mats[i] = toonMat;
                                modified = true;
                            }
                        }
                        r.sharedMaterials = mats;
                    }
                    if (modified)
                        Debug.Log("Material Toon aplicado ao prefab PlayerArmature.");
                }
                else
                {
                    Debug.LogWarning("Material 'Toon Example' do Distant Lands não encontrado!");
                }

                if (modified)
                {
                    EditorUtility.SetDirty(prefab);
                    AssetDatabase.SaveAssets();
                    Debug.Log("Prefab PlayerArmature atualizado com sucesso!");
                }
                else
                {
                    Debug.Log("Prefab PlayerArmature já estava com as configurações corretas.");
                }
            }
        }
        else
        {
            Debug.LogWarning("Prefab PlayerArmature não encontrado.");
        }

        Debug.Log("Setup de Automação Concluído!");
    }
}
