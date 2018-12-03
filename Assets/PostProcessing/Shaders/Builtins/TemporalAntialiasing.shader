Shader "Hidden/PostProcessing/TemporalAntialiasing"
{
    HLSLINCLUDE

        #pragma exclude_renderers gles
        #include "../StdLib.hlsl"
        #include "../Colors.hlsl"

    #if UNITY_VERSION >= 201710
        #define _MainTexSampler sampler_LinearClamp
    #else
        #define _MainTexSampler sampler_MainTex
    #endif

	#ifdef UNITY_STEREO_INSTANCING_ENABLED
		TEXTURE2DARRAY_SAMPLER2DARRAY(_MainTex, _MainTexSampler);
		TEXTURE2DARRAY_SAMPLER2DARRAY(_HistoryTex, sampler_HistoryTex);
		TEXTURE2DARRAY_SAMPLER2DARRAY(_CameraDepthTexture, sampler_CameraDepthTexture);
		TEXTURE2DARRAY_SAMPLER2DARRAY(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture);
	#else
        TEXTURE2D_SAMPLER2D(_MainTex, _MainTexSampler);
		TEXTURE2D_SAMPLER2D(_HistoryTex, sampler_HistoryTex);
		TEXTURE2D_SAMPLER2D(_CameraDepthTexture, sampler_CameraDepthTexture);
		TEXTURE2D_SAMPLER2D(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture);
	#endif

        float4 _MainTex_TexelSize;
        float4 _CameraDepthTexture_TexelSize;

        float2 _Jitter;
        float4 _FinalBlendParameters; // x: static, y: dynamic, z: motion amplification
        float _Sharpness;
		float4 _ScreenSize;

		float sqr(in float x)
		{
			return x * x;
		}

		float3 sqr(in float3 x)
		{
			return x * x;
		}

        float2 GetClosestFragment(float2 uv, int sliceIndex)
        {
            const float2 k = _CameraDepthTexture_TexelSize.xy;

#ifdef UNITY_STEREO_INSTANCING_ENABLED
			const float4 neighborhood = float4(
				SAMPLE_TEXTURE2D_ARRAY(_CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoClamp(uv - k), sliceIndex).r,
				SAMPLE_TEXTURE2D_ARRAY(_CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoClamp(uv + float2(k.x, -k.y)), sliceIndex).r,
				SAMPLE_TEXTURE2D_ARRAY(_CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoClamp(uv + float2(-k.x, k.y)), sliceIndex).r,
				SAMPLE_TEXTURE2D_ARRAY(_CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoClamp(uv + k), sliceIndex).r
				);
#else
            const float4 neighborhood = float4(
                SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoClamp(uv - k)),
                SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoClamp(uv + float2(k.x, -k.y))),
                SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoClamp(uv + float2(-k.x, k.y))),
                SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoClamp(uv + k))
            );
#endif

        #if defined(UNITY_REVERSED_Z)
            #define COMPARE_DEPTH(a, b) step(b, a)
        #else
            #define COMPARE_DEPTH(a, b) step(a, b)
        #endif

#ifdef UNITY_STEREO_INSTANCING_ENABLED
            float3 result = float3(0.0, 0.0, SAMPLE_TEXTURE2D_ARRAY(_CameraDepthTexture, sampler_CameraDepthTexture, uv, sliceIndex).r);
#else
			float3 result = float3(0.0, 0.0, SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv));
