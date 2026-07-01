using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimplePlayerController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float turnSpeed = 120f;
    public float gravity = -9.81f;

    CharacterController controller;
    float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        float turn = Input.GetAxis("Horizontal") * turnSpeed * Time.deltaTime;
        transform.Rotate(0f, turn, 0f);

        float forwardInput = Input.GetAxis("Vertical");
        Vector3 move = transform.forward * forwardInput * moveSpeed;

        if (controller.isGrounded)
            verticalVelocity = 0f;
        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }
}
