Shader "ScreenSpaceShadow"
{
     HLSLINCLUDE

        #pragma target 4.5

        #include "CoreRP/ShaderLibrary/Common.hlsl"
        #include "../../ShaderVariables.hlsl"
        #include "../ShaderPasses/FragInputs.hlsl"
        #include "../ShaderPasses/VaryingMesh.hlsl"
        #include "../ShaderPasses/VertMesh.hlsl"
        #include "../Lit/Lighting/ClusterUtils.cginc"
        #include "../ShadowUtils.cginc"

        struct Attributes
        {
            uint vertexID : SV_VertexID;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            return output;
        }

        float4 GetWorldPosFromThreadID(float2 uv, float depth, float4x4 InvVPMatrix)
        {
            float4 clipPos = float4(uv.x / ClusterScreenParams.x, 1 - uv.y / ClusterScreenParams.y, depth, 1.0f);

            clipPos.xy = clipPos.xy * 2 - 1;

            float4 WorldPos = mul(InvVPMatrix, clipPos);

            return WorldPos / WorldPos.w;
        }

        half frag(Varyings input) : SV_Target
        {
#ifdef UNITY_STEREO_INSTANCING_ENABLED
            float srcDepth = LOAD_TEXTURE2D_ARRAY(_CameraDepthTexture, input.positionCS.xy, input.stereoTargetEyeIndex).x;
            float4x4 invVP = _InvViewProjMatrixStereo[input.stereoTargetEyeIndex];
#else
            float srcDepth = LOAD_TEXTURE2D(_CameraDepthTexture, input.positionCS.xy).x;
            float4x4 invVP = _InvViewProjMatrix;
#endif

            float4 worldPos = GetWorldPosFromThreadID(input.positionCS.xy, srcDepth, invVP);

            DirectionalLightDataSimple dirLight = _DirectionalLightsDataSimpleBuffer[0];

            return Compute_Direction_Light_Shadow(worldPos, dirLight);
        }

    ENDHLSL

	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{     
            Stencil
            {
                ReadMask 64
                Ref  64 // StencilBitMask.ObjectVelocity
                Comp Equal
                Pass Keep
            }

            Cull Off ZWrite Off ZTest Off

            HLSLPROGRAM
            
			#pragma multi_compile_instancing
			#pragma vertex vert
			#pragma fragment frag
			// make fog work

            ENDHLSL
		}
            
		Pass
		{     
            Cull Off ZWrite Off ZTest Off

            HLSLPROGRAM
            
			#pragma multi_compile_instancing
			#pragma vertex vert
			#pragma fragment frag
			// make fog work

            ENDHLSL
		}
	}
}
