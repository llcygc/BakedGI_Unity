Shader "Hidden/PostProcessing/Bloom"
{
    HLSLINCLUDE
        
        #include "../StdLib.hlsl"
        #include "../Colors.hlsl"
        #include "../Sampling.hlsl"

#ifdef UNITY_STEREO_INSTANCING_ENABLED
		TEXTURE2DARRAY_SAMPLER2DARRAY(_MainTex, sampler_MainTex);
        TEXTURE2DARRAY_SAMPLER2DARRAY(_BloomTex, sampler_BloomTex);
#else
		TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
		TEXTURE2D_SAMPLER2D(_BloomTex, sampler_BloomTex);
#endif

		TEXTURE2D_SAMPLER2D(_AutoExposureTex, sampler_AutoExposureTex);

        float4 _MainTex_TexelSize;
        float _SampleScale;
        float4 _ColorIntensity;
        float4 _Threshold; // x: threshold value (linear), y: threshold - knee, z: knee * 2, w: 0.25 / knee

        // ----------------------------------------------------------------------------------------
        // Prefilter

        half4 Prefilter(half4 color, float2 uv, int sliceIndex = 0)
        {
			half autoExposure = SAMPLE_TEXTURE2D(_AutoExposureTex, sampler_AutoExposureTex, uv).r;
			color *= autoExposure;
            color = QuadraticThreshold(color, _Threshold.x, _Threshold.yzw);
            return color;
        }

        half4 FragPrefilter13(VaryingsDefault i) : SV_Target
        {
			int sliceIndex = 0;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
		sliceIndex = i.stereoTargetEyeIndex;
#endif
            half4 color = DownsampleBox13Tap(_MainTex, sampler_MainTex, i.texcoord, _MainTex_TexelSize.xy, sliceIndex);
            return Prefilter(SafeHDR(color), i.texcoord, sliceIndex);
        }

        half4 FragPrefilter4(VaryingsDefault i) : SV_Target
        {
			int sliceIndex = 0;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
		sliceIndex = i.stereoTargetEyeIndex;
#endif
            half4 color = DownsampleBox4Tap(_MainTex, sampler_MainTex, i.texcoord, _MainTex_TexelSize.xy, sliceIndex);
            return Prefilter(SafeHDR(color), i.texcoord, sliceIndex);
        }

        // ----------------------------------------------------------------------------------------
        // Downsample

        half4 FragDownsample13(VaryingsDefault i) : SV_Target
        {
			int sliceIndex = 0;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
			sliceIndex = i.stereoTargetEyeIndex;
#endif
            half4 color = DownsampleBox13Tap(_MainTex, sampler_MainTex, i.texcoord, _MainTex_TexelSize.xy, sliceIndex);
            return color;
        }

        half4 FragDownsample4(VaryingsDefault i) : SV_Target
        {
			int sliceIndex = 0;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
			sliceIndex = i.stereoTargetEyeIndex;
#endif
            half4 color = DownsampleBox4Tap(_MainTex, sampler_MainTex, i.texcoord, _MainTex_TexelSize.xy, sliceIndex);
            return color;
        }

        // ----------------------------------------------------------------------------------------
        // Upsample & combine

        half4 Combine(half4 bloom, float2 uv, int sliceIndex = 0)
        {
#ifdef UNITY_STEREO_INSTANCING_ENABLED
            half4 color = SAMPLE_TEXTURE2D_ARRAY(_BloomTex, sampler_BloomTex, uv, sliceIndex);
#else
			half4 color = SAMPLE_TEXTURE2D(_BloomTex, sampler_BloomTex, uv);
#endif
            return bloom + color;
        }

        half4 FragUpsampleTent(VaryingsDefault i) : SV_Target
        {
			int sliceIndex = 0;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
		sliceIndex = i.stereoTargetEyeIndex;
#endif
            half4 bloom = UpsampleTent(_MainTex, sampler_MainTex, i.texcoord, _MainTex_TexelSize.xy, _SampleScale, sliceIndex);
            return Combine(bloom, i.texcoordStereo, sliceIndex);
        }

        half4 FragUpsampleBox(VaryingsDefault i) : SV_Target
        {
			int sliceIndex = 0;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
		sliceIndex = i.stereoTargetEyeIndex;
#endif
            half4 bloom = UpsampleBox(_MainTex, sampler_MainTex, i.texcoord, _MainTex_TexelSize.xy, _SampleScale, sliceIndex);
            return Combine(bloom, i.texcoordStereo, sliceIndex);
        }

        // ----------------------------------------------------------------------------------------
        // Debug overlays

        half4 FragDebugOverlayThreshold(VaryingsDefault i) : SV_Target
        {
			int sliceIndex = 0;

#ifdef UNITY_STEREO_INSTANCING_ENABLED
			sliceIndex = i.stereoTargetEyeIndex;
            half4 color = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, i.texcoordStereo, sliceIndex);
#else
			half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoordStereo);
