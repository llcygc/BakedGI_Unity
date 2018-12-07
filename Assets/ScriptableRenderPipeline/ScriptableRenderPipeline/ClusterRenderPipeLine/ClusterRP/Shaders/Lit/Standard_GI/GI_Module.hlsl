
#define SAMPLE_COUNT 64
#define SAMPLE_DIM 8

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
		if (tempCos >= cosTheta)
		{
			index = indices[i];
			cosTheta = tempCos;
		}
	}

	return index;
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

half3 GlobalIllumination_Trace(BRDFData_Direct brdfData, half3 normalWS, half3 tangent, half3 viewDirectionWS, float3 worldPos)
{
	uint4 indices1 = 0;
	uint4 indices2 = 0;

	FetchProbeIndices(worldPos, indices1, indices2);
	uint indices[8] = { indices1.x, indices1.y, indices1.z, indices1.w, indices2.x, indices2.y, indices2.z, indices2.w };
	half3 bitangent = normalize(cross(normalWS, tangent));
	tangent = normalize(cross(normalWS, bitangent));

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

		float3 dir = normalWS * z + tangent * y + bitangent * x;
		uint closestIndex = FetchClosestProbe(worldPos, dir, indices);

		ProbeData pData = ProbeDataBuffer[indices[closestIndex]];
		
		bool hit = false;
		float firstLength = 0;
		float lastLength = 0;
		float hitDist = 0;
		for (int j = 1; j <= 32; j++)
		{
			float3 localPos = (worldPos + dir * j * step) - pData.position;
			float dist = length(localPos);
			float3 sampleCoord = localPos;
			sampleCoord.y *= -1;
			half4 normalDist = SAMPLE_TEXTURECUBE_ARRAY(GI_NormalTexture, sampler_GI_NormalTexture, sampleCoord, closestIndex);
			if (normalDist.w < 1 && dist > normalDist.w * 1000)
			{
				if (dist - normalDist.w * 1000 < 0.7)
				{
					hit = true;
					firstLength = j * 0.5;
					lastLength = (j - 1) * 0.5;
					break;
				}
			}
		}

		half3 hitColor;
		half4 hitNormalDist;
		float3 pos;
		float dist;
		if (lastLength < firstLength && hit)
		{
			for (int j = 0; j < 8; j++)
			{
				float3 localPos = (worldPos + dir * ( firstLength + j * step / 8)) - pData.position;
				float tempdist = length(localPos);
				float3 sampleCoord = localPos;
				sampleCoord.y *= -1;
				half4 normalDist = SAMPLE_TEXTURECUBE_ARRAY(GI_NormalTexture, sampler_GI_NormalTexture, sampleCoord, closestIndex);
				if (tempdist > normalDist.w * 1000)
				{
					count++;
					hitColor = SAMPLE_TEXTURECUBE_ARRAY(GI_ProbeTexture, sampler_GI_ProbeTexture, sampleCoord, closestIndex);;
					hitNormalDist = normalDist;
					dist = tempdist;
					float distSqr = tempdist / 10.0f;
					//pos = worldPos + dir * j * (firstLength - lastLength) / 16;
					color += saturate(dot(dir, normalWS)) * hitColor / ( 2 * PI * distSqr * distSqr);
					break;
				}
			}
			
		}
		//color = dist / 1000;
		//color = IndexToCoord(closestIndex);
	}
	
	return (color / count) * brdfData.diffuse;
}
