Shader "StencilMaskBlit"
{
     HLSLINCLUDE

        #pragma target 4.5

        #include "CoreRP/ShaderLibrary/Common.hlsl"
        #include "../../ShaderVariables.hlsl"
        #include "../ShaderPasses/FragInputs.hlsl"
        #include "../ShaderPasses/VaryingMesh.hlsl"
        #include "../ShaderPasses/VertMesh.hlsl"

        TEXTURE2D(_R8Buffer);

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

        half4 frag(Varyings input) : SV_Target
        {
            half value = LOAD_TEXTURE2D(_R8Buffer, input.positionCS.xy).x;
            clip(value - 0.5);

            return half4(0, 0, 0, 0);
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
                WriteMask 255
                Ref 64
                Comp Always
                Pass Replace
            }

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
