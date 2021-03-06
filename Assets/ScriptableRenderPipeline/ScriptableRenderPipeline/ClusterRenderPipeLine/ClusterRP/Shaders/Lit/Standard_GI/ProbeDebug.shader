﻿Shader "Unlit/GI_ProbeDebug"
{
	Properties
	{
		_DebugProbeID("Probe ID", Int) = 0
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

			TEXTURE2D_ARRAY(DistMapMinMipOctan);
			SAMPLER(sampler_DistMapMinMipOctan);

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
			
			half3 LowResTrace(uint index, float3 localPos, float3 dir, inout float2 startUV, in float2 segEndUV, inout float2 hitEndUV, in float2 uv)
			{
				hitEndUV = startUV;
				float2 startCoord = startUV * CubeOctanResolution.zw;
				float2 segEndCoord = segEndUV * CubeOctanResolution.zw;

				float texelSize = 1.0f / CubeOctanResolution.z;
				float2 delta = segEndCoord - startCoord;

				segEndCoord += ((sqrLength(startCoord - segEndCoord) < 0.0001) ? 0.01 : 0.0);

				float steps = max(abs(delta.x), abs(delta.y));
				float dist = length(delta);
				float2 traceDir = delta / dist;

				float traceDist = 0;
				float3 dirBefore = octDecode(startUV * 2.0 - 1.0);
				dirBefore.y *= -1;
				float distBefore = max(0.0, distanceToIntersectionFix(localPos, dir, dirBefore));
				float2 currentCoord = startCoord;

				while (traceDist < dist)
				{
					float sceneMinDist = LOAD_TEXTURE2D_ARRAY(DistMapMinMipOctan, floor(currentCoord), index) * ProbeProjectonParam.y;

					float2 deltaToPixelEdge = frac(currentCoord) - (sign(delta) * 0.5 + 0.5);
					float2 distToPixelEdge = deltaToPixelEdge * (-1.0f / traceDir);
					float traceStep = min(abs(distToPixelEdge.x), abs(distToPixelEdge.y));

					float2 hitEndUV = (currentCoord + traceDir * traceStep) / CubeOctanResolution.zw;
					if (length(hitEndUV * CubeOctanResolution.zw - startCoord) > dist)
						hitEndUV = segEndUV;

					float3 dirEnd = octDecode(hitEndUV * 2.0 - 1.0);
					dirEnd.y *= -1;
					float distAfter = max(0.0, distanceToIntersectionFix(localPos, dir, dirEnd));

					float2 currentCenter = floor(currentCoord) + 0.5f;

					if (length(uv * CubeOctanResolution.zw - currentCenter) <= 0.5f)
					{
						//return max(distBefore, distAfter) / ProbeProjectonParam.y;
						if (max(distBefore, distAfter) > sceneMinDist)
						{
							startUV = currentCoord / CubeOctanResolution.zw;
							return RED;
						}
						else
							return GREEN;
					}

					const float epsilon = 0.001; // pixels
					currentCoord += traceDir * (traceStep + epsilon);
					traceDist += (traceStep + epsilon);
				}

				startUV = segEndUV;
				return BLACK;
			}

			half3 TraceSingleProbeDebugLow(uint index, float3 worldPos, float3 dir, float tMin, float tMax, float2 uv)
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

				/*float3 startPointD = localPos;
				startPointD.y *= -1;
				float2 startUVD = octEncode(normalize(startPointD)) * 0.5 + 0.5;
				float2 startCoordD = startUVD * CubeOctanResolution.x;
				float sceneDistD = LOAD_TEXTURE2D_ARRAY(DistMapOctan, startCoordD, index) * ProbeProjectonParam.y;
				if (length(uv - startUVD) <= (1 / CubeOctanResolution.x))
				return length(localPos) / ProbeProjectonParam.y;*/

				for (int i = 0; i < 4; i++)
				{
					float t0 = boundaryTs[i];
					float t1 = boundaryTs[i + 1];
					if (abs(t0 - t1) > degenerateEpsilon)
					{
						float3 startPoint = localPos + dir * (t0 + rayBumpEpsilon);
						float3 endPoint = localPos + dir * (t1 - rayBumpEpsilon);

						if (sqrLength(startPoint) < 0.001)
							startPoint = dir;

						startPoint.y *= -1;
						endPoint.y *= -1;

						float2 startUV = octEncode(normalize(startPoint)) * 0.5 + 0.5;
						float2 endUV = octEncode(normalize(endPoint)) * 0.5 + 0.5;
						float2 hitEndUV;

						half3 debugColor = LowResTrace(index, localPos, dir, startUV, endUV, hitEndUV, uv);
						if (!all(debugColor == 0))
							return debugColor;
					}
				}

				return 0;
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

				/*float3 startPointD = localPos;
				startPointD.y *= -1;
				float2 startUVD = octEncode(normalize(startPointD)) * 0.5 + 0.5;
				float2 startCoordD = startUVD * CubeOctanResolution.x;
				float sceneDistD = LOAD_TEXTURE2D_ARRAY(DistMapOctan, startCoordD, index) * ProbeProjectonParam.y;
				if (length(uv - startUVD) <= (1 / CubeOctanResolution.x))
					return length(localPos) / ProbeProjectonParam.y;*/

				for (int i = 0; i < 4; i++)
				{
					float t0 = boundaryTs[i];
					float t1 = boundaryTs[i + 1];
					if (abs(t0 - t1) > degenerateEpsilon)
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
						float distBefore = max(0.0, distanceToIntersectionFix(localPos, dir, dirBefore));

						float traceDist = 0;
						bool behind = false;
						while (traceDist < dist)
						{
							float2 currentUV = startUV + traceDir * min(traceDist + traceStep * 0.5, dist);
							if (all(currentUV >= 0) && all(currentUV <= 1))
							{
								int2 currentCoord = (int2) (currentUV * CubeOctanResolution.xy);
								float sceneDist = SAMPLE_TEXTURE2D_ARRAY_LOD(DistMapOctan, sampler_DistMapOctan, currentUV, index, 0) * ProbeProjectonParam.y;

								float2 afterUV = startUV + traceDir * min(traceDist + traceStep, dist);
								float3 dirAfter = octDecode(currentUV * 2.0 - 1.0);
								dirAfter.y *= -1;
								float distAfter = max(0.0, distanceToIntersectionFix(localPos, dir, dirAfter));

								float maxRayDist = max(distBefore, distAfter);

								half3 finalColor = 0;
								if (length(uv - currentUV) <= (2 / CubeOctanResolution.x))
								{
									float minRayDist = min(distBefore, distAfter);
									//return sceneDist / ProbeProjectonParam.y;
									if (maxRayDist >= sceneDist)
									{
										if (minRayDist < sceneDist)
											return WHITE;
										else
										{
											return RED;
										}
									}
									//else return tempColor;
								}


								distBefore = distAfter;
								traceDist += traceStep;
							}
							else
								break;
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
				
				half3 color = 0;				

				if (GI_DebugMode == 0)
					color += SAMPLE_TEXTURE2D_ARRAY(RadMapOctan, sampler_RadMapOctan, uv, _DebugProbeID);
				else if (GI_DebugMode == 1)
					color += SAMPLE_TEXTURE2D_ARRAY(NormalMapOctan, sampler_NormalMapOctan, uv, _DebugProbeID);
				else if(GI_DebugMode == 2)
					color += SAMPLE_TEXTURE2D_ARRAY(DistMapOctan, sampler_DistMapOctan, uv, _DebugProbeID).rrr;
				else
					color += LOAD_TEXTURE2D_ARRAY(DistMapMinMipOctan, (uv * CubeOctanResolution.zw), _DebugProbeID).rrr;

				//color = half3(uv, 0);
				//color = IndexToCoord(_DebugProbeID);

				//Trace line debug
				half3 resultColor = TraceSingleProbeDebugLow((uint)_DebugProbeID, DebugPos, DebugDir, 0.0f, 1000.0f, uv);

				if(!all(resultColor == 0))
					color = resultColor;
				

				return color;
			}

			half3 frag(v2f i) : SV_Target
			{
				// sample the texture
				//return IndexToCoord(_DebugProbeID);
				return SampleProbeColor_Octan(normalize(i.uv)); //normalize(i.uv);//
			}
			ENDCG
		}
	}
}
