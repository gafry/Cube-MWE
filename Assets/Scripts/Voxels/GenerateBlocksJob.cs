using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

public enum Biome : ushort
{
    plain = 0x0000,
    forest = 0x0001,
    wasteland = 0x0002
}

[BurstCompile]
public struct GenerateBlocksJob : IJob
{
    public struct ChunkData
    {
        public NativeList<Block> blocks { get; set; }
        public NativeList<Vector3> trees;
        public NativeArray<bool> isEmpty;
    }

    [WriteOnly]
    public ChunkData chunkData;

    [ReadOnly]
    public Vector3 position;

    [ReadOnly]
    public NativeList<Vector2> centroids;

    public void Execute()
    {
        chunkData.isEmpty[0] = true;

        for (int x = -1; x < 17; x++)
        {
            for (int z = -1; z < 17; z++)
            {
                var y = GenerateHeight(((int)position.x + x + 32000), ((int)position.z + z + 32000));
                var yStone = GenerateStoneHeight(((int)position.x + x + 32000), ((int)position.z + z + 32000));

                for (int i = (int)position.y - 1; i < (int)position.y + 17; i++)
                {
                    if (GetBiome(x, z) == Biome.plain)
                    {
                        if (i > 42 && i <= y)
                        {
                            chunkData.isEmpty[0] = false;
                            chunkData.blocks.Add(Block.ice);
                        }
                        else if (i < yStone && i <= y)
                        {
                            chunkData.isEmpty[0] = false;
                            chunkData.blocks.Add(Block.stone);
                        }
                        else if (i <= y)
                        {
                            chunkData.isEmpty[0] = false;
                            chunkData.blocks.Add(Block.dirt);
                        }
                        else
                            chunkData.blocks.Add(Block.air);
                    }
                    else if (GetBiome(x, z) == Biome.forest)
                    {
                        if (i > 42 && i <= y)
                        {
                            chunkData.isEmpty[0] = false;
                            chunkData.blocks.Add(Block.ice);
                        }
                        else if (i <= y)
                        {
                            chunkData.isEmpty[0] = false;
                            chunkData.blocks.Add(Block.dirt);
                            if (i == y && i < (int)position.y + 15 && i > (int)position.y - 1 && x >= 0 && x < 16 && z >= 0 && z < 16)
                            {
                                chunkData.trees.Add(new Vector3(x, (y + 1) % 16, z));
                            }
                        }
                        else
                            chunkData.blocks.Add(Block.air);
                    }
                    else if (GetBiome(x, z) == Biome.wasteland)
                    {
                        if (i > 42 && i <= y)
                        {
                            chunkData.isEmpty[0] = false;
                            chunkData.blocks.Add(Block.ice);
                        }
                        else if (i < yStone && i <= y)
                        {
                            chunkData.isEmpty[0] = false;
                            chunkData.blocks.Add(Block.sandstone);
                        }
                        else if (i <= y)
                        {
                            chunkData.isEmpty[0] = false;
                            chunkData.blocks.Add(Block.sand);
                        }
                        else
                            chunkData.blocks.Add(Block.air);
                    }
                    else
                    {
                        if (i > 42 && i <= y)
                        {
                            chunkData.isEmpty[0] = false;
                            chunkData.blocks.Add(Block.ice);
                        }
                        else if (i <= y)
                        {
                            chunkData.isEmpty[0] = false;
                            chunkData.blocks.Add(Block.stone);
                        }
                        else
                            chunkData.blocks.Add(Block.air);
                    }
                }
            }
        }
    }

    public int GenerateStoneHeight(float x, float z)
    {
        int maxHeight = 47;
        float smooth = 0.01f;
        int octaves = 4;
        float persistence = 0.5f;
        float height = Map(0, maxHeight - 5, 0, 1, fBM(x * smooth * 2, z * smooth * 2, octaves + 1, persistence));
        return (int)height;
    }

    public int GenerateHeight(float x, float z)
    {
        int maxHeight = 47;
        float smooth = 0.01f;
        int octaves = 4;
        float persistence = 0.55f;
        float height = Map(0, maxHeight, 0, 1, fBM(x * smooth, z * smooth, octaves, persistence));
        return (int)height;
    }

    public float Map(float newmin, float newmax, float origmin, float origmax, float value)
    {
        return Mathf.Lerp(newmin, newmax, Mathf.InverseLerp(origmin, origmax, value));
    }

    public float fBM(float x, float z, int oct, float pers)
    {
        float total = 0;
        float frequency = 1;
        float amplitude = 1;
        float maxValue = 0;
        for (int i = 0; i < oct; i++)
        {
            total += Mathf.PerlinNoise(x * frequency, z * frequency) * amplitude;

            maxValue += amplitude;

            amplitude *= pers;
            frequency *= 2;
        }

        return total / maxValue;
    }

    public Biome GetBiome(int x, int z)
    {
        var pos = new Vector2(position.x + x, position.z + z);
        float smalestDistance = float.MaxValue;
        int smalestIndex = 0;

        for (int i = 0; i < centroids.Length; i++)
        {
            Vector2 heading;
            heading.x = centroids[i].x - pos.x;
            heading.y = centroids[i].y - pos.y;
            float distanceSquared = heading.x * heading.x + heading.y * heading.y;
            float distance = Mathf.Sqrt(distanceSquared);
            if (distance < smalestDistance)
            {
                smalestDistance = distance;
                smalestIndex = i;
            }
        }

        return (Biome)(smalestIndex % 3);
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
}