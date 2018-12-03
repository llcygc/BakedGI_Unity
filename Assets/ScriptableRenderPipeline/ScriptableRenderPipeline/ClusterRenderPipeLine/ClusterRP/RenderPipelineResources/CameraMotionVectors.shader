Shader "Hidden/ClusterRenderPipeline/CameraMotionVectors"
{
    HLSLINCLUDE

        #pragma target 4.5

        #include "CoreRP/ShaderLibrary/Common.hlsl"
        #include "../ShaderVariables.hlsl"
        #include "../Shaders/ShaderPasses/FragInputs.hlsl"
        #include "../Shaders/ShaderPasses/VaryingMesh.hlsl"
        #include "../Shaders/ShaderPasses/VertMesh.hlsl"

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

        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            return output;
        }

        float4 Frag(Varyings input) : SV_Target
        {
#ifdef UNITY_STEREO_INSTANCING_ENABLED
            float depth = LOAD_TEXTURE2D_ARRAY(_CameraDepthTexture, input.positionCS.xy, input.stereoTargetEyeIndex).x;
#else
            float depth = LOAD_TEXTURE2D(_CameraDepthTexture, input.positionCS.xy).x;
#endif

            PositionInputs posInput = GetPositionInput(input.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

            float4 worldPos = float4(posInput.positionWS, 1.0);
            float4 prevPos = worldPos;

#ifdef UNITY_STEREO_INSTANCING_ENABLED
            float4 prevClipPos = mul(_PrevViewProjMatrixStereo[input.stereoTargetEyeIndex], prevPos);
            float4 curClipPos = mul(_NonJitteredViewProjMatrixStereo[input.stereoTargetEyeIndex], worldPos);
#else
            float4 prevClipPos = mul(_PrevViewProjMatrix, prevPos);
            float4 curClipPos = mul(_NonJitteredViewProjMatrix, worldPos);
#endif
            float2 prevHPos = prevClipPos.xy / prevClipPos.w;
            float2 curHPos = curClipPos.xy / curClipPos.w;

            float2 previousPositionCS = (prevHPos + 1.0) / 2.0;
            float2 positionCS = (curHPos + 1.0) / 2.0;

        #if UNITY_UV_STARTS_AT_TOP
            previousPositionCS.y = 1.0 - previousPositionCS.y;
            positionCS.y = 1.0 - positionCS.y;
        #endif

            return float4(positionCS - previousPositionCS, 0.0, 1.0);
        }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "ClusterRenderPipeline" }

        Pass
        {
            // We will perform camera motion velocity only where there is no object velocity
            Stencil
            {
                ReadMask 128
                Ref  128 // StencilBitMask.ObjectVelocity
                Comp NotEqual
                Pass Keep
            }

            Cull Off ZWrite Off ZTest Always

            HLSLPROGRAM
            
				#pragma multi_compile_instancing
                #pragma vertex Vert
                #pragma fragment Frag

            ENDHLSL
        }
    }
}
