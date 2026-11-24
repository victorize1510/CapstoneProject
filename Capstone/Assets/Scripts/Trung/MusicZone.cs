using UnityEngine;

// Vùng nhạc trong scene (hang động, làng, khu rừng...). Player đi vào -> đổi bài, đi ra -> về nhạc scene.
// Gắn lên object có Collider (Is Trigger) bao quanh khu vực.
[RequireComponent(typeof(Collider))]
public class MusicZone : MonoBehaviour
{
    [SerializeField] AudioClip clip;
    [SerializeField, Range(0f, 1f)] float volume = 1f;

    int playerInside;

    void Reset() => GetComponent<Collider>().isTrigger = true;

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerInside++;
        if (playerInside == 1) MusicManager.Push(this, clip, volume);
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerInside = Mathf.Max(0, playerInside - 1);
        if (playerInside == 0) MusicManager.Release(this);
    }

    void OnDisable()
    {
        playerInside = 0;
        MusicManager.Release(this);
    }

    static bool IsPlayer(Collider other) => other.GetComponentInParent<BasicPlayerMovement>() != null;
}