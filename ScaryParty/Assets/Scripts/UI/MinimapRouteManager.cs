using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the drawing of colored routes on the minimap for pizza deliveries.
/// Also handles toggling the minimap between HUD mode (small corner) and fullscreen mode (M key).
/// Call TrackPlayer() with the local player transform so the map follows the player.
/// </summary>
public class MinimapRouteManager : MonoBehaviour
{
    public static MinimapRouteManager Instance { get; private set; }

    [Header("Route Settings")]
    public float lineWidth = 2f;
    public float lineElevation = 15f;

    [Header("Minimap Settings")]
    [Tooltip("The WorldImage component used for the minimap camera")]
    public Kamgam.UGUIWorldImage.WorldImage worldImage;
    [Tooltip("Canvas Group wrapping the minimap HUD (small corner map)")]
    public CanvasGroup hudCanvasGroup;
    [Tooltip("Orthographic size when minimap is small (HUD mode)")]
    public float hudOrthoSize = 60f;
    [Tooltip("Orthographic size when minimap is fullscreen (M mode)")]
    public float fullscreenOrthoSize = 200f;

    [Header("Player Icon")]
    [Tooltip("A simple sprite/quad to represent the player on the map")]
    public GameObject playerIconPrefab;

    private CityGraphPathfinder _pathfinder;
    private Dictionary<string, GameObject> _activeRoutes = new Dictionary<string, GameObject>();
    private Dictionary<string, Color> _routeColors = new Dictionary<string, Color>();

    private Color[] _availableColors = new Color[]
    {
        Color.cyan,
        Color.magenta,
        Color.yellow,
        Color.green,
        new Color(1f, 0.5f, 0f), // Orange
        new Color(0.5f, 0f, 1f)  // Purple
    };

    private Transform _playerTransform;
    private GameObject _playerIconInstance;
    private bool _isFullscreen = false;

    private Vector3 _panOffset = Vector3.zero;
    public bool IsFullscreen => _isFullscreen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Try to auto-find the WorldImage if not assigned
        if (worldImage == null)
            worldImage = FindObjectOfType<Kamgam.UGUIWorldImage.WorldImage>();

        if (hudCanvasGroup == null && worldImage != null)
            hudCanvasGroup = worldImage.GetComponentInParent<CanvasGroup>();

