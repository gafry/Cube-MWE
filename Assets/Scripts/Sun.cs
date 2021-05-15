using PathCreation;
using UnityEngine;

public class Sun : MonoBehaviour
{
    public PathCreator pathCreator;
    public float speed = 5.0f;
    public float progress = 0.0f;
    float distanceTravelled;
    float time = 0.0f;
    float lastTime = 0.0f;

    void Update()
    {
        if (Settings.Instance.dayNightEfect)
        {
            time += Time.deltaTime - lastTime;
            distanceTravelled += speed * Time.deltaTime;
        }
        else
        {
            time = 12.0f;
            distanceTravelled = speed * time;
        }
        lastTime = Time.deltaTime;
        transform.position = pathCreator.path.GetPointAtDistance(distanceTravelled);
        progress = (distanceTravelled % pathCreator.path.length) / pathCreator.path.length;
    }
}
