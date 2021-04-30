using UnityEngine;

public class Underground : MonoBehaviour
{
    public GameObject player;

    void Update()
    {
        transform.position = new Vector3(player.transform.position.x, -5, player.transform.position.z);
    }
}
