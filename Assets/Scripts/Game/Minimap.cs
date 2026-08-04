using Roguelike.LevelGeneration;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public sealed class Minimap : MonoBehaviour
{
    private const string PlayerTag = "Player";
    private const string FloorTilemapName = "Floor";
    private const string WallTilemapName = "Wall";

    [SerializeField, Min(64f)] private float _mapSize = 220f;
    [SerializeField] private Vector2 _margin = new Vector2(20f, 20f);
    [SerializeField] private Color _backgroundColor = new Color(0.05f, 0.05f, 0.07f, 0.85f);
    [SerializeField] private Color _floorColor = new Color(0.55f, 0.55f, 0.6f, 1f);
    [SerializeField] private Color _wallColor = new Color(0.18f, 0.18f, 0.22f, 1f);
    [SerializeField] private Color _emptyColor = new Color(0.02f, 0.02f, 0.03f, 1f);
    [SerializeField] private Color _playerColor = new Color(0.25f, 1f, 0.4f, 1f);
    [SerializeField, Min(4f)] private float _playerMarkerSize = 10f;

    private GameObject _canvasObject;
    private RawImage _mapImage;
    private RectTransform _mapRect;
    private RectTransform _markerRect;
    private Texture2D _mapTexture;
    private Tilemap _floorTilemap;
    private Tilemap _wallTilemap;
    private Transform _player;
    private BoundsInt _mapBounds;
    private bool _hasMap;

    void OnEnable()
    {
        LevelGenerator.LevelGenerated += RebuildFromLevel;
    }

    void OnDisable()
    {
        LevelGenerator.LevelGenerated -= RebuildFromLevel;
    }

    void Start()
    {
        EnsureUi();
        ResolvePlayer();
        RebuildFromLevel();
    }

    void LateUpdate()
    {
        if (!_hasMap)
            return;

        ResolvePlayer();
        UpdatePlayerMarker();
    }

    void OnDestroy()
    {
        DestroyMapTexture();
    }

    public void RebuildFromLevel()
    {
        EnsureUi();

        if (!ResolveTilemaps())
        {
            _hasMap = false;
            return;
        }

        BuildMapTexture();
        _hasMap = true;
        UpdatePlayerMarker();
    }

    private void EnsureUi()
    {
        if (_canvasObject != null)
            return;

        _canvasObject = new GameObject("MinimapCanvas");
        _canvasObject.transform.SetParent(transform, false);

        var canvas = _canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        var scaler = _canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _canvasObject.AddComponent<GraphicRaycaster>();

        var frameObject = CreateUiObject("Frame", _canvasObject.transform);
        var frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(1f, 1f);
        frameRect.anchorMax = new Vector2(1f, 1f);
        frameRect.pivot = new Vector2(1f, 1f);
        frameRect.anchoredPosition = new Vector2(-_margin.x, -_margin.y);
        frameRect.sizeDelta = new Vector2(_mapSize + 16f, _mapSize + 16f);

        var frameImage = frameObject.AddComponent<Image>();
        frameImage.color = _backgroundColor;

        var mapObject = CreateUiObject("Map", frameObject.transform);
        _mapRect = mapObject.GetComponent<RectTransform>();
        _mapRect.anchorMin = new Vector2(0.5f, 0.5f);
        _mapRect.anchorMax = new Vector2(0.5f, 0.5f);
        _mapRect.pivot = new Vector2(0.5f, 0.5f);
        _mapRect.sizeDelta = new Vector2(_mapSize, _mapSize);

        _mapImage = mapObject.AddComponent<RawImage>();
        _mapImage.color = Color.white;

        var markerObject = CreateUiObject("PlayerMarker", mapObject.transform);
        _markerRect = markerObject.GetComponent<RectTransform>();
        _markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        _markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        _markerRect.pivot = new Vector2(0.5f, 0.5f);
        _markerRect.sizeDelta = new Vector2(_playerMarkerSize, _playerMarkerSize);

        var markerImage = markerObject.AddComponent<Image>();
        markerImage.color = _playerColor;
        markerImage.raycastTarget = false;
    }

    private bool ResolveTilemaps()
    {
        if (_floorTilemap != null && _wallTilemap != null)
            return true;

        var tilemaps = FindObjectsByType<Tilemap>();
        for (var i = 0; i < tilemaps.Length; i++)
        {
            var tilemap = tilemaps[i];
            if (tilemap.name == FloorTilemapName)
                _floorTilemap = tilemap;
            else if (tilemap.name == WallTilemapName)
                _wallTilemap = tilemap;
        }

        return _floorTilemap != null;
    }

    private void ResolvePlayer()
    {
        if (_player != null)
            return;

        var playerObject = GameObject.FindGameObjectWithTag(PlayerTag);
        if (playerObject != null)
            _player = playerObject.transform;
    }

    private void BuildMapTexture()
    {
        _floorTilemap.CompressBounds();
        if (_wallTilemap != null)
            _wallTilemap.CompressBounds();

        var floorBounds = _floorTilemap.cellBounds;
        var wallBounds = _wallTilemap != null ? _wallTilemap.cellBounds : floorBounds;
        _mapBounds = EncapsulateBounds(floorBounds, wallBounds);

        var width = Mathf.Max(1, _mapBounds.size.x);
        var height = Mathf.Max(1, _mapBounds.size.y);

        DestroyMapTexture();
        _mapTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "MinimapTexture"
        };

        var pixels = new Color32[width * height];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = _emptyColor;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var cell = new Vector3Int(_mapBounds.xMin + x, _mapBounds.yMin + y, 0);
                var index = y * width + x;

                if (_floorTilemap.HasTile(cell))
                    pixels[index] = _floorColor;
                else if (_wallTilemap != null && _wallTilemap.HasTile(cell))
                    pixels[index] = _wallColor;
            }
        }

        _mapTexture.SetPixels32(pixels);
        _mapTexture.Apply(false, false);
        _mapImage.texture = _mapTexture;

        FitMapImageAspect(width, height);
    }

    private void FitMapImageAspect(int width, int height)
    {
        var aspect = (float)width / height;
        if (aspect >= 1f)
            _mapRect.sizeDelta = new Vector2(_mapSize, _mapSize / aspect);
        else
            _mapRect.sizeDelta = new Vector2(_mapSize * aspect, _mapSize);
    }

    private void UpdatePlayerMarker()
    {
        if (_player == null || _markerRect == null || _floorTilemap == null)
        {
            if (_markerRect != null)
                _markerRect.gameObject.SetActive(false);
            return;
        }

        var cell = _floorTilemap.WorldToCell(_player.position);
        var inside = cell.x >= _mapBounds.xMin && cell.x < _mapBounds.xMax &&
                     cell.y >= _mapBounds.yMin && cell.y < _mapBounds.yMax;

        _markerRect.gameObject.SetActive(inside);
        if (!inside)
            return;

        var u = (cell.x - _mapBounds.xMin + 0.5f) / _mapBounds.size.x;
        var v = (cell.y - _mapBounds.yMin + 0.5f) / _mapBounds.size.y;
        var size = _mapRect.rect.size;
        _markerRect.anchoredPosition = new Vector2((u - 0.5f) * size.x, (v - 0.5f) * size.y);
    }

    private void DestroyMapTexture()
    {
        if (_mapTexture == null)
            return;

        if (_mapImage != null && _mapImage.texture == _mapTexture)
            _mapImage.texture = null;

        Destroy(_mapTexture);
        _mapTexture = null;
    }

    private static BoundsInt EncapsulateBounds(BoundsInt a, BoundsInt b)
    {
        var xMin = Mathf.Min(a.xMin, b.xMin);
        var yMin = Mathf.Min(a.yMin, b.yMin);
        var xMax = Mathf.Max(a.xMax, b.xMax);
        var yMax = Mathf.Max(a.yMax, b.yMax);
        return new BoundsInt(xMin, yMin, 0, xMax - xMin, yMax - yMin, 1);
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var uiObject = new GameObject(name, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }
}