#endif

            return half4(Prefilter(SafeHDR(color), i.texcoord, sliceIndex).rgb, 1.0);
        }

        half4 FragDebugOverlayTent(VaryingsDefault i) : SV_Target
        {
			int sliceIndex = 0;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
		sliceIndex = i.stereoTargetEyeIndex;
#endif
            half4 bloom = UpsampleTent(_MainTex, sampler_MainTex, i.texcoord, _MainTex_TexelSize.xy, _SampleScale, sliceIndex);
            return half4(bloom.rgb * _ColorIntensity.w * _ColorIntensity.rgb, 1.0);
        }

        half4 FragDebugOverlayBox(VaryingsDefault i) : SV_Target
        {
			int sliceIndex = 0;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
		sliceIndex = i.stereoTargetEyeIndex;
#endif
            half4 bloom = UpsampleBox(_MainTex, sampler_MainTex, i.texcoord, _MainTex_TexelSize.xy, _SampleScale, sliceIndex);
            return half4(bloom.rgb * _ColorIntensity.w * _ColorIntensity.rgb, 1.0);
        }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
		
        // 0: Prefilter 13 taps
        Pass
        {
            HLSLPROGRAM
			
				#pragma multi_compile_instancing
                #pragma vertex VertDefault
                #pragma fragment FragPrefilter13

            ENDHLSL
        }

        // 1: Prefilter 4 taps
        Pass
        {
            HLSLPROGRAM
			
				#pragma multi_compile_instancing
                #pragma vertex VertDefault
                #pragma fragment FragPrefilter4

            ENDHLSL
        }

        // 2: Downsample 13 taps
        Pass
        {
            HLSLPROGRAM
			
				#pragma multi_compile_instancing
                #pragma vertex VertDefault
                #pragma fragment FragDownsample13

            ENDHLSL
        }

        // 3: Downsample 4 taps
        Pass
        {
            HLSLPROGRAM
			
				#pragma multi_compile_instancing
                #pragma vertex VertDefault
                #pragma fragment FragDownsample4

            ENDHLSL
        }

        // 4: Upsample tent filter
        Pass
        {
            HLSLPROGRAM
			
				#pragma multi_compile_instancing
                #pragma vertex VertDefault
                #pragma fragment FragUpsampleTent

            ENDHLSL
        }

        // 5: Upsample box filter
        Pass
        {
            HLSLPROGRAM
			
				#pragma multi_compile_instancing
                #pragma vertex VertDefault
                #pragma fragment FragUpsampleBox

            ENDHLSL
        }

        // 6: Debug overlay (threshold)
        Pass
        {
            HLSLPROGRAM
			
				#pragma multi_compile_instancing
                #pragma vertex VertDefault
                #pragma fragment FragDebugOverlayThreshold

            ENDHLSL
        }

        // 7: Debug overlay (tent filter)
        Pass
        {
            HLSLPROGRAM
			
				#pragma multi_compile_instancing
                #pragma vertex VertDefault
                #pragma fragment FragDebugOverlayTent

            ENDHLSL
        }

        // 8: Debug overlay (box filter)
        Pass
        {
            HLSLPROGRAM
			
				#pragma multi_compile_instancing
                #pragma vertex VertDefault
                #pragma fragment FragDebugOverlayBox

            ENDHLSL
        }
    }
}
