// Ray payload for shadow rays
struct RayPayloadShadow
{
    int visibility;
};

// Shoots shadow rays and returns 1 if not occluded
float shootShadowRay(float3 orig, float3 dir, float minT, float maxT)
{
    uint flags = RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_SKIP_CLOSEST_HIT_SHADER;
    RayDesc shadowRay = { orig, minT, dir, maxT };
    RayPayloadShadow shadowRayPayload = { 0.0f };

    TraceRay(_AccelerationStructure, flags, 0xFF, 0, 1, 1, shadowRay, shadowRayPayload);
    return shadowRayPayload.visibility;
}

