using UnityEngine;
using Unity.Netcode;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Offset")]
    public Vector3 offset = new Vector3(0f, 6f, -4f);

    [Header("Follow Settings")]
    public float followSpeed = 8f;

    [Header("Look Settings")]
    public float lookHeightOffset = 1.5f;

    void LateUpdate()
    {
        if (target == null) return;

        // Desired position above/behind player
        Vector3 desiredPosition = target.position + offset;

        // Smooth follow
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSpeed * Time.deltaTime
        );

        // Always look at player (slightly above feet)
        Vector3 lookTarget = target.position + Vector3.up * lookHeightOffset;
        transform.LookAt(lookTarget);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}