using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 3f, -6f);
    public float followSpeed = 10f;

    [Header("Look")]
    public float mouseSensitivity = 0.1f;
    public float controllerSensitivity = 150f; 

    [Header("Collision")]
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float collisionSmoothSpeed = 15f;
    [SerializeField] private LayerMask collisionMask;

    private float yaw;
    private float pitch;
    private GameInput controls;
    private Vector2 lookInput;
    private float _currentDistance;
    private float _targetDistance;

    private void OnEnable()
    {
        controls = new GameInput();
        controls.Disable();
        controls.PlayerMovement.Enable();
        controls.PlayerMovement.Look.performed += ctx =>
            lookInput = ctx.ReadValue<Vector2>();
        controls.PlayerMovement.Look.canceled += ctx =>
            lookInput = Vector2.zero;

        _currentDistance = offset.magnitude;
        _targetDistance = _currentDistance;
    }

    private void OnDisable()
    {
        controls.Disable();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Scale controller input by deltaTime, mouse input is already a delta
        bool isGamepad = Gamepad.current != null &&
                         Gamepad.current.rightStick.ReadValue().magnitude > 0.01f;

        if (isGamepad)
        {
            yaw += lookInput.x * controllerSensitivity * Time.deltaTime;
            pitch -= lookInput.y * controllerSensitivity * Time.deltaTime;
        }
        else
        {
            yaw += lookInput.x * mouseSensitivity;
            pitch -= lookInput.y * mouseSensitivity;
        }

        pitch = Mathf.Clamp(pitch, -30f, 60f);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        float desiredDistance = offset.magnitude;
        Vector3 direction = rotation * offset.normalized;

        // Raycast from player toward desired camera position
        if (Physics.SphereCast(
            target.position,
            collisionRadius,
            direction,
            out RaycastHit hit,
            desiredDistance,
            collisionMask))
        {
            // Pull camera in just before the wall
            _targetDistance = Mathf.Clamp(hit.distance, minDistance, desiredDistance);
        }
        else
        {
            // Nothing in the way — restore full distance
            _targetDistance = desiredDistance;
        }

        // Smooth the transition so it doesn't snap
        _currentDistance = Mathf.Lerp(
            _currentDistance,
            _targetDistance,
            Time.deltaTime * collisionSmoothSpeed
        );

        Vector3 desiredPosition = target.position + direction * _currentDistance;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            1f - Mathf.Exp(-followSpeed * Time.deltaTime)
        );

        transform.LookAt(target.position + Vector3.up * 1.5f);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}