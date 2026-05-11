using UnityEngine;

public class Move : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 15f;
    public float smoothTime = 0.1f;

    [Header("Look")]
    public float lookSpeed = 5f;
    public bool holdRightMouseToLook = true;

    float _yaw;
    float _pitch;
    Vector3 _currentVelocity;
    Vector3 _velocitySmooth;

    void Start()
    {
        Vector3 euler = transform.eulerAngles;
        _yaw = euler.y;
        _pitch = euler.x;
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
    }

    void HandleLook()
    {
        bool canLook = !holdRightMouseToLook || Input.GetMouseButton(1);

        if (canLook)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _yaw += Input.GetAxis("Mouse X") * lookSpeed;
            _pitch -= Input.GetAxis("Mouse Y") * lookSpeed;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void HandleMovement()
    {
        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;

        Vector3 input = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) input.z += 1f;
        if (Input.GetKey(KeyCode.S)) input.z -= 1f;
        if (Input.GetKey(KeyCode.A)) input.x -= 1f;
        if (Input.GetKey(KeyCode.D)) input.x += 1f;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) input.y += 1f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) input.y -= 1f;

        Vector3 worldDir = transform.right * input.x
                         + transform.up * input.y
                         + transform.forward * input.z;

        Vector3 targetVelocity = worldDir.normalized * speed;
        _currentVelocity = Vector3.SmoothDamp(_currentVelocity, targetVelocity, ref _velocitySmooth, smoothTime);

        transform.position += _currentVelocity * Time.deltaTime;
    }
}
