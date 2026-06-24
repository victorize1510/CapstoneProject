using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AutoSlidingDoor : MonoBehaviour
{
    [Header("Door Leaves")]
    public Transform[] doorLeaves;
    public Vector3[] openLocalOffsets;

    [Header("Behaviour")]
    public string playerTag = "Player";
    public float openSpeed = 2.5f;
    public float closeSpeed = 2.5f;

    Vector3[] closedLocalPositions;
    Vector3[] targetLocalPositions;
    int playersInside;

    void Awake()
    {
        closedLocalPositions = new Vector3[doorLeaves.Length];
        targetLocalPositions = new Vector3[doorLeaves.Length];
        for (int i = 0; i < doorLeaves.Length; i++)
        {
            if (doorLeaves[i] == null) continue;
            closedLocalPositions[i] = doorLeaves[i].localPosition;
            targetLocalPositions[i] = closedLocalPositions[i];
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        playersInside++;
        SetOpen(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        playersInside = Mathf.Max(0, playersInside - 1);
        if (playersInside == 0) SetOpen(false);
    }

    void SetOpen(bool open)
    {
        for (int i = 0; i < doorLeaves.Length; i++)
        {
            Vector3 offset = (open && i < openLocalOffsets.Length) ? openLocalOffsets[i] : Vector3.zero;
            targetLocalPositions[i] = closedLocalPositions[i] + offset;
        }
    }

    void Update()
    {
        for (int i = 0; i < doorLeaves.Length; i++)
        {
            Transform leaf = doorLeaves[i];
            if (leaf == null) continue;
            bool closing = (targetLocalPositions[i] - closedLocalPositions[i]).sqrMagnitude < 0.0001f;
            float speed = closing ? closeSpeed : openSpeed;
            leaf.localPosition = Vector3.MoveTowards(leaf.localPosition, targetLocalPositions[i], speed * Time.deltaTime);
        }
    }
}
