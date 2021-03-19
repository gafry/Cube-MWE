using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using System.Linq;

public class Chunk
{
    private MeshFilter _meshFilter;

    private int _chunkSize = 16;

    private Material _material;

    private ChunkJob.MeshData _meshData;
    private NativeArray<Block> _blocks;
    private ChunkJob _chunkJob;

    public GameObject chunk;

    public Chunk() { }

    public Chunk(Vector3 position, Material material, string name)
    {
        chunk = new GameObject("chunk_" + name);
        chunk.transform.position = position;
        _material = material;
    }

    public JobHandle StartCreatingChunk()
    {
        _blocks = new NativeArray<Block>(_chunkSize * _chunkSize * _chunkSize, Allocator.TempJob);

        float perlinCoef = 0.045f;
        float perlinStoneCoef = 0.15f;

        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                var y = Mathf.Min(Mathf.Max(Mathf.FloorToInt(Mathf.PerlinNoise((chunk.transform.position.x + (x + 32000)) * perlinCoef, (chunk.transform.position.z + (z + 32000)) * perlinCoef) * _chunkSize), 1), 15);
                var stoneY = Mathf.FloorToInt(Mathf.PerlinNoise((chunk.transform.position.x + (x + 32000)) * perlinStoneCoef, (chunk.transform.position.z + (z + 32000)) * perlinStoneCoef) * _chunkSize);

                for (int i = 0; i < y; i++)
                {
                    if (i <= stoneY)
                        _blocks[BlockUtils.GetBlockIndex(new int3(x, i, z))] = Block.wall;
                    else
                        _blocks[BlockUtils.GetBlockIndex(new int3(x, i, z))] = Block.dirt;
                }

                for (int i = y; i < _chunkSize; i++)
                {
                    _blocks[BlockUtils.GetBlockIndex(new int3(x, i, z))] = Block.air;
                }
            }
        }

        _meshData = new ChunkJob.MeshData
        {
            vertices = new NativeList<int3>(Allocator.TempJob),
            triangles = new NativeList<int>(Allocator.TempJob),
            uvs = new NativeList<Vector2>(Allocator.TempJob)
        };

        _chunkJob = new ChunkJob
        {
            meshData = _meshData,
            chunkData = new ChunkJob.ChunkData
            {
                blocks = _blocks
            },
            blockData = new ChunkJob.BlockData
            {
                vertices = BlockData.Vertices,
                triangles = BlockData.Triangles,
                uvs = BlockData.UVs
            }
        };

        JobHandle _jobHandle = _chunkJob.Schedule();

        return _jobHandle;
    }

    public void PrepareMesh()
    {
        MeshRenderer renderer = chunk.gameObject.AddComponent(typeof(MeshRenderer)) as MeshRenderer;
        renderer.material = _material;

        _meshFilter = (MeshFilter)chunk.gameObject.AddComponent(typeof(MeshFilter));
    }

    public void FinishCreatingChunk()
    {
        Mesh mesh = new Mesh
        {
            vertices = _meshData.vertices.ToArray().Select(vertex => new Vector3(vertex.x, vertex.y, vertex.z)).ToArray(),
            triangles = _meshData.triangles.ToArray(),
            uv = _meshData.uvs.ToArray()
        };

        _meshData.vertices.Dispose();
        _meshData.triangles.Dispose();
        _meshData.uvs.Dispose();
        _blocks.Dispose();

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        _meshFilter.mesh = mesh;
    }
}
