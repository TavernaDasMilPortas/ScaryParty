using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.UI.Piaria.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Owns the city camera, viewport transform and cached UI-only routes.</summary>
public class MinimapRouteManager : MonoBehaviour
{
    public static MinimapRouteManager Instance { get; private set; }
    public Kamgam.UGUIWorldImage.WorldImage worldImage;
    public float hudOrthoSize = 60f;
    public float fullscreenOrthoSize = 200f;
    public bool IsFullscreen => _expanded;
    public bool IsCityReady => _hasBounds;
    public bool IsOverview => _expanded && _hasBounds && _ortho >= _fitOrtho * .8f;
    public Vector3 Center => _center;
    public Transform LocalPlayerTransform => _localPlayer;
    public CityGraphPathfinder Pathfinder => _pathfinder;
    public IReadOnlyDictionary<string, List<Vector3>> Routes => _routes;
    public IReadOnlyDictionary<ulong, PlayerState> Players => _players;

    private sealed class RouteInfo
    {
        public Vector3 Start, End, LastStart, LastEnd;
        public ulong Owner;
        public int Revision = -1;
        public float NextUpdate;
    }

    private readonly Dictionary<string, List<Vector3>> _routes = new Dictionary<string, List<Vector3>>();
    private readonly Dictionary<string, RouteInfo> _routeInfo = new Dictionary<string, RouteInfo>();
    private readonly Dictionary<string, Color> _colors = new Dictionary<string, Color>();
    private readonly Dictionary<ulong, PlayerState> _players = new Dictionary<ulong, PlayerState>();
    private readonly List<string> _routeKeys = new List<string>();
    private CityGraphPathfinder _pathfinder;
    private CityGenerator _city;
    private Transform _cameraOrigin;
    private Transform _localPlayer;
    private Vector3 _center;
    private Vector2 _min, _max, _viewport = new Vector2(800f, 600f);
    private float _ortho, _fitOrtho, _aspect = 1f, _nextDiscovery, _nextPlayers;
    private int _routeCursor;
    private bool _expanded, _hasBounds, _ownsGate;
    private float _cameraHeight = 200f;
    private static readonly Color[] Palette = { Color.cyan, Color.yellow, Color.green, new Color(1f, .55f, .2f), Color.magenta };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (worldImage == null) worldImage = FindFirstObjectByType<Kamgam.UGUIWorldImage.WorldImage>();
    }

    private void OnDisable()
    {
        if (_expanded) CloseMap();
        DisconnectCity();
        if (worldImage != null && worldImage.CameraOrigin == _cameraOrigin) worldImage.CameraOrigin = null;
    }

    private void OnDestroy()
    {
        DisconnectCity();
        if (worldImage != null && worldImage.CameraOrigin == _cameraOrigin) worldImage.CameraOrigin = null;
        if (_cameraOrigin != null) Destroy(_cameraOrigin.gameObject);
        if (Instance == this) Instance = null;
    }

    private void DisconnectCity()
    {
        if (_city == null) return;
        _city.OnCityGenerated -= RefreshCity;
        _city.OnCityCleared -= ClearCity;
        _city = null;
    }

    private void Update()
    {
        if (_city == null && Time.unscaledTime >= _nextDiscovery)
        {
            _nextDiscovery = Time.unscaledTime + .5f;
            _city = FindFirstObjectByType<CityGenerator>();
            if (_city != null)
            {
                _city.OnCityGenerated += RefreshCity;
                _city.OnCityCleared += ClearCity;
                if (_city.CityData != null) RefreshCity();
            }
        }
        if (Time.unscaledTime >= _nextPlayers)
        {
            _nextPlayers = Time.unscaledTime + 1f;
            foreach (var player in FindObjectsByType<PlayerState>(FindObjectsSortMode.None))
                if (player.IsSpawned) RegisterPlayer(player);
            var stale = new List<ulong>();
            foreach (var pair in _players)
                if (pair.Value == null || !pair.Value.IsSpawned) stale.Add(pair.Key);
            foreach (var key in stale) _players.Remove(key);
        }
        if (_localPlayer == null && NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var player = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (player != null) _localPlayer = player.transform;
        }
        if (!_expanded && _localPlayer != null) _center = _localPlayer.position;
        bool mapKey, escapeKey;
#if ENABLE_INPUT_SYSTEM
        mapKey = Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
        escapeKey = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        mapKey = Input.GetKeyDown(KeyCode.M);
        escapeKey = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (mapKey && (_expanded || !GameplayInputGate.IsLocked)) ToggleFullscreen();
        else if (_expanded && escapeKey && UIModalStack.Instance == null) ToggleFullscreen();
        UpdateOneRoute();
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        if (worldImage == null) return;
        if (_cameraOrigin == null)
        {
            _cameraOrigin = new GameObject("MapCameraOrigin").transform;
            _cameraOrigin.SetParent(transform, false);
        }
        _cameraOrigin.position = new Vector3(_center.x, 0f, _center.z);
        if (worldImage.CameraOrigin != _cameraOrigin) worldImage.CameraOrigin = _cameraOrigin;
        worldImage.CameraOrthographic = true;
        worldImage.CameraLookAtPosition = Vector3.zero;
        worldImage.CameraOffset = new Vector3(0f, _cameraHeight, 0f);
        worldImage.CameraFollowBoundsCenter = false;
        worldImage.CameraUseBoundsToClip = false;
        worldImage.CameraOrthographicSize = _expanded ? _ortho : hudOrthoSize;
        worldImage.CameraFarClipPlane = Mathf.Max(750f, _cameraHeight + 100f);
        int uiLayer = LayerMask.NameToLayer("UI");
        worldImage.CameraCullingMask = uiLayer >= 0 ? ~(1 << uiLayer) : ~0;
        var camera = worldImage.ObjectCamera?.Camera;
        if (camera == null) return;
        camera.orthographic = true;
        camera.orthographicSize = _expanded ? _ortho : hudOrthoSize;
        camera.aspect = ImageAspect;
        camera.transform.SetPositionAndRotation(new Vector3(_center.x, _cameraHeight, _center.z), Quaternion.Euler(90f, 0f, 0f));
        camera.nearClipPlane = .3f;
        camera.farClipPlane = Mathf.Max(750f, _cameraHeight + 100f);
    }

    private float ImageAspect => worldImage != null && worldImage.RenderTexture != null && worldImage.RenderTexture.height > 0
        ? (float)worldImage.RenderTexture.width / worldImage.RenderTexture.height : _viewport.x / Mathf.Max(1f, _viewport.y);

    private void ClearCity()
    {
        _hasBounds = false;
        _pathfinder = null;
        _routes.Clear();
        _routeInfo.Clear();
        _routeKeys.Clear();
        _colors.Clear();
    }

    private void RefreshCity()
    {
        ClearCity();
        if (_city == null || _city.CityData == null) return;
        _pathfinder = _city.GetPathfinder();
        _min = new Vector2(float.MaxValue, float.MaxValue);
        _max = new Vector2(float.MinValue, float.MinValue);
        void Include(Vector3 p)
        {
            _min = Vector2.Min(_min, new Vector2(p.x, p.z));
            _max = Vector2.Max(_max, new Vector2(p.x, p.z));
        }
        var graph = _city.CityData.streetGraph;
        if (graph != null && graph.nodes != null)
            foreach (var node in graph.nodes) Include(node.worldPosition);
        if (_city.CityData.blocks != null)
            foreach (var block in _city.CityData.blocks)
                if (block.polygon != null) foreach (var vertex in block.polygon) Include(vertex);
        Include(_city.CityData.pizzariaPosition);
        foreach (var point in FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None)) Include(point.transform.position);
        float highest = 0f;
        foreach (var building in FindObjectsByType<CityBuilding>(FindObjectsSortMode.None))
            foreach (var renderer in building.GetComponentsInChildren<Renderer>())
                highest = Mathf.Max(highest, renderer.bounds.max.y);
        _cameraHeight = Mathf.Max(200f, highest + 60f);
        _hasBounds = _min.x <= _max.x;
        if (_hasBounds && _expanded) FitCity();
    }

    public void FitCity()
    {
        if (!_hasBounds) return;
        _center = new Vector3((_min.x + _max.x) * .5f, 0f, (_min.y + _max.y) * .5f);
        _ortho = Mathf.Max((_max.y - _min.y) * .5f, (_max.x - _min.x) / (2f * Mathf.Max(.1f, ImageAspect))) * 1.12f + 12f;
        _fitOrtho = _ortho;
    }

    public void CenterOn(Vector3 point) { _center = point; ClampCenter(); }
    public void CenterOnPlayer() { if (_localPlayer != null) CenterOn(_localPlayer.position); }
    public void SetViewport(float width, float height) => SetViewportSize(new Vector2(width, height));
    public void SetViewportSize(Vector2 size)
    {
        if (size.x <= 0 || size.y <= 0) return;
        _viewport = size;
        float aspect = ImageAspect;
        if (Mathf.Abs(_aspect - aspect) > .001f)
        {
            _aspect = aspect;
            if (_expanded && _hasBounds) FitCity();
        }
    }

    public Vector2 Project(Vector3 point, float width, float height)
    {
        float size = _expanded ? _ortho : hudOrthoSize;
        float aspect = ImageAspect;
        return new Vector2(width * (.5f + (point.x - _center.x) / (2f * size * aspect)),
            height * (.5f - (point.z - _center.z) / (2f * size)));
    }

    public void Zoom(float wheel, Vector2 cursor, Vector2 viewport)
    {
        if (!_expanded || !_hasBounds || viewport.x <= 0 || viewport.y <= 0) return;
        float aspect = ImageAspect;
        var before = new Vector2(_center.x + (cursor.x / viewport.x - .5f) * 2f * _ortho * aspect,
            _center.z + (.5f - cursor.y / viewport.y) * 2f * _ortho);
        float max = Mathf.Max(40f, Mathf.Max(_max.y - _min.y, (_max.x - _min.x) / aspect) * 1.5f);
        _ortho = Mathf.Clamp(_ortho * Mathf.Pow(1.15f, wheel), 12f, max);
        _center.x = before.x - (cursor.x / viewport.x - .5f) * 2f * _ortho * aspect;
        _center.z = before.y - (.5f - cursor.y / viewport.y) * 2f * _ortho;
        ClampCenter();
    }

    public void PanMap(Vector2 delta)
    {
        if (!_expanded) return;
        _center.x -= delta.x * 2f * _ortho * ImageAspect / Mathf.Max(1f, _viewport.x);
        _center.z += delta.y * 2f * _ortho / Mathf.Max(1f, _viewport.y);
        ClampCenter();
    }

    private void ClampCenter()
    {
        if (!_hasBounds) return;
        float x = _ortho * ImageAspect, z = _ortho;
        _center.x = _max.x - _min.x <= 2f * x ? (_min.x + _max.x) * .5f : Mathf.Clamp(_center.x, _min.x + x, _max.x - x);
        _center.z = _max.y - _min.y <= 2f * z ? (_min.y + _max.y) * .5f : Mathf.Clamp(_center.z, _min.y + z, _max.y - z);
    }

    public void RegisterPlayer(PlayerState player)
    {
        if (player == null || !player.IsSpawned) return;
        _players[player.NetworkObjectId] = player;
        if (player.IsOwner) _localPlayer = player.transform;
    }
    public void UnregisterPlayer(ulong objectId)
    {
        if (_players.TryGetValue(objectId, out var player))
        {
            if (player != null && _localPlayer == player.transform) _localPlayer = null;
            _players.Remove(objectId);
        }
    }
    public void TrackPlayer(Transform player) => _localPlayer = player;
    public void Initialize(CityGraphPathfinder pathfinder) { _pathfinder = pathfinder; }

    public void ToggleFullscreen()
    {
        if (_expanded) { CloseMap(); return; }
        if (GameplayInputGate.IsLocked) return;
        _expanded = true;
        FitCity();
        var presenter = ScaryParty.UI.Piaria.Exceptions.MinimapPresenter.Instance;
        if (UIModalStack.Instance != null && presenter != null) UIModalStack.Instance.PushModal(presenter);
        else { GameplayInputGate.PushModal(); _ownsGate = true; }
        presenter?.ToggleFullscreen(true);
    }

    private void CloseMap()
    {
        _expanded = false;
        var presenter = ScaryParty.UI.Piaria.Exceptions.MinimapPresenter.Instance;
        presenter?.ToggleFullscreen(false);
        if (UIModalStack.Instance != null && presenter != null) UIModalStack.Instance.PopModal(presenter);
        if (_ownsGate) { GameplayInputGate.PopModal(); _ownsGate = false; }
    }

    public Color CreateRoute(string id, Vector3 start, Vector3 end) => CreateRoute(id, start, end, ulong.MaxValue);
    public Color CreateRoute(string id, Vector3 start, Vector3 end, ulong owner)
    {
        if (!_colors.ContainsKey(id)) _colors[id] = Palette[_colors.Count % Palette.Length];
        bool isNew = false;
        if (!_routeInfo.TryGetValue(id, out var info))
        {
            info = new RouteInfo();
            _routeInfo[id] = info;
            _routeKeys.Add(id);
            _routes[id] = new List<Vector3>();
            isNew = true;
        }
        bool endpointChanged = isNew || (info.End - end).sqrMagnitude > 0.25f;
        info.Owner = owner;
        info.Start = start;
        info.End = end;
        if (endpointChanged || !_routes.TryGetValue(id, out var currentPath) || currentPath == null || currentPath.Count < 2)
        {
            ComputeRouteNow(id, info);
        }
        return _colors[id];
    }

    private void ComputeRouteNow(string key, RouteInfo info)
    {
        if (_pathfinder != null && _pathfinder.IsReady)
        {
            var path = _pathfinder.FindPath(info.Start, info.End);
            if (path != null && path.Count >= 2)
            {
                _routes[key] = path;
                info.LastStart = info.Start;
                info.LastEnd = info.End;
                info.NextUpdate = Time.time + .35f;
                info.Revision = _pathfinder.Revision;
                return;
            }
        }
        _routes[key] = new List<Vector3> { info.Start, info.End };
        info.LastStart = info.Start;
        info.LastEnd = info.End;
        info.NextUpdate = Time.time + .2f;
        info.Revision = -1;
    }

    private void UpdateOneRoute()
    {
        if (_routeKeys.Count == 0) return;
        if (_pathfinder == null && _city != null) _pathfinder = _city.GetPathfinder();
        for (int i = 0; i < _routeKeys.Count; i++)
        {
            if (_routeCursor >= _routeKeys.Count) _routeCursor = 0;
            string key = _routeKeys[_routeCursor++];
            var info = _routeInfo[key];
            if (info.Owner != ulong.MaxValue)
            {
                if (_localPlayer != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
                    info.Owner == NetworkManager.Singleton.LocalClientId)
                {
                    info.Start = _localPlayer.position;
                }
                else
                {
                    foreach (var player in _players.Values)
                        if (player != null && player.OwnerClientId == info.Owner) { info.Start = player.transform.position; break; }
                }
            }
            int currentRev = _pathfinder != null && _pathfinder.IsReady ? _pathfinder.Revision : -1;
            if (Time.time < info.NextUpdate && info.Revision == currentRev &&
                (info.Start - info.LastStart).sqrMagnitude < 4f && (info.End - info.LastEnd).sqrMagnitude < 1f) continue;
            ComputeRouteNow(key, info);
            break;
        }
    }

    public void RemoveRoute(string id)
    {
        _routes.Remove(id);
        _routeInfo.Remove(id);
        _routeKeys.Remove(id);
        _colors.Remove(id);
    }
    public Color GetOrderColor(string id) => _colors.TryGetValue(id, out var color) ? color : Color.white;
}
