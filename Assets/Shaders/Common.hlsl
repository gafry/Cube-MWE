// Utils for shaders

#include "UnityRaytracingMeshUtils.cginc"

#define M_PI (3.14159265358979323846264338327950288)

#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };

// Macro that interpolate any attribute using barycentric coordinates
#define INTERPOLATE_RAYTRACING_ATTRIBUTE(A0, A1, A2, BARYCENTRIC_COORDINATES) (A0 * BARYCENTRIC_COORDINATES.x + A1 * BARYCENTRIC_COORDINATES.y + A2 * BARYCENTRIC_COORDINATES.z)

#define LOAD_TEXTURE2D(textureName, unCoord2) textureName.Load(int3(unCoord2, 0))

#define UNITY_RAW_FAR_CLIP_VALUE (0.0)

#define UNITY_DECLARE_TEX2D(tex) Texture2D tex; SamplerState sampler##tex
#define UNITY_SAMPLE_TEX2D(tex,coord) tex.Sample (sampler##tex,coord)

CBUFFER_START(CameraBuffer)
float4x4 _InvCameraViewProj;
float3 _WorldSpaceCameraPos;
float _CameraFarDistance;
float3 _FocusCameraLeftBottomCorner;
float3 _FocusCameraRight;
float3 _FocusCameraUp;
float2 _FocusCameraSize;
float _FocusCameraHalfAperture;
CBUFFER_END

RaytracingAccelerationStructure _AccelerationStructure;

struct RayPayloadGBuffer
{
	float3 normal;
	float id;
	float3 worldPosition;
	float3 albedo;
	float distance;
	float material;
};

struct RayPayloadAO
{
	int AOValue;
};

struct AttributeData
{
  float2 barycentrics;
};

struct IntersectionVertex
{
	float3 normalOS;
	float2 texCoord0;
};

struct ONB
{
	float3 forward;
	float3 right;
	float3 up;
};

ONB BuildONBFromNormal(float3 normal) 
{
	ONB onb;
	onb.up = normal;
	float3 tmp;
	if (abs(onb.up.x) > 0.0f)
		tmp = float3(0.0f, 1.0f, 0.0f);
	else
		tmp = float3(1.0f, 0.0f, 0.0f);
	onb.right = normalize(cross(onb.up, tmp));
	onb.forward = cross(onb.up, onb.right);
	return onb;
}

float3 ONBLocal(inout ONB onb, float3 dir) 
{
	return dir.x * onb.forward + dir.y * onb.right + dir.z * onb.up;
}

inline float3 BackgroundColor(float3 origin, float3 direction, float LightProgress)
{
	float multiplier;
	// t -> [0,1]
	float t = 0.5f * (direction.y + 1.0f);
	//return ((1.0f - t) * float3(1.0f, 0.5f, 0.0f) + t * float3(0.5f, 0.7f, 1.0f));// *multiplier;
	if (LightProgress < 0.15f || LightProgress > 0.85f)
		return ((1.0f - t) * float3(1.0f, 1.0f, 1.0f) + t * float3(0.5f, 0.7f, 1.0f));
	else if (LightProgress < 0.25f)
	{
		float s = (LightProgress - 0.15f) / (0.25f - 0.15f);
		multiplier = (LightProgress - 0.15f) / (0.35f - 0.15f);
		return ((1.0f - t) * lerp(float3(1.0f, 1.0f, 1.0f), float3(1.0f, 0.5f, 0.0f), s) + t * float3(0.5f, 0.7f, 1.0f)) * (1 - multiplier);
	}
	else if (LightProgress < 0.35f)
	{
		multiplier = (LightProgress - 0.15f) / (0.35f - 0.15f);
		return ((1.0f - t) * float3(1.0f, 0.5f, 0.0f) + t * float3(0.5f, 0.7f, 1.0f)) * max(0.07f, (1 - multiplier));
	}
	else if (LightProgress < 0.60f)
		return ((1.0f - t) * float3(1.0f, 0.5f, 0.0f) + t * float3(0.5f, 0.7f, 1.0f)) * 0.07f;
	else if (LightProgress < 0.65f)
	{
		float s = (LightProgress - 0.60f) / (0.65f - 0.60f);
		return ((1.0f - t) * lerp(float3(1.0f, 0.5f, 0.0f), float3(1.0f, 1.0f, 1.0f), s) + t * float3(0.5f, 0.7f, 1.0f)) * 0.07f;
	}
	else
	{
		multiplier = (LightProgress - 0.65f) / (0.85f - 0.65f);
		return ((1.0f - t) * float3(1.0f, 1.0f, 1.0f) + t * float3(0.5f, 0.7f, 1.0f)) * max(0.07f, multiplier);
	}
}

// Generates a seed for a random number generator from 2 inputs plus a backoff
uint initRand(uint val0, uint val1, uint backoff = 16)
{
	uint v0 = val0, v1 = val1, s0 = 0;

	[unroll]
	for (uint n = 0; n < backoff; n++)
	{
		s0 += 0x9e3779b9;
		v0 += ((v1 << 4) + 0xa341316c) ^ (v1 + s0) ^ ((v1 >> 5) + 0xc8013ea4);
		v1 += ((v0 << 4) + 0xad90777d) ^ (v0 + s0) ^ ((v0 >> 5) + 0x7e95761e);
	}
	return v0;
}

