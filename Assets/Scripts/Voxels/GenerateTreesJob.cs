using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

[BurstCompile]
public struct GenerateTreesJob : IJob
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

    [WriteOnly]
    public MeshData meshData;

    [ReadOnly]
    public BlockData blockData;

    [ReadOnly]
    public NativeList<Vector3> trees;

    public NativeArray<Block> treeBlocks;

    private int vCount;

    public void Execute()
    {
        for (int i = 0; i < 16 * 22 * 16; i++)
            treeBlocks[i] = Block.air;

        for (int i = 0; i < trees.Length; i++)
        {
            uint hash = Hash((uint)(i + trees[i].x * 1007 + trees[i].y * 53 + trees[i].z)) % 1000;
            if ((hash > 10 && hash < 15) || (hash > 100 && hash < 105))
                GenerateTree(new Vector3(trees[i].x, trees[i].y, trees[i].z));
        }

        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                for (int y = 0; y < 22; y++)
                {
                    Block block = treeBlocks[BlockUtils.GetBlockIndex16x22x16(new int3(x, y, z))];
                    if (block.IsEmpty())
                    {
                        continue;
                    }

                    // Loop all posible directions on cube and if
                    // the next block in the direction is not empty,
                    // save UVs to mesh data
                    for (int i = 0; i < 6; i++)
                    {
                        if (Check(BlockUtils.GetPositionInDirection((Directions)i, x, y, z)) && ((Directions)i != Directions.Down || block != Block.tree))
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

    public void GenerateTree(Vector3 position)
    {
        if (22 - position.y + 4 < 0 || position.x == 0 || position.x == 15 || position.z == 0 || position.z == 15)
            return;

        int height = 4 + (int)(Hash((uint)(position.x * 1007 + position.y * 53 + position.z)) % 7);
        int treeHeight = Mathf.Min(2 + (int)(Hash((uint)(position.x * 1007 + position.y * 53 + position.z)) % 3), height - 3);

        for (int i = 0; i < height; i++)
        {
            if (position.y + i >= 22)
                continue;

            if (i == height - 1)
                treeBlocks[BlockUtils.GetBlockIndex16x22x16(new int3((int)position.x, (int)position.y + i, (int)position.z))] = Block.leaves;
            else
                treeBlocks[BlockUtils.GetBlockIndex16x22x16(new int3((int)position.x, (int)position.y + i, (int)position.z))] = Block.tree;

            // leaves
            if (i >= treeHeight)
            {
                for (int x = -2; x <= 2; x++)
                {
                    for (int z = -2; z <= 2; z++)
                    {
                        if ((x == 0 && z == 0) || (int)position.x + x >= 16 || (int)position.x + x < 0 ||
                            (int)position.z + z < 0 || (int)position.z + z >= 16)
                            continue;

                        Block block = treeBlocks[BlockUtils.GetBlockIndex16x22x16(new int3((int)position.x + x, (int)position.y + i, (int)position.z + z))];
                        if (block != Block.tree)
                            treeBlocks[BlockUtils.GetBlockIndex16x22x16(new int3((int)position.x + x, (int)position.y + i, (int)position.z + z))] = Block.leaves;
                    }
                }
            }
        }
    }

    public uint Hash(uint input)
    {
        input ^= 2747636419u;
        input *= 2654435769u;
        input ^= input >> 16;
        input *= 2654435769u;
        input ^= input >> 16;
        input *= 2654435769u;

        return input;
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
    }

    private bool Check(int3 position)
    {
        if (position.x < 0 || position.x >= 16 || position.z < 0 || position.z >= 16 || position.y >= 22)
            return true;
        else if (position.y < 0)
            return false;

        return treeBlocks[BlockUtils.GetBlockIndex16x22x16(position)].IsEmpty();
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