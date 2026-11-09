using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UIElements;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.UI.Piaria.Core;
using ScaryParty.UI.Piaria.Presenters;

namespace ScaryParty.UI.Piaria.Exceptions
{
    /// <summary>City image annotations and route overlays, entirely inside UI Toolkit.</summary>
    public class MinimapPresenter : MonoBehaviour, IModalView
    {
        public static MinimapPresenter Instance { get; private set; }
        public Kamgam.UGUIWorldImage.WorldImage worldImage;
        public MinimapRouteManager routeManager;
        public bool CanCloseWithEscape => true;
        public VisualElement RootElement => _container;
        public void Close() { if (routeManager != null && routeManager.IsFullscreen) routeManager.ToggleFullscreen(); }

        private sealed class MarkerEntry
        {
            public VisualElement Root;
            public VisualElement Icon;
            public Label Text;
        }

        private sealed class RouteView
        {
            public int Address, Count;
            public ulong Owner;
            public Color Color;
        }

        private sealed class MapLines : VisualElement
        {
            public MinimapPresenter presenter;
            public MapLines() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
            private void Draw(MeshGenerationContext context)
            {
                var map = presenter?.routeManager;
                if (map == null) return;
                var painter = context.painter2D;
                foreach (var route in map.Routes)
                {
                    if (route.Value == null || route.Value.Count < 2) continue;
                    if (!presenter._cargoRoutes.TryGetValue(route.Key, out var cargo)) continue;
                    painter.lineWidth = map.IsFullscreen ? 4.5f : 3f;
                    painter.strokeColor = cargo.Color;
                    painter.BeginPath();
                    bool first = true;
                    foreach (var point in route.Value)
                    {
                        var p = map.Project(point, resolvedStyle.width, resolvedStyle.height);
                        if (first) { painter.MoveTo(p); first = false; } else painter.LineTo(p);
                    }
                    painter.Stroke();
                }
            }
        }

        private VisualElement _image, _container, _markers;
        private MapLines _lines;
        private readonly Dictionary<string, MarkerEntry> _markerCache = new Dictionary<string, MarkerEntry>();
        private readonly Dictionary<int, DeliveryPoint> _points = new Dictionary<int, DeliveryPoint>();
        private readonly Dictionary<int, Vector3> _addressPositions = new Dictionary<int, Vector3>();
        private readonly Dictionary<string, RouteView> _cargoRoutes = new Dictionary<string, RouteView>();
        private readonly HashSet<string> _liveMarkers = new HashSet<string>();
        private readonly HashSet<string> _activeRoutes = new HashSet<string>();
        private readonly HashSet<ulong> _seenItems = new HashSet<ulong>();
        private readonly List<string> _stale = new List<string>();
        private readonly List<Vector2> _occupiedPositions = new List<Vector2>();
        private RenderTexture _texture;
        private CityGenerator _city;
        private bool _drag;
        private int _pointer, _hoveredAddress = -1;
        private Vector2 _last;
        private float _refresh, _nextDiscovery;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (worldImage == null) worldImage = FindFirstObjectByType<Kamgam.UGUIWorldImage.WorldImage>();
            if (routeManager == null) routeManager = MinimapRouteManager.Instance;
        }

        private void OnDisable()
        {
            Close();
            UnbindUI();
            DisconnectCity();
        }

        private void OnDestroy()
        {
            UnbindUI();
            DisconnectCity();
            if (Instance == this) Instance = null;
        }

        private void DisconnectCity()
        {
            if (_city == null) return;
            _city.OnCityGenerated -= RefreshAddresses;
            _city.OnCityCleared -= ClearAddresses;
            _city = null;
        }

        private void UnbindUI()
        {
            if (_image != null)
            {
                if (_drag && _image.HasPointerCapture(_pointer)) _image.ReleasePointer(_pointer);
                _image.UnregisterCallback<PointerDownEvent>(Down);
                _image.UnregisterCallback<PointerMoveEvent>(Move);
                _image.UnregisterCallback<PointerUpEvent>(Up);
                _image.UnregisterCallback<PointerCancelEvent>(Cancel);
                _image.UnregisterCallback<PointerCaptureOutEvent>(CaptureOut);
                _image.UnregisterCallback<WheelEvent>(Wheel);
                _lines?.RemoveFromHierarchy();
                _markers?.RemoveFromHierarchy();
            }
            _image = _container = _markers = null;
            _lines = null;
            _drag = false;
            _hoveredAddress = -1;
            _texture = null;
            _markerCache.Clear();
        }

