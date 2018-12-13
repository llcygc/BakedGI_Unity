#include "GI_Data.hlsl"

TEXTURE2D_ARRAY(RadMapOctan);
TEXTURE2D_ARRAY(DistMapOctan);
TEXTURE2D_ARRAY(NormalMapOctan);

//TRACE_HIT 0
//TRACE_MISS 1
//TRACE_UNKNONW 2
uint TraceSingleProbe(uint index, float3 worldPos, float3 dir, out half3 radColor)
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

    for (int i = 0; i < 4; i++)
    {
        float t0 = boundaryTs[i];
        float t1 = boundaryTs[i + 1];
        if (abs(t0 - t1) >= degenerateEpsilon)
        {
            float3 startPoint = localPos + dir * (t0 + rayBumpEpsilon);
            float3 endPoint = localPos + dir * (t1 + rayBumpEpsilon);

            if (sqrLength(startPoint) < 0.001)
                startPoint = dir;

            float2 startUV = octEncode(normalize(startPoint)) * 0.5 + 0.5;
            float2 endUV = octEncode(normalize(endPoint)) * 0.5 + 0.5;

            float2 startCoord = startUV * CubeOctanResolution.x;
            float2 endCoord = endUV * CubeOctanResolution.x;

            endCoord += sqrLength(startCoord - endCoord) > 0.0001 ? 0.01 : 0.0;
            float2 delta = endCoord - startCoord;
            float2 traceDir = normalize(delta);
            float dist = length(delta);

            float traceStep = dist / max(abs(delta.x), abs(delta.y));

            float2 currentCoord = startCoord;
            float traceDist = 0;
            while (traceDist < dist)
            {
                int2 currentCoord = (int2) floor(startCoord + traceDir * traceDist);
                float sceneDist = LOAD_TEXTURE2D_ARRAY(DistMapOctan, currentCoord, index);

                float2 currentUV = currentCoord / CubeOctanResolution.xy;
                float3 currentDir = octDecode(currentUV * 2.0 - 1.0);

                float currentRayDist = distanceToIntersection(worldPos, dir, currentDir);
            }
        }
    }

    radColor += 0;
    return TRACE_MISS;
}

half3 GlobalIllumination_Trace(BRDFData_Direct brdfData, half3 normalWS, half3 tangent, half3 binormal, half3 viewDirectionWS, float3 worldPos)
{
	uint4 indices1 = 0;
	uint4 indices2 = 0;

	FetchProbeIndices(worldPos, indices1, indices2);
	uint indices[8] = { indices1.x, indices1.y, indices1.z, indices1.w, indices2.x, indices2.y, indices2.z, indices2.w };

	half3 color = 0;

	int count = 0;
	float step = 0.5f;
    for (int i = 0; i < SAMPLE_COUNT; i++)
	{
		float divX = floor(i / SAMPLE_DIM) + 1;
		float divY = i % SAMPLE_DIM + 1;
		float theta = 2 * PI * (divX / (SAMPLE_DIM + 1));
		float phi = 0.5 * PI * (divY / (SAMPLE_DIM + 1));

		float x = sin(phi) * cos(theta);
		float y = sin(phi) * sin(theta);
		float z = cos(phi);

		float3 dir = normalWS * z + binormal * y + tangent * x;
        half3 skyColor;
        
        SortProbes(worldPos, dir, indices);
        
        //TRACE_HIT 0
        //TRACE_MISS 1
        //TRACE_UNKNONW 2
        uint result = TRACE_UNKNONW;
        bool traceComplete = false;
        half3 radColor = 0;
        
        for (uint j = 0; j < 8; j++)
        {
            if (TraceSingleProbe(indices[j], worldPos, dir, radColor) != TRACE_UNKNONW)
                break;
        }

        uint closestIndex = FetchClosestProbe(worldPos, dir, indices);

        ProbeData pData = ProbeDataBuffer[closestIndex];
		
		bool hit = false;
		float firstLength = 0;
		float lastLength = 0;
		float hitDist = 0;
		for (int j = 1; j <= 64; j++)
		{
            float3 localPos = (worldPos + dir * j * step) - pData.position;
            float dist = CubeMapDist(localPos);
			float3 sampleCoord = localPos;
			sampleCoord.y *= -1;

            half probeDist = SAMPLE_TEXTURECUBE_ARRAY(GI_DepthTexture, sampler_GI_DepthTexture, sampleCoord, closestIndex);
            
            if (probeDist < 1 && (dist > probeDist * 1000/* + 0.5*/))
			{
                //if (dist - probeDist * 1000 < step)
				{
					hit = true;
                    firstLength = j * step;
                    lastLength = (j - 1) * step;
					break;
				}
			}
		}

		half3 hitColor;
		half4 hitNormalDist;
		float3 pos;
		float dist;
        if (hit && lastLength < firstLength)
		{
			for (int j = 1; j <= 8; j++)
			{
                float3 localPos = (worldPos + dir * (lastLength + j * step / 8)) - pData.position;
				float tempdist = CubeMapDist(localPos);
				float3 sampleCoord = localPos;
                sampleCoord.y *= -1;
                half probeDist = SAMPLE_TEXTURECUBE_ARRAY(GI_DepthTexture, sampler_GI_DepthTexture, sampleCoord, closestIndex);
                if (tempdist > probeDist * 1000)
				{
					count++;
					hitColor = SAMPLE_TEXTURECUBE_ARRAY(GI_ProbeTexture, sampler_GI_ProbeTexture, sampleCoord, closestIndex);
					float3 hitNormal = SAMPLE_TEXTURECUBE_ARRAY(GI_NormalTexture, sampler_GI_NormalTexture, sampleCoord, closestIndex);
                    hitNormalDist = probeDist;
					dist = tempdist;
                    float distSqr = (lastLength + j * step / 8);
					//pos = worldPos + dir * j * (firstLength - lastLength) / 16;
                    color += saturate(dot(dir, normalWS)) * saturate(dot(-dir, hitNormal)) * hitColor / (2 * PI * distSqr * distSqr);
					//color = probeDist;// lastLength; // half3(0.3, 0.5, 1.0);
					break;
				}
			}
        }

		//color = IndexToCoord(closestIndex);
    }
	
	return (color) * brdfData.diffuse;
}
