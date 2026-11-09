#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

// Sem namespace específico (ou pode colocar em ferramentas genéricas) para facilitar a exportação para outros projetos.
public class ScenePlayShortcutSettingsWindow : EditorWindow
{
    private string[] _buildScenesPaths;
    private string[] _buildScenesNames;
    private int _selectedIndex = 0;

    [MenuItem("Tools/Configurações do Atalho de Play (Ctrl+G)")]
    public static void ShowWindow()
    {
        GetWindow<ScenePlayShortcutSettingsWindow>("Atalho de Play");
    }

    private void OnEnable()
    {
        RefreshScenes();
    }

    private void RefreshScenes()
    {
        // Pega todas as cenas ativadas no Build Settings
        var buildScenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        
        _buildScenesPaths = new string[buildScenes.Length];
        _buildScenesNames = new string[buildScenes.Length];

        for (int i = 0; i < buildScenes.Length; i++)
        {
            _buildScenesPaths[i] = buildScenes[i].path;
            // Usando global::System.IO para evitar colisão com namespaces locais (ex: CelestialCross.System)
            _buildScenesNames[i] = global::System.IO.Path.GetFileNameWithoutExtension(buildScenes[i].path);
        }

        // Recupera a cena salva, se houver
        string savedPath = EditorPrefs.GetString("PlayShortcutScenePath", "");
        if (!string.IsNullOrEmpty(savedPath))
        {
            // Usando global::System.Array para evitar colisão
            _selectedIndex = global::System.Array.IndexOf(_buildScenesPaths, savedPath);
            if (_selectedIndex == -1) _selectedIndex = 0;
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Configuração do Atalho Ctrl+G", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (_buildScenesPaths == null || _buildScenesPaths.Length == 0)
        {
            EditorGUILayout.HelpBox("Nenhuma cena ativada no Build Settings (File > Build Settings). Adicione suas cenas lá primeiro.", MessageType.Warning);
            if (GUILayout.Button("Atualizar Lista"))
            {
                RefreshScenes();
            }
            return;
        }

        _selectedIndex = EditorGUILayout.Popup("Cena Inicial:", _selectedIndex, _buildScenesNames);

        GUILayout.Space(10);

        if (GUILayout.Button("Salvar Configuração"))
        {
            if (_selectedIndex >= 0 && _selectedIndex < _buildScenesPaths.Length)
            {
                EditorPrefs.SetString("PlayShortcutScenePath", _buildScenesPaths[_selectedIndex]);
                Debug.Log($"[PlayShortcut] Cena configurada para: {_buildScenesNames[_selectedIndex]}");
                ShowNotification(new GUIContent("Salvo com sucesso!"));
            }
        }
    }
}

public static class ScenePlayShortcut
{
    // Cria um item no menu superior da Unity
    // Atalho: Ctrl + G (Windows) ou Cmd + G (Mac)
    [MenuItem("Tools/Jogar da Cena Escolhida %g")]
    public static void PlayFromSelectedScene()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        // Pega as cenas do Build Settings
        var buildScenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        if (buildScenes.Length == 0)
        {
            Debug.LogError("[PlayShortcut] Nenhuma cena encontrada no Build Settings. Vá em File > Build Settings e adicione suas cenas.");
            return;
        }

        // Tenta pegar a cena salva nas preferências.
        // Caso não tenha nenhuma, usa a PRIMEIRA cena do Build Settings como fallback para ser 100% genérico.
        string fallbackPath = buildScenes[0].path;
        string savedPath = EditorPrefs.GetString("PlayShortcutScenePath", fallbackPath);

        // Tenta carregar o asset da cena
        SceneAsset targetScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(savedPath);

        if (targetScene != null)
        {
            // Salva todas as cenas abertas antes de entrar no Play Mode
            EditorSceneManager.SaveOpenScenes();

            // Define que SEMPRE que o jogo iniciar, começará pela cena escolhida
            EditorSceneManager.playModeStartScene = targetScene;
            EditorApplication.isPlaying = true;
            
            // Registra um evento para limpar essa configuração quando você parar o jogo
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }
        else
        {
            Debug.LogError($"[PlayShortcut] Não foi possível encontrar a cena no caminho: {savedPath}. Use Tools > Configurações do Atalho de Play (Ctrl+G) para selecionar uma nova cena.");
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // Quando o editor volta para o modo de edição (parou o jogo)
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            // Limpa a cena de início forçado
            EditorSceneManager.playModeStartScene = null;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }
    }
}
#endif
