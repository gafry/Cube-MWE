struct RayPayload
{
    int remainingDepth;
    float3 color;
    uint randomSeed;
};

// Shoots direct sun rays
float3 shootDirectLightRay(float3 orig, float3 dir, float minT, float maxT, int remainingDepth, uint randSeed)
{
    uint flags = RAY_FLAG_CULL_BACK_FACING_TRIANGLES;

    RayDesc rayDescriptor;
    rayDescriptor.Origin = orig;
    rayDescriptor.Direction = dir;
    rayDescriptor.TMin = minT;
    rayDescriptor.TMax = maxT;

    RayPayload rayPayload;
    rayPayload.randomSeed = randSeed;
    rayPayload.remainingDepth = remainingDepth - 1;
    rayPayload.color = float3(0.0f, 0.0f, 0.0f);

    TraceRay(_AccelerationStructure, flags, 0x01, 0, 1, 0, rayDescriptor, rayPayload);

    return rayPayload.color;
}