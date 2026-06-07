using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Attach to each classroom GameObject in the scene.
// Manages the session timer, teacher spawning, door activation,
// and telling clique groups whether class is in session.
public class ClassroomSession : MonoBehaviour
{
    [Header("Session Timing")]
    [SerializeField] private float _minTimeBetweenSessions = 3f;
    [SerializeField] private float _maxTimeBetweenSessions = 8f;
    [SerializeField] private float _sessionDuration = 15f; // how long class stays in session

    [Header("Teacher")]
    [SerializeField] private GameObject _teacherPrefab;
    [SerializeField] private Transform _teacherSpawnPoint;

    [Header("Doors")]
    // Assign the empty GameObjects that represent closed doors
    [SerializeField] private GameObject[] _doorObjects;

    [Header("Clique Groups In This Class")]
    // Assign only the CliqueGroup objects that belong to this classroom
    [SerializeField] private CliqueGroup[] _cliqueGroupsInClass;

    private GameObject _currentTeacher = null;
    private bool _sessionActive = false;

    private void Start()
    {
        // Doors start open — deactivated
        SetDoors(false);
        StartCoroutine(SessionCycle());
    }

    private IEnumerator SessionCycle()
    {
        while (true)
        {
            // Wait a random interval before starting next session
            float waitTime = Random.Range(_minTimeBetweenSessions, _maxTimeBetweenSessions);
            yield return new WaitForSeconds(waitTime);

            StartSession();

            yield return new WaitForSeconds(_sessionDuration);

            EndSession();
        }
    }

    private void StartSession()
    {
        _sessionActive = true;

        // Close the doors
        SetDoors(true);

        // Spawn the teacher at their spawn point
        if (_teacherPrefab != null && _teacherSpawnPoint != null)
        {
            _currentTeacher = Instantiate(
                _teacherPrefab,
                _teacherSpawnPoint.position,
                _teacherSpawnPoint.rotation
            );
        }

        // Tell each clique group in this class that session has started
        // and give them the teacher's transform to face
        foreach (CliqueGroup group in _cliqueGroupsInClass)
        {
            if (group == null) continue;
            Transform teacherTransform = _currentTeacher != null
                ? _currentTeacher.transform
                : null;
            group.StartClassSession(teacherTransform);
        }

        Debug.Log($"{gameObject.name} session started");
    }

    private void EndSession()
    {
        _sessionActive = false;

        // Open the doors
        SetDoors(false);

        // Destroy the teacher
        if (_currentTeacher != null)
        {
            Destroy(_currentTeacher);
            _currentTeacher = null;
        }

        // Tell each clique group class has ended
        foreach (CliqueGroup group in _cliqueGroupsInClass)
        {
            if (group == null) continue;
            group.EndClassSession();
        }

        Debug.Log($"{gameObject.name} session ended");
    }

    private void SetDoors(bool closed)
    {
        foreach (GameObject door in _doorObjects)
        {
            if (door != null)
                door.SetActive(closed);
        }
    }

    public bool IsSessionActive => _sessionActive;
}