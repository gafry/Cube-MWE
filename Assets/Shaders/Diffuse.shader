Shader "RayTracing/Diffuse"
{
    Properties
    {
        _Color("Main Color", Color) = (1, 1, 1, 1)
        _Kd("Kd", Float) = 0.5
        _BaseColorMap("BaseColorMap", 2D) = "white" {}
    }

    SubShader
    {
        Pass
        {
            Name "PositionBuffer"

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            struct VertInput
            {
                float4 position : POSITION;
            };

            struct VertOutput
            {
                float4 position : SV_POSITION;
                float4 color : COLOR;
            };

            VertOutput vert(VertInput input)
            {
                VertOutput output;

                output.position = UnityObjectToClipPos(input.position);
                float3 worldPosition = mul(unity_ObjectToWorld, input.position).xyz;
                output.color = float4(worldPosition, 1.0f);

                return output;
            }

            float4 frag(VertOutput input) : SV_Target
            {
                return float4(input.color);
            }

            ENDCG
        }
    }

    SubShader
    {
        Pass
        {
            Name "DirectLighting"
            Tags { "LightMode" = "RayTracing" }

            HLSLPROGRAM

            #pragma raytracing test

            #include "./Common.hlsl"

            #include "Shadows.hlsl"
            #include "DirectRays.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Kd;
            CBUFFER_END

            cbuffer PointLight
            {
                float3 LightPosition;
                float LightProgress;
            };

            Texture2D _BaseColorMap;
            SamplerState sampler_BaseColorMap;

            [shader("closesthit")]
            void ClosestHitShader(inout RayPayload rayPayload : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
            {
                /*// Fetch the indices of the currentr triangle
                uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());

                // Fetch the 3 vertices
                IntersectionVertex v0, v1, v2;
                FetchIntersectionVertex(triangleIndices.x, v0);
                FetchIntersectionVertex(triangleIndices.y, v1);
                FetchIntersectionVertex(triangleIndices.z, v2);

                // Compute the full barycentric coordinates
                float3 barycentricCoordinates = float3(1.0 - attributeData.barycentrics.x - attributeData.barycentrics.y, attributeData.barycentrics.x, attributeData.barycentrics.y);

                float3 normalOS = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.normalOS, v1.normalOS, v2.normalOS, barycentricCoordinates);
                float3x3 objectToWorld = (float3x3)ObjectToWorld3x4();
                float3 worldNorm = normalize(mul(objectToWorld, normalOS));

                // Get position in world space.
                float3 origin = WorldRayOrigin();
                float3 direction = WorldRayDirection();
                float t = RayTCurrent();
                float3 worldPos = origin + direction * t;
                float3 worldDir = getCosHemisphereSample(rayPayload.randomSeed, worldNorm);

                float primary = 0.0f;
                float sunSize = 100.0f;

                if (LightProgress < 0.4f || LightProgress > 0.6f)
                {
                    float distToLight;
                    float3 dirToLight = normalize(LightPosition - worldPos);
                    distToLight = length(LightPosition - worldPos) - 0.2f;
                    float nDotL = max(0.3f, dot(worldNorm, dirToLight));

                    // Shoot shadow ray with our encapsulated shadow tracing function
                    float shadow = shootShadowRay(worldPos + worldNorm * 0.001f, dirToLight, 1.0e-4f, distToLight);
                    primary += nDotL * max(0.05f, shadow);
                }
                else
                {
                    float3 dirToLight = normalize(LightPosition - worldPos);
                    float shadow = 0.0f;
                    primary += shadow;
                }

                float2 texCoord0 = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.texCoord0, v1.texCoord0, v2.texCoord0, barycentricCoordinates);
                float4 textureColor;
                texCoord0.x = min(0.9375f + (texCoord0.x / 16), 1.0f);
                texCoord0.y = texCoord0.y / 16;
                textureColor = _BaseColorMap.SampleLevel(sampler_BaseColorMap, texCoord0, 0);
                rayPayload.color = primary * 0.3f * textureColor;*/
                rayPayload.color = float3(0.0f, 0.0f, 0.0f);
            }

            ENDHLSL
        }
    }

    /*SubShader
    {
        Pass
        {
            Name "DirectLighting"
            Tags { "LightMode" = "RayTracing" }

            HLSLPROGRAM

            #pragma raytracing test

            #include "./Common.hlsl"

            #include "Shadows.hlsl"
            #include "DirectRays.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Kd;
            CBUFFER_END

            cbuffer PointLight
            {
                float3 LightPosition;
            };

            [shader("closesthit")]
            void ClosestHitShader(inout RayPayload rayPayload : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
            {
                // Fetch the indices of the currentr triangle
                uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());

                // Fetch the 3 vertices
                IntersectionVertex v0, v1, v2;
                FetchIntersectionVertex(triangleIndices.x, v0);
                FetchIntersectionVertex(triangleIndices.y, v1);
                FetchIntersectionVertex(triangleIndices.z, v2);

                // Compute the full barycentric coordinates
                float3 barycentricCoordinates = float3(1.0 - attributeData.barycentrics.x - attributeData.barycentrics.y, attributeData.barycentrics.x, attributeData.barycentrics.y);

                float3 normalOS = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.normalOS, v1.normalOS, v2.normalOS, barycentricCoordinates);
                float3x3 objectToWorld = (float3x3)ObjectToWorld3x4();
                float3 worldNorm = normalize(mul(objectToWorld, normalOS));

                // Get position in world space.
                float3 origin = WorldRayOrigin();
                float3 direction = WorldRayDirection();
                float t = RayTCurrent();
                float3 worldPos = origin + direction * t;
                float3 worldDir = getCosHemisphereSample(rayPayload.randomSeed, worldNorm);

                float4 color = (0, 0, 0, 0);
                float primary = 0.0f;
                float sunSize = 100.0f;
                float secondary = 0.0f;

                float lightIntensity = 2.0f;

                if (rayPayload.remainingDepth > 0)
                    secondary = shootDirectLightRay(worldPos, worldDir, 1e-5f, _CameraFarDistance, rayPayload.remainingDepth, rayPayload.randomSeed);
                else
                    secondary = 0.f;

                if (LightPosition.y + sunSize > 0)
                {
                    float3 dirToLight = normalize(LightPosition - worldPos);
                    float3 sampleOnHemisphere = LightPosition + sunSize * SampleHemisphereCosine(rayPayload.randomSeed, dirToLight);
                    dirToLight = normalize(sampleOnHemisphere - worldPos);
                    float distToLight3 = length(sampleOnHemisphere - worldPos) - 0.2f;
                    float4 lightColor3 = float4(1.0f, 1.0f, 1.0f, 1.0f);
                    //float nDotL = max(0.f, dot(worldNorm, dirToLight));
                    float nDotL = saturate(dot(worldNorm, dirToLight));

                    // Shoot shadow ray with our encapsulated shadow tracing function
                    float shadow = shootShadowRay(worldPos, dirToLight, 1.0e-4f, distToLight3);
                    primary = nDotL * max(0.f, shadow) * lightIntensity;
                }
                else
                {
                    primary = 0.f;
                }

                float shadow = min(1.f, primary + secondary);
                //color = float4(shadow, shadow, shadow, 1);

                rayPayload.color = shadow;
            }

            ENDHLSL
        }
    }*/

    /*SubShader
    {
        Pass
        {
            Name "RayTracing"
            Tags { "LightMode" = "RayTracing" }

            HLSLPROGRAM

            #pragma raytracing test

            #include "./Common.hlsl"

            #include "Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Kd;
            CBUFFER_END

            cbuffer PointLight
            {
                float3 LightPosition;
            };

            [shader("closesthit")]
            void ClosestHitShader(inout RayPayload rayPayload : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
            {
                // Fetch the indices of the currentr triangle
                uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());

                // Fetch the 3 vertices
                IntersectionVertex v0, v1, v2;
                FetchIntersectionVertex(triangleIndices.x, v0);
                FetchIntersectionVertex(triangleIndices.y, v1);
                FetchIntersectionVertex(triangleIndices.z, v2);

                // Compute the full barycentric coordinates
                float3 barycentricCoordinates = float3(1.0 - attributeData.barycentrics.x - attributeData.barycentrics.y, attributeData.barycentrics.x, attributeData.barycentrics.y);

                float3 normalOS = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.normalOS, v1.normalOS, v2.normalOS, barycentricCoordinates);
                float3x3 objectToWorld = (float3x3)ObjectToWorld3x4();
                float3 normalWS = normalize(mul(objectToWorld, normalOS));

                float4 color = (0, 0, 0, 1);

                // Get position in world space.
                float3 origin = WorldRayOrigin();
                float3 direction = WorldRayDirection();
                float t = RayTCurrent();
                float3 positionWS = origin + direction * t;

                if (rayPayload.remainingDepth > 0)
                {
                    // Make reflection ray.
                    RayDesc rayDescriptor;
                    rayDescriptor.Origin = positionWS + 0.001f * normalWS;
                    rayDescriptor.Direction = getCosHemisphereSample(rayPayload.randomSeed, normalWS);
                    rayDescriptor.TMin = 1e-5f;
                    rayDescriptor.TMax = _CameraFarDistance;

                    // Tracing reflection.
                    RayPayload reflectionRayPayload;
                    reflectionRayPayload.remainingDepth = rayPayload.remainingDepth - 1;
                    reflectionRayPayload.randomSeed = rayPayload.randomSeed;
                    reflectionRayPayload.color = float4(0.0f, 0.0f, 0.0f, 0.0f);

                    TraceRay(_AccelerationStructure, RAY_FLAG_CULL_BACK_FACING_TRIANGLES, 0xFF, 0, 1, 0, rayDescriptor, reflectionRayPayload);

                    color = reflectionRayPayload.color * 0.2f;                                        
                }

                float4 finalColor = (0, 0, 0, 0);

                float sunSize = 100.0f;
                float isLit = 0.0f;

                if (LightPosition.y + sunSize > 0)
                {
                    float3 dirToLight = normalize(LightPosition - positionWS);
                    float3 sampleOnHemisphere = LightPosition + sunSize * SampleHemisphereCosine(rayPayload.randomSeed, dirToLight);
                    dirToLight = normalize(sampleOnHemisphere - positionWS);
                    float distToLight3 = length(sampleOnHemisphere - positionWS) - 0.2f;
                    //float NdotL3 = saturate(dot(normalWS, dirToLight));
                    //float lightIntensity3 = 20.5f;
                    float4 lightColor3 = float4(1.0f, 1.0f, 1.0f, 1.0f);

                    // Shoot shadow ray with our encapsulated shadow tracing function
                    isLit = shootShadowRay(positionWS, dirToLight, 1.0e-4f, distToLight3);
                }
                else
                {
                    isLit = 0;
                }
                //isLit = max(isLit, color.x);
                isLit = max(isLit, color.x / 4);

                rayPayload.color = float4(isLit, isLit, isLit, 1);
            }

            ENDHLSL
        }
    }*/

    SubShader
    {
        Pass
        {
            Name "GBuffer"
            Tags { "LightMode" = "RayTracing" }

            HLSLPROGRAM

            #pragma target 3.5
            #pragma raytracing test

            #include "./Common.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            CBUFFER_END

            Texture2D _BaseColorMap;
            SamplerState sampler_BaseColorMap;

            [shader("closesthit")]
            void ClosestHitShader(inout RayPayloadGBuffer rayPayload : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
            {
                // Fetch the indices of the currentr triangle
                uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());

                // Fetch the 3 vertices
                IntersectionVertex v0, v1, v2;
                FetchIntersectionVertex(triangleIndices.x, v0);
                FetchIntersectionVertex(triangleIndices.y, v1);
                FetchIntersectionVertex(triangleIndices.z, v2);

                // Compute the full barycentric coordinates
                float3 barycentricCoordinates = float3(1.0 - attributeData.barycentrics.x - attributeData.barycentrics.y, attributeData.barycentrics.x, attributeData.barycentrics.y);

                float3 normalOS = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.normalOS, v1.normalOS, v2.normalOS, barycentricCoordinates);
                float3x3 objectToWorld = (float3x3)ObjectToWorld3x4();
                float3 normalWS = normalize(mul(objectToWorld, normalOS));

                // Get position in world space.
                float3 origin = WorldRayOrigin();
                float3 direction = WorldRayDirection();
                float t = RayTCurrent();
                float3 positionWS = origin + direction * t;

                float instanceID = InstanceID();

                float2 texCoord0 = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.texCoord0, v1.texCoord0, v2.texCoord0, barycentricCoordinates);
                float4 textureColor;
                if (t < 20)
                {
                    texCoord0.x = min(0.5f + (texCoord0.x / 2), 1.0f);
                    texCoord0.y = texCoord0.y / 2;
                    textureColor = _BaseColorMap.SampleLevel(sampler_BaseColorMap, texCoord0, 0);
                }
                else if (t < 30)
                {
                    texCoord0.x = min(0.75f + (texCoord0.x / 4), 1.0f);
                    texCoord0.y = texCoord0.y / 4;
                    textureColor = _BaseColorMap.SampleLevel(sampler_BaseColorMap, texCoord0, 0);
                }
                else if (t < 100)
                {
                    texCoord0.x = min(0.875f + (texCoord0.x / 8), 1.0f);
                    texCoord0.y = texCoord0.y / 8;
                    textureColor = _BaseColorMap.SampleLevel(sampler_BaseColorMap, texCoord0, 0);
                }
                else
                {
                    texCoord0.x = min(0.9375f + (texCoord0.x / 16), 1.0f);
                    texCoord0.y = texCoord0.y / 16;
                    textureColor = _BaseColorMap.SampleLevel(sampler_BaseColorMap, texCoord0, 0);
                }

                rayPayload.normal = normalWS;
                rayPayload.worldPosition = positionWS;
                rayPayload.albedo = textureColor.xyz;
                rayPayload.distance = t;
                rayPayload.id = instanceID;
                rayPayload.material = 1.0f;
            }

            ENDHLSL
        }
    }

    SubShader
    {
        Pass
        {
            Name "CubeLights"
            Tags { "LightMode" = "RayTracing" }

            HLSLPROGRAM

            #pragma target 3.5
            #pragma raytracing test

            #include "./Common.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            CBUFFER_END

            [shader("closesthit")]
            void ClosestHitShader(inout RayPayloadAO rayPayload : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
            {
                rayPayload.AOValue = 0.0;
            }

            ENDHLSL
        }
    }
}