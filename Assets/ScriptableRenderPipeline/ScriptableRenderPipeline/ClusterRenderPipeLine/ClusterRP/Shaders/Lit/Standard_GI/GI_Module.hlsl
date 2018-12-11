
#define SAMPLE_COUNT 64
#define SAMPLE_DIM 8

#define TRACE_HIT 0
#define TRACE_MISS 1
#define TRACE_UNKNONW 2

struct ProbeData
{
	float3 position;
	float4 scaleOffset;
	float4 probeID;
};

CBUFFER_START(ProbeInfo)
float4 ProbeDimenson;
float4 ProbeMin;
float4 ProbeMax;
float4x4 ProbeProjMatrix;
float4x4 ProbeRotationMatrix[6];
CBUFFER_END

TEXTURECUBE_ARRAY(GI_ProbeTexture);
SAMPLER(sampler_GI_ProbeTexture);

TEXTURECUBE_ARRAY(GI_NormalTexture);
SAMPLER(sampler_GI_NormalTexture);

TEXTURECUBE_ARRAY(GI_DepthTexture);
SAMPLER(sampler_GI_DepthTexture);

StructuredBuffer<ProbeData> ProbeDataBuffer;

int CoordToIndex(int3 coord)
{
	return coord.x * ProbeDimenson.y * ProbeDimenson.z + coord.y * ProbeDimenson.z + coord.z;
}

half3 IndexToCoord(int index)
{
	half3 coord = 0;
	coord.z = index % ((int)ProbeDimenson.z);
	coord.y = floor(index % ((int)ProbeDimenson.z * (int)ProbeDimenson.y) / ProbeDimenson.z);
	coord.x = floor(index / ((int)ProbeDimenson.z * (int)ProbeDimenson.y));

	return coord / ProbeDimenson;
}

void FetchProbeIndices(float3 worldPos, out int4 indices1, out int4 indices2)
{
	float3 totalDist = ProbeMax.xyz - ProbeMin.xyz;
	float3 currentDist = worldPos - ProbeMin.xyz;
	float3 coord = saturate(currentDist / totalDist);

    coord = floor(coord * (ProbeDimenson - 1));

    if (coord.x == ProbeDimenson.x - 1)
        coord.x -= 1;
    if (coord.y == ProbeDimenson.y - 1)
        coord.y -= 1;
    if (coord.z == ProbeDimenson.z - 1)
        coord.z -= 1;

	indices1.x = CoordToIndex(coord);
	indices1.y = CoordToIndex(coord + int3(1, 0, 0));
	indices1.z = CoordToIndex(coord + int3(0, 1, 0));
	indices1.w = CoordToIndex(coord + int3(1, 1, 0));

	indices2.x = CoordToIndex(coord + int3(0, 0, 1));
	indices2.y = CoordToIndex(coord + int3(1, 0, 1));
	indices2.z = CoordToIndex(coord + int3(0, 1, 1));
	indices2.w = CoordToIndex(coord + int3(1, 1, 1));
}

half3 FetchProbeIndices(float3 worldPos)
{
	float3 totalDist = ProbeMax.xyz - ProbeMin.xyz;
	float3 currentDist = worldPos - ProbeMin.xyz;
	float3 coord = saturate(currentDist / totalDist);

	coord = floor(coord * (ProbeDimenson - 1));
    if (coord.x == ProbeDimenson.x - 1)
        coord.x -= 1;
    if (coord.y == ProbeDimenson.y - 1)
        coord.y -= 1;
    if (coord.z == ProbeDimenson.z - 1)
        coord.z -= 1;
	
    return coord / ProbeDimenson;
}


uint FetchClosestProbe(float3 worldPos, float3 dir, uint indices[8])
{
	float cosTheta = 0;
	uint index = 0;
	for (uint i = 0; i < 8; i++)
	{
		ProbeData tempData = ProbeDataBuffer[indices[i]];
		float3 probePos = tempData.position;
		float3 localPos = probePos - worldPos;
		float3 probeDir = normalize(localPos);
		float tempCos = dot(probeDir, dir);
		if (tempCos > cosTheta)
		{
			index = indices[i];
			cosTheta = tempCos;
		}
	}

	return index;
}

float CubeMapDist(float3 localPos)
{
	float dist;
	float3 dir = normalize(localPos);

	if (abs(dir.z) >= abs(dir.x) && abs(dir.z) >= abs(dir.y))
	{
		dist = abs(localPos.z);
	}
	else if (abs(dir.y) >= abs(dir.x))
	{
		dist = abs(localPos.y);
	}
	else
	{
		dist = abs(localPos.x);
	}

	return dist;
}

float3 DebugCloset(float3 worldPos, float3 dir, uint indices[8])
{
	float cosTheta = 0;
	uint index = 0;
	uint i = 0;
	ProbeData tempData = ProbeDataBuffer[indices[i]];
	float3 probePos = tempData.position;
	float3 localPos = probePos - worldPos;
	float3 probeDir = normalize(localPos);
	float tempCos = abs(dot(probeDir, dir));
	return tempCos;
}

void SortProbes(float3 worldPos, float3 dir, inout uint indices[8])
{

}

void TraceSingleProbe(uint index, float3 worldPos, float3 dir, out half3 radColor)
{    
    ProbeData pData = ProbeDataBuffer[closestIndex];

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
        
        SortProbes(dir, indices[8]);
        
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
