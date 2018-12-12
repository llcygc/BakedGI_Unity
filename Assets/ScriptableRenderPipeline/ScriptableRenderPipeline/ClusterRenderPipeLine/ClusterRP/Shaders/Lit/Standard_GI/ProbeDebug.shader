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
			#include "Octahedral.hlsl"

			TEXTURECUBE_ARRAY(GI_ProbeTexture);
			SAMPLER(sampler_GI_ProbeTexture);

			TEXTURECUBE_ARRAY(GI_NormalTexture);
			SAMPLER(sampler_GI_NormalTexture);

			TEXTURECUBE_ARRAY(GI_DepthTexture);
			SAMPLER(sampler_GI_DepthTexture);

			TEXTURE2D_ARRAY(RadMapOctan);
			SAMPLER(sampler_RadMapOctan);

			TEXTURE2D_ARRAY(DistMapOctan);
			SAMPLER(sampler_DistMapOctan);

			TEXTURE2D_ARRAY(NormalMapOctan);
			SAMPLER(sampler_NormalMapOctan);

			float _ProbeID;
			float GI_DebugMode;

			CBUFFER_START(ProbeInfo)
			float4 ProbeDimenson;
			float4 ProbeMin;
			float4 ProbeMax;
			CBUFFER_END

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

			half3 IndexToCoord(int index)
			{
				half3 coord = 0;
				coord.z = index % ((int)ProbeDimenson.z);
				coord.y = floor(index % ((int)ProbeDimenson.z * (int)ProbeDimenson.y) / ProbeDimenson.z);
				coord.x = floor(index / ((int)ProbeDimenson.z * (int)ProbeDimenson.y));

				return coord / ProbeDimenson;
			}

			half3 SampleProbeColor_Cube(float3 dir)
			{
				if (GI_DebugMode == 0)
					return SAMPLE_TEXTURECUBE_ARRAY(GI_ProbeTexture, sampler_GI_ProbeTexture, dir, _ProbeID);
				else if (GI_DebugMode == 1)
					return SAMPLE_TEXTURECUBE_ARRAY(GI_NormalTexture, sampler_GI_NormalTexture, dir, _ProbeID);
				else
					return SAMPLE_TEXTURECUBE_ARRAY(GI_DepthTexture, sampler_GI_DepthTexture, dir, _ProbeID).rrr;
			}

			half3 SampleProbeColor_Octan(float3 dir)
			{
				float2 uv = octEncode(dir);
				uv = uv * 0.5 + 0.5;
				if (GI_DebugMode == 0)
					return SAMPLE_TEXTURE2D_ARRAY(RadMapOctan, sampler_RadMapOctan, uv, _ProbeID);
				else if (GI_DebugMode == 1)
					return SAMPLE_TEXTURE2D_ARRAY(NormalMapOctan, sampler_NormalMapOctan, uv, _ProbeID);
				else
					return SAMPLE_TEXTURE2D_ARRAY(DistMapOctan, sampler_DistMapOctan, uv, _ProbeID).rrr;
			}

			half3 frag(v2f i) : SV_Target
			{
				// sample the texture
				//return IndexToCoord(_ProbeID);
				return SampleProbeColor_Octan(normalize(i.uv)); //normalize(i.uv);//
			}
			ENDCG
		}
	}
}
