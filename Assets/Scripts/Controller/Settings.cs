using System.IO;
using UnityEngine;
using UnityEngine.UI;

// Controls menu and saves/load options to/from file
public class Settings : MonoBehaviour
{
    private static Settings s_Instance;

    private string saveFile = "./Assets/Options/Settings.json";
    //private string saveFile = "./Save/Settings.json";

    [Header("World options")]
    public bool dayNightEfect = false;
    public bool loadWorld = false;
    public int WorldRadius = 13;

    [Header("Ray tracing options")]
    public bool groundTruthIfThereIsNoMotion = false;
    public bool directLightingOn = true;
    public bool indirectLightingOn = false;
    public int AO = 0;
    public bool reprojectionOn = false;
    public bool varianceOn = false;
    public bool filteringOn = false;
    public bool softShadowsOn = false;
    public bool combineAlbedoAndShadows = false;
    public int depthOfRecursion = 2;
    [Space(20)]

    [Header("Runtime variables")]
    public bool reprojectWithIDs = true;
    public bool isMenuOpen = false;
    private bool _isAdvancedOpen = false;
    public int SunSpeed = 0;
    public float StartCoef;
    public float AdaptCoef;
    public float MinCoef;
    public float frameTime;
    public int CameraSpeed;
    [Space(20)]

    [Header("UI")]
    public Text TextAdvanced;
    public Text TextFrameTime;
    public Canvas OptionMenu;
    public GameObject AdvancedMenu;
    public Toggle ToggleDL;
    public Toggle ToggleLL;
    public Toggle ToggleReprojection;
    public Toggle ToggleVariance;
    public Toggle ToggleFiltering;
    public Toggle ToggleDayNight;
    public Toggle ToggleWorldGen;
    public Toggle ToggleAlbedo;
    public Toggle ToggleSoftShadows;
    public Slider SliderAO;
    public Slider SliderWorldRadius;
    public Slider SliderStartCoef;
    public Slider SliderMinCoef;
    public Slider SliderAdaptCoef;
    public Slider SliderSunSpeed;

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

    public void Start()
    {
#if UNITY_EDITOR
        saveFile = "./Assets/Options/Settings.json";
#else
        saveFile = "./Save/Settings.json";
#endif
        LoadFromFile();
        OptionMenu.enabled = false;
        AdvancedMenu.SetActive(false);
        isMenuOpen = false;
        _isAdvancedOpen = false;
        Cursor.lockState = CursorLockMode.Locked;        
    }

    public void Exit()
    {
        SaveToFile();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
         Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        SaveToFile();
    }

    public void Advanced()
    {
        if (_isAdvancedOpen)
        {
            _isAdvancedOpen = false;
            TextAdvanced.text = "Advanced ↓";
            AdvancedMenu.SetActive(false);
        }
        else
        {
            _isAdvancedOpen = true;
            TextAdvanced.text = "Advanced ↑";
            AdvancedMenu.SetActive(true);
        }
    }

    public void SetDOR(float value)
    {
        depthOfRecursion = (int)value;
    }

    public void SetSunSpeed(float value)
    {
        SunSpeed = (int)value;
    }

    public void SetStartCoef(float value)
    {
        StartCoef = value;
    }

    public void SetMinCoef(float value)
    {
        MinCoef = value;
    }

    public void SetAdaptCoef(float value)
    {
        AdaptCoef = value;
    }

    public void SetAmbientOcclusion(float value)
    {
        AO = (int)value;
    }

    public void SetWorldRadius(float value)
    {
        WorldRadius = (int)value;
    }

    public void SetCameraSpeed(float value)
    {
        CameraSpeed = (int)value;
    }

    public void SetDirectLighting(bool b)
    {
        directLightingOn = !directLightingOn;
    }

    public void SetLocalLighting(bool b)
    {
        indirectLightingOn = !indirectLightingOn;
    }

    public void SetReprojection(bool b)
    {
        reprojectionOn = !reprojectionOn;
    }