// Takes seed, updates it, and returns a pseudorandom float in [0..1]
float nextRand(inout uint s)
{
	s = (1664525u * s + 1013904223u);
	return float(s & 0x00FFFFFF) / float(0x01000000);
}

float3 GetRandomCosineDir(uint seed) 
{
	float r1 = nextRand(seed);
	float r2 = nextRand(seed);
	float z = sqrt(1.0f - r2);
	float phi = 2.0f * M_PI * r1;
	float x = cos(phi) * sqrt(r2);
	float y = sin(phi) * sqrt(r2);
	return float3(x, y, z);
}

// Function to get a vector perpendicular to an input vector 
float3 getPerpendicularVector(float3 u)
{
	float3 a = abs(u);
	uint xm = ((a.x - a.y) < 0 && (a.x - a.z) < 0) ? 1 : 0;
	uint ym = (a.y - a.z) < 0 ? (1 ^ xm) : 0;
	uint zm = 1 ^ (xm | ym);
	return cross(u, float3(xm, ym, zm));
}

// Get a cosine-weighted random vector centered around a specified normal direction.
inline float3 getCosHemisphereSample(inout uint randSeed, float3 hitNorm)
{
	// Get 2 random numbers to select our sample with
	float2 randVal = float2(nextRand(randSeed), nextRand(randSeed));

	// Cosine weighted hemisphere sample from RNG
	float3 bitangent = getPerpendicularVector(hitNorm);
	float3 tangent = cross(bitangent, hitNorm);
	float r = sqrt(randVal.x);
	float phi = 2.0f * 3.14159265f * randVal.y;

	// Get our cosine-weighted hemisphere lobe sample direction
	return tangent * (r * cos(phi).x) + bitangent * (r * sin(phi)) + hitNorm.xyz * sqrt(1 - randVal.x);
}

inline float3 GetWorldPositionByPixelCoordAndDepth(uint2 pixelCoord, float depth)
{
	float2 xy = pixelCoord + 0.5f; // center in the middle of the pixel.
	float2 screenPos = xy / DispatchRaysDimensions().xy * 2.0f - 1.0f;

	float4 world = mul(_InvCameraViewProj, float4(screenPos, depth, 1));
	world.xyz /= world.w;
	return world.xyz;
}

float3 SphericalToCartesian(float phi, float cosTheta)
{
	float sinPhi, cosPhi;
	sincos(phi, sinPhi, cosPhi);

	float sinTheta = sqrt(saturate(1 - cosTheta * cosTheta));

	return float3(float2(cosPhi, sinPhi) * sinTheta, cosTheta);
}

float3 SampleSphereUniform(float2 rand)
{
	float phi = 2 * M_PI * rand.x;
	float cosTheta = 1.0f - 2.0f * rand.y;

	return SphericalToCartesian(phi, cosTheta);
}

// Cosine-weighted sampling without the tangent frame.
// Ref: http://www.amietia.com/lambertnotangent.html
float3 SampleHemisphereCosine(inout uint randSeed, float3 normal)
{
	float2 randVal = float2(nextRand(randSeed), nextRand(randSeed));
	float3 pointOnSphere = SampleSphereUniform(randVal);
	return normalize(normal + pointOnSphere);
}

float3 GetPointOnSphere(inout uint randSeed)
{
	float3 randVector = float3(nextRand(randSeed), nextRand(randSeed), nextRand(randSeed));
	return normalize(randVector);
}

void FetchIntersectionVertex(uint vertexIndex, out IntersectionVertex outVertex)
{
	outVertex.normalOS = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributeNormal);
	outVertex.texCoord0 = UnityRayTracingFetchVertexAttribute2(vertexIndex, kVertexAttributeTexCoord0);
}

inline void GenerateCameraRay(out float3 origin, out float3 direction)
{
  float2 xy = DispatchRaysIndex().xy + 0.5f; // center in the middle of the pixel.
  float2 screenPos = xy / DispatchRaysDimensions().xy * 2.0f - 1.0f;

  // Un project the pixel coordinate into a ray.
  float4 world = mul(_InvCameraViewProj, float4(screenPos, 0, 1));

  world.xyz /= world.w;
  origin = _WorldSpaceCameraPos.xyz;
  direction = normalize(world.xyz - origin);
}

inline void GenerateCameraRayWithOffset(out float3 origin, out float3 direction, float2 offset)
{
  float2 xy = DispatchRaysIndex().xy + offset;
  float2 screenPos = xy / DispatchRaysDimensions().xy * 2.0f - 1.0f;

  // Un project the pixel coordinate into a ray.
  float4 world = mul(_InvCameraViewProj, float4(screenPos, 0, 1));

  world.xyz /= world.w;
  origin = _WorldSpaceCameraPos.xyz;
  direction = normalize(world.xyz - origin);
}

inline void GenerateFocusCameraRayWithOffset(out float3 origin, out float3 direction, float2 apertureOffset, float2 offset)
{
	float2 xy = DispatchRaysIndex().xy + offset;
	float2 uv = xy / DispatchRaysDimensions().xy;

	float3 world = _FocusCameraLeftBottomCorner + uv.x * _FocusCameraSize.x * _FocusCameraRight + uv.y * _FocusCameraSize.y * _FocusCameraUp;
	origin = _WorldSpaceCameraPos.xyz + _FocusCameraHalfAperture * apertureOffset.x * _FocusCameraRight + _FocusCameraHalfAperture * apertureOffset.y * _FocusCameraUp;
	direction = normalize(world.xyz - origin);
}