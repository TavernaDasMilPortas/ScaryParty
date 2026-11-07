using UnityEngine;
using UnityEngine.UIElements;

public class InGameLobbyUIController : MonoBehaviour
{
    private void Awake()
    {
        // The ready room owns the lobby UI. Keep any shared network components on this object.
        var doc = GetComponent<UIDocument>();
        if (doc != null)
        {
            doc.enabled = false;
            Destroy(doc);
        }
        Destroy(this);
    }
}
