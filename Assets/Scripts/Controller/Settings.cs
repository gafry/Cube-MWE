using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Settings : MonoBehaviour
{
    private static Settings s_Instance;

    public bool groundTruthIfThereIsNoMotion = true;
    public bool rayTracingOn = true;
    public bool reprojectionOn = false;
    public int depthOfRecursion = 2;

    public bool cameraMoved = false;
    public bool mouseMoved = false;

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
