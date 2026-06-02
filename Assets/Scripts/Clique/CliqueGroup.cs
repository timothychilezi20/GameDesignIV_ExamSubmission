using UnityEngine;

public class CliqueGroup : MonoBehaviour
{
    // Added: enum so each group knows which clique it belongs to.
    // Set this in the Inspector on each Group object.
    public enum CliqueType { Artists, Nerds, Athletes }

    [Header("Clique")]
    public CliqueType cliqueType;

    [Header("Detection")]
    [SerializeField] private float _facePlayerDistance = 5f;
    [SerializeField] private float _interactDistance = 2f;

    private Transform[] _members;
    public bool HasBeenCollected { get; private set; } = false;
    private Transform _nearbyPlayer = null;
    public float InteractDistance => _interactDistance;

    private void Start()
    {
        _members = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
            _members[i] = transform.GetChild(i);
    }

    private void Update()
    {
        if (_nearbyPlayer == null) return;

        float distance = Vector3.Distance(transform.position, _nearbyPlayer.position);

        if (distance <= _facePlayerDistance)
        {
            foreach (Transform member in _members)
            {
                Vector3 direction = (_nearbyPlayer.position - member.position);
                direction.y = 0f;
                if (direction != Vector3.zero)
                    member.rotation = Quaternion.Slerp(
                        member.rotation,
                        Quaternion.LookRotation(direction),
                        Time.deltaTime * 5f
                    );
            }
        }
    }

    public void SetNearbyPlayer(Transform player) => _nearbyPlayer = player;
    public void ClearNearbyPlayer() => _nearbyPlayer = null;

    public bool IsPlayerInInteractRange(Vector3 playerPosition)
    {
        bool inRange = Vector3.Distance(transform.position, playerPosition) <= _interactDistance;
        if (inRange) Debug.Log("Player is in range");
        return inRange;
    }

    public int GetMemberCount() => _members.Length;
    public void MarkCollected() => HasBeenCollected = true;
}