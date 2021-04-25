using Unity.Collections;
using Unity.Mathematics;
using System;

public enum Block : ushort
{
    air = 0x0000,
    grass = 0x0001,
    dirt = 0x0002,
    wall = 0x0003,
    stone = 0x0004,
    snow = 0x0005,
    ice = 0x0006,
    rock = 0x0007,
    none = 0x0008,
    sand = 0x0009,
    sandstone = 0x000a,
    tree = 0x000b,
    leaves = 0x000c,
}

public enum Directions
{
    Forward,
    Right,
    Back,
    Left,
    Up,
    Down
}

public struct BlockData
{
    [ReadOnly]
    public static readonly NativeArray<int3> Vertices = new NativeArray<int3>(8, Allocator.Persistent)
    {
        [0] = new int3(1, 1, 1),
        [1] = new int3(0, 1, 1),
        [2] = new int3(0, 0, 1),
        [3] = new int3(1, 0, 1),
        [4] = new int3(0, 1, 0),
        [5] = new int3(1, 1, 0),
        [6] = new int3(1, 0, 0),
        [7] = new int3(0, 0, 0)
    };

    [ReadOnly]
    public static readonly NativeArray<int> Triangles = new NativeArray<int>(24, Allocator.Persistent)
    {
        [0] = 0,
        [1] = 1,
        [2] = 2,
        [3] = 3,
        [4] = 5,
        [5] = 0,
        [6] = 3,
        [7] = 6,
        [8] = 4,
        [9] = 5,
        [10] = 6,
        [11] = 7,
        [12] = 1,
        [13] = 4,
        [14] = 7,
        [15] = 2,
        [16] = 5,
        [17] = 4,
        [18] = 1,
        [19] = 0,
        [20] = 3,
        [21] = 2,
        [22] = 7,
        [23] = 6
    };

    [ReadOnly]
    public static readonly NativeArray<float2x4> UVs = new NativeArray<float2x4>(13, Allocator.Persistent)
    {
        // bottom-left, top-left, top-right, bottom-right
        // float2x4 have to be written like { 0, 2, 4, 6, 1, 3, 5, 7}
        [0] = new float2x4(0, 0, 0, 0, 0, 0, 0, 0),
        [1] = new float2x4(new float2(0, 0.75f), new float2(0, 1), new float2(0.25f, 1), new float2(0.25f, 0.75f)),
        [2] = new float2x4(new float2(0.25f, 0.75f), new float2(0.25f, 1), new float2(0.5f, 1), new float2(0.5f, 0.75f)),
        [3] = new float2x4(new float2(0, 0.5f), new float2(0, 0.75f), new float2(0.25f, 0.75f), new float2(0.25f, 0.5f)),
        [4] = new float2x4(new float2(0.25f, 0.5f), new float2(0.25f, 0.75f), new float2(0.5f, 0.75f), new float2(0.5f, 0.5f)),
        [5] = new float2x4(new float2(0, 0.25f), new float2(0, 0.5f), new float2(0.25f, 0.5f), new float2(0.25f, 0.25f)),
        [6] = new float2x4(new float2(0.25f, 0.25f), new float2(0.25f, 0.5f), new float2(0.5f, 0.5f), new float2(0.5f, 0.25f)),
        [7] = new float2x4(new float2(0, 0), new float2(0, 0.25f), new float2(0.25f, 0.25f), new float2(0.25f, 0)),
        [8] = new float2x4(new float2(0.25f, 0), new float2(0.25f, 0.25f), new float2(0.5f, 0.25f), new float2(0.5f, 0)),
        [9] = new float2x4(new float2(0.5f, 0.75f), new float2(0.5f, 1), new float2(0.75f, 1), new float2(0.75f, 0.75f)),
        [10] = new float2x4(new float2(0.75f, 0.75f), new float2(0.75f, 1), new float2(1, 1), new float2(1, 0.75f)),
        [11] = new float2x4(new float2(0.5f, 0.5f), new float2(0.5f, 0.75f), new float2(0.75f, 0.75f), new float2(0.75f, 0.5f)),
        [12] = new float2x4(new float2(0.75f, 0.5f), new float2(0.75f, 0.75f), new float2(1, 0.75f), new float2(1, 0.5f))
    };
}

public static class BlockUtils
{
    public static NativeArray<int3> GetFaceVertices(Directions direction, int scale, int3 position)
    {
        NativeArray<int3> faceVertices = new NativeArray<int3>(4, Allocator.Temp);

        for (int i = 0; i < 4; i++)
        {
            int index = BlockData.Triangles[(int)direction * 4 + i];
            faceVertices[i] = BlockData.Vertices[index] * scale + position;
        }

        return faceVertices;
    }

    public static int GetBlockIndex(int3 position) => position.y + position.z * 18 + position.x * 18 * 18;
    //public static int GetBlockIndex(int3 position) => position.x + position.z * 16 + position.y * 16 * 16;

    public static int GetBlockIndex16x22x16(int3 position) => position.y + position.x * 22 + position.z * 22 * 16;

    public static bool IsEmpty(this Block block) => block == Block.air;
    public static bool IsLeaf(this Block block) => block == Block.leaves;

    public static int3 GetPositionInDirection(Directions direction, int x, int y, int z)
    {
        switch (direction)
        {
            case Directions.Forward:
                return new int3(x, y, z + 1);
            case Directions.Right:
                return new int3(x + 1, y, z);
            case Directions.Back:
                return new int3(x, y, z - 1);
            case Directions.Left:
                return new int3(x - 1, y, z);
            case Directions.Up:
                return new int3(x, y + 1, z);
            case Directions.Down:
                return new int3(x, y - 1, z);
            default:
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
        }
    }
}