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

        Pass
        {
            Name "GBuffer"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
                // make fog work
                #pragma multi_compile_fog

                #include "UnityCG.cginc"

                struct appdata
                {
                    float4 vertex : POSITION;
                    float3 normal : NORMAL;
                };

                struct v2f
                {
                    float3 normal : TEXCOORD0;
                    UNITY_FOG_COORDS(1)
                    float4 vertex : SV_POSITION;
                };

                v2f vert(appdata v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.normal = UnityObjectToWorldNormal(v.normal);
                    UNITY_TRANSFER_FOG(o, o.vertex);
                    return o;
                }

                float4 frag(v2f i) : SV_Target
                {
                    float4 col = float4(i.normal, i.vertex.z);
                    return col;
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

                    CBUFFER_START(UnityPerMaterial)
                    float4 _Color;
                    CBUFFER_END

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
                        float3 normalWS = normalize(mul(objectToWorld, normalOS));

                        rayPayload.color = float4(0.5f * (normalWS + 1.0f), 0);*/

                        rayPayload.color = _Color;
                    }

                    ENDHLSL
                }
            }

            SubShader
            {
                Pass
                {
                    Name "AO"
                    Tags { "LightMode" = "RayTracing" }

                    HLSLPROGRAM

                    #pragma raytracing test

                    #include "./Common.hlsl"

                    [shader("closesthit")]
                    void ClosestHitShader(inout RayPayloadAO rayPayloadAO : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
                    {
                    rayPayloadAO.AOValue = 0.0f;
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
                    //Texture2D _BaseColorMap;
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
                        rayPayload.albedo = _Color.xyz;

                        //SamplerState sampler_BaseColorMap;
                        //float2 texCoord0 = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.texCoord0, v1.texCoord0, v2.texCoord0, barycentricCoordinates);
                        //float4 textureColor = _BaseColorMap.SampleLevel(sampler_BaseColorMap, texCoord0, 0);
                        //UNITY_DECLARE_TEX2D(_BaseColorMap);
                        //float4 textureColor = UNITY_SAMPLE_TEX2D(_BaseColorMap, float2(0.1, 0.2));
                        //float4 textureColor = tex2D(_BaseColorMap, float2(0.1, 0.2));
                        //rayPayload.albedo = textureColor.xyz;
                    }

                    ENDHLSL
                }
        }
}