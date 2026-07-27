using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D _rb;
    private Vector2 _moveInput;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
    }

    void Update()
    {
        _moveInput = ReadMoveInput();
    }

    void FixedUpdate()
    {
        _rb.linearVelocity = _moveInput * moveSpeed;
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
