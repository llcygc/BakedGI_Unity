#include "GI_Data.hlsl"

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
