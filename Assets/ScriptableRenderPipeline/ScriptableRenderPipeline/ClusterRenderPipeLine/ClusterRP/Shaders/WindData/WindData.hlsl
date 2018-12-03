#ifndef WIND_DATA_CLUSTER_INCLUDE
#define WIND_DATA_CLUSTER_INCLUDE

CBUFFER_START(WindData_PerTree)
float _TrunkStiffness;
float _BranchStiffness;
float _BranchFrequency;
float _BranchAmplify;
float _DetailFrequency;
float _DetailAmplify;
CBUFFER_END

CBUFFER_START(WindDatas)
float4 _WindDirectionStrength;
float4 _WindDirectionStrengthLastFrame;
CBUFFER_END

#ifdef _ENABLE_WIND_HIERARCHY
#define APPLY_VERTEX_OFFSET(posWorld, vertex, windColor, normal) ApplyVertexOffset(posWorld, vertex, windColor, normal, _WindDirectionStrength)
#define APPLY_VERTEX_OFFSET_LASTFRAME(posWorld, vertex, windColor, normal) ApplyVertexOffset(posWorld, vertex, windColor, normal, _WindDirectionStrengthLastFrame)
#elif defined(_ENABLE_WIND_PROCEDURAL)
#define APPLY_VERTEX_OFFSET(posWorld, vertex, windColor, normal) ApplyVertexOffset_Procedrual(posWorld, vertex, windColor, normal, _WindDirectionStrength)
#define APPLY_VERTEX_OFFSET_LASTFRAME(posWorld, vertex, windColor, normal) ApplyVertexOffset_Procedrual(posWorld, vertex, windColor, normal, _WindDirectionStrengthLastFrame)
#elif defined(_ENABLE_WIND_SINGLE)
#define APPLY_VERTEX_OFFSET(posWorld, vertex, windColor, normal) ApplyVertexOffset_Single(posWorld, vertex, windColor, normal, _WindDirectionStrength)
#define APPLY_VERTEX_OFFSET_LASTFRAME(posWorld, vertex, windColor, normal) ApplyVertexOffset_Single(posWorld, vertex, windColor, normal, _WindDirectionStrengthLastFrame)
#else
#define APPLY_VERTEX_OFFSET(posWorld, vertex, windColor, normal) 0
#define APPLY_VERTEX_OFFSET_LASTFRAME(posWorld, vertex, windColor, normal) 0
#endif

float3 ApplyVertexOffset(float3 posWorld, float3 vertex, half4 windColor, float3 normal, float4 windDirStrength)
{
	float3 objectPos = float3(UNITY_MATRIX_M[0].w, UNITY_MATRIX_M[1].w, UNITY_MATRIX_M[2].w);
	float dist = length(posWorld - objectPos);
	float3 windOffset = windDirStrength.xyz;
	float bendFactor = windColor.a * windColor.a;
	//bendFactor *= bendFactor;
	float branchBendFactor = windColor.b * windColor.b;

	float fObjPhase = dot(objectPos.xyz, 1);
	float windPhase = (2 * sin(1 * (fObjPhase + _Time.y))) + 1;

	float3 offset = posWorld + windPhase * windOffset * bendFactor * _TrunkStiffness + windPhase * windOffset * branchBendFactor * _BranchStiffness;
	//// Phases (object, vertex, branch)
	//// Edge (xy) and branch bending (z)

	float fBranchPhase = windColor.g + fObjPhase;

	float fVtxPhase = dot(vertex.xyz, 1) + fBranchPhase;
	//// x is used for edges; y is used for branches
	float2 vWavesIn = _Time.x * windDirStrength.w * float2(_DetailFrequency, _BranchFrequency) + float2(fVtxPhase, fBranchPhase);
	//// 1.975, 0.793, 0.375, 0.193 are good frequencies
	float4 vWaves = (frac(vWavesIn.xxyy *
		float4(1.975, 0.793, 0.375, 0.193)) *
		2.0 - 1.0) /** _Speed * _DetailFrequency*/;
	
	vWaves = sin(vWaves * PI);

	//offset.xyz += float3(0, 1, 0) * vWaves.z * branchBendFactor * _BrachAmplify;

	float2 vWavesSum = vWaves.xz + vWaves.yw;

	float2 multi = windColor.r * normal.xz * _DetailAmplify;
	offset.xyz += vWavesSum.xyx * float3(multi.x, multi.y, 0) + vWavesSum.yyy * half3(0, 1, 0) * branchBendFactor * _BranchAmplify;

	offset = normalize(offset - objectPos) * dist + objectPos;

	return offset;
}

