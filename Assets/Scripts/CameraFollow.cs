using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    private Vector3 offset = new Vector3(0, 10, -10);

    void LateUpdate()
    {
        if (target == null) return;

        transform.position = target.position + offset;
    }
}