Shader "Hidden/ClusterRenderPipeline/DebugViewTiles"
{
    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 ps4 xboxone vulkan metal

            #pragma vertex Vert
            #pragma fragment Frag

            #define LIGHTLOOP_TILE_PASS

            #pragma multi_compile USE_FPTL_LIGHTLIST USE_CLUSTERED_LIGHTLIST
            #pragma multi_compile SHOW_LIGHT_CATEGORIES SHOW_FEATURE_VARIANTS

            //-------------------------------------------------------------------------------------
            // Include
            //-------------------------------------------------------------------------------------

            #include "CoreRP/ShaderLibrary/Common.hlsl"

            // Note: We have fix as guidelines that we have only one deferred material (with control of GBuffer enabled). Mean a users that add a new
            // deferred material must replace the old one here. If in the future we want to support multiple layout (cause a lot of consistency problem),
            // the deferred shader will require to use multicompile.
            #define UNITY_MATERIAL_LIT // Need to be define before including Material.hlsl
            #include "ClusterRP/ShaderVariables.hlsl"
            #include "CoreRP/ShaderLibrary/Debug.hlsl"
            //#include "../Shaders/Lit/Lighting/Lighting.hlsl" // This include Material.hlsl
            #include "ClusterRP/Shaders/Lit/Lighting/ClusterUtils.cginc"

            //-------------------------------------------------------------------------------------
            // variable declaration
            //-------------------------------------------------------------------------------------

            uint _ViewTilesFlags;
            uint _NumTiles;
            float4 _MousePixelCoord; // xy unorm, zw norm

            StructuredBuffer<uint> g_TileList;
            Buffer<uint> g_DispatchIndirectBuffer;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                int variant : TEXCOORD0;
            };


            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.variant = 0; // unused
                return output;
            }

            float4 AlphaBlend(float4 c0, float4 c1) // c1 over c0
            {
                return float4(lerp(c0.rgb, c1.rgb, c1.a), c0.a + c1.a - c0.a * c1.a);
            }

            float4 OverlayHeatMap(uint2 pixCoord, uint n)
            {
                const float4 kRadarColors[12] =
                {
                    float4(0.0, 0.0, 0.0, 0.0),   // black
                    float4(0.0, 0.0, 0.6, 0.5),   // dark blue
                    float4(0.0, 0.0, 0.9, 0.5),   // blue
                    float4(0.0, 0.6, 0.9, 0.5),   // light blue
                    float4(0.0, 0.9, 0.9, 0.5),   // cyan
                    float4(0.0, 0.9, 0.6, 0.5),   // blueish green
                    float4(0.0, 0.9, 0.0, 0.5),   // green
                    float4(0.6, 0.9, 0.0, 0.5),   // yellowish green
                    float4(0.9, 0.9, 0.0, 0.5),   // yellow
                    float4(0.9, 0.6, 0.0, 0.5),   // orange
                    float4(0.9, 0.0, 0.0, 0.5),   // red
                    float4(1.0, 0.0, 0.0, 0.9)    // strong red
                };

                float maxNrLightsPerTile = 20; // TODO: setup a constant for that

                int colorIndex = n == 0 ? 0 : (1 + (int)floor(10 * (log2((float)n) / log2(maxNrLightsPerTile))));
                colorIndex = colorIndex < 0 ? 0 : colorIndex;
                float4 col = colorIndex > 11 ? float4(1.0, 1.0, 1.0, 1.0) : kRadarColors[colorIndex];

                int2 coord = pixCoord - int2(1, 1);

                float4 color = float4(PositivePow(col.xyz, 2.2), 0.3 * col.w);
                if (n >= 0)
                {
                    if (SampleDebugFontNumber(coord, n))        // Shadow
                        color = float4(0, 0, 0, 1);
                    if (SampleDebugFontNumber(coord + 1, n))    // Text
                        color = float4(1, 1, 1, 1);
                }
                return color;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                // positionCS is SV_Position
                float depth = LOAD_TEXTURE2D(_MainDepthTexture, input.positionCS.xy).x;
                PositionInputs posInput = GetPositionInput(input.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_VP, uint2(input.positionCS.xy) / CLUSTER_RES);


                int2 pixelCoord = posInput.positionSS.xy;
                int2 tileCoord = (float2)pixelCoord / CLUSTER_RES;
                int2 mouseTileCoord = _MousePixelCoord.xy / CLUSTER_RES;
                int2 offsetInTile = pixelCoord - tileCoord * CLUSTER_RES;

                int n = 0;

                half3 clusterId = PosWorldToClusterID(float4(posInput.positionWS, 1.0f), Cluster_Matrix_LinearZ);
                half3 clusterUV = PosWorldToClusterUV(float4(posInput.positionWS, 1.0f), Cluster_Matrix_LinearZ);

                float startOffset = (clusterId.x * CullingClusterParams.y * CullingClusterParams.z + clusterId.y * CullingClusterParams.z) * 4 + clusterId.z * 4;
                uint nextNode = ClusterStartOffsetIndexIn.Load((int)startOffset);
                int loopCount = 0;

                [loop] while (loopCount < _PunctualLightCount && nextNode != 0xFFFFFFFF)
                {
                    loopCount++;
                    uint lightLinkNode = ClusterLightsLinkListIn[nextNode];
                    uint lightId = lightLinkNode >> 24;

                    uint nextNodeId = lightLinkNode | 0xFF000000;
                    if (nextNodeId != 0xFFFFFFFF)
                        nextNode = lightLinkNode & 0xFFFFFF;
                    else
                        nextNode = 0xFFFFFFFF;
                }

                float4 result = float4(0.0, 0.0, 0.0, 0.0);

                if (clusterId.z >= 127)
                    loopCount = 0;

                // Tile overlap counter
                if (loopCount > 0)
                {
                    int2 ssPosInt = (int2)(clusterUV.xy * ClusterScreenParams.xy);
                    ssPosInt.y = -ssPosInt.y;
                    result = OverlayHeatMap(CLUSTER_RES - ssPosInt & (CLUSTER_RES - 1), loopCount);
                }

//#ifdef SHOW_LIGHT_CATEGORIES
//                // Highlight selected tile
//                if (all(mouseTileCoord == tileCoord))
//                {
//                    bool border = any(offsetInTile == 0 || offsetInTile == (int)CLUSTER_RES - 1);
//                    float4 result2 = float4(1.0, 1.0, 1.0, border ? 1.0 : 0.5);
//                    result = AlphaBlend(result, result2);
//                }
//
//                // Print light lists for selected tile at the bottom of the screen
//                int maxLights = 32;
//                if (tileCoord.y < LIGHTCATEGORY_COUNT && tileCoord.x < maxLights + 3)
//                {
//                    float depthMouse = LOAD_TEXTURE2D(_MainDepthTexture, _MousePixelCoord.xy).x;
//                    PositionInputs mousePosInput = GetPositionInput(_MousePixelCoord.xy, _ScreenSize.zw, depthMouse, UNITY_MATRIX_I_VP, UNITY_MATRIX_VP, mouseTileCoord);
//
//                    uint category = (LIGHTCATEGORY_COUNT - 1) - tileCoord.y;
//                    uint start;
//                    uint count;
//                    GetCountAndStart(mousePosInput, category, start, count);
//
//                    float4 result2 = float4(.1,.1,.1,.9);
//                    int2 fontCoord = int2(pixelCoord.x, offsetInTile.y);
//                    int lightListIndex = tileCoord.x - 2;
//
//                    int n = -1;
//                    if(tileCoord.x == 0)
//                    {
//                        n = (int)count;
//                    }
//                    else if(lightListIndex >= 0 && lightListIndex < (int)count)
//                    {
//                        n = FetchIndex(start, lightListIndex);
//                    }
//
//                    if (n >= 0)
//                    {
//                        if (SampleDebugFontNumber(offsetInTile, n))
//                            result2 = float4(0.0, 0.0, 0.0, 1.0);
//                        if (SampleDebugFontNumber(offsetInTile + 1, n))
//                            result2 = float4(1.0, 1.0, 1.0, 1.0);
//                    }
//
//                    result = AlphaBlend(result, result2);
//                }
//#endif

                return result;
            }

            ENDHLSL
        }
    }
    Fallback Off
}
