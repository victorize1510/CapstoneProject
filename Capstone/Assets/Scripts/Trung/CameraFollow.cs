using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 6f, -6f);
    public float followLerp = 8f;

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followLerp * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 1.2f);
    }
}
