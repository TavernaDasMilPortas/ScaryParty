using UnityEngine;

/// <summary>
/// Adds a visual highlight to interactable objects when the player looks at them.
/// Tints the material yellow/emissive.
/// </summary>
public class InteractableHighlight : MonoBehaviour
{
    private Material[] _originalMaterials;
    private Material[] _highlightMaterials;
    private Renderer _renderer;

    private void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
        if (_renderer != null)
        {
            _originalMaterials = _renderer.sharedMaterials;
            _highlightMaterials = new Material[_originalMaterials.Length];
            
            for (int i = 0; i < _originalMaterials.Length; i++)
            {
                if (_originalMaterials[i] == null) continue;

                // Create a duplicate material and make it bright/emissive
                Material mat = new Material(_originalMaterials[i]);
                
                // Try URP / Standard properties
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", Color.yellow);
                else if (mat.HasProperty("_Color"))
                    mat.color = Color.yellow;
                
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", Color.yellow * 0.5f);
                }
                
                _highlightMaterials[i] = mat;
            }
        }
    }

    public void EnableHighlight()
    {
        if (_renderer != null && _highlightMaterials != null) 
        {
            _renderer.sharedMaterials = _highlightMaterials;
            // Also pulse scale slightly
            transform.localScale = Vector3.one * 1.1f;
        }
    }

    public void DisableHighlight()
    {
        if (_renderer != null && _originalMaterials != null) 
        {
            _renderer.sharedMaterials = _originalMaterials;
            // Restore scale
            transform.localScale = Vector3.one;
        }
    }
}
