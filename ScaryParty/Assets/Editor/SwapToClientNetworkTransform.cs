using UnityEngine;
using UnityEditor;
using Unity.Netcode.Components;

public class SwapToClientNetworkTransform : EditorWindow
{
    [MenuItem("Tools/Trocar NetworkTransform por ClientNetworkTransform")]
    public static void Swap()
    {
        string prefabPath = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefabAsset == null)
        {
            Debug.LogError("Prefab PlayerArmature não encontrado em: " + prefabPath);
            return;
        }

        GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

        // Remove o NetworkTransform antigo (se existir)
        NetworkTransform oldNT = prefabContents.GetComponent<NetworkTransform>();
        if (oldNT != null && oldNT.GetType() == typeof(NetworkTransform))
        {
            // Salvar configurações antes de remover
            DestroyImmediate(oldNT);
            Debug.Log("NetworkTransform removido com sucesso.");
        }

        // Adiciona o ClientNetworkTransform (se ainda não tiver)
        ClientNetworkTransform clientNT = prefabContents.GetComponent<ClientNetworkTransform>();
        if (clientNT == null)
        {
            clientNT = prefabContents.AddComponent<ClientNetworkTransform>();
            Debug.Log("ClientNetworkTransform adicionado com sucesso.");
        }

        // Trocar NetworkAnimator por OwnerNetworkAnimator
        NetworkAnimator oldAnim = prefabContents.GetComponent<NetworkAnimator>();
        Animator targetAnim = null;
        if (oldAnim != null && oldAnim.GetType() == typeof(NetworkAnimator))
        {
            targetAnim = oldAnim.Animator;
            DestroyImmediate(oldAnim);
            Debug.Log("NetworkAnimator padrão removido.");
        }

        OwnerNetworkAnimator ownerAnim = prefabContents.GetComponent<OwnerNetworkAnimator>();
        if (ownerAnim == null)
        {
            ownerAnim = prefabContents.AddComponent<OwnerNetworkAnimator>();
            if (targetAnim == null) targetAnim = prefabContents.GetComponent<Animator>();
            ownerAnim.Animator = targetAnim;
            Debug.Log("OwnerNetworkAnimator adicionado com sucesso.");
        }

        // Otimizar: desmarcar Sync Scale e rotações desnecessárias
        SerializedObject so = new SerializedObject(clientNT);
        SetBoolProperty(so, "SyncScaleX", false);
        SetBoolProperty(so, "SyncScaleY", false);
        SetBoolProperty(so, "SyncScaleZ", false);
        SetBoolProperty(so, "SyncRotAngleX", false);
        SetBoolProperty(so, "SyncRotAngleZ", false);
        so.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabContents);

        Debug.Log("Pronto! O prefab PlayerArmature agora usa ClientNetworkTransform (Owner Authoritative). Clientes poderão mover seus próprios personagens.");
    }

    private static void SetBoolProperty(SerializedObject so, string propName, bool value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null)
        {
            prop.boolValue = value;
        }
    }
}
