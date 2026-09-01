using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Editor Window para pré-visualizar os AnimationClips de arma gerados,
/// sem precisar modificar o Animator Controller.
/// Menu: Tools > Weapons > Animation Previewer
/// 
/// Como usar:
///   1. Abra a janela via Tools > Weapons > Animation Previewer
///   2. Arraste o PlayerArmature (da cena ou do Project) no campo "Target Avatar"
///   3. Clique em qualquer clip da lista para samplear no avatar
///   4. Use o scrubber ou o botão Play
/// </summary>
public class WeaponAnimationPreviewer : EditorWindow
{
    // ── Estado da janela ─────────────────────────────────────────────────────
    private GameObject        _targetObject;
    private AnimationClip     _selectedClip;
    private List<AnimationClip> _clips = new List<AnimationClip>();
    private Vector2           _scrollPos;

    private bool  _isPlaying;
    private float _currentTime;
    private double _lastEditorTime;

    // Componentes desativados durante preview para evitar conflito com AnimationMode
    private CharacterController _disabledCC;
    private bool                _ccWasEnabled;

    // ── Layout ────────────────────────────────────────────────────────────────
    private const string ClipFolder = "Assets/Animations/Weapons/Generated";
    private GUIStyle _headerStyle;
    private GUIStyle _clipButtonStyle;
    private GUIStyle _selectedClipButtonStyle;
    private GUIStyle _infoBoxStyle;
    private bool _stylesInitialized;

    // ── Cores ─────────────────────────────────────────────────────────────────
    private static readonly Color ColAccent   = new Color(0.29f, 0.69f, 1.00f);
    private static readonly Color ColSelected = new Color(0.20f, 0.50f, 0.80f);
    private static readonly Color ColBg       = new Color(0.15f, 0.15f, 0.18f);
    private static readonly Color ColCard     = new Color(0.20f, 0.20f, 0.24f);
    private static readonly Color ColGreen    = new Color(0.30f, 0.85f, 0.40f);
    private static readonly Color ColOrange   = new Color(1.00f, 0.65f, 0.20f);

    [MenuItem("Tools/Weapons/Animation Previewer")]
    public static void ShowWindow()
    {
        var w = GetWindow<WeaponAnimationPreviewer>("Weapon Animator");
        w.minSize = new Vector2(340, 520);
        w.Show();
    }

    private void OnEnable()
    {
        LoadClips();
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        StopPreview();
    }