        private void EnsureUI()
        {
            var hud = HudPresenter.Instance;
            hud?.EnsureInitialized();
            var root = hud?.uiDocument?.rootVisualElement;
            var image = root?.Q<VisualElement>("MinimapImage");
            if (_image == image && _image != null && _image.panel != null) return;
            UnbindUI();
            _image = image;
            _container = root?.Q<VisualElement>("MinimapContainer");
            if (_image == null || _container == null) return;
            _image.pickingMode = PickingMode.Position;
            _image.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            _image.style.overflow = Overflow.Hidden;
            _lines = new MapLines { presenter = this };
            _lines.style.position = Position.Absolute;
            _lines.style.left = _lines.style.top = _lines.style.right = _lines.style.bottom = 0;
            _image.Add(_lines);
            _markers = new VisualElement { pickingMode = PickingMode.Ignore };
            _markers.style.position = Position.Absolute;
            _markers.style.left = _markers.style.top = _markers.style.right = _markers.style.bottom = 0;
            _image.Add(_markers);
            _image.RegisterCallback<PointerDownEvent>(Down);
            _image.RegisterCallback<PointerMoveEvent>(Move);
            _image.RegisterCallback<PointerUpEvent>(Up);
            _image.RegisterCallback<PointerCancelEvent>(Cancel);
            _image.RegisterCallback<PointerCaptureOutEvent>(CaptureOut);
            _image.RegisterCallback<WheelEvent>(Wheel);
        }

