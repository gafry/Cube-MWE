using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class SceneManager : MonoBehaviour
{
	private RayTracingAccelerationStructure _accelerationStructure;

	public readonly int accelerationStructureShaderId = Shader.PropertyToID("_AccelerationStructure");

	private static SceneManager s_Instance;

	public static SceneManager Instance
	{
		get
		{
			if (s_Instance != null) return s_Instance;

			s_Instance = GameObject.FindObjectOfType<SceneManager>();
			s_Instance?.InitRaytracingAccelerationStructure();
			return s_Instance;
		}
	}

	// Update is called once per frame
	void Update()
	{
		if (_accelerationStructure != null)
			_accelerationStructure.Update();
	}

	public RayTracingAccelerationStructure RequestAccelerationStructure()
	{
		return _accelerationStructure;
	}

	private void InitRaytracingAccelerationStructure()
	{
		RayTracingAccelerationStructure.RASSettings settings = new RayTracingAccelerationStructure.RASSettings();
		// include default layer, not lights
		settings.layerMask = -1;
		// enable automatic updates
		settings.managementMode = RayTracingAccelerationStructure.ManagementMode.Automatic;
		// include all renderer types
		settings.rayTracingModeMask = RayTracingAccelerationStructure.RayTracingModeMask.Everything;

		_accelerationStructure = new RayTracingAccelerationStructure(settings);

		// collect all objects in scene and add them to raytracing scene
		Renderer[] renderers = FindObjectsOfType<Renderer>();
		foreach (Renderer r in renderers)
		{
			if (r.CompareTag("Light")) _accelerationStructure.AddInstance(r, null, null, true, false, 16);
			else _accelerationStructure.AddInstance(r, null, null, true, false, 8);
		}
		
		// build raytrasing scene
		_accelerationStructure.Build();
	}
}
