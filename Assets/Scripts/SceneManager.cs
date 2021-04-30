using UnityEngine;
using UnityEngine.Experimental.Rendering;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

public enum Phase : ushort
{
    Resting = 0x0000,
    GeneratingBlocks = 0x0001,
    GeneratingMeshData = 0x0002,
    GeneratingFlora = 0x0003,
    FillingMeshes = 0x0004,
    RemovingChunks = 0x0005
}

public class SceneManager : MonoBehaviour
{
    public Material material;

    public GameObject player;

    public GameObject sun;

    public static Dictionary<Vector3Int, Chunk> chunks;

    private int _chunkSize = 16;
    private int _chunkSizeHalf = 8;
    private int _radius = 13;
    private Vector3Int _chunkWherePlayerStood;

    private NativeList<JobHandle> _jobHandles;
    private bool _jobHandlesAlocated;
    private int _maxJobsAtOnce;
    private Phase phase = Phase.Resting;
    private List<Chunk> _toGenerate;
    private List<Vector3Int> _toRemove;

    public NativeList<Vector2> centroids;

    private int _iter;
    private int runningJobs;

    private RayTracingAccelerationStructure _accelerationStructure;

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
        Random.InitState(420420);

        // initialization of lists and variables
        _maxJobsAtOnce = System.Environment.ProcessorCount - 1;

        _toGenerate = new List<Chunk>();
        _toRemove = new List<Vector3Int>();

        runningJobs = 0;

        chunks = new Dictionary<Vector3Int, Chunk>();

        centroids = new NativeList<Vector2>(Allocator.Persistent);
        centroids.Add(new Vector2(Random.Range(-300, 300), Random.Range(-300, 300)));
        centroids.Add(new Vector2(Random.Range(-300, 300), Random.Range(-300, 300)));
        centroids.Add(new Vector2(Random.Range(-300, 300), Random.Range(-300, 300)));
        centroids.Add(new Vector2(Random.Range(-300, 300), Random.Range(-300, 300)));
        centroids.Add(new Vector2(Random.Range(-300, 300), Random.Range(-300, 300)));
        centroids.Add(new Vector2(Random.Range(-300, 300), Random.Range(-300, 300)));
        centroids.Add(new Vector2(Random.Range(-300, 300), Random.Range(-300, 300)));
        centroids.Add(new Vector2(Random.Range(-300, 300), Random.Range(-300, 300)));
        centroids.Add(new Vector2(Random.Range(-300, 300), Random.Range(-300, 300)));
        centroids.Add(new Vector2(Random.Range(-300, 300), Random.Range(-300, 300)));

        // create starting chunks
        _jobHandles = new NativeList<JobHandle>(Allocator.Temp);
        Vector3Int chunkWherePlayerStands = ChunkWherePlayerStands();
        int radiusInChunks = _chunkSize * _radius;
        FindMissingChunks(chunkWherePlayerStands, radiusInChunks);

        foreach (var chunkToGen in _toGenerate)
            _jobHandles.Add(chunkToGen.GenerateBlocks(centroids));

        JobHandle.CompleteAll(_jobHandles);
        _jobHandles.Dispose();

        _jobHandles = new NativeList<JobHandle>(Allocator.Temp);

        foreach (var chunkToGen in _toGenerate)
        {
            if (!chunkToGen.IsEmpty())
            {
                _jobHandles.Add(chunkToGen.GenerateMeshData());
                chunkToGen.PrepareMesh();
            }
        }

        JobHandle.CompleteAll(_jobHandles);
        _jobHandles.Dispose();

        _jobHandles = new NativeList<JobHandle>(Allocator.Temp);

        foreach (var chunkToGen in _toGenerate)
        {
            if (chunkToGen.AreTrees())
            {
                _jobHandles.Add(chunkToGen.GenerateTrees());
            }
        }

        JobHandle.CompleteAll(_jobHandles);
        _jobHandles.Dispose();

        foreach (var chunkToGen in _toGenerate)
            if (!chunkToGen.IsEmpty())
                chunkToGen.FinishCreatingChunk();

        _toGenerate.Clear();

        InitRaytracingAccelerationStructure();

