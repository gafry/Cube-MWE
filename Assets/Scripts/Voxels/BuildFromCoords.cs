using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildFromCoords : MonoBehaviour
{
    public GameObject cubePrefab;
    public GameObject cubePrefab_ground;

    public static readonly Vector3[] voxelCoords = new Vector3[119]
    {
        new Vector3(1,1,1),
        new Vector3(1,1,2),
        new Vector3(1,1,3),
        new Vector3(1,1,4),
        new Vector3(1,1,5),
        new Vector3(1,1,8),
        new Vector3(1,2,1),
        new Vector3(1,2,5),
        new Vector3(1,2,8),
        new Vector3(1,3,1),
        new Vector3(1,3,5),
        new Vector3(1,3,8),
        new Vector3(1,4,1),
        new Vector3(1,4,2),
        new Vector3(1,4,3),
        new Vector3(1,4,4),
        new Vector3(1,4,5),
        new Vector3(1,4,8),
        new Vector3(1,5,1),
        new Vector3(1,5,2),
        new Vector3(1,5,3),
        new Vector3(1,5,4),
        new Vector3(1,5,5),
        new Vector3(1,5,6),
        new Vector3(1,5,7),
        new Vector3(1,5,8),
        new Vector3(2,1,1),
        new Vector3(2,1,5),
        new Vector3(2,2,1),
        new Vector3(2,2,5),
        new Vector3(2,3,1),
        new Vector3(2,3,5),
        new Vector3(2,4,1),
        new Vector3(2,4,5),
        new Vector3(2,5,1),
        new Vector3(2,5,2),
        new Vector3(2,5,3),
        new Vector3(2,5,4),
        new Vector3(2,5,5),
        new Vector3(2,5,6),
        new Vector3(2,5,7),
        new Vector3(2,5,8),
        new Vector3(3,1,1),
        new Vector3(3,3,5),
        new Vector3(3,4,1),
        new Vector3(3,4,5),
        new Vector3(3,5,1),
        new Vector3(3,5,2),
        new Vector3(3,5,3),
        new Vector3(3,5,4),
        new Vector3(3,5,6),
        new Vector3(3,5,7),
        new Vector3(3,5,8),
        new Vector3(4,1,1),
        new Vector3(4,3,5),
        new Vector3(4,4,1),
        new Vector3(4,4,5),
        new Vector3(4,5,1),
        new Vector3(4,5,2),
        new Vector3(4,5,3),
        new Vector3(4,5,4),
        new Vector3(4,5,5),
        new Vector3(4,5,6),
        new Vector3(4,5,7),
        new Vector3(4,5,8),
        new Vector3(5,1,1),
        new Vector3(5,3,5),
        new Vector3(5,4,1),
        new Vector3(5,4,5),
        new Vector3(5,5,1),
        new Vector3(5,5,2),
        new Vector3(5,5,3),
        new Vector3(5,5,4),
        new Vector3(5,5,5),
        new Vector3(5,5,6),
        new Vector3(5,5,7),
        new Vector3(5,5,8),
        new Vector3(6,1,1),
        new Vector3(6,1,5),
        new Vector3(6,2,1),
        new Vector3(6,2,5),
        new Vector3(6,3,1),
        new Vector3(6,3,5),
        new Vector3(6,4,1),
        new Vector3(6,4,5),
        new Vector3(6,5,1),
        new Vector3(6,5,2),
        new Vector3(6,5,3),
        new Vector3(6,5,4),
        new Vector3(6,5,5),
        new Vector3(6,5,6),
        new Vector3(6,5,7),
        new Vector3(6,5,8),
        new Vector3(7,1,1),
        new Vector3(7,1,2),
        new Vector3(7,1,3),
        new Vector3(7,1,4),
        new Vector3(7,1,5),
        new Vector3(7,1,8),
        new Vector3(7,2,1),
        new Vector3(7,2,5),
        new Vector3(7,2,8),
        new Vector3(7,3,1),
        new Vector3(7,3,5),
        new Vector3(7,3,8),
        new Vector3(7,4,1),
        new Vector3(7,4,2),
        new Vector3(7,4,3),
        new Vector3(7,4,4),
        new Vector3(7,4,5),
        new Vector3(7,4,8),
        new Vector3(7,5,1),
        new Vector3(7,5,2),
        new Vector3(7,5,3),
        new Vector3(7,5,4),
        new Vector3(7,5,5),
        new Vector3(7,5,6),
        new Vector3(7,5,7),
        new Vector3(7,5,8)
    };

    void Start()
    {
        //for (int i = -25; i < 28; i += 10)
        //{
            foreach (Vector3 coords in voxelCoords)
            {
                Instantiate(cubePrefab, new Vector3(coords.x + transform.position.x/* + i*/, coords.y - 0.5f + transform.position.y, coords.z + transform.position.z), Quaternion.identity);
            }
        //}

        int ground = 0;

        for (int i = -30; i < 30; ++i)
        {
            for (int j = -30; j < 30; ++j)
            {
                Instantiate(cubePrefab_ground, new Vector3(i + transform.position.x, ground - 0.5f + transform.position.y, j + transform.position.z), Quaternion.identity);
            }
        }
    }
}
