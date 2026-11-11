using UnityEngine;

namespace ScaryParty.VisualStyles
{
    /// <summary>
    /// Attaches to the visual hierarchy of prefabs without dedicated factories and binds
    /// materials from the active VisualStyleManager on initialization or style swap.
    /// </summary>
    [DisallowMultipleComponent]
    public class VisualStyleBinder : MonoBehaviour
    {
        public string contextKey;
        public VisualSemanticRole defaultRole = VisualSemanticRole.Unclassified;
        public string defaultCategoryId = VisualCategoryIds.Props;
        public bool includeInactiveChildren = true;
        public bool applyOnAwake = true;
        public bool listenForStyleChanges = true;

        private bool _subscribed;

        private void Awake()
        {
            if (applyOnAwake)
            {
                ApplyNow();
            }
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (_subscribed && VisualStyleManager.Active != null)
            {
                VisualStyleManager.Active.OnStyleChanged -= HandleStyleChanged;
                _subscribed = false;
            }
        }

        public void ApplyNow()
        {
            var manager = VisualStyleManager.Active;
            if (manager == null || manager.Resolver == null) return;
            manager.Resolver.ApplyToHierarchy(gameObject, contextKey, defaultRole, defaultCategoryId, includeInactiveChildren, recordOriginal: true);
        }

        private void TrySubscribe()
        {
            if (!listenForStyleChanges || _subscribed || VisualStyleManager.Active == null) return;
            VisualStyleManager.Active.OnStyleChanged += HandleStyleChanged;
            _subscribed = true;
        }

        private void HandleStyleChanged(VisualStyleManager manager)
        {
            if (manager == null || manager.Resolver == null) return;
            manager.Resolver.ApplyToHierarchy(gameObject, contextKey, defaultRole, defaultCategoryId, includeInactiveChildren, recordOriginal: true);
        }
    }
}
