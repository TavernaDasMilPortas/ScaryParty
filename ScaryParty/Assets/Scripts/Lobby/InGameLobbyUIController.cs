using UnityEngine;
using UnityEngine.UIElements;

public class InGameLobbyUIController : MonoBehaviour
{
    private void Awake()
    {
        // Script obsoleto. O lobby de espera foi movido para a cena 'ReadyScene'.
        // Oculta e destrói imediatamente para garantir que nenhum resquício visual persista na GameScene.
        var doc = GetComponent<UIDocument>();
        if (doc != null)
        {
            if (doc.rootVisualElement != null)
            {
                doc.rootVisualElement.style.display = DisplayStyle.None;
            }
            doc.enabled = false;
        }

        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
