using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Roguelike.LevelGeneration;

[DefaultExecutionOrder(-100)]
public sealed class Game : MonoBehaviour
{
    private const string EnemyPrefabResourcePath = "Prefabs/Enemy";
    private const string PlayerTag = "Player";

    public static Game Instance { get; private set; }

    [SerializeField, InspectorName("Тестовое поле")]
    private bool _testField;

    [SerializeField, Min(0f), InspectorName("Дальность спавна врага")]
    private float _enemySpawnDistance = 1.5f;

    private GameObject _enemyPrefab;
    private Transform _enemiesRoot;
    private GameObject _controlCanvas;

    public bool TestField => _testField;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_testField)
            CreateControlPanel();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void CreateEnemy()
    {
        if (!EnsureEnemyPrefab())
            return;

        var spawnPosition = ResolveEnemySpawnPosition();
        var enemyObject = Instantiate(_enemyPrefab, spawnPosition, Quaternion.identity, GetEnemiesRoot());
        var enemy = enemyObject.GetComponent<Enemy>();
        var player = ResolvePlayerTransform();
        if (enemy != null && player != null)
            enemy.SetTarget(player);
    }

    private bool EnsureEnemyPrefab()
    {
        if (_enemyPrefab != null)
            return true;

        _enemyPrefab = Resources.Load<GameObject>(EnemyPrefabResourcePath);
        if (_enemyPrefab != null)
            return true;

        Debug.LogError("Game: не найден префаб врага в Resources/Prefabs/Enemy.");
        return false;
    }

    private Vector3 ResolveEnemySpawnPosition()
    {
        var player = ResolvePlayerTransform();
        if (player != null)
            return player.position + new Vector3(_enemySpawnDistance, 0f, 0f);

        return Vector3.zero;
    }

    private Transform ResolvePlayerTransform()
    {
        var playerObject = GameObject.FindGameObjectWithTag(PlayerTag);
        return playerObject != null ? playerObject.transform : null;
    }

    private Transform GetEnemiesRoot()
    {
        if (_enemiesRoot != null)
            return _enemiesRoot;

        var generator = FindAnyObjectByType<LevelGenerator>();
        if (generator != null)
        {
            var existing = generator.transform.Find("LevelEnemies");
            if (existing != null)
            {
                _enemiesRoot = existing;
                return _enemiesRoot;
            }

            var rootObject = new GameObject("LevelEnemies");
            rootObject.transform.SetParent(generator.transform, false);
            _enemiesRoot = rootObject.transform;
            return _enemiesRoot;
        }

        _enemiesRoot = transform;
        return _enemiesRoot;
    }

    private void CreateControlPanel()
    {
        if (_controlCanvas != null)
            return;

        EnsureEventSystem();

        var canvasObject = new GameObject("GameControlCanvas");
        canvasObject.transform.SetParent(transform, false);
        _controlCanvas = canvasObject;

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        var panelObject = CreateUiObject("ControlPanel", canvasObject.transform);
        var panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-20f, 20f);
        panelRect.sizeDelta = new Vector2(200f, 80f);

        var panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.08f, 0.1f, 0.75f);

        var buttonObject = CreateUiObject("CreateEnemyButton", panelObject.transform);
        var buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.08f, 0.18f);
        buttonRect.anchorMax = new Vector2(0.92f, 0.82f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        var buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.45f, 0.75f, 1f);

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(CreateEnemy);

        var labelObject = CreateUiObject("Label", buttonObject.transform);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelObject.AddComponent<Text>();
        label.text = "Создать врага";
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.fontSize = 18;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (label.font == null)
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var uiObject = new GameObject(name, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }
}
