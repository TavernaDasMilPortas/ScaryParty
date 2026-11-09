using ScaryParty.Pizzeria.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace ScaryParty.Pizzeria.Editor
{
    [CustomEditor(typeof(KitchenProcessEffects))]
    public sealed class KitchenProcessEffectsEditor : UnityEditor.Editor
    {
        private GameObject _preview;
        private double _lastTime;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Preview at the Scene view pivot. Adjust Effect Scale and restart to compare. No gameplay required.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Preview VFX"))
                {
                    StopPreview();
                    var view = SceneView.lastActiveSceneView;
                    var position = view != null ? view.pivot : ((KitchenProcessEffects)target).transform.position;
                    _preview = ((KitchenProcessEffects)target).CreateEffectPreview(position);
                    foreach (var tr in _preview.GetComponentsInChildren<Transform>(true))
                        tr.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    _lastTime = EditorApplication.timeSinceStartup;
                    EditorApplication.update += TickPreview;
                }
                if (GUILayout.Button("Stop Preview")) StopPreview();
            }
        }

        private void TickPreview()
        {
            if (_preview == null || Application.isPlaying)
            {
                StopPreview();
                return;
            }
            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Clamp((float)(now - _lastTime), 0f, 0.1f);
            _lastTime = now;
            foreach (var effect in _preview.GetComponentsInChildren<VisualEffect>())
                effect.Simulate(delta, 1);
            SceneView.RepaintAll();
        }

        private void StopPreview()
        {
            EditorApplication.update -= TickPreview;
            if (_preview != null) DestroyImmediate(_preview);
            _preview = null;
        }

        private void OnDisable() => StopPreview();
    }
}