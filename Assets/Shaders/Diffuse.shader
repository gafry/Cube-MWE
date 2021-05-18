Shader "RayTracing/Diffuse"
{
    Properties
    {
        _Color("Main Color", Color) = (1, 1, 1, 1)
        _Kd("Kd", Float) = 0.5
        _BaseColorMap("BaseColorMap", 2D) = "white" {}
        _BaseColorMapAO("BaseColorMapAO", 2D) = "white" {}
    }

    // Just for scene inspector
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
            Name "DirectAndIndirectLighting"
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
                rayPayload.color = primary * 0.5f * textureColor;
            }

            ENDHLSL
        }
    }

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
            Texture2D _BaseColorMapAO;
            SamplerState sampler_BaseColorMapAO;

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
                float4 textureAO;
                float3 color = float3(1, 1, 1);
                if (t > 6)
                {
                    if (t < 10)
                    {
                        texCoord0.x = min(0.5f + (texCoord0.x / 2), 1.0f);
                        texCoord0.y = texCoord0.y / 2;
                        color = float3(1.5, 1, 1);
                    }
                    else if (t < 15)
                    {
                        texCoord0.x = min(0.75f + (texCoord0.x / 4), 1.0f);
                        texCoord0.y = texCoord0.y / 4;
                        color = float3(1, 1.5, 1);
                    }
                    else if (t < 50)
                    {
                        texCoord0.x = min(0.875f + (texCoord0.x / 8), 1.0f);
                        texCoord0.y = texCoord0.y / 8;
                        color = float3(1, 1, 1.5);
                    }
                    else
                    {
                        texCoord0.x = min(0.9375f + (texCoord0.x / 16), 1.0f);
                        texCoord0.y = texCoord0.y / 16;
                    }
                }
                textureColor = _BaseColorMap.SampleLevel(sampler_BaseColorMap, texCoord0, 0);
                textureAO = _BaseColorMapAO.SampleLevel(sampler_BaseColorMapAO, texCoord0, 0);

                rayPayload.normal = normalWS;
                rayPayload.worldPosition = positionWS;
                rayPayload.albedo = textureColor.xyz * textureAO.xyz;
                rayPayload.distance = t;
                rayPayload.id = instanceID;
                rayPayload.material = 1.0f;
            }

            ENDHLSL
        }
    }
}