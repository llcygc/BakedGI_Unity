Shader "Hidden/PostProcessing/FinalPass"
{
    HLSLINCLUDE

        #pragma multi_compile __ UNITY_COLORSPACE_GAMMA
        #pragma multi_compile __ FXAA FXAA_LOW
        #pragma multi_compile __ FXAA_KEEP_ALPHA
        #include "../StdLib.hlsl"
        #include "../Colors.hlsl"
        #include "Dithering.hlsl"

        // PS3 and XBOX360 aren't supported in Unity anymore, only use the PC variant
        #define FXAA_PC 1

        #if FXAA_KEEP_ALPHA
            // Luma hasn't been encoded in alpha
            #define FXAA_GREEN_AS_LUMA 1
        #else
            // Luma is encoded in alpha after the first Uber pass
            #define FXAA_GREEN_AS_LUMA 0
        #endif

        #if FXAA_LOW
            #define FXAA_QUALITY__PRESET 12
            #define FXAA_QUALITY_SUBPIX 1.0
            #define FXAA_QUALITY_EDGE_THRESHOLD 0.166
            #define FXAA_QUALITY_EDGE_THRESHOLD_MIN 0.0625
        #else
            #define FXAA_QUALITY__PRESET 28
            #define FXAA_QUALITY_SUBPIX 1.0
            #define FXAA_QUALITY_EDGE_THRESHOLD 0.063
            #define FXAA_QUALITY_EDGE_THRESHOLD_MIN 0.0312
        #endif

        #include "FastApproximateAntialiasing.hlsl"
#ifdef UNITY_STEREO_INSTANCING_ENABLED
		TEXTURE2DARRAY_SAMPLER2DARRAY(_MainTex, sampler_MainTex);
#else
        TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
#endif
		float4 _MainTex_TexelSize;

        float4 Frag(VaryingsDefault i) : SV_Target
        {
            half4 color = 0.0;

			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)

			int sliceIndex = 0;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
			sliceIndex = i.stereoTargetEyeIndex;
#endif

            // Fast Approximate Anti-aliasing
            #if FXAA || FXAA_LOW
            {
                #if FXAA_HLSL_4 || FXAA_HLSL_5
                    FxaaTex mainTex;
                    mainTex.tex = _MainTex;
                    mainTex.smpl = sampler_MainTex;
                #else
                    FxaaTex mainTex = _MainTex;
                #endif

                color = FxaaPixelShader(
                    i.texcoord,                 // pos
                    0.0,                        // fxaaConsolePosPos (unused)
                    mainTex,                    // tex
                    mainTex,                    // fxaaConsole360TexExpBiasNegOne (unused)
                    mainTex,                    // fxaaConsole360TexExpBiasNegTwo (unused)
                    _MainTex_TexelSize.xy,      // fxaaQualityRcpFrame
                    0.0,                        // fxaaConsoleRcpFrameOpt (unused)
                    0.0,                        // fxaaConsoleRcpFrameOpt2 (unused)
                    0.0,                        // fxaaConsole360RcpFrameOpt2 (unused)
                    FXAA_QUALITY_SUBPIX,
                    FXAA_QUALITY_EDGE_THRESHOLD,
                    FXAA_QUALITY_EDGE_THRESHOLD_MIN,
                    0.0,                        // fxaaConsoleEdgeSharpness (unused)
                    0.0,                        // fxaaConsoleEdgeThreshold (unused)
                    0.0,                        // fxaaConsoleEdgeThresholdMin (unused)
                    0.0                         // fxaaConsole360ConstDir (unused)
                );

                #if FXAA_KEEP_ALPHA
                {
#ifdef UNITY_STEREO_INSTANCING_ENABLED
                    color.a = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, i.texcoordStereo, sliceIndex).a;
#else
					color.a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoordStereo).a;
#endif
                }
                #endif
            }
            #else
            {
#ifdef UNITY_STEREO_INSTANCING_ENABLED
				color = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, i.texcoordStereo, sliceIndex);
#else
                color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoordStereo);
#endif
            }
            #endif

            color.rgb = Dither(color.rgb, i.texcoord);
            return color;
        }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
			
				#pragma multi_compile_instancing
                #pragma vertex VertUVTransform
                #pragma fragment Frag
                #pragma target 5.0

            ENDHLSL
        }
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
			
				#pragma multi_compile_instancing
                #pragma vertex VertUVTransform
                #pragma fragment Frag
                #pragma target 3.0

            ENDHLSL
        }
    }
}