#endif
            result = lerp(result, float3(-1.0, -1.0, neighborhood.x), COMPARE_DEPTH(neighborhood.x, result.z));
            result = lerp(result, float3( 1.0, -1.0, neighborhood.y), COMPARE_DEPTH(neighborhood.y, result.z));
            result = lerp(result, float3(-1.0,  1.0, neighborhood.z), COMPARE_DEPTH(neighborhood.z, result.z));
            result = lerp(result, float3( 1.0,  1.0, neighborhood.w), COMPARE_DEPTH(neighborhood.w, result.z));

            return (uv + result.xy * k);
        }

        float4 ClipToAABB(float4 color, float3 minimum, float3 maximum)
        {
            // Note: only clips towards aabb center (but fast!)
            float3 center = 0.5 * (maximum + minimum);
            float3 extents = 0.5 * (maximum - minimum);

            // This is actually `distance`, however the keyword is reserved
            float3 offset = color.rgb - center;

            float3 ts = abs(extents / (offset + 0.0001));
            float t = saturate(Min3(ts.x, ts.y, ts.z));
            color.rgb = center + offset * t;
			color.a = 1.0f/t;
            return color;
        }

        struct OutputSolver
        {
            float4 destination : SV_Target0;
            float4 history     : SV_Target1;
        };

        OutputSolver Solve(float2 motion, float2 texcoord, int sliceIndex)
        {
            const float2 k = _MainTex_TexelSize.xy;
            float2 uv = UnityStereoClamp(texcoord - _Jitter);

#ifdef UNITY_STEREO_INSTANCING_ENABLED
			float4 color = SAMPLE_TEXTURE2D_ARRAY(_MainTex, _MainTexSampler, uv, sliceIndex);
			float4 topLeft = SAMPLE_TEXTURE2D_ARRAY(_MainTex, _MainTexSampler, UnityStereoClamp(uv - k * 0.5), sliceIndex);
			float4 bottomRight = SAMPLE_TEXTURE2D_ARRAY(_MainTex, _MainTexSampler, UnityStereoClamp(uv + k * 0.5), sliceIndex);
			float4 history = SAMPLE_TEXTURE2D_ARRAY(_HistoryTex, sampler_HistoryTex, UnityStereoClamp(texcoord - motion), sliceIndex);
#else
            float4 color = SAMPLE_TEXTURE2D(_MainTex, _MainTexSampler, uv);
            float4 topLeft = SAMPLE_TEXTURE2D(_MainTex, _MainTexSampler, UnityStereoClamp(uv - k * 0.5));
            float4 bottomRight = SAMPLE_TEXTURE2D(_MainTex, _MainTexSampler, UnityStereoClamp(uv + k * 0.5));
			float4 history = SAMPLE_TEXTURE2D(_HistoryTex, sampler_HistoryTex, UnityStereoClamp(texcoord - motion));
#endif
            float4 corners = 4.0 * (topLeft + bottomRight) - 2.0 * color;

            // Sharpen output
            color += (color - (corners * 0.166667)) * 2.718282 * _Sharpness;
            color = max(0.0, color);

            // Tonemap color and history samples
            float4 average = FastTonemap((corners + color) * 0.142857);

            topLeft = FastTonemap(topLeft);
            bottomRight = FastTonemap(bottomRight);

            color = FastTonemap(color);


            float motionLength = length(motion);
            float2 luma = float2(Luminance(average), Luminance(color));
            //float nudge = 4.0 * abs(luma.x - luma.y);
            float nudge = lerp(4.0, 0.25, saturate(motionLength * 100.0)) * abs(luma.x - luma.y);

            float4 minimum = min(bottomRight, topLeft) - nudge;
            float4 maximum = max(topLeft, bottomRight) + nudge;

            history = FastTonemap(history);

            // Clip history samples
            history = ClipToAABB(history, minimum.xyz, maximum.xyz);

            // Blend method
            float weight = clamp(
                lerp(_FinalBlendParameters.x, _FinalBlendParameters.y, motionLength * _FinalBlendParameters.z),
                _FinalBlendParameters.y, _FinalBlendParameters.x
            );

            color = FastTonemapInvert(lerp(color, history, weight));

            OutputSolver output;
            output.destination = color;
            output.history = color;
            return output;
        }

        OutputSolver FragSolverDilate(VaryingsDefault i)
        {
			int sliceIndex = 0;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
			sliceIndex = i.stereoTargetEyeIndex;
#endif
            float2 closest = GetClosestFragment(i.texcoordStereo, sliceIndex);

#ifdef UNITY_STEREO_INSTANCING_ENABLED
			float2 motion = SAMPLE_TEXTURE2D_ARRAY(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture, closest, sliceIndex).xy;
#else
            float2 motion = SAMPLE_TEXTURE2D(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture, closest).xy;
#endif
            return Solve(motion, i.texcoordStereo, sliceIndex);
        }

		OutputSolver FragSolverNoDilate(VaryingsDefault i)
		{
			int sliceIndex = 0;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
			sliceIndex = i.stereoTargetEyeIndex;
#endif

#ifdef UNITY_STEREO_INSTANCING_ENABLED
			float2 motion = SAMPLE_TEXTURE2D_ARRAY(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture, i.texcoordStereo, sliceIndex).xy;
#else
			float2 motion = SAMPLE_TEXTURE2D(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture, i.texcoordStereo).xy;
#endif
			return Solve(motion, i.texcoordStereo, sliceIndex);
		}

		float4 SloveDestination(float2 motion, float2 texcoord, int sliceIndex)
		{
			const float2 k = _MainTex_TexelSize.xy;
			float2 uv = UnityStereoClamp(texcoord - _Jitter);

#ifdef UNITY_STEREO_INSTANCING_ENABLED
			float4 color = SAMPLE_TEXTURE2D_ARRAY(_MainTex, _MainTexSampler, uv, sliceIndex);
			float3 topLeft = SAMPLE_TEXTURE2D_ARRAY(_MainTex, _MainTexSampler, UnityStereoClamp(uv - k * 0.5), sliceIndex);
			float3 bottomRight = SAMPLE_TEXTURE2D_ARRAY(_MainTex, _MainTexSampler, UnityStereoClamp(uv + k * 0.5), sliceIndex);
			float3 topRight = SAMPLE_TEXTURE2D_ARRAY(_MainTex, _MainTexSampler, UnityStereoClamp(uv + k * float2(-0.5, 0.5)), sliceIndex);
			float3 bottomLeft = SAMPLE_TEXTURE2D_ARRAY(_MainTex, _MainTexSampler, UnityStereoClamp(uv + k * float2(0.5, -0.5)), sliceIndex);
			float4 history = SAMPLE_TEXTURE2D_ARRAY(_HistoryTex, sampler_HistoryTex, UnityStereoClamp(texcoord - motion), sliceIndex);
#else
			float4 color = SAMPLE_TEXTURE2D(_MainTex, _MainTexSampler, uv);
			float3 topLeft = SAMPLE_TEXTURE2D(_MainTex, _MainTexSampler, UnityStereoClamp(uv - k * 0.5));
			float3 bottomRight = SAMPLE_TEXTURE2D(_MainTex, _MainTexSampler, UnityStereoClamp(uv + k * 0.5));
			float3 topRight = SAMPLE_TEXTURE2D(_MainTex, _MainTexSampler, UnityStereoClamp(uv + k * float2(-0.5, 0.5)));
			float3 bottomLeft = SAMPLE_TEXTURE2D(_MainTex, _MainTexSampler, UnityStereoClamp(uv + k * float2(0.5, -0.5)));
			float4 history = SAMPLE_TEXTURE2D(_HistoryTex, sampler_HistoryTex, UnityStereoClamp(texcoord - motion));
#endif

			float4 corners = 4.0 * (topLeft.rgbb + bottomRight.rgbb) - 2.0 * color;

			// Sharpen output
			color += (color - (corners * 0.166667)) * 2.718282 * _Sharpness;
			color = max(0.0, color);

			// Tonemap color and history samples
			float4 average = FastTonemap((corners + color) * 0.142857);

			topLeft = FastTonemap(topLeft);
			bottomRight = FastTonemap(bottomRight);
			topRight = FastTonemap(topRight);
			bottomLeft = FastTonemap(bottomLeft);
			color = FastTonemap(color);

			const float3 sum = topLeft + bottomRight + topRight + color.rgb + bottomLeft;
			const float3 sum_of_squares = sqr(topLeft) + sqr(bottomRight) + sqr(topRight) + sqr(color.rgb) + sqr(bottomLeft);
			const float kk = rcp(5.0);

			float motionLength = length(motion);
			float2 luma = float2(Luminance(average), Luminance(color));
			//float nudge = 4.0 * abs(luma.x - luma.y);
			float nudge = lerp(4.0, 0.25, saturate(motionLength * 100.0)) * abs(luma.x - luma.y);

			const float3 avg = sum * kk;
			const float3 sigma = sqrt(abs(sum_of_squares * kk - avg * avg));

			float3 minimum = avg - sigma; //min(bottomRight, topLeft) - nudge;
			float3 maximum = avg + sigma;// max(topLeft, bottomRight) + nudge;

			history = FastTonemap(history);

			// Clip history samples
			history = ClipToAABB(history, minimum.xyz, maximum.xyz);
			
			const float2 v = motion * _ScreenSize.xy;
			const float velocity_confidence = 1.0 - saturate((length(v) - 2.0) / 13.0);

			float acceptance = pow(saturate(min(history.a, 2 - history.a)), 0.25);

			const float blend_factor = lerp(0.65, 0.95, velocity_confidence * acceptance);
			// Blend method
			float weight = clamp(
				lerp(_FinalBlendParameters.x, _FinalBlendParameters.y, motionLength * _FinalBlendParameters.z),
				_FinalBlendParameters.y, _FinalBlendParameters.x
			);

			color = FastTonemapInvert(lerp(color, history, blend_factor));

			return color;
		}

		float4 FragSolverDilateDestination(VaryingsDefault i) : SV_Target
		{
			int sliceIndex = 0;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
			sliceIndex = i.stereoTargetEyeIndex;
#endif
			float2 closest = GetClosestFragment(i.texcoordStereo, sliceIndex);

#ifdef UNITY_STEREO_INSTANCING_ENABLED
			float2 motion = SAMPLE_TEXTURE2D_ARRAY(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture, closest, sliceIndex).xy;
#else
			float2 motion = SAMPLE_TEXTURE2D(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture, closest).xy;
#endif

#ifdef UNITY_STEREO_INSTANCING_ENABLED
			float2 testUV = closest * 2.0 - 1.0;
			if (dot(testUV, testUV) > 1)
				return float4(0, 0, 0, 1);
#endif
			return SloveDestination(motion, i.texcoordStereo, sliceIndex);
		}

    ENDHLSL

    SubShader
    {
		
        Cull Off ZWrite Off ZTest Always

        // 0: Perspective
        Pass
        {
            HLSLPROGRAM
			
				#pragma multi_compile_instancing
                #pragma vertex VertDefault
                #pragma fragment FragSolverDilate

            ENDHLSL
        }

        // 1: Ortho
        Pass
        {
            HLSLPROGRAM
			
				#pragma multi_compile_instancing
                #pragma vertex VertDefault
                #pragma fragment FragSolverNoDilate

            ENDHLSL
        }

        // 2: Perspective and seperate
        Pass
        {
            HLSLPROGRAM
			
				#pragma multi_compile_instancing
                #pragma vertex VertDefault
                #pragma fragment FragSolverDilateDestination

            ENDHLSL
        }
    }
}
