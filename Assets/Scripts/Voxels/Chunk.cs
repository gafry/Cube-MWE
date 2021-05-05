using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using System.Linq;

public class Chunk
{
    private MeshFilter _meshFilter;

    private Material _material;

    private GenerateMeshData.MeshData _meshData;
    private GenerateMeshData _chunkJob;

    private GenerateBlocksJob.ChunkData _chunkData;
    private GenerateBlocksJob _generateBlocksJob;

    private GenerateTreesJob _generateTreesJob;
    private GenerateTreesJob.MeshData _treesMeshData;
    private NativeArray<Block> _treeBlocks;

    public GameObject chunk;

    public Chunk() { }

    public Chunk(Vector3Int position, Material material)
    {
        chunk = new GameObject();
        chunk.transform.position = position;
        _material = material;
    }

    public JobHandle GenerateBlocks(NativeList<Vector2> Centroids)
    {
        _chunkData = new GenerateBlocksJob.ChunkData
        {
            blocks = new NativeList<Block>(Allocator.Persistent),
            trees = new NativeList<Vector3>(Allocator.Persistent),
            isEmpty = new NativeArray<bool>(1, Allocator.Persistent)
        };

        _generateBlocksJob = new GenerateBlocksJob
        {
            chunkData = _chunkData,
            position = chunk.transform.position,
            centroids = Centroids
        };

        JobHandle jobHandle = _generateBlocksJob.Schedule();

        return jobHandle;
    }

    public JobHandle GenerateMeshData()
    {
        _meshData = new GenerateMeshData.MeshData
        {
            vertices = new NativeList<int3>(Allocator.Persistent),
            triangles = new NativeList<int>(Allocator.Persistent),
            uvs = new NativeList<Vector2>(Allocator.Persistent)
        };

        _chunkJob = new GenerateMeshData
        {
            meshData = _meshData,
            chunkData = new GenerateMeshData.ChunkData
            {
                blocks = _chunkData.blocks
            },
            blockData = new GenerateMeshData.BlockData
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

    public bool IsEmpty()
    {
        return _chunkData.isEmpty[0];
    }

    public NativeList<Block> GetBlocks()
    {
        return _chunkData.blocks;
    }

    public bool AreTrees()
    {
        if (_chunkData.trees.Length >= 4)
            return true;
        else
            return false;
    }

    public JobHandle GenerateTrees()
    {
        _treesMeshData = new GenerateTreesJob.MeshData
        {
            vertices = new NativeList<int3>(Allocator.Persistent),
            triangles = new NativeList<int>(Allocator.Persistent),
            uvs = new NativeList<Vector2>(Allocator.Persistent)
        };

        _treeBlocks = new NativeArray<Block>(16 * 16 * 22, Allocator.Persistent);

        _generateTreesJob = new GenerateTreesJob
        {
            meshData = _treesMeshData,
            treeBlocks = _treeBlocks,
            trees = _chunkData.trees,
            blockData = new GenerateTreesJob.BlockData
            {
                vertices = BlockData.Vertices,
                triangles = BlockData.Triangles,
                uvs = BlockData.UVs
            }
        };

        JobHandle _jobHandle = _generateTreesJob.Schedule();

        return _jobHandle;
    }

    public void DisposeBlocks()
    {
        _chunkData.blocks.Dispose();
        _chunkData.trees.Dispose();
        _chunkData.isEmpty.Dispose();
    }

    public int FinishCreatingChunk()
    {
        if (!AreTrees())
        {
            Mesh mesh = new Mesh
            {
                vertices = _meshData.vertices.ToArray().Select(vertex => new Vector3(vertex.x, vertex.y, vertex.z)).ToArray(),
                triangles = _meshData.triangles.ToArray(),
                uv = _meshData.uvs.ToArray()
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _meshFilter.mesh = mesh;

            _meshData.vertices.Dispose();
            _meshData.triangles.Dispose();
            _meshData.uvs.Dispose();

            if (mesh.vertices.Length < 200)
                return 0;
            else
                return 1;
        }
        else
        {
            int triangleCount = _treesMeshData.vertices.Length;
            Mesh mesh = new Mesh
            {
                vertices = _treesMeshData.vertices.ToArray().Select(vertex => new Vector3(vertex.x, vertex.y, vertex.z)).ToArray()
                .Concat(_meshData.vertices.ToArray().Select(vertex => new Vector3(vertex.x, vertex.y, vertex.z)).ToArray()).ToArray(),
                triangles = _treesMeshData.triangles.ToArray().Concat(_meshData.triangles.ToArray().Select(triangle => triangleCount + triangle)).ToArray(),
                uv = _treesMeshData.uvs.ToArray().Concat(_meshData.uvs.ToArray()).ToArray()
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _meshFilter.mesh = mesh;

            _treeBlocks.Dispose();
            _treesMeshData.vertices.Dispose();
            _treesMeshData.triangles.Dispose();
            _treesMeshData.uvs.Dispose();
            _meshData.vertices.Dispose();
            _meshData.triangles.Dispose();
            _meshData.uvs.Dispose();

            return 2;
        }
    }
}