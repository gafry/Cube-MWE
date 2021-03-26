using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using PathCreation;

public enum Phase : ushort
{
	StartingJobs = 0x0000,
	FinishingJobs = 0x0001,
	Resting = 0x0002
}

public class SceneManager : MonoBehaviour
{
	public Material material;

	public GameObject player;

	public GameObject sun;

	//public PathCreator pathCreator;

	public static Dictionary<string, Chunk> chunks;

	private int _chunkSize = 16;
	private int _chunkSizeHalf = 8;
	private int _radius = 13;
	private string _chunkWherePlayerStood = "";

	private NativeList<JobHandle> _jobHandles;
	private bool _jobsDone = true;
	private int _maxJobsAtOnce;
	private Phase phase = Phase.StartingJobs;
	private List<Chunk> _toFinish = new List<Chunk>();
	private bool runningJobs = false;

	private RayTracingAccelerationStructure _accelerationStructure;

	public readonly int accelerationStructureShaderId = Shader.PropertyToID("_AccelerationStructure");

	private static SceneManager s_Instance;

	public static SceneManager Instance
	{
		get
		{
			if (s_Instance != null) return s_Instance;
			
			s_Instance = GameObject.FindObjectOfType<SceneManager>();
			return s_Instance;
		}
	}

	private void Start()
	{
		_maxJobsAtOnce = Mathf.Max(SystemInfo.processorCount / 2, 2);

		chunks = new Dictionary<string, Chunk>();
		_jobHandles = new NativeList<JobHandle>(Allocator.Temp);

		for (int x = -(_chunkSize * _radius); x <= (_chunkSize * _radius); x += _chunkSize)
		{
			for (int z = -(_chunkSize * _radius); z <= (_chunkSize * _radius); z += _chunkSize)
			{
				string name = BuildChunkName(new Vector3(x, 0, z));
				Chunk chunk = new Chunk(new Vector3(x, 0, z), material, name);
				chunk.chunk.transform.parent = this.transform;

				chunks.Add(name, chunk);

				JobHandle jobHandle = chunk.StartCreatingChunk();
				_jobHandles.Add(jobHandle);
			}
		}

		foreach (KeyValuePair<string, Chunk> pair in chunks)
		{
			pair.Value.PrepareMesh();
		}

		JobHandle.CompleteAll(_jobHandles);

		foreach (KeyValuePair<string, Chunk> pair in chunks)
		{
			pair.Value.FinishCreatingChunk();
		}

		_jobHandles.Dispose();

		InitRaytracingAccelerationStructure();
	}

