using PathCreation;
using UnityEngine;

public class Sun : MonoBehaviour
{
    public PathCreator pathCreator;
    public float speed = 5.0f;
    float distanceTravelled;

    void Update()
    {
        distanceTravelled += speed * Time.deltaTime;
        transform.position = pathCreator.path.GetPointAtDistance(distanceTravelled);
    }
}
