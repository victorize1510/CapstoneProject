using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimatorRootMotionRelay : MonoBehaviour
{
    public BasicPlayerMovement movement;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (movement == null)
        {
            movement = GetComponentInParent<BasicPlayerMovement>();
        }
    }

    private void OnAnimatorMove()
    {
        if (movement == null || animator == null)
        {
            return;
        }

        movement.ApplyAnimatorRootMotion(animator.deltaPosition, animator.deltaRotation);
    }
}
