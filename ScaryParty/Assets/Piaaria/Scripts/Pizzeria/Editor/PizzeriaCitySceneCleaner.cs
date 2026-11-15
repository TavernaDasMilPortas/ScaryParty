using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScaryParty.Pizzeria.Editor
{
    /// <summary>
    /// Ferramenta de autoria para limpeza da cidade estática serializada na GameScene (Fase 4).
    /// Remove com segurança o nó estático '__City__' (gerado em preview anterior com ~64k objetos)
    /// enquanto preserva backup de autoria, referências, iluminação e a geração procedural limpa.
    /// </summary>
    public static class PizzeriaCitySceneCleaner
    {
        private const string GameScenePath = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity";
        private const string BackupDirectory = "Assets/Piaaria/Scenes/Backup";
        private const string BackupScenePath = "Assets/Piaaria/Scenes/Backup/GameScene_PreCleanup_Backup.unity";

        [MenuItem("Tools/Scary Party/Setup e Uso Único/Pizzaria/Limpar Cidade Estática da GameScene (Fase 4)", false, 245)]
        public static void CleanStaticCityFromGameScene()
        {
            if (!File.Exists(GameScenePath))
            {
                Debug.LogError($"[CitySceneCleaner] Cena não encontrada em {GameScenePath}");
                return;
            }

            // 1. Criar diretório de backup se não existir
            if (!Directory.Exists(BackupDirectory))
            {
                Directory.CreateDirectory(BackupDirectory);
                AssetDatabase.Refresh();
            }

            // 2. Fazer backup seguro do arquivo da cena
            File.Copy(GameScenePath, BackupScenePath, overwrite: true);
            AssetDatabase.ImportAsset(BackupScenePath);
            Debug.Log($"[CitySceneCleaner] 💾 Backup de segurança criado em {BackupScenePath}");

            // 3. Abrir a cena de produção no EditorSceneManager
            Scene currentScene = EditorSceneManager.GetActiveScene();
            bool wasDifferentScene = currentScene.path != GameScenePath;
            Scene gameScene = wasDifferentScene
                ? EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single)
                : currentScene;

            int deletedCount = 0;
            var rootObjects = gameScene.GetRootGameObjects();

            for (int i = 0; i < rootObjects.Length; i++)
            {
                var go = rootObjects[i];
                if (go == null) continue;

                // Localizar o nó estático da cidade de preview
                if (go.name == "__City__" || go.name == "--- PREVIEW CITY ---")
                {
                    int childCount = go.GetComponentsInChildren<Transform>(true).Length;
                    Debug.Log($"[CitySceneCleaner] 🗑️ Removendo raiz estática '{go.name}' com {childCount} sub-objetos...");
                    Undo.DestroyObjectImmediate(go);
                    deletedCount++;
                }
            }

            if (deletedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(gameScene);
                EditorSceneManager.SaveScene(gameScene);
                AssetDatabase.SaveAssets();
                Debug.Log($"[CitySceneCleaner] ✅ GameScene limpa e salva com sucesso! {deletedCount} raízes estáticas removidas.");
            }
            else
            {
                Debug.Log("[CitySceneCleaner] ℹ️ Nenhum nó estático '__City__' encontrado em GameScene (já estava limpa ou é gerada em runtime).");
            }
        }
    }
}
