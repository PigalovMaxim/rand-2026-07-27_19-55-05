using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class Player : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float skinWidth = 0.02f;

    private Rigidbody2D _rb;
    private BoxCollider2D _collider;
    private Vector2 _moveInput;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<BoxCollider2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Update()
    {
        _moveInput = ReadMoveInput();
    }

    void FixedUpdate()
    {
        if (_moveInput.sqrMagnitude < 0.01f)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        var delta = _moveInput * (moveSpeed * Time.fixedDeltaTime);
        var target = _rb.position + delta;
        target = ResolveCollision(_rb.position, target);
        _rb.MovePosition(target);
    }

    private Vector2 ResolveCollision(Vector2 from, Vector2 to)
    {
        var movement = to - from;
        if (movement.sqrMagnitude <= 0f)
        {
            return from;
        }

        var distance = movement.magnitude;
        var direction = movement / distance;
        var size = _collider.size * 0.95f;

        var hit = Physics2D.BoxCast(from, size, 0f, direction, distance + skinWidth);
        if (hit.collider == null || hit.collider.gameObject == gameObject)
        {
            return to;
        }

        return from + direction * Mathf.Max(0f, hit.distance - skinWidth);
    }

    private Vector2 ReadMoveInput()
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