    public void SetVariance(bool b)
    {
        varianceOn = !varianceOn;
    }

    public void SetFiltering(bool b)
    {
        filteringOn = !filteringOn;
    }

    public void SetAlbedo(bool b)
    {
        combineAlbedoAndShadows = !combineAlbedoAndShadows;
    }

    public void SetDayNight(bool b)
    {
        dayNightEfect = !dayNightEfect;
    }

    public void SetWorldGeneration(bool b)
    {
        loadWorld = !loadWorld;
    }

    public void SetSoftShadows(bool b)
    {
        softShadowsOn = !softShadowsOn;
    }

    private void Update()
    {
        if (Input.GetKeyDown("m") || Input.GetKeyDown(KeyCode.Escape))
        {
            if (isMenuOpen)
            {
                OptionMenu.enabled = false;
                isMenuOpen = false;
                _isAdvancedOpen = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                OptionMenu.enabled = true;
                isMenuOpen = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        TextFrameTime.text = frameTime + "ms";
    }

    private void SaveToFile()
    {
        SettingsData data = new SettingsData();
        data.AdaptCoef = AdaptCoef;
        data.AO = AO;
        data.combineAlbedoAndShadows = combineAlbedoAndShadows;
        data.dayNightEfect = dayNightEfect;
        data.filteringOn = filteringOn;
        data.loadWorld = loadWorld;
        data.localLightsOn = indirectLightingOn;
        data.MinCoef = MinCoef;
        data.rayTracingOn = directLightingOn;
        data.reprojectionOn = reprojectionOn;
        data.StartCoef = StartCoef;
        data.varianceOn = varianceOn;
        data.depthOfRecursion = depthOfRecursion;
        data.softShadowsOn = softShadowsOn;
        data.worldRadius = WorldRadius;
        data.sunSpeed = SunSpeed;
        data.cameraSpeed = CameraSpeed;

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(saveFile, json);
    }

    private void LoadFromFile()
    {
        if (!File.Exists(saveFile))
            return;
        string json = File.ReadAllText(saveFile);
        SettingsData data = JsonUtility.FromJson<SettingsData>(json);

        AdaptCoef = data.AdaptCoef;
        AO = data.AO;
        WorldRadius = data.worldRadius;
        combineAlbedoAndShadows = true;
        dayNightEfect = true;
        filteringOn = true;
        loadWorld = true;
        indirectLightingOn = true;
        MinCoef = data.MinCoef;
        directLightingOn = true;
        reprojectionOn = true;
        StartCoef = data.StartCoef;
        varianceOn = true;
        depthOfRecursion = data.depthOfRecursion;
        softShadowsOn = true;
        SunSpeed = data.sunSpeed;
        CameraSpeed = data.cameraSpeed;

        SliderAdaptCoef.value = AdaptCoef;
        SliderMinCoef.value = MinCoef;
        SliderStartCoef.value = StartCoef;
        SliderAO.value = AO;
        SliderWorldRadius.value = WorldRadius;
        SliderSunSpeed.value = SunSpeed;

        if (!data.combineAlbedoAndShadows)
            ToggleAlbedo.isOn = data.combineAlbedoAndShadows;
        if (!data.dayNightEfect)
            ToggleDayNight.isOn = data.dayNightEfect;
        if (!data.rayTracingOn)
            ToggleDL.isOn = data.rayTracingOn;
        if (!data.filteringOn)
            ToggleFiltering.isOn = data.filteringOn;
        if (!data.localLightsOn)
            ToggleLL.isOn = data.localLightsOn;
        if (!data.reprojectionOn)
            ToggleReprojection.isOn = data.reprojectionOn;
        if (!data.varianceOn)
            ToggleVariance.isOn = data.varianceOn;
        if (!data.loadWorld)
            ToggleWorldGen.isOn = data.loadWorld;
        if (!data.softShadowsOn)
            ToggleSoftShadows.isOn = data.softShadowsOn;
    }
}
