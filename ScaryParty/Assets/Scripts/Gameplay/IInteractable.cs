using UnityEngine;

/// <summary>
/// Interface for objects the player can interact with.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// The prompt shown on the UI when looking at this object.
    /// </summary>
    string InteractPrompt { get; }

    /// <summary>
    /// Called when the player presses the interact button while looking at this object.
    /// </summary>
    void OnInteract(GameObject player);

    /// <summary>
    /// Called when the player looks at the object.
    /// </summary>
    void OnFocus();

    /// <summary>
    /// Called when the player stops looking at the object.
    /// </summary>
    void OnLoseFocus();
}
