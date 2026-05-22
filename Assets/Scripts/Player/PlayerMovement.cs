using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour, PlayerControls.IPlayerMovementActions
{
    public PlayerControls PlayerControls { get; private set; }
    public Vector2 MovementInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    // Changed: OnEnable → OnNetworkSpawn
    // OnEnable fires for ALL instances including remote players.
    // OnNetworkSpawn lets us check IsOwner so only the local player
    // reads input — remote players must never process input locally.
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        PlayerControls = new PlayerControls();
        PlayerControls.Enable();
        PlayerControls.PlayerMovement.Enable();
        PlayerControls.PlayerMovement.SetCallbacks(this);
    }

    // Changed: OnDisable → OnNetworkDespawn
    // Mirrors the spawn guard — only the owner cleans up input.
    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        PlayerControls.PlayerMovement.Disable();
        PlayerControls.PlayerMovement.RemoveCallbacks(this);
        PlayerControls.Disable();
    }

    // Fixed: Vector3 → Vector2
    // ReadValue<Vector2> was being stored in a Vector3, which works
    // but is semantically wrong and will cause bugs with LookInput.z
    public void OnLook(InputAction.CallbackContext context)
    {
        LookInput = context.ReadValue<Vector2>();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        MovementInput = context.ReadValue<Vector2>();
    }
}