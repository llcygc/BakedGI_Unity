#include "Octahedral.hlsl"

#define SAMPLE_COUNT 64
#define SAMPLE_DIM 8

#define TRACE_HIT 0
#define TRACE_MISS 1
#define TRACE_UNKNONW 2

#define RED half3(1, 0, 0)
#define GREEN half3(0, 1, 0)
#define BLUE half3(0, 0, 1)
#define PURPLE half3(1, 0, 1)
#define YELLOW half3(1, 1, 0)
#define ORANGE half3(1, 0.5, 0)
#define WHITE half3(1, 1, 1)
#define BLACK half3(0, 0, 0)

const float minThickness = 0.03; // meters
const float maxThickness = 0.50; // meters

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
float4 CubeOctanResolution;
float4 ProbeProjectonParam;
CBUFFER_END

#define rayBumpEpsilon 0.005f // meters

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

/** Two-element sort: maybe swaps a and b so that a' = min(a, b), b' = max(a, b). */
void minSwap(inout float a, inout float b) {
	float temp = min(a, b);
	b = max(a, b);
	a = temp;
}


/** Sort the three values in v from least to
greatest using an exchange network (i.e., no branches) */
void sort(inout float3 v) {
	minSwap(v[0], v[1]);
	minSwap(v[1], v[2]);
	minSwap(v[0], v[1]);
}

void SortProbes(float3 worldPos, float3 dir, inout uint indices[8])
{

}

inline float sqrLength(float3 vec)
{
	return dot(vec, vec);
}


inline float sqrLength(float2 vec)
{
	return dot(vec, vec);
}

/** Returns the distance along v from the origin to the intersection
with ray R (which it is assumed to intersect) */
float distanceToIntersection(in float3 origin, in float3 dir, in float3 v) {
	float numer;
	float denom = v.y * dir.z - v.z * dir.y;

	if (abs(denom) > 0.1) {
		numer = origin.y * dir.z - origin.z * dir.y;
	}
	else {
		// We're in the yz plane; use another one
		numer = origin.x * dir.y - origin.y * dir.x;
		denom = v.x * dir.y - v.y * dir.x;
	}

	return numer / denom;
}

float distanceToIntersectionFix(in float3 origin, in float3 dir, in float3 v) {
	float3 e = v;
	float3 f = dir;
	float3 g = origin;

	float3 H = cross(f, g);
	float3 K = cross(f, e);

	float h = length(H);
	float k = length(K);

	float result = h / k;

	if (dot(H, K) <= 0)
		result *= -1;

	return max(0, result);
	
}