        private void Update()
        {
            if (routeManager == null) routeManager = MinimapRouteManager.Instance;
            if (worldImage == null && Time.unscaledTime >= _nextDiscovery)
                worldImage = FindFirstObjectByType<Kamgam.UGUIWorldImage.WorldImage>();
            EnsureUI();
            if (_image == null || routeManager == null) return;
            var currentTexture = worldImage != null ? worldImage.RenderTexture : null;
            if (_texture != currentTexture)
            {
                _texture = currentTexture;
                if (_texture != null) _image.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_texture));
                else _image.style.backgroundImage = StyleKeyword.None;
            }
            if (_city == null && Time.unscaledTime >= _nextDiscovery)
            {
                _nextDiscovery = Time.unscaledTime + .5f;
                _city = FindFirstObjectByType<CityGenerator>();
                if (_city != null)
                {
                    _city.OnCityGenerated += RefreshAddresses;
                    _city.OnCityCleared += ClearAddresses;
                    if (_city.CityData != null) RefreshAddresses();
                }
            }
            var frame = _image.parent;
            if (routeManager.IsFullscreen && _container != null && _container.parent != null)
            {
                float rootW = _container.parent.resolvedStyle.width;
                float rootH = _container.parent.resolvedStyle.height;
                if (rootW > 0f && rootH > 0f)
                {
                    float side = Mathf.Round(Mathf.Min(rootW, rootH) * 0.78f);
                    _container.style.width = side;
                    _container.style.height = side;
                    _container.style.left = Mathf.Round((rootW - side) * 0.5f);
                    _container.style.top = Mathf.Round((rootH - side) * 0.5f);
                }
            }
            float frameWidth = frame?.resolvedStyle.width ?? 0f;
            float frameHeight = frame?.resolvedStyle.height ?? 0f;
            float imageWidth = frameWidth;
            float imageHeight = frameHeight;
            if (imageWidth <= 0 || imageHeight <= 0) return;
            _image.style.width = imageWidth;
            _image.style.height = imageHeight;
            _image.style.marginLeft = 0f;
            _image.style.marginTop = 0f;
            var size = new Vector2(imageWidth, imageHeight);
            routeManager.SetViewportSize(size);
            if (Time.time >= _refresh) { _refresh = Time.time + .25f; UpdateCargoRoutes(); }
            _lines?.MarkDirtyRepaint();
            UpdateMarkers(size);
        }

        private void RefreshAddresses()
        {
            _points.Clear();
            _addressPositions.Clear();
            foreach (var point in FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None))
            {
                if (point != null && point.AddressId >= 0)
                {
                    _points[point.AddressId] = point;
                    _addressPositions[point.AddressId] = point.transform.position;
                }
            }
            var root = PizzeriaRoot.Instance;
            if (root != null && root.AddressRegistry != null)
            {
                foreach (var addr in root.AddressRegistry.GetAllIds())
                {
                    if (addr.Value >= 0 && !_addressPositions.ContainsKey(addr.Value) &&
                        root.AddressRegistry.TryGetPosition(addr, out var dPos))
                    {
                        _addressPositions[addr.Value] = new Vector3(dPos.X, dPos.Y, dPos.Z);
                    }
                }
            }
            _refresh = 0f;
        }

        private void ClearAddresses()
        {
            _points.Clear();
            _addressPositions.Clear();
            foreach (var key in _cargoRoutes.Keys) routeManager?.RemoveRoute(key);
            _cargoRoutes.Clear();
            _hoveredAddress = -1;
            _refresh = 0f;
        }

        private bool TryResolveAddress(int rawDestId, out int resolvedAddressId, out Vector3 worldPos)
        {
            resolvedAddressId = rawDestId;
            worldPos = default;
            if (rawDestId < 0) return false;

            if (_addressPositions.Count == 0) RefreshAddresses();

            if (_points.TryGetValue(rawDestId, out var dp) && dp != null)
            {
                worldPos = dp.transform.position;
                _addressPositions[rawDestId] = worldPos;
                return true;
            }
            if (_addressPositions.TryGetValue(rawDestId, out worldPos))
                return true;

            var root = PizzeriaRoot.Instance;
            if (root != null && root.AddressRegistry != null &&
                root.AddressRegistry.TryGetPosition(new AddressId(rawDestId), out var dPos))
            {
                worldPos = new Vector3(dPos.X, dPos.Y, dPos.Z);
                _addressPositions[rawDestId] = worldPos;
                return true;
            }

            // Re-scan scene in case DeliveryPoints spawned after initial check
            RefreshAddresses();
            if (_addressPositions.TryGetValue(rawDestId, out worldPos))
                return true;

            // Fallback if a 1-based destination ID was stored (e.g., #8 -> index 7)
            if (rawDestId > 0 && _addressPositions.TryGetValue(rawDestId - 1, out worldPos))
            {
                resolvedAddressId = rawDestId - 1;
                return true;
            }

            return false;
        }

        private bool IsLocal(ulong owner) => NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening && owner == NetworkManager.Singleton.LocalClientId;

        private bool TryGetLocalPlayer(out ulong clientId, out Vector3 position)
        {
            clientId = 0;
            position = default;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                clientId = NetworkManager.Singleton.LocalClientId;
                if (routeManager != null && routeManager.LocalPlayerTransform != null)
                {
                    position = routeManager.LocalPlayerTransform.position;
                    return true;
                }
                var playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
                if (playerObj != null)
                {
                    position = playerObj.transform.position;
                    return true;
                }
            }
            if (routeManager != null)
            {
                foreach (var pair in routeManager.Players)
                {
                    var p = pair.Value;
                    if (p != null && p.IsSpawned && (p.IsOwner || IsLocal(p.OwnerClientId)))
                    {
                        clientId = p.OwnerClientId;
                        position = p.transform.position;
                        return true;
                    }
                }
            }
            return false;
        }

        private void UpdateCargoRoutes()
        {
            if (routeManager == null) return;
            if (_addressPositions.Count == 0) RefreshAddresses();

            _activeRoutes.Clear();
            _seenItems.Clear();

            var cargo = PizzeriaNetworkState.Instance;
            if (cargo != null && TryGetLocalPlayer(out ulong localClientId, out Vector3 playerPos))
            {
                var counts = new Dictionary<int, int>();
                var positions = new Dictionary<int, Vector3>();

                foreach (var item in CarriedCargoResolver.GetCarriedItems(cargo, localClientId))
                {
                    if (!_seenItems.Add(item.ItemId)) continue;
                    if (item.LabelDestinationId < 0) continue;

                    if (TryResolveAddress(item.LabelDestinationId, out int resolvedId, out Vector3 targetPos))
                    {
                        counts.TryGetValue(resolvedId, out int count);
                        counts[resolvedId] = count + 1;
                        positions[resolvedId] = targetPos;
                    }
                }

                foreach (var entry in counts)
                {
                    int addrId = entry.Key;
                    Vector3 targetPos = positions[addrId];
                    string key = "Cargo_" + localClientId + "_" + addrId;
                    _activeRoutes.Add(key);
                    var color = routeManager.CreateRoute(key, playerPos, targetPos, localClientId);
                    _cargoRoutes[key] = new RouteView
                    {
                        Address = addrId,
                        Count = entry.Value,
                        Owner = localClientId,
                        Color = color
                    };
                }
            }

            _stale.Clear();
            foreach (var key in _cargoRoutes.Keys)
                if (!_activeRoutes.Contains(key)) _stale.Add(key);
            foreach (var key in _stale)
            {
                routeManager.RemoveRoute(key);
                _cargoRoutes.Remove(key);
            }
        }

        private void UpdateMarkers(Vector2 size)
        {
            _liveMarkers.Clear();
            _occupiedPositions.Clear();

            if (_addressPositions.Count == 0 && _city?.CityData != null)
                RefreshAddresses();

            // 1. Reserve screen positions for loaded delivery points, Pizzaria, and Players so standard labels don't overlap them
            if (_city?.CityData != null)
            {
                var pPos = routeManager.Project(_city.CityData.pizzariaPosition, size.x, size.y);
                _occupiedPositions.Add(pPos);
            }

            foreach (var route in _cargoRoutes.Values)
            {
                if (_addressPositions.TryGetValue(route.Address, out var worldPos))
                {
                    var screenPos = routeManager.Project(worldPos, size.x, size.y);
                    _occupiedPositions.Add(screenPos);
                }
            }

            // 2. Render Delivery Points
            bool isCloseZoom = !routeManager.IsOverview;
            foreach (var kvp in _addressPositions)
            {
                int id = kvp.Key;
                Vector3 worldPos = kvp.Value;
                if (_points.TryGetValue(id, out var dp) && dp != null)
                {
                    worldPos = dp.transform.position;
                }

                int cargoCount = 0;
                foreach (var route in _cargoRoutes.Values)
                {
                    if (route.Address == id) cargoCount += route.Count;
                }

                bool hasCargo = cargoCount > 0;
                if (!routeManager.IsFullscreen && !hasCargo) continue;

                var screenPos = routeManager.Project(worldPos, size.x, size.y);
                bool isHovered = routeManager.IsFullscreen && _hoveredAddress == id;

                bool overlaps = false;
                if (!hasCargo && !isHovered)
                {
                    const float minSqDist = 42f * 42f;
                    for (int i = 0; i < _occupiedPositions.Count; i++)
                    {
                        if ((_occupiedPositions[i] - screenPos).sqrMagnitude < minSqDist)
                        {
                            overlaps = true;
                            break;
                        }
                    }
                    if (!overlaps)
                    {
                        _occupiedPositions.Add(screenPos);
                    }
                }

                bool showLabel = hasCargo || isHovered || (isCloseZoom && !overlaps);
                string labelText = cargoCount > 1 ? $"#{id + 1} (x{cargoCount})" : $"#{id + 1}";

                UpdateDeliveryMarker(
                    "address_" + id,
                    id,
                    labelText,
                    screenPos,
                    size,
                    hasCargo,
                    isHovered,
                    showLabel);
            }

            // 3. Render Pizzaria Marker
            if (_city?.CityData != null)
            {
                AddBannerMarker("pizzeria", "Pizzaria", _city.CityData.pizzariaPosition, new Color(1f, 0.85f, 0.2f), size);
            }

            // 4. Render Players
            foreach (var pair in routeManager.Players)
            {
                var player = pair.Value;
                if (player == null || !player.IsSpawned) continue;
                bool local = player.IsOwner || IsLocal(player.OwnerClientId);
                float heading = Mathf.Atan2(player.transform.forward.x, player.transform.forward.z) * Mathf.Rad2Deg;
                string direction =
                    heading > -22.5f && heading <= 22.5f ? "↑" :
                    heading > 22.5f && heading <= 67.5f ? "↗" :
                    heading > 67.5f && heading <= 112.5f ? "→" :
                    heading > 112.5f && heading <= 157.5f ? "↘" :
                    heading > 157.5f || heading <= -157.5f ? "↓" :
                    heading > -157.5f && heading <= -112.5f ? "↙" :
                    heading > -112.5f && heading <= -67.5f ? "←" : "↖";

                AddBannerMarker(
                    "player_" + pair.Key,
                    (local ? "Voce" : player.PlayerName.Value.ToString()) + " " + direction,
                    player.transform.position,
                    player.PlayerColor.Value,
                    size);
            }

            _stale.Clear();
            foreach (var entry in _markerCache)
            {
                if (!_liveMarkers.Contains(entry.Key))
                {
                    entry.Value.Root.RemoveFromHierarchy();
                    _stale.Add(entry.Key);
                }
            }
            foreach (var key in _stale) _markerCache.Remove(key);
        }

        private void UpdateDeliveryMarker(
            string key,
            int addressId,
            string text,
            Vector2 pos,
            Vector2 size,
            bool hasCargo,
            bool isHovered,
            bool showLabel)
        {
            _liveMarkers.Add(key);
            if (!_markerCache.TryGetValue(key, out var entry))
            {
                var root = new VisualElement();
                root.style.position = Position.Absolute;
                root.style.flexDirection = FlexDirection.Row;
                root.style.alignItems = Align.Center;
                root.style.paddingLeft = 4;
                root.style.paddingRight = 5;
                root.style.paddingTop = 2;
                root.style.paddingBottom = 2;
                root.style.borderTopLeftRadius = 8;
                root.style.borderTopRightRadius = 8;
                root.style.borderBottomLeftRadius = 8;
                root.style.borderBottomRightRadius = 8;
                root.style.borderTopWidth = 1.5f;
                root.style.borderBottomWidth = 1.5f;
                root.style.borderLeftWidth = 1.5f;
                root.style.borderRightWidth = 1.5f;
                root.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(-50, LengthUnit.Percent));

                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.style.width = 14;
                icon.style.height = 14;
                icon.style.borderTopLeftRadius = 7;
                icon.style.borderTopRightRadius = 7;
                icon.style.borderBottomLeftRadius = 7;
                icon.style.borderBottomRightRadius = 7;
                var catalog = PiariaUIAssetCatalog.Instance;
                if (catalog != null && catalog.iconDeliveries != null)
                {
                    icon.style.backgroundImage = new StyleBackground(catalog.iconDeliveries);
                }
                else
                {
                    icon.style.backgroundColor = new Color(1f, 0.55f, 0.15f);
                }
                root.Add(icon);

                var label = new Label { pickingMode = PickingMode.Ignore };
                label.style.fontSize = 12;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.marginLeft = 4;
                label.style.paddingLeft = 0;
                label.style.paddingRight = 0;
                label.style.paddingTop = 0;
                label.style.paddingBottom = 0;
                root.Add(label);

                int capturedId = addressId;
                root.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    _hoveredAddress = capturedId;
                    root.BringToFront();
                });
                root.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (_hoveredAddress == capturedId) _hoveredAddress = -1;
                });

                _markers.Add(root);
                entry = new MarkerEntry { Root = root, Icon = icon, Text = label };
                _markerCache[key] = entry;
            }

            bool outOfBounds = pos.x < 0 || pos.y < 0 || pos.x > size.x || pos.y > size.y;
            entry.Root.style.display = outOfBounds ? DisplayStyle.None : DisplayStyle.Flex;
            if (outOfBounds) return;

            entry.Root.pickingMode = routeManager.IsFullscreen ? PickingMode.Position : PickingMode.Ignore;
            entry.Root.style.left = pos.x;
            entry.Root.style.top = pos.y;

            Color accent = hasCargo
                ? new Color(1f, 0.88f, 0.2f)
                : isHovered
                    ? new Color(0.55f, 0.92f, 1f)
                    : new Color(0.95f, 0.92f, 0.85f);

            entry.Root.style.backgroundColor = hasCargo
                ? new Color(0.18f, 0.14f, 0.04f, 0.95f)
                : isHovered
                    ? new Color(0.1f, 0.22f, 0.26f, 0.96f)
                    : new Color(0.12f, 0.16f, 0.15f, 0.88f);

            entry.Root.style.borderTopColor = accent;
            entry.Root.style.borderBottomColor = accent;
            entry.Root.style.borderLeftColor = accent;
            entry.Root.style.borderRightColor = accent;

            entry.Text.style.color = accent;
            if (entry.Text.text != text) entry.Text.text = text;
            entry.Text.style.display = showLabel ? DisplayStyle.Flex : DisplayStyle.None;

            if (hasCargo || isHovered)
                entry.Root.BringToFront();
        }

        private void AddBannerMarker(string key, string text, Vector3 world, Color color, Vector2 size)
        {
            _liveMarkers.Add(key);
            if (!_markerCache.TryGetValue(key, out var entry))
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.style.position = Position.Absolute;
                label.style.backgroundColor = new Color(.12f, .16f, .15f, .9f);
                label.style.fontSize = 12;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.paddingLeft = 5;
                label.style.paddingRight = 5;
                label.style.paddingTop = 2;
                label.style.paddingBottom = 2;
                label.style.borderTopLeftRadius = 4;
                label.style.borderTopRightRadius = 4;
                label.style.borderBottomLeftRadius = 4;
                label.style.borderBottomRightRadius = 4;
                label.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(-50, LengthUnit.Percent));
                _markers.Add(label);
                entry = new MarkerEntry { Root = label, Text = label };
                _markerCache[key] = entry;
            }
            var pos = routeManager.Project(world, size.x, size.y);
            bool outOfBounds = pos.x < 0 || pos.y < 0 || pos.x > size.x || pos.y > size.y;
            entry.Root.style.display = outOfBounds ? DisplayStyle.None : DisplayStyle.Flex;
            if (outOfBounds) return;
            entry.Root.style.left = pos.x;
            entry.Root.style.top = pos.y;
            entry.Text.style.color = color;
            if (entry.Text.text != text) entry.Text.text = text;
            entry.Root.BringToFront();
        }

        private void Down(PointerDownEvent evt)
        {
            if (routeManager == null || !routeManager.IsFullscreen || evt.button != 0) return;
            _drag = true; _pointer = evt.pointerId; _last = evt.position;
            _image.CapturePointer(evt.pointerId); evt.StopPropagation();
        }
        private void Move(PointerMoveEvent evt)
        {
            if (!_drag || routeManager == null || evt.pointerId != _pointer) return;
            var now = (Vector2)evt.position;
            routeManager.PanMap(now - _last); _last = now; evt.StopPropagation();
        }
        private void Up(PointerUpEvent evt)
        {
            if (!_drag || evt.pointerId != _pointer || evt.button != 0) return;
            _drag = false;
            if (_image.HasPointerCapture(evt.pointerId)) _image.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }
        private void Cancel(PointerCancelEvent evt) => StopDrag();
        private void CaptureOut(PointerCaptureOutEvent evt) => _drag = false;
        private void StopDrag()
        {
            if (!_drag) return;
            _drag = false;
            if (_image != null && _image.HasPointerCapture(_pointer)) _image.ReleasePointer(_pointer);
        }
        private void Wheel(WheelEvent evt)
        {
            if (routeManager == null || !routeManager.IsFullscreen) return;
            var local = _image.WorldToLocal(evt.mousePosition);
            routeManager.Zoom(evt.delta.y, local, new Vector2(_image.resolvedStyle.width, _image.resolvedStyle.height));
            evt.StopPropagation();
        }
        public void ToggleFullscreen(bool expanded)
        {
            EnsureUI();
            HudPresenter.Instance?.ToggleMinimapFullscreen(expanded);
            if (!expanded) _hoveredAddress = -1;
            StopDrag();
            _refresh = 0f;
        }
    }
}
