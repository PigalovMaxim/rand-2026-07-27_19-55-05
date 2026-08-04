using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class Player : MonoBehaviour
{
    private const string WallTag = "Wall";
    private const int MaxRayHits = 16;

    private static readonly RaycastHit2D[] _rayHits = new RaycastHit2D[MaxRayHits];

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float detectionRange = 20f;
    [SerializeField] private Joystick _moveJoystick;

    private Rigidbody2D _rb;
    private Collider2D _collider;
    private Vector2 _moveInput;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        ResolveMoveJoystick();
    }

    void Update()
    {
        _moveInput = ReadMoveInput();
        DetectEnemies();
    }

    void FixedUpdate()
    {
        _rb.linearVelocity = _moveInput * moveSpeed;
    }

    private void DetectEnemies()
    {
        var origin = (Vector2)transform.position;
        var enemies = Enemy.ActiveEnemies;

        for (var i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (enemy == null)
                continue;

            var enemyCenter = enemy.VisionCenter;
            var distance = Vector2.Distance(origin, enemyCenter);
            if (distance > detectionRange)
                continue;

            if (IsRayClearToEnemy(origin, enemyCenter, enemy))
                enemy.SeePlayer(transform);
        }
    }

    private bool IsRayClearToEnemy(Vector2 origin, Vector2 enemyCenter, Enemy enemy)
    {
        var delta = enemyCenter - origin;
        var distance = delta.magnitude;
        if (distance <= Mathf.Epsilon)
            return true;

        var direction = delta / distance;
        var hitCount = Physics2D.RaycastNonAlloc(origin, direction, _rayHits, distance);

        for (var i = 0; i < hitCount; i++)
        {
            var hitCollider = _rayHits[i].collider;
            if (hitCollider == null || hitCollider == _collider)
                continue;

            if (hitCollider.CompareTag(WallTag))
                return false;

            if (BelongsToEnemy(hitCollider, enemy))
                return true;
        }

        return true;
    }

    private static bool BelongsToEnemy(Collider2D hitCollider, Enemy enemy)
    {
        return hitCollider.transform == enemy.transform ||
               hitCollider.transform.IsChildOf(enemy.transform);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        var origin = (Vector2)transform.position;

        if (Application.isPlaying)
            DrawEnemyVisionRays(origin, Enemy.ActiveEnemies);
        else
            DrawEnemyVisionRays(origin, FindObjectsByType<Enemy>(FindObjectsSortMode.None));
    }

    private void DrawEnemyVisionRays(Vector2 origin, IReadOnlyList<Enemy> enemies)
    {
        for (var i = 0; i < enemies.Count; i++)
            DrawEnemyVisionRay(origin, enemies[i]);
    }

    private void DrawEnemyVisionRays(Vector2 origin, Enemy[] enemies)
    {
        for (var i = 0; i < enemies.Length; i++)
            DrawEnemyVisionRay(origin, enemies[i]);
    }

    private void DrawEnemyVisionRay(Vector2 origin, Enemy enemy)
    {
        if (enemy == null)
            return;

        var enemyCenter = enemy.VisionCenter;
        var distance = Vector2.Distance(origin, enemyCenter);
        var inRange = distance <= detectionRange;
        var clear = inRange && IsRayClearToEnemy(origin, enemyCenter, enemy);

        Gizmos.color = !inRange
            ? new Color(0.5f, 0.5f, 0.5f, 0.35f)
            : clear
                ? new Color(0.2f, 1f, 0.35f, 0.9f)
                : new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawLine(origin, enemyCenter);
    }

    private Vector2 ReadMoveInput()
    {
        ResolveMoveJoystick();

        var joystick = ReadJoystickInput();
        if (joystick.sqrMagnitude > 0f)
            return joystick;

        var gamepad = ReadGamepadInput();
        if (gamepad.sqrMagnitude > 0f)
            return gamepad;

        return ReadKeyboardInput();
    }

    private void ResolveMoveJoystick()
    {
        if (_moveJoystick != null)
            return;

        var mobileControls = FindAnyObjectByType<MobileControls>();
        if (mobileControls != null && mobileControls.Joystick != null)
        {
            _moveJoystick = mobileControls.Joystick;
            return;
        }

        _moveJoystick = FindAnyObjectByType<Joystick>();
    }

    private Vector2 ReadJoystickInput()
    {
        if (_moveJoystick == null)
            return Vector2.zero;

        var direction = _moveJoystick.Direction;
        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
    }

    private static Vector2 ReadGamepadInput()
    {
        var gamepad = Gamepad.current;
        if (gamepad == null)
            return Vector2.zero;

        var stick = gamepad.leftStick.ReadValue();
        return stick.sqrMagnitude > 1f ? stick.normalized : stick;
    }

    private Vector2 ReadKeyboardInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return Vector2.zero;

        float x = 0f;
        float y = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            y += 1f;

        Vector2 input = new Vector2(x, y);
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }
}
