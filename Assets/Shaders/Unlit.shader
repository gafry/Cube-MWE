Shader "RayTracing/Unlit"
{
    Properties
    {
        _Color("Main Color", Color) = (1, 1, 1, 1)
    }
        SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100

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
            };

            Texture2D _BaseColorMap;
            SamplerState sampler_BaseColorMap;

            [shader("closesthit")]
            void ClosestHitShader(inout RayPayload rayPayload : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
            {
                rayPayload.color = _Color.xyz;
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

                rayPayload.normal = normalWS;
                rayPayload.worldPosition = positionWS;
                rayPayload.albedo = _Color.xyz;
                rayPayload.distance = t;
                rayPayload.id = instanceID;
                rayPayload.material = 2.0f;
            }

        ENDHLSL
        }
    }
}