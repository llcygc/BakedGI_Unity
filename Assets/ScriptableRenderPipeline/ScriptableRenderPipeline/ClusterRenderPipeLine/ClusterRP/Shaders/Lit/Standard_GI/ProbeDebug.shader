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

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float3 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			float _ProbeID;
			
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
				half3 col = SAMPLE_TEXTURECUBE_ARRAY(GI_ProbeTexture, sampler_GI_ProbeTexture, i.uv, _ProbeID);
				return half4(col, 1.0f);
			}
			ENDCG
		}
	}
}
