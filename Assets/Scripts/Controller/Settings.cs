﻿using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class Settings : MonoBehaviour
{
    private static Settings s_Instance;

    [Header("World options")]
    public bool dayNightEfect = false;
    public bool loadWorld = false;
    public int WorldRadius = 13;
    public bool volumetricLightingOn = false;

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
    public Toggle ToggleVolumetricLighting;
    public Slider SliderAO;
    public Slider SliderWorldRadius;
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

    public void SetWorldRadius(float value)
    {
        WorldRadius = (int)value;
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

    public void SetVolumetricLighting(bool b)
    {
        volumetricLightingOn = !volumetricLightingOn;
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
        data.localLightsOn = indirectLightingOn;
        data.MinCoef = MinCoef;
        data.rayTracingOn = directLightingOn;
        data.reprojectionOn = reprojectionOn;
        data.StartCoef = StartCoef;
        data.varianceOn = varianceOn;
        data.depthOfRecursion = depthOfRecursion;
        data.softShadowsOn = softShadowsOn;
        data.worldRadius = WorldRadius;
        data.volumetricLighting = volumetricLightingOn;

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText("./Assets/Options/Settings.json", json);
    }

    private void LoadFromFile()
    {
        if (!File.Exists("./Assets/Options/Settings.json"))
            return;
        string json = File.ReadAllText("./Assets/Options/Settings.json");
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
        volumetricLightingOn = true;

        SliderAdaptCoef.value = AdaptCoef;
        SliderMinCoef.value = MinCoef;
        SliderStartCoef.value = StartCoef;
        SliderAO.value = AO;
        SliderWorldRadius.value = WorldRadius;
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
        if (!data.volumetricLighting)
            ToggleVolumetricLighting.isOn = data.volumetricLighting;
    }
}
