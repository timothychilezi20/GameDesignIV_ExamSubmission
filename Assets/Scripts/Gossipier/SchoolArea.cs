using UnityEngine;

public class SchoolArea : MonoBehaviour
{
    public enum AreaType { Interior, Exterior }

    public AreaType areaType;
    public string areaName; // e.g. "The Art Studio", "The Courts"
}