float3 ApplyVertexOffset_Procedrual(float3 posWorld, float3 vertex, half4 windColor, float3 normal, float4 windDirStrength)
{
	float3 objectPos = float3(UNITY_MATRIX_M[0].w, UNITY_MATRIX_M[1].w, UNITY_MATRIX_M[2].w);
	float3 windOffset = windDirStrength.xyz;

	float fObjPhase = dot(objectPos.xyz, 1);
	float windPhase = (2 * sin(1 * (fObjPhase + _Time.y))) + 1;
	float3 offset = posWorld;

	float fDetailPhase = dot(vertex, 1);
	float fVtxPhase = dot(vertex.xyz, fDetailPhase + fObjPhase);
	//// x is used for edges; y is used for branches
	float3 vWavesIn = _Time.x * windDirStrength.w * _DetailFrequency + float3(fVtxPhase + vertex.x, fVtxPhase , fVtxPhase + vertex.z);
	//// 1.975, 0.793, 0.375, 0.193 are good frequencies
	float3 vWaves = (frac(vWavesIn.xyz *
		float3(1.975, 0.793, 0.375)) *
		2.0 - 1.0);

	vWaves = sin(vWaves * PI);

	offset.xyz += vWaves.xyz * _DetailAmplify + windOffset * _TrunkStiffness * windPhase;

	return offset;
}

float3 ApplyVertexOffset_Single(float3 posWorld, float3 vertex, half4 windColor, float3 normal, float4 windDirStrength)
{
	float3 objectPos = float3(UNITY_MATRIX_M[0].w, UNITY_MATRIX_M[1].w, UNITY_MATRIX_M[2].w);
	float dist = length(posWorld - objectPos);
	float3 windOffset = windDirStrength.xyz;
	float bendFactor = windColor.b;
	bendFactor *= bendFactor;

	float fObjPhase = dot(objectPos.xyz, 1);
	float windPhase = (2 * sin(1 * (fObjPhase + _Time.y))) + 1;

	float3 offset = posWorld + windOffset * windPhase * bendFactor * _BranchStiffness;
	//// Phases (object, vertex, branch)
	//// Edge (xy) and branch bending (z)

	float fBranchPhase = windColor.g + fObjPhase;

	float fVtxPhase = dot(vertex.xyz, 1) + fBranchPhase;
	//// x is used for edges; y is used for branches
	float2 vWavesIn = _Time.x * windDirStrength.w * float2(_DetailFrequency, _BranchFrequency) + float2(fVtxPhase, fBranchPhase);
	//// 1.975, 0.793, 0.375, 0.193 are good frequencies
	float4 vWaves = (frac(vWavesIn.xxyy *
		float4(1.975, 0.793, 0.375, 0.193)) *
		2.0 - 1.0) /** _Speed * _DetailFrequency*/;

	vWaves = sin(vWaves * PI);
	float2 vWavesSum = vWaves.xz + vWaves.yw;

	float2 multi = (windColor.r / 20.0f) * normal.xz * _DetailAmplify;
	offset.xyz += vWavesSum.xyx * float3(multi.x, 0, multi.y) + vWavesSum.yyy * half3(0, 1, 0) * bendFactor * _BranchAmplify;

	offset = normalize(offset - objectPos) * dist + objectPos;

	return offset;
}

#endif