using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SettingsData
{
    public bool dayNightEfect;
    public bool loadWorld;
    public bool rayTracingOn;
    public bool localLightsOn;
    public int AO;
    public int depthOfRecursion;
    public bool reprojectionOn;
    public bool varianceOn;
    public bool filteringOn;
    public bool combineAlbedoAndShadows;
    public float StartCoef;
    public float AdaptCoef;
    public float MinCoef;
    public bool softShadowsOn;
    public int worldRadius;
    public int sunSpeed;
    public int cameraSpeed;
}