	// Update is called once per frame
	//void Update()
	public void StartUpdate()
	{
		Vector3Int chunkWherePlayerStandsV3 = ChunkWherePlayerStands();
		string chunkWherePlayerStands = BuildChunkName(chunkWherePlayerStandsV3);
		int jobsCounter = 0;

		// If the player stands on the new chunk, calculate distance
		// from this chunks center to other chunks centers in specified radius.
		// If the chunk is in the distance, its added to dictionary and
		// his mesh is created on another thread.
		// Then, in meantime, loop the dictionary, if any chunk is too far away, remove
		// it from the dictionary. Then finish processed chunks.
		if (Settings.Instance.loadWorld)
		{
			if ((!chunkWherePlayerStands.Equals(_chunkWherePlayerStood) || !_jobsDone) && phase == Phase.StartingJobs)
			{
				//List<Chunk> toFinish = new List<Chunk>();
				List<string> toRemove = new List<string>();
				_jobHandles = new NativeList<JobHandle>(Allocator.Temp);

				Vector3Int chunkPosition = chunkWherePlayerStandsV3;
				int radiusInChunks = (_chunkSize * _radius);

				for (int x = chunkPosition.x - radiusInChunks; x < chunkPosition.x + radiusInChunks + 1; x += _chunkSize)
				{
					for (int z = chunkPosition.z - radiusInChunks; z < chunkPosition.z + radiusInChunks + 1; z += _chunkSize)
					{
						Vector2 heading;
						heading.x = chunkPosition.x + _chunkSizeHalf - x;
						heading.y = chunkPosition.z + _chunkSizeHalf - z;
						float distanceSquared = heading.x * heading.x + heading.y * heading.y;
						float distance = Mathf.Sqrt(distanceSquared);
						if (distance <= radiusInChunks)
						{
							string chunkName = BuildChunkName(new Vector3(x, 0, z));
							if (!chunks.ContainsKey(chunkName))
							{
								Chunk chunk = new Chunk(new Vector3(x, 0, z), material, chunkName);
								chunk.chunk.transform.parent = this.transform;

								chunks.Add(chunkName, chunk);
								_toFinish.Add(chunk);

								JobHandle jobHandle = chunk.StartCreatingChunk();
								_jobHandles.Add(jobHandle);

								jobsCounter++;
							}
						}

						if (jobsCounter == _maxJobsAtOnce)
							break;
					}

					if (jobsCounter == _maxJobsAtOnce)
						break;
				}

				foreach (KeyValuePair<string, Chunk> pair in chunks)
				{
					Vector3 chunkPos = pair.Value.chunk.transform.position;
					Vector2 heading;
					heading.x = chunkPosition.x + _chunkSizeHalf - chunkPos.x;
					heading.y = chunkPosition.z + _chunkSizeHalf - chunkPos.z;
					float distanceSquared = heading.x * heading.x + heading.y * heading.y;
					float distance = Mathf.Sqrt(distanceSquared);
					if (distance > radiusInChunks + 2)
					{
						_accelerationStructure.RemoveInstance(pair.Value.chunk.GetComponent<Renderer>());						
						Destroy(pair.Value.chunk);
						toRemove.Add(pair.Key);
					}
				}

				foreach (string key in toRemove)
				{
					chunks.Remove(key);
				}

				foreach (Chunk chunk in _toFinish)
				{
					chunk.PrepareMesh();
				}

				toRemove.Clear();

				_chunkWherePlayerStood = chunkWherePlayerStands;

				if (jobsCounter == _maxJobsAtOnce)
					_jobsDone = false;
				else
					_jobsDone = true;

				runningJobs = true;
				/*phase = Phase.FinishingJobs;

				JobHandle.CompleteAll(_jobHandles);

				_jobHandles.Dispose();*/
			}
			else if (phase == Phase.FinishingJobs)
			{
				foreach (Chunk chunk in _toFinish)
				{
					chunk.FinishCreatingChunk();
					_accelerationStructure.AddInstance(chunk.chunk.GetComponent<Renderer>(), null, null, true, false, 0x01);
				}

				_toFinish.Clear();

				phase = Phase.Resting;
			}
			else
			{
				phase = Phase.StartingJobs;
			}
		}

		if (_accelerationStructure != null)
        {
			//_accelerationStructure.RemoveInstance(sun.GetComponent<Renderer>());
			//_accelerationStructure.AddInstance(sun.GetComponent<Renderer>(), null, null, true, false, 0x10);
			_accelerationStructure.UpdateInstanceTransform(sun.GetComponent<Renderer>());
			_accelerationStructure.Build();
		}
	}

	public void FinishUpdate()
	{
		if (Settings.Instance.loadWorld)
		{
			if (runningJobs && phase == Phase.StartingJobs)
			{
				phase = Phase.FinishingJobs;

				JobHandle.CompleteAll(_jobHandles);

				_jobHandles.Dispose();

				runningJobs = false;
			}
		}
	}

	private Vector3Int ChunkWherePlayerStands()
	{
		int x = ((int)player.transform.position.x / _chunkSize) * _chunkSize;
		int z;

		if (player.transform.position.z < 0)
			z = (((int)player.transform.position.z / _chunkSize) - 1) * _chunkSize;
		else
			z = ((int)player.transform.position.z / _chunkSize) * _chunkSize;

		return new Vector3Int(x, 0, z);
	}

	public static string BuildChunkName(Vector3 position)
	{
		// Builds chunk name based on position 
		return (int)position.x + "_" + (int)position.y + "_" + (int)position.z;
	}

	public Vector3 GetSunPosition()
    {
		return sun.transform.position;
    }

	/*public void GetSunProgress()
    {
		return (distanceTravelled % pathCreator.path.length) / pathCreator.path.length;
	}*/

	private void OnDestroy()
	{
		// When the program ends, its necessary to
		// dealocate data in BlockData native containers
		BlockData.Vertices.Dispose();
		BlockData.Triangles.Dispose();
		BlockData.UVs.Dispose();
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
		settings.managementMode = RayTracingAccelerationStructure.ManagementMode.Manual;
		// include all renderer types
		settings.rayTracingModeMask = RayTracingAccelerationStructure.RayTracingModeMask.Everything;

		_accelerationStructure = new RayTracingAccelerationStructure(settings);

		// collect all objects in scene and add them to raytracing scene
		Renderer[] renderers = FindObjectsOfType<Renderer>();
		foreach (Renderer r in renderers)
		{
			if (r.CompareTag("Light"))
			{
				// mask for lights is 0x10 (for shadow rays - dont want to check intersection)
				_accelerationStructure.AddInstance(r, null, null, true, false, 0x10);
			}
			else
			{
				_accelerationStructure.AddInstance(r, null, null, true, false, 0x01);
			}
		}
		
		// build raytracing AS
		_accelerationStructure.Build();
	}

	public void AddInstanceToAS(Renderer renderer)
    {
		_accelerationStructure.AddInstance(renderer, null, null, true, false, 0x01);
		_accelerationStructure.Build();
	}
}
