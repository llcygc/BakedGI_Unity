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
			#include "GI_Data.hlsl"

			/*TEXTURECUBE_ARRAY(GI_ProbeTexture);
			SAMPLER(sampler_GI_ProbeTexture);

			TEXTURECUBE_ARRAY(GI_NormalTexture);
			SAMPLER(sampler_GI_NormalTexture);

			TEXTURECUBE_ARRAY(GI_DepthTexture);
			SAMPLER(sampler_GI_DepthTexture);*/

			TEXTURE2D_ARRAY(RadMapOctan);
			SAMPLER(sampler_RadMapOctan);

			TEXTURE2D_ARRAY(DistMapOctan);
			SAMPLER(sampler_DistMapOctan);

			TEXTURE2D_ARRAY(NormalMapOctan);
			SAMPLER(sampler_NormalMapOctan);

			CBUFFER_START(ProbeDebugInfo)
			float _DebugProbeID;
			float GI_DebugMode;
			float4 DebugPos;
			float4 DebugDir;
			float4 DebugParam;
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

			half3 TraceSingleProbeDebug(uint index, float3 worldPos, float3 dir, float tMin, float tMax, float2 uv)
			{
				ProbeData pData = ProbeDataBuffer[index];
				float3 localPos = worldPos - pData.position;

				float boundaryTs[5];

				boundaryTs[0] = 0.0f;

				float3 t = localPos * -(1.0f / dir);
				sort(t);

				for (int i = 0; i < 3; ++i)
				{
					boundaryTs[i + 1] = clamp(t[i], 0.0f, 1000.0f);
				}

				boundaryTs[4] = 1000.0f;

				const float degenerateEpsilon = 0.001f;

				const half3 colors[4] = { RED, GREEN, BLUE, ORANGE };

				for (int i = 0; i < 4; i++)
				{
					float t0 = boundaryTs[i];
					float t1 = boundaryTs[i + 1];
					if (abs(t0 - t1) >= degenerateEpsilon)
					{
						float3 startPoint = localPos + dir * (t0 + rayBumpEpsilon);
						float3 endPoint = localPos + dir * (t1 - rayBumpEpsilon);

						if (sqrLength(startPoint) < 0.001)
							startPoint = dir;

						startPoint.y *= -1;
						endPoint.y *= -1;

						float2 startUV = octEncode(normalize(startPoint)) * 0.5 + 0.5;
						float2 endUV = octEncode(normalize(endPoint)) * 0.5 + 0.5;						

						float2 startCoord = startUV * CubeOctanResolution.x;
						float2 endCoord = endUV * CubeOctanResolution.x;

						endUV += sqrLength(startUV - endUV) > 0.0001 ? 0.01 : 0.0;
						float2 delta = endUV - startUV;
						float2 traceDir = normalize(delta);
						float dist = length(delta);

						float traceStep = dist / max(abs(delta.x * CubeOctanResolution.x), abs(delta.y * CubeOctanResolution.y));

						float3 dirBefore = octDecode((startUV) * 2.0 - 1.0);
						dirBefore.y *= -1;
						float distBefore = max(0.0, distanceToIntersection(localPos, dir, dirBefore));

						float traceDist = 0;
						while (traceDist < dist)
						{
							float2 currentUV = startUV + traceDir * min(traceDist + traceStep * 0.5, dist);
							int2 currentCoord = (int2) (currentUV * CubeOctanResolution.xy);
							float sceneDist = LOAD_TEXTURE2D_ARRAY(DistMapOctan, currentCoord, index) * ProbeProjectonParam.y;

							float2 afterUV = startUV + traceDir * min(traceDist + traceStep, dist);
							float3 dirAfter = octDecode(currentUV * 2.0 - 1.0);
							dirAfter.y *= -1;
							float distAfter = max(0.0, distanceToIntersection(localPos, dir, dirAfter));

							float maxRayDist = max(distBefore, distAfter);

							half3 finalColor = 0;

							if (length(uv - currentUV) <= (1 / CubeOctanResolution.x))
							{
								float minRayDist = min(distBefore, distAfter);
								if (maxRayDist >= sceneDist)
								{
									if (minRayDist < sceneDist)
										return WHITE;
									else 
										return RED;
								}
							}


							distBefore = distAfter;
							dirBefore = dirAfter;
							traceDist += traceStep;
						}
					}
				}

				return 0;
			}

			half3 SampleProbeColor_Cube(float3 dir)
			{
				if (GI_DebugMode == 0)
					return SAMPLE_TEXTURECUBE_ARRAY(GI_ProbeTexture, sampler_GI_ProbeTexture, dir, _DebugProbeID);
				else if (GI_DebugMode == 1)
					return SAMPLE_TEXTURECUBE_ARRAY(GI_NormalTexture, sampler_GI_NormalTexture, dir, _DebugProbeID);
				else
					return SAMPLE_TEXTURECUBE_ARRAY(GI_DepthTexture, sampler_GI_DepthTexture, dir, _DebugProbeID).rrr;
			}

			half3 SampleProbeColor_Octan(float3 dir)
			{
				float2 uv = octEncode(dir);
				uv = uv * 0.5 + 0.5;
				
				float3 vertDir = dir;
				vertDir.y *= -1;
				float3 localPos = DebugPos - ProbeDataBuffer[(int)_DebugProbeID].position;
				float3 tempDir = cross(DebugDir, vertDir);
				float result = dot(tempDir, normalize(localPos));
				half3 color = 0;
				

				if (GI_DebugMode == 0)
					color += SAMPLE_TEXTURE2D_ARRAY(RadMapOctan, sampler_RadMapOctan, uv, _DebugProbeID);
				else if (GI_DebugMode == 1)
					color += SAMPLE_TEXTURE2D_ARRAY(NormalMapOctan, sampler_NormalMapOctan, uv, _DebugProbeID);
				else
					color += SAMPLE_TEXTURE2D_ARRAY(DistMapOctan, sampler_DistMapOctan, uv, _DebugProbeID).rrr;

				half3 resultColor = TraceSingleProbeDebug((uint)_DebugProbeID, DebugPos, DebugDir, 0.0f, 1000.0f, uv);
				if(!all(resultColor == 0))
					color = resultColor;
				

				return color;
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
