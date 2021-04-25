using System.IO;
using UnityEngine;
using UnityEngine.UI;

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
    public int AO = 0;
    public bool reprojectionOn = false;
    public bool varianceOn = false;
    public bool filteringOn = false;
    public bool softShadowsOn = false;
    public bool combineAlbedoAndShadows = false;
    public int depthOfRecursion = 2;
    [Space(20)]

    [Header("Runtime variables")]
    public bool cameraMoved = false;
    public bool reprojectWithIDs = true;
    public bool isMenuOpen = false;
    private bool _isAdvancedOpen = false;
    public float StartCoef;
    public float AdaptCoef;
    public float MinCoef;
    public float frameTime;
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
    public Slider SliderStartCoef;
    public Slider SliderMinCoef;
    public Slider SliderAdaptCoef;

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
        //Application.Quit();
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

    public void SetDirectLighting(bool b)
    {
        rayTracingOn = !rayTracingOn;
    }

    public void SetLocalLighting(bool b)
    {
        localLightsOn = !localLightsOn;
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
        if (Input.GetKeyDown("m"))
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
        data.localLightsOn = localLightsOn;
        data.MinCoef = MinCoef;
        data.rayTracingOn = rayTracingOn;
        data.reprojectionOn = reprojectionOn;
        data.StartCoef = StartCoef;
        data.varianceOn = varianceOn;
        data.depthOfRecursion = depthOfRecursion;
        data.softShadowsOn = softShadowsOn;

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText("./Assets/Options/Settings.json", json);
    }

    private void LoadFromFile()
    {
        string json = File.ReadAllText("./Assets/Options/Settings.json");
        SettingsData data = JsonUtility.FromJson<SettingsData>(json);

        AdaptCoef = data.AdaptCoef;
        AO = data.AO;
        combineAlbedoAndShadows = true;
        dayNightEfect = true;
        filteringOn = true;
        loadWorld = true;
        localLightsOn = true;
        MinCoef = data.MinCoef;
        rayTracingOn = data.rayTracingOn;
        reprojectionOn = true;
        StartCoef = data.StartCoef;
        varianceOn = true;
        depthOfRecursion = data.depthOfRecursion;
        softShadowsOn = true;

        SliderAdaptCoef.value = AdaptCoef;
        SliderMinCoef.value = MinCoef;
        SliderStartCoef.value = StartCoef;
        SliderAO.value = AO;
        ToggleAlbedo.isOn = data.combineAlbedoAndShadows;
        ToggleDayNight.isOn = data.dayNightEfect;
        ToggleDL.isOn = rayTracingOn;
        ToggleFiltering.isOn = data.filteringOn;
        ToggleLL.isOn = data.localLightsOn;
        ToggleReprojection.isOn = data.reprojectionOn;
        ToggleVariance.isOn = data.varianceOn;
        ToggleWorldGen.isOn = data.loadWorld;
        ToggleSoftShadows.isOn = data.softShadowsOn;
    }
}
