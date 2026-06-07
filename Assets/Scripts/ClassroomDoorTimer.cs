using UnityEngine;
using Unity.Netcode;

public class ClassroomDoorTimer : NetworkBehaviour
{
    [Header("References")]
    public Animator doorAnimator;          

    [Header("Settings")]
    public float lockDuration = 20f;       

    private float remainingTime;
    private bool isLocked = true;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            LockDoor(lockDuration); 
        }
    }

    void Update()
    {
        if (!IsServer) return; 

        if (isLocked)
        {
            remainingTime -= Time.deltaTime;

            if (remainingTime <= 0f)
            {
                UnlockDoor();
            }
        }
    }

    void LockDoor(float duration)
    {
        isLocked = true;
        remainingTime = duration;
        doorAnimator.SetBool("isLocked", true); 
    }

    void UnlockDoor()
    {
        isLocked = false;
        doorAnimator.SetBool("isLocked", false); 
    }
}
