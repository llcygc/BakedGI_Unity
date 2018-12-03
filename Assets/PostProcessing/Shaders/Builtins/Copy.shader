Shader "Hidden/PostProcessing/Copy"
{
	HLSLINCLUDE

	#include "../StdLib.hlsl"

#ifdef UNITY_STEREO_INSTANCING_ENABLED
		TEXTURE2DARRAY_SAMPLER2DARRAY(_MainTex, sampler_MainTex);
#else
		TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
#endif

        float4 Frag(VaryingsDefault i) : SV_Target
        {
#ifdef UNITY_STEREO_INSTANCING_ENABLED
			half4 color = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, i.texcoord, i.stereoTargetEyeIndex);
#else
			half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
#endif
            return color;
        }

        float4 FragKillNaN(VaryingsDefault i) : SV_Target
        {
#ifdef UNITY_STEREO_INSTANCING_ENABLED
			half4 color = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, i.texcoord, i.stereoTargetEyeIndex);
#else
			half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
#endif

            if (AnyIsNan(color))
            {
                color = (0.0).xxxx;
            }

            return color;
        }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // 0 - Fullscreen triangle copy
        Pass
        {
            HLSLPROGRAM

				#pragma multi_compile_instancing
                #pragma vertex VertDefault
                #pragma fragment Frag

            ENDHLSL
        }

        // 1 - Fullscreen triangle copy + NaN killer
        Pass
        {
            HLSLPROGRAM

				#pragma multi_compile_instancing
                #pragma vertex VertDefault
                #pragma fragment FragKillNaN

            ENDHLSL
        }
    }
}
