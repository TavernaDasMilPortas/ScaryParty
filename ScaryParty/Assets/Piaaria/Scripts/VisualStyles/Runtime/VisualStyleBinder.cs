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
        private VisualStyleManager _subscribedManager;

        private void Awake()
        {
            if (applyOnAwake)
            {
                ApplyNow();
            }
        }

        private void OnEnable()
        {
            VisualStyleManager.OnActiveManagerChanged += HandleActiveManagerChanged;
            TrySubscribe();
            if (_subscribed)
            {
                _subscribedManager = VisualStyleManager.Active;
            }
            if (applyOnAwake && VisualStyleManager.Active != null)
            {
                ApplyNow();
            }
        }

        private void OnDisable()
        {
            VisualStyleManager.OnActiveManagerChanged -= HandleActiveManagerChanged;
            var targetMgr = _subscribedManager != null ? _subscribedManager : VisualStyleManager.Active;
            if (_subscribed && targetMgr != null)
            {
                targetMgr.OnStyleChanged -= HandleStyleChanged;
            }
            _subscribed = false;
            _subscribedManager = null;
        }

        private void HandleActiveManagerChanged(VisualStyleManager mgr)
        {
            if (_subscribed && _subscribedManager != null && _subscribedManager != mgr)
            {
                _subscribedManager.OnStyleChanged -= HandleStyleChanged;
                _subscribed = false;
            }
            TrySubscribe();
            if (_subscribed)
            {
                _subscribedManager = VisualStyleManager.Active;
            }
            if (applyOnAwake)
            {
                ApplyNow();
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
