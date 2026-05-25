using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;

    public Vector3 offset = new Vector3(0f, 3f, -6f);
    public float followSpeed = 10f;

    private float yaw;
    private float pitch;

    public float sensitivity = 0.1f;

    private GameInput controls;
    private Vector2 lookInput;

    private void OnEnable()
    {
        controls = new GameInput();
        controls.Enable();

        controls.PlayerMovement.Look.performed += ctx =>
            lookInput = ctx.ReadValue<Vector2>();

        controls.PlayerMovement.Look.canceled += ctx =>
            lookInput = Vector2.zero;
    }

    private void OnDisable()
    {
        controls.Disable();
    }

    void LateUpdate()
    {
        if (target == null) return;

        yaw += lookInput.x * sensitivity;
        pitch -= lookInput.y * sensitivity;
        pitch = Mathf.Clamp(pitch, -30f, 60f);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 desiredPosition = target.position + rotation * offset;

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