        // Start with minimap hidden until a player is tracked
        SetMinimapVisible(false);
    }

    public void PanMap(Vector2 screenDelta)
    {
        if (!_isFullscreen) return;

        // Convert UI pixel delta to world units based on ortho size.
        // Screen Y moves the camera along World Z. Screen X moves camera along World X.
        // To drag the map intuitively: mouse right = map right = camera LEFT.
        float sensitivity = fullscreenOrthoSize / 400f; 
        _panOffset.x -= screenDelta.x * sensitivity;
        _panOffset.z += screenDelta.y * sensitivity; 
    }

    private void Update()
    {
        // Follow player with minimap camera
        if (_playerTransform != null && worldImage != null)
        {
            if (!_isFullscreen)
            {
                _panOffset = Vector3.zero; // Recenter correctly when returning to HUD
            }
            worldImage.CameraLookAtPosition = _playerTransform.position + _panOffset;
        }

        // Update player icon position and rotation
        if (_playerIconInstance != null && _playerTransform != null)
        {
            Vector3 iconPos = _playerTransform.position + Vector3.up * lineElevation;
            _playerIconInstance.transform.position = iconPos;
            // Rotate icon to match player facing on XZ plane
            _playerIconInstance.transform.rotation = Quaternion.Euler(0f, _playerTransform.eulerAngles.y, 0f);
        }

        // Toggle fullscreen with M key
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleFullscreen();
        }
    }

    public void Initialize(CityGraphPathfinder pathfinder)
    {
        _pathfinder = pathfinder;
    }

    /// <summary>
    /// Call this from the player's OnNetworkSpawn so the minimap follows the local player.
    /// </summary>
    public void TrackPlayer(Transform playerTransform)
    {
        _playerTransform = playerTransform;

        // Show the minimap now that we have a player
        SetMinimapVisible(true);

        // Create player icon
        if (_playerIconInstance != null)
            Destroy(_playerIconInstance);

        if (playerIconPrefab != null)
        {
            _playerIconInstance = Instantiate(playerIconPrefab);
            _playerIconInstance.name = "PlayerMinimapIcon";
            SetLayerRecursively(_playerIconInstance, LayerMask.NameToLayer("MinimapOnly"));
        }
        else
        {
            // Create a nice Arrow Mesh instead of a cylinder
            _playerIconInstance = new GameObject("PlayerMinimapIcon");
            MeshFilter mf = _playerIconInstance.AddComponent<MeshFilter>();
            MeshRenderer rend = _playerIconInstance.AddComponent<MeshRenderer>();
            
            Mesh arrowMesh = new Mesh();
            arrowMesh.vertices = new Vector3[] {
                new Vector3(0, 0, 1.5f),   // tip
                new Vector3(1f, 0, -1f),   // right back
                new Vector3(0, 0, -0.5f),  // center back indentation
                new Vector3(-1f, 0, -1f)   // left back
            };
            arrowMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            arrowMesh.RecalculateNormals();
            mf.mesh = arrowMesh;
            
            _playerIconInstance.transform.localScale = new Vector3(8f, 1f, 8f);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.blue);
            else if (mat.HasProperty("_Color")) mat.color = Color.blue;
            rend.sharedMaterial = mat;
            
            SetLayerRecursively(_playerIconInstance, LayerMask.NameToLayer("MinimapOnly"));
        }

        Debug.Log("[MINIMAP] Now tracking player: " + playerTransform.name);
    }

    public void ToggleFullscreen()
    {
        _isFullscreen = !_isFullscreen;

        if (worldImage != null)
        {
            worldImage.CameraOrthographicSize = _isFullscreen ? fullscreenOrthoSize : hudOrthoSize;
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ToggleMinimapFullscreen(_isFullscreen);
        }

        Debug.Log($"[MINIMAP] Toggled to {(_isFullscreen ? "FULLSCREEN" : "HUD")} mode.");
    }

    private void SetMinimapVisible(bool visible)
    {
        if (hudCanvasGroup != null)
        {
            hudCanvasGroup.alpha = visible ? 1f : 0f;
            hudCanvasGroup.interactable = false;
            hudCanvasGroup.blocksRaycasts = false;
        }
    }

    private void SetLayerRecursively(GameObject go, int layer)
    {
        if (layer < 0) return; // Layer not found, skip
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    /// <summary>Creates a route on the minimap. Returns the assigned color.</summary>
    public Color CreateRoute(string orderId, Vector3 startPos, Vector3 endPos)
    {
        if (_activeRoutes.ContainsKey(orderId))
            RemoveRoute(orderId);

        Color routeColor = _availableColors[_activeRoutes.Count % _availableColors.Length];
        _routeColors[orderId] = routeColor;

        if (_pathfinder == null) return routeColor;

        List<Vector3> path = _pathfinder.FindPath(startPos, endPos);
        if (path == null || path.Count == 0) return routeColor;

        GameObject routeObj = new GameObject($"Route_{orderId}");
        routeObj.transform.SetParent(transform);
        routeObj.layer = LayerMask.NameToLayer("MinimapOnly");

        LineRenderer lr = routeObj.AddComponent<LineRenderer>();
        lr.positionCount = path.Count;
        lr.startWidth = 5f; // Thicker lines
        lr.endWidth = 5f;
        lr.numCornerVertices = 5; // Smooth corners
        lr.numCapVertices = 5; // Rounded ends
        lr.useWorldSpace = true;

        // Use Hidden/Internal-Colored which perfectly supports LineRenderer vertex colors in URP/Standard
        Shader lineShader = Shader.Find("Hidden/Internal-Colored");
        if (lineShader == null) lineShader = Shader.Find("Sprites/Default");
        Material mat = new Material(lineShader);
        if (mat.HasProperty("_Color")) mat.color = routeColor;
        lr.material = mat;
        
        // This is what actually colors the line when using Internal-Colored or Sprites/Default
        lr.startColor = routeColor;
        lr.endColor = routeColor;

        for (int i = 0; i < path.Count; i++)
            lr.SetPosition(i, path[i] + Vector3.up * lineElevation);

        _activeRoutes[orderId] = routeObj;
        return routeColor;
    }

    /// <summary>Removes a route from the minimap.</summary>
    public void RemoveRoute(string orderId)
    {
        if (_activeRoutes.TryGetValue(orderId, out GameObject routeObj))
        {
            if (routeObj != null) Destroy(routeObj);
            _activeRoutes.Remove(orderId);
            _routeColors.Remove(orderId);
        }
    }

    /// <summary>Get the color associated with an order.</summary>
    public Color GetOrderColor(string orderId)
    {
        if (_routeColors.TryGetValue(orderId, out Color color))
            return color;
        return Color.white;
    }
}
