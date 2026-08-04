using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DefaultExecutionOrder(-60)]
public sealed class MobileControls : MonoBehaviour
{
    [SerializeField] private Joystick _joystickPrefab;

    private Joystick _joystick;

    public Joystick Joystick => _joystick;

    void Awake()
    {
        EnsureEventSystem();
        EnsureJoystick();
    }

    private void EnsureJoystick()
    {
        if (_joystick != null)
            return;

        _joystick = FindAnyObjectByType<Joystick>();
        if (_joystick != null)
        {
            ConfigureLeftHalfTouchZone(_joystick);
            return;
        }

        if (_joystickPrefab == null)
        {
            Debug.LogError("MobileControls: не назначен префаб джойстика из Joystick Pack.");
            return;
        }

        var canvas = CreateCanvas();
        _joystick = Instantiate(_joystickPrefab, canvas.transform, false);
        _joystick.name = _joystickPrefab.name;
        ConfigureLeftHalfTouchZone(_joystick);
    }

    private static void ConfigureLeftHalfTouchZone(Joystick joystick)
    {
        var rect = joystick.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0f, 0f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        var image = joystick.GetComponent<Image>();
        if (image == null)
            return;

        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;

        if (image.sprite == null)
            image.sprite = CreateTransparentSprite();
    }

    private Canvas CreateCanvas()
    {
        var canvasObject = new GameObject("MobileControlsCanvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private static Sprite CreateTransparentSprite()
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "JoystickTouchZone"
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }
}
