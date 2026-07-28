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
    private ContactFilter2D _contactFilter;
    private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<BoxCollider2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        _contactFilter = new ContactFilter2D();
        _contactFilter.useTriggers = false;
        _contactFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
        _contactFilter.useLayerMask = true;
    }

    void Update()
    {
        _moveInput = ReadMoveInput();
    }

    void FixedUpdate()
    {
        if (_moveInput.sqrMagnitude < 0.01f)
            return;

        var delta = _moveInput * (moveSpeed * Time.fixedDeltaTime);
        var position = _rb.position;
        position = MoveAxis(position, new Vector2(delta.x, 0f));
        position = MoveAxis(position, new Vector2(0f, delta.y));
        _rb.MovePosition(position);
    }

    private Vector2 MoveAxis(Vector2 from, Vector2 delta)
    {
        var distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (distance <= 0f)
            return from;

        var direction = delta / distance;
        var origin = from + GetScaledOffset();
        var castSize = GetCastSize();
        var hitCount = Physics2D.BoxCast(
            origin,
            castSize,
            0f,
            direction,
            _contactFilter,
            _hits,
            distance + skinWidth);

        var allowed = distance;
        for (var i = 0; i < hitCount; i++)
        {
            var hit = _hits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - skinWidth));
        }

        return from + direction * allowed;
    }

    private Vector2 GetScaledOffset()
    {
        var scale = transform.lossyScale;
        return new Vector2(_collider.offset.x * scale.x, _collider.offset.y * scale.y);
    }

    private Vector2 GetCastSize()
    {
        var scale = transform.lossyScale;
        var size = new Vector2(
            Mathf.Abs(_collider.size.x * scale.x),
            Mathf.Abs(_collider.size.y * scale.y));
        size.x = Mathf.Max(0.01f, size.x - skinWidth * 2f);
        size.y = Mathf.Max(0.01f, size.y - skinWidth * 2f);
        return size;
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
