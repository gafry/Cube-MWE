using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Settings : MonoBehaviour
{
    private static Settings s_Instance;

    [Header("World options")]
    public bool dayNightEfect = false;
    public bool loadWorld = false;

    [Header("Ray tracing options")]
    public bool groundTruthIfThereIsNoMotion = false;
    public bool rayTracingOn = true;
    public bool localLightsOn = false;
    public bool AO = false;
    public bool reprojectionOn = false;
    public bool varianceOn = false;
    public bool filteringOn = false;
    public bool combineAlbedoAndShadows = false;
    [Range(0, 5)]
    public int depthOfRecursion = 2;
    [Space(20)]

    [Header("Runtime variables")]
    public bool cameraMoved = false;
    public bool mouseMoved = false;
    public bool reprojectWithIDs = true;

    public static Settings Instance
    {
        get
        {
            if (s_Instance != null)
                return s_Instance;

            s_Instance = GameObject.FindObjectOfType<Settings>();
            return s_Instance;
        }
    }
}