    // ── Carrega todos os clips da pasta Generated ─────────────────────────────
    private void LoadClips()
    {
        _clips.Clear();
        if (!AssetDatabase.IsValidFolder(ClipFolder)) return;

        string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) _clips.Add(clip);
        }

        // Ordenar: Pistol primeiro, depois Shotgun; dentro de cada grupo, alfa
        _clips.Sort((a, b) =>
        {
            int groupA = a.name.StartsWith("Pistol") ? 0 : 1;
            int groupB = b.name.StartsWith("Pistol") ? 0 : 1;
            if (groupA != groupB) return groupA.CompareTo(groupB);
            return string.Compare(a.name, b.name, System.StringComparison.Ordinal);
        });
    }

    // ── Update do editor (playback) ───────────────────────────────────────────
    private void OnEditorUpdate()
    {
        if (!_isPlaying || _selectedClip == null) return;

        double dt = EditorApplication.timeSinceStartup - _lastEditorTime;
        _lastEditorTime = EditorApplication.timeSinceStartup;
        _currentTime += (float)dt;

        if (_currentTime > _selectedClip.length)
        {
            if (_selectedClip.isLooping)
                _currentTime %= _selectedClip.length;
            else
            {
                _currentTime = _selectedClip.length;
                _isPlaying = false;
            }
        }

        SampleCurrentClip();
        Repaint();
    }

    // ── GUI principal ─────────────────────────────────────────────────────────
    private void OnGUI()
    {
        InitStyles();

        DrawBackground();
        DrawHeader();
        DrawAvatarField();
        EditorGUILayout.Space(6);
        DrawClipList();
        EditorGUILayout.Space(6);
        DrawPlaybackControls();
        DrawClipInfo();
    }

    private void DrawBackground()
    {
        EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), ColBg);
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(10);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(12);
            GUILayout.Label("🎬  Weapon Animation Previewer", _headerStyle);
        }

        var line = GUILayoutUtility.GetRect(position.width, 2);
        EditorGUI.DrawRect(line, ColAccent);
        EditorGUILayout.Space(8);
    }

    private void DrawAvatarField()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(10);
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUI.DrawRect(GUILayoutUtility.GetRect(position.width - 20, 1), ColCard);
                EditorGUILayout.Space(6);

                GUILayout.Label("Target Avatar (PlayerArmature na cena)", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                var newObj = (GameObject)EditorGUILayout.ObjectField(
                    _targetObject, typeof(GameObject), allowSceneObjects: true,
                    GUILayout.Height(22));

                if (newObj != _targetObject)
                {
                    StopPreview();
                    _targetObject = newObj;
                }

                if (_targetObject == null)
                {
                    EditorGUILayout.HelpBox(
                        "Arraste o PlayerArmature da cena ou do Project aqui.",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "O CharacterController é desativado automaticamente durante o preview para evitar conflito de física (desnível).",
                        MessageType.None);
                }

                EditorGUILayout.Space(6);
                EditorGUI.DrawRect(GUILayoutUtility.GetRect(position.width - 20, 1), ColCard);
            }
        }
    }

    private void DrawClipList()
    {
        GUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(10);
            GUILayout.Label("Clips Gerados", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            var refreshCol = GUI.color;
            GUI.color = ColOrange;
            if (GUILayout.Button("↻ Reload", GUILayout.Width(70), GUILayout.Height(20)))
                LoadClips();
            GUI.color = refreshCol;
            GUILayout.Space(10);
        }

        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(10);
            using (new EditorGUILayout.VerticalScope())
            {
                string lastGroup = "";
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos,
                    GUILayout.Height(Mathf.Min(_clips.Count * 30 + 40, 280)));

                foreach (var clip in _clips)
                {
                    // Cabeçalho de grupo
                    string group = clip.name.StartsWith("Pistol") ? "🔫  PISTOLA" : "💥  ESCOPETA";
                    if (group != lastGroup)
                    {
                        EditorGUILayout.Space(4);
                        GUILayout.Label(group, EditorStyles.boldLabel);
                        lastGroup = group;
                    }

                    bool isSelected = _selectedClip == clip;
                    var  btnStyle   = isSelected ? _selectedClipButtonStyle : _clipButtonStyle;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(8);

                        // Ícone de loop
                        string loopIcon = clip.isLooping ? "🔁" : "▶";
                        var prevColor = GUI.color;
                        GUI.color = isSelected ? ColAccent : Color.white;

                        if (GUILayout.Button($"  {loopIcon}  {clip.name}  ({clip.length:F2}s)",
                                btnStyle, GUILayout.Height(26)))
                        {
                            SelectClip(clip);
                        }
                        GUI.color = prevColor;
                        GUILayout.Space(8);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
            GUILayout.Space(10);
        }
    }

    private void DrawPlaybackControls()
    {
        if (_selectedClip == null) return;

        EditorGUILayout.Space(4);
        var separatorRect = GUILayoutUtility.GetRect(position.width, 1);
        EditorGUI.DrawRect(separatorRect, ColAccent);
        EditorGUILayout.Space(8);

        // Scrubber
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(10);
            float newTime = EditorGUILayout.Slider(_currentTime, 0f, _selectedClip.length);
            if (!Mathf.Approximately(newTime, _currentTime))
            {
                _currentTime = newTime;
                SampleCurrentClip();
            }
            GUILayout.Space(10);
        }

        EditorGUILayout.Space(6);

        // Botões de controle
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            // ⏮ Rewind
            if (GUILayout.Button("⏮", GUILayout.Width(36), GUILayout.Height(32)))
            {
                _currentTime = 0f;
                SampleCurrentClip();
            }

            GUILayout.Space(4);

            // ▶ / ⏸ Play/Pause
            string playLabel = _isPlaying ? "⏸ Pause" : "▶ Play";
            var prevColor = GUI.color;
            GUI.color = _isPlaying ? ColOrange : ColGreen;
            if (GUILayout.Button(playLabel, GUILayout.Width(90), GUILayout.Height(32)))
                TogglePlay();
            GUI.color = prevColor;

            GUILayout.Space(4);

            // ⏹ Stop
            if (GUILayout.Button("⏹", GUILayout.Width(36), GUILayout.Height(32)))
                StopPreview();

            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.Space(6);

        // Barra de progresso
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(10);
            var barRect = GUILayoutUtility.GetRect(position.width - 20, 6);
            EditorGUI.DrawRect(barRect, ColCard);
            float progress = _selectedClip.length > 0 ? _currentTime / _selectedClip.length : 0f;
            var fillRect = new Rect(barRect.x, barRect.y, barRect.width * progress, barRect.height);
            EditorGUI.DrawRect(fillRect, _isPlaying ? ColGreen : ColAccent);
            GUILayout.Space(10);
        }
    }

    private void DrawClipInfo()
    {
        if (_selectedClip == null) return;

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(10);
            using (new EditorGUILayout.VerticalScope(_infoBoxStyle))
            {
                EditorGUILayout.Space(6);
                GUILayout.Label($"📋  {_selectedClip.name}", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                DrawInfoRow("Duração",    $"{_selectedClip.length:F3}s");
                DrawInfoRow("Frame Rate", $"{_selectedClip.frameRate:F0} fps");
                DrawInfoRow("Loop",       _selectedClip.isLooping ? "✅ Sim" : "❌ Não");
                DrawInfoRow("Tempo",      $"{_currentTime:F3}s / {_selectedClip.length:F3}s");
                DrawInfoRow("Frame",      $"{Mathf.RoundToInt(_currentTime * _selectedClip.frameRate)} / {Mathf.RoundToInt(_selectedClip.length * _selectedClip.frameRate)}");

                // Status do avatar
                EditorGUILayout.Space(4);
                if (_targetObject == null)
                {
                    var prevColor = GUI.color;
                    GUI.color = ColOrange;
                    GUILayout.Label("⚠️  Nenhum avatar selecionado", EditorStyles.miniLabel);
                    GUI.color = prevColor;
                }
                else
                {
                    var prevColor = GUI.color;
                    GUI.color = ColGreen;
                    GUILayout.Label($"✅  Preview em: {_targetObject.name}", EditorStyles.miniLabel);
                    GUI.color = prevColor;
                }
                EditorGUILayout.Space(6);
            }
            GUILayout.Space(10);
        }
    }

    private void DrawInfoRow(string label, string value)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(4);
            GUILayout.Label(label + ":", EditorStyles.miniLabel, GUILayout.Width(80));
            GUILayout.Label(value, EditorStyles.boldLabel);
        }
    }

    // ── Lógica de preview ─────────────────────────────────────────────────────

    private void SelectClip(AnimationClip clip)
    {
        bool wasPlaying = _isPlaying;
        StopPreview();

        _selectedClip = clip;
        _currentTime  = 0f;

        DisablePhysicsComponents();
        SampleCurrentClip(); // Mostra frame 0 imediatamente

        if (wasPlaying) TogglePlay();
    }

    private void TogglePlay()
    {
        if (_selectedClip == null || _targetObject == null) return;

        _isPlaying = !_isPlaying;

        if (_isPlaying)
        {
            _lastEditorTime = EditorApplication.timeSinceStartup;
            if (_currentTime >= _selectedClip.length && !_selectedClip.isLooping)
                _currentTime = 0f;

            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();
        }
    }

    private void SampleCurrentClip()
    {
        if (_selectedClip == null || _targetObject == null) return;

        if (!AnimationMode.InAnimationMode())
            AnimationMode.StartAnimationMode();

        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(_targetObject, _selectedClip, _currentTime);
        AnimationMode.EndSampling();

        SceneView.RepaintAll();
    }

    private void StopPreview()
    {
        _isPlaying = false;

        if (AnimationMode.InAnimationMode())
            AnimationMode.StopAnimationMode();

        RestorePhysicsComponents();
        SceneView.RepaintAll();
    }

    // ── Desativa CharacterController durante preview ───────────────────────────
    // O CC é um componente de física que conflita com AnimationMode:
    // enquanto o AnimationMode anima os bones, o CC tenta reposicionar o objeto,
    // causando o famoso "desnível" (personagem flutua ou fica deslocado).

    private void DisablePhysicsComponents()
    {
        if (_targetObject == null) return;

        _disabledCC = _targetObject.GetComponentInChildren<CharacterController>();
        if (_disabledCC != null)
        {
            _ccWasEnabled    = _disabledCC.enabled;
            _disabledCC.enabled = false;
        }
    }

    private void RestorePhysicsComponents()
    {
        if (_disabledCC != null)
        {
            _disabledCC.enabled = _ccWasEnabled;
            _disabledCC         = null;
        }
    }

    // ── Inicialização de estilos ──────────────────────────────────────────────

    private void InitStyles()
    {
        if (_stylesInitialized) return;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 14,
            alignment = TextAnchor.MiddleLeft,
            normal    = { textColor = ColAccent }
        };

        _clipButtonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize  = 11,
            normal    = { background = MakeTex(ColCard) }
        };

        _selectedClipButtonStyle = new GUIStyle(_clipButtonStyle)
        {
            fontStyle = FontStyle.Bold,
            normal    = { background = MakeTex(ColSelected), textColor = Color.white }
        };

        _infoBoxStyle = new GUIStyle(GUI.skin.box)
        {
            normal    = { background = MakeTex(ColCard) },
            padding   = new RectOffset(8, 8, 4, 4)
        };

        _stylesInitialized = true;
    }

    private static Texture2D MakeTex(Color col)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, col);
        tex.Apply();
        return tex;
    }
}
