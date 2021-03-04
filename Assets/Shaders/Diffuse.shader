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
                float3 _LightPosition;
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
                    // Make ONB.
                    ONB uvw;
                    GenerateONBFromN(uvw, normalWS);

                    // Make reflection ray.
                    RayDesc rayDescriptor;
                    rayDescriptor.Origin = positionWS + 0.001f * normalWS;
                    rayDescriptor.Direction = ONBLocal(uvw, GetRandomCosineDir(rayPayload.randomSeed));
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

                float3 gLightPosition3 = float3(530.0f, 500.0f, 370.0f);
                float3 dirToLight3 = normalize(gLightPosition3 - positionWS);
                //float3 sampleOnHemisphere = gLightPosition3;
                float3 sampleOnHemisphere = gLightPosition3 + 100 * SampleHemisphereCosine(rayPayload.randomSeed, dirToLight3);
                dirToLight3 = normalize(sampleOnHemisphere - positionWS);
                float distToLight3 = length(sampleOnHemisphere - positionWS) - 0.2f;
                float NdotL3 = saturate(dot(normalWS, dirToLight3));
                float lightIntensity3 = 20.5f;
                float4 lightColor3 = float4(1.0f, 1.0f, 1.0f, 1.0f);

                // Shoot shadow ray with our encapsulated shadow tracing function
                float isLit = shootShadowRay(positionWS, dirToLight3, 1.0e-4f, distToLight3);
                isLit = max(isLit, color.x);

                rayPayload.color = float4(isLit, isLit, isLit, 1);
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

            [shader("closesthit")]
            void ClosestHitShader(inout RayPayloadNormals rayPayload : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
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

                float instanceID = InstanceID() / 4.0f;
                rayPayload.normalAndId = float4(normalWS, instanceID);
                rayPayload.worldPosition = float4(positionWS, 1.0f);
                //rayPayload.albedo = _Color.xyz;

                float2 texCoord0 = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.texCoord0, v1.texCoord0, v2.texCoord0, barycentricCoordinates);
                float4 textureColor = _BaseColorMap.SampleLevel(sampler_BaseColorMap, texCoord0, 0);
                rayPayload.albedo = textureColor.xyz;
            }

            ENDHLSL
        }
    }
}