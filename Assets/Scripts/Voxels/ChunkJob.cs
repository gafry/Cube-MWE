using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

[BurstCompile]
public struct ChunkJob : IJob
{
    public struct MeshData
    {
        public NativeList<int3> vertices { get; set; }
        public NativeList<int> triangles { get; set; }
        public NativeList<Vector2> uvs { get; set; }
    }

    public struct BlockData
    {
        public NativeArray<int3> vertices { get; set; }
        public NativeArray<int> triangles { get; set; }
        public NativeArray<float2x4> uvs { get; set; }
    }

    public struct ChunkData
    {
        public NativeArray<Block> blocks { get; set; }
    }

    [WriteOnly]
    public MeshData meshData;

    [ReadOnly]
    public ChunkData chunkData;

    [ReadOnly]
    public BlockData blockData;

    private int vCount;

    public void Execute()
    {
        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                for (int y = 0; y < 16; y++)
                {
                    Block block = chunkData.blocks[BlockUtils.GetBlockIndex(new int3(x, y, z))];
                    if (block.IsEmpty())
                    {
                        continue;
                    }

                    for (int i = 0; i < 6; i++)
                    {
                        if (Check(BlockUtils.GetPositionInDirection((Directions)i, x, y, z)))
                        {
                            CreateFace((Directions)i, new int3(x, y, z));
                            meshData.uvs.Add(blockData.uvs[(int)block][0]);
                            meshData.uvs.Add(blockData.uvs[(int)block][1]);
                            meshData.uvs.Add(blockData.uvs[(int)block][2]);
                            meshData.uvs.Add(blockData.uvs[(int)block][3]);
                        }
                    }
                }
            }
        }
    }

    private void CreateFace(Directions direction, int3 pos)
    {
        var _vertices = GetFaceVertices(direction, 1, new int3(pos.x, pos.y, pos.z));

        meshData.vertices.AddRange(_vertices);

        _vertices.Dispose();

        vCount += 4;

        meshData.triangles.Add(vCount - 4);
        meshData.triangles.Add(vCount - 4 + 1);
        meshData.triangles.Add(vCount - 4 + 2);
        meshData.triangles.Add(vCount - 4);
        meshData.triangles.Add(vCount - 4 + 2);
        meshData.triangles.Add(vCount - 4 + 3);

        /*meshData.uvs.Add(new Vector2(0, 0));
        meshData.uvs.Add(new Vector2(0, 0.5f));
        meshData.uvs.Add(new Vector2(0.5f, 0));
        meshData.uvs.Add(new Vector2(0.5f, 0.5f));*/
    }

    private bool Check(int3 position)
    {
        if (position.x >= 16 || position.x < 0 || position.z >= 16 || position.z < 0 || position.y < 0)
            return true;
        if (position.y >= 16)
            return false;

        return chunkData.blocks[BlockUtils.GetBlockIndex(position)].IsEmpty();
    }

    public NativeArray<int3> GetFaceVertices(Directions direction, int scale, int3 position)
    {
        NativeArray<int3> faceVertices = new NativeArray<int3>(4, Allocator.Temp);

        for (int i = 0; i < 4; i++)
        {
            int index = blockData.triangles[(int)direction * 4 + i];
            faceVertices[i] = blockData.vertices[index] * scale + position;
        }

        return faceVertices;
    }
}
