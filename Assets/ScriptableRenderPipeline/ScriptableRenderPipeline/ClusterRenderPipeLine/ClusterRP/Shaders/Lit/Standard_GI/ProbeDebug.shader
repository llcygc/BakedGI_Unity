Shader "Unlit/GI_ProbeDebug"
{
	Properties
	{
		_ProbeID("Probe ID", Vector) = (0, 0, 0)
	}
		SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			UNITY_DECLARE_TEXCUBE(GI_ProbeTexture);			

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float3 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.vertex.xyz;
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				// sample the texture
				fixed4 col = UNITY_SAMPLE_TEXCUBE(GI_ProbeTexture, i.uv);
				return col;
			}
			ENDCG
		}
	}
}
