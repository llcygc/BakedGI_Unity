Shader "Unlit/GI_ProbeDebug"
{
	Properties
	{
		_ProbeID("Probe ID", Int) = 0
	}
		SubShader
	{
		Tags { "RenderType" = "Opaque" }

		Pass
		{
			CGPROGRAM
			#pragma target 5.0
			#pragma vertex vert
			#pragma fragment frag
			
			//#include "UnityCG.cginc"
			#include "CoreRP/ShaderLibrary/Common.hlsl"

			TEXTURECUBE_ARRAY(GI_ProbeTexture);
			SAMPLER(sampler_GI_ProbeTexture);

			TEXTURECUBE_ARRAY(GI_NormalTexture);
			SAMPLER(sampler_GI_NormalTexture);

			float _ProbeID;
			float GI_DebugMode;

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float3 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.vertex.xyz;
				o.uv.y *= -1;
				return o;
			}
			
			half4 frag (v2f i) : SV_Target
			{
				// sample the texture
				half4 col = GI_DebugMode == 0 ? SAMPLE_TEXTURECUBE_ARRAY(GI_ProbeTexture, sampler_GI_ProbeTexture, i.uv, _ProbeID) 
												: SAMPLE_TEXTURECUBE_ARRAY(GI_NormalTexture, sampler_GI_NormalTexture, i.uv, _ProbeID);
				
				half3 finalColor = GI_DebugMode == 2 ? col.aaa : col.rgb;
				return half4(finalColor.rgb, 1.0f);
			}
			ENDCG
		}
	}
}