        if (Settings.Instance.loadWorld)
        {
            _jobHandles = new NativeList<JobHandle>(Allocator.Persistent);
            _jobHandlesAlocated = true;
        }
        else
        {
            _jobHandlesAlocated = false;
        }
    }

    // Update is called once per frame
    //void Update()
    public void Update()
    {
        // if loading world is not enabled, build AS and return
        if (!Settings.Instance.loadWorld)
        {
            if (_accelerationStructure != null)
            {
                _accelerationStructure.UpdateInstanceTransform(sun.GetComponent<Renderer>());
                _accelerationStructure.Build();
            }

            return;
        }

        if (runningJobs > 0)
        {
            runningJobs = 0;
            JobHandle.CompleteAll(_jobHandles);
        }
        if (_jobHandlesAlocated)
            _jobHandles.Dispose();
        _jobHandles = new NativeList<JobHandle>(Allocator.Persistent);
        _jobHandlesAlocated = true;

        Vector3Int chunkWherePlayerStands = ChunkWherePlayerStands();
        if (!chunkWherePlayerStands.Equals(_chunkWherePlayerStood) && phase == Phase.Resting)
        {
            _chunkWherePlayerStood = chunkWherePlayerStands;

            int radiusInChunks = _chunkSize * _radius;
            FindMissingChunks(chunkWherePlayerStands, radiusInChunks);

            if (_toGenerate.Count > 0)
            {
                _iter = _toGenerate.Count - 1;
                phase = Phase.GeneratingBlocks;
            }
            else
            {
                phase = Phase.Resting;
            }

        }
        else if (phase == Phase.GeneratingBlocks)
        {
            for (; _iter >= 0 && runningJobs < _maxJobsAtOnce * 5; _iter--)
            {
                _jobHandles.Add(_toGenerate[_iter].GenerateBlocks(centroids));
                runningJobs++;
            }

            if (_iter < 0)
            {
                phase = Phase.GeneratingMeshData;
                _iter = _toGenerate.Count - 1;
            }
        }
        else if (phase == Phase.GeneratingMeshData)
        {
            for (; _iter >= 0 && runningJobs <= _maxJobsAtOnce * 4; _iter--)
            {
                if (!_toGenerate[_iter].IsEmpty())
                {
                    _jobHandles.Add(_toGenerate[_iter].GenerateMeshData());
                    _toGenerate[_iter].PrepareMesh();
                    runningJobs++;
                }
            }

            if (_iter < 0)
            {
                phase = Phase.GeneratingFlora;
                _iter = _toGenerate.Count - 1;
            }
        }
        else if (phase == Phase.GeneratingFlora)
        {
            for (; _iter >= 0 && runningJobs <= _maxJobsAtOnce * 4; _iter--)
            {
                if (_toGenerate[_iter].AreTrees())
                {
                    _jobHandles.Add(_toGenerate[_iter].GenerateTrees());
                    runningJobs++;
                }
            }

            if (_iter < 0)
            {
                phase = Phase.FillingMeshes;
                _iter = _toGenerate.Count - 1;
            }
        }
        else if (phase == Phase.FillingMeshes)
        {
            int runningFinishes = 0;

            for (; _iter >= 0 && runningFinishes < 2; _iter--)
            {
                if (!_toGenerate[_iter].IsEmpty())
                {
                    runningFinishes += _toGenerate[_iter].FinishCreatingChunk();
                    _accelerationStructure.AddInstance(_toGenerate[_iter].chunk.GetComponent<Renderer>(), null, null, true, false, 0x01);
                }
            }

            if (_iter < 0)
            {
                phase = Phase.RemovingChunks;
                _toGenerate.Clear();
            }
        }
        else if (phase == Phase.RemovingChunks)
        {
            int radiusInChunks = _chunkSize * _radius;
            RemoveChunks(chunkWherePlayerStands, radiusInChunks);
            if (_iter < 0)
                phase = Phase.Resting;
        }
        else
        {
            phase = Phase.Resting;
        }

        if (_accelerationStructure != null)
        {
            //_accelerationStructure.RemoveInstance(sun.GetComponent<Renderer>());
            //_accelerationStructure.AddInstance(sun.GetComponent<Renderer>(), null, null, true, false, 0x10);
            _accelerationStructure.UpdateInstanceTransform(sun.GetComponent<Renderer>());
            _accelerationStructure.Build();
        }

        if (phase == Phase.Resting)
            Settings.Instance.reprojectWithIDs = true;
        else
            Settings.Instance.reprojectWithIDs = false;
    }

    // Loops through all possible chunks and saves those that are not created yet to _toFinish list
    public void FindMissingChunks(Vector3Int chunkPosition, int radiusInChunks)
    {
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
                    if (!chunks.ContainsKey(new Vector3Int(x, 0, z)))
                    {
                        for (int y = 0; y < 3; y++)
                        {
                            Chunk chunk = new Chunk(new Vector3Int(x, y * 16, z), material);
                            chunk.chunk.transform.parent = this.transform;

                            chunks.Add(new Vector3Int(x, y * 16, z), chunk);
                            _toGenerate.Add(chunk);
                        }
                    }
                }
            }
        }
    }

    // Loop through all chunks in the scene and check their distance,
    // if not in radius, destroy them
    public void RemoveChunks(Vector3Int chunkPosition, int radiusInChunks)
    {
        foreach (KeyValuePair<Vector3Int, Chunk> pair in chunks)
        {
            Vector3 chunkPos = pair.Value.chunk.transform.position;
            Vector2 heading;
            heading.x = chunkPosition.x + _chunkSizeHalf - chunkPos.x;
            heading.y = chunkPosition.z + _chunkSizeHalf - chunkPos.z;
            float distanceSquared = heading.x * heading.x + heading.y * heading.y;
            float distance = Mathf.Sqrt(distanceSquared);
            if (distance > radiusInChunks + 2)
            {
                if (!pair.Value.IsEmpty())
                    _accelerationStructure.RemoveInstance(pair.Value.chunk.GetComponent<Renderer>());
                pair.Value.DisposeBlocks();
                Destroy(pair.Value.chunk);
                _toRemove.Add(pair.Key);
            }
        }

        foreach (Vector3Int key in _toRemove)
        {
            chunks.Remove(key);
        }

        _toRemove.Clear();
    }

    // Returns chunk position of the chunk where player stands at the moment
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

    private void OnDestroy()
    {
        // When the program ends, its necessary to
        // dealocate data in BlockData native containers
        BlockData.Vertices.Dispose();
        BlockData.Triangles.Dispose();
        BlockData.UVs.Dispose();

        foreach (KeyValuePair<Vector3Int, Chunk> pair in chunks)
        {
            pair.Value.DisposeBlocks();
            Destroy(pair.Value.chunk);
        }

        centroids.Dispose();
        if (_jobHandlesAlocated)
            _jobHandles.Dispose();
    }

    public RayTracingAccelerationStructure RequestAccelerationStructure()
    {
        return _accelerationStructure;
    }

    public Vector3 GetSunPosition()
    {
        return sun.transform.position;
    }

    public float GetSunProgress()
    {
        Sun sunScript = sun.GetComponent<Sun>();
        return sunScript.progress;
    }

    public Vector3 GetPlayerPosition()
    {
        return player.transform.position;
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
}