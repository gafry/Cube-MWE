using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;

    public float speed = 12f;

    void LateUpdate()
    {
        speed = Settings.Instance.CameraSpeed;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        float up = Input.GetAxis("Jump");
        float down = Input.GetAxis("Fire3");

        float upDown = up - down;

        Vector3 move = transform.right * x + transform.up * upDown + transform.forward * z;

        controller.Move(move * speed * Time.deltaTime);
    }
}
