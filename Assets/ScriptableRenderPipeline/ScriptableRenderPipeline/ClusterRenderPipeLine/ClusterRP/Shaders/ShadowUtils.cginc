#ifndef SHADOW_UTILS
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#define SHADOW_UTILS

#include "ShadowData.hlsl"
#include "CoreRP/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

float4 EvalShadow_WorldToShadow(ShadowData sd, float3 positionWS, bool perspProj)
{
    if (perspProj)
    {
        positionWS = positionWS - sd.pos;
        float3x3 view = { sd.rot0, sd.rot1, sd.rot2 };
        positionWS = mul(view, positionWS);
    }
    else
    {
        float3x4 view;
        view[0] = float4(sd.rot0, sd.pos.x);
        view[1] = float4(sd.rot1, sd.pos.y);
        view[2] = float4(sd.rot2, sd.pos.z);
        positionWS = mul(view, float4(positionWS, 1.0)).xyz;
    }

    float4x4 proj;
    proj = 0.0;
    proj._m00 = sd.proj[0];
    proj._m11 = sd.proj[1];
    proj._m22 = sd.proj[2];
    proj._m23 = sd.proj[3];
    if (perspProj)
        proj._m32 = -1.0;
    else
        proj._m33 = 1.0;

    return mul(proj, float4(positionWS, 1.0));
}

// function called by spot, point and directional eval routines to calculate shadow coordinates
float3 EvalShadow_GetTexcoords(ShadowData sd, float3 positionWS, out float3 posNDC, bool perspProj)
{
    float4 posCS = EvalShadow_WorldToShadow(sd, positionWS, perspProj);
    posNDC = perspProj ? (posCS.xyz / posCS.w) : posCS.xyz;
	// calc TCs
    posNDC = float3(posNDC.xy * 0.5 + 0.5, posNDC.z);
    float3 posTC = posNDC;
    posTC.xy = posNDC.xy * sd.scaleOffset.xy + sd.scaleOffset.zw;

    return posTC;
}

float3 EvalShadow_GetTexcoords(ShadowData sd, float3 positionWS, bool perspProj)
{
    float3 ndc;
    return EvalShadow_GetTexcoords(sd, positionWS, ndc, perspProj);
}

float2 ClampShadowUv(float2 vUv, float4 vShadowMinMaxUv)
{
#if ( D_VALVE_SHADOWING_POINT_LIGHTS )
	{
		vUv.xy = max(vUv.xy, vShadowMinMaxUv.xy);
		vUv.xy = min(vUv.xy, vShadowMinMaxUv.zw);
	}
#endif
	return vUv.xy;
}

float ComputeShadow_Exponential(float3 vPositionWs, float4x4 matWorldToShadow)
{
	float4 vPositionTextureSpace = mul(matWorldToShadow, float4(vPositionWs.xyz, 1.0));
	vPositionTextureSpace.xy /= vPositionTextureSpace.w;
	vPositionTextureSpace.xy = (vPositionTextureSpace.xy + 1.0f) / 2.0f;

	float lightDepth = SAMPLE_TEX2D_LOD(g_tShadowBufferExp, vPositionTextureSpace.xy, 0).x;

	float dist = clamp((lightDepth - vPositionTextureSpace.z), 0.0, 0.5);

	return saturate(exp(dist * 20.0f));
}

float ComputeShadow_Exponential_DepthColor(float3 vPositionWs, ShadowData sd, bool proj)
{
    float3 posTC = EvalShadow_GetTexcoords(sd, vPositionWs, proj);

    float lightDepth = SAMPLE_TEX2D_LOD(g_tShadowBufferExp, posTC.xy, 0).x;

    return lightDepth;
}

float ComputeShadow_Exponential_Static(float3 vPositionWs, ShadowData sd, bool proj)
{
    float3 posTC = EvalShadow_GetTexcoords(sd, vPositionWs, proj);

    float lightDepth = SAMPLE_TEX2D_LOD(_StaticShadowmap, posTC.xy, 0).x;

	if (posTC.z - lightDepth < 0)
		return 0.0f;
	else return 1.0f;
    //return saturate(exp((posTC.z - lightDepth) * 1000.0f));
}

float ComputeShadow_Exponential(float3 vPositionWs, ShadowData sd, bool proj, int cookieIndex = -1)
{
    float3 posTC = EvalShadow_GetTexcoords(sd, vPositionWs, proj);

	float4 lightLeakParam = sd.normalBias;

    float lightDepth = SAMPLE_TEX2D_LOD(g_tShadowBufferExp, posTC.xy, 0).x;
	float linearDist = sd.proj.w / (sd.proj.z + posTC.z) * lightLeakParam.z;
	float far = sd.proj.w / sd.proj.z;
	float near = sd.proj.w / (1 + sd.proj.z);


	float newDist = (linearDist - near) * (far * lightLeakParam.x - near) / (far - near) + near ;
	float depth = saturate(sd.proj.w / newDist - sd.proj.z);
	float dist = clamp((lightDepth - posTC.z), 0, lightLeakParam.x);
	
	float finalMultiplier = lightLeakParam.y;

	return saturate(exp(-finalMultiplier * dist));
}

float CompareDepth(float depth, float lightDepth)
{
    return depth - lightDepth + 0.02 < 0 ? 0 : 1;
}

float SampleShadowmap(float2 coord, float slice)
{
    if(slice < 0.0f)
        return SAMPLE_TEX2D_LOD(_StaticShadowmap, coord, 0).x;
    else
        return SAMPLE_TEX2D_LOD(g_tShadowBufferExp, coord, 0).x;
}

float Compute_Light_Attenuation(float3 vPositionWs, int shadowIndex, int cookieIndex, bool proj)
{
	float shadowAtten = 1.0;
	float cookieAtten = 1.0;

	if (shadowIndex >= 0 || cookieIndex >= 0)
	{
		float3 posNDC;
		ShadowData sd = _ShadowDatasExp[shadowIndex];
		float3 posTC = EvalShadow_GetTexcoords(sd, vPositionWs, posNDC, proj);
		if (shadowIndex >= 0)
		{
			float lightDepth = SampleShadowmap(posTC.xy, sd.slice);//SAMPLE_TEX2D_LOD(g_tShadowBufferExp, posTC.xy, 0).x;
			float dist = posTC.z - lightDepth;
			float finalMultiplier = 500.0f;// lerp(300.0f, 30.0f, posTC.z - lightDepth);
			shadowAtten = saturate(exp(finalMultiplier * dist));
		}

		if (cookieIndex >= 0)
		{
#ifdef SHADER_STAGE_COMPUTE
			cookieAtten = SAMPLE_TEXTURE2D_ARRAY_LOD(_CookieTextures, sampler_CookieTextures, posNDC.xy, cookieIndex, 0).r;
#elif SHADER_STAGE_FRAGMENT
			cookieAtten = SAMPLE_TEXTURE2D_ARRAY(_CookieTextures, sampler_CookieTextures, posNDC.xy, cookieIndex).r;
#endif
		}
	}

	return shadowAtten * cookieAtten;
}

int Compute_Cascade_Index(float3 vPositionWs, int shadowDataIdx)
{
	float4 dirShadowSplitSpheres[4];
	uint offset = _ShadowDatasExp[shadowDataIdx + 1].payloadOffset;
	dirShadowSplitSpheres[0] = asfloat(_ShadowPayloads[offset + 0]);
	dirShadowSplitSpheres[1] = asfloat(_ShadowPayloads[offset + 1]);
	dirShadowSplitSpheres[2] = asfloat(_ShadowPayloads[offset + 2]);
	dirShadowSplitSpheres[3] = asfloat(_ShadowPayloads[offset + 3]);

	float3 fromCenter0 = vPositionWs.xyz - dirShadowSplitSpheres[0].xyz;
	float3 fromCenter1 = vPositionWs.xyz - dirShadowSplitSpheres[1].xyz;
	float3 fromCenter2 = vPositionWs.xyz - dirShadowSplitSpheres[2].xyz;
	float3 fromCenter3 = vPositionWs.xyz - dirShadowSplitSpheres[3].xyz;
	float4 distances2 = float4(dot(fromCenter0, fromCenter0), dot(fromCenter1, fromCenter1), dot(fromCenter2, fromCenter2), dot(fromCenter3, fromCenter3));

	float4 dirShadowSplitSphereSqRadii;
	dirShadowSplitSphereSqRadii.x = dirShadowSplitSpheres[0].w;
	dirShadowSplitSphereSqRadii.y = dirShadowSplitSpheres[1].w;
	dirShadowSplitSphereSqRadii.z = dirShadowSplitSpheres[2].w;
	dirShadowSplitSphereSqRadii.w = dirShadowSplitSpheres[3].w;

	float4 weights = float4(distances2 < dirShadowSplitSphereSqRadii);
	weights.yzw = saturate(weights.yzw - weights.xyz);

	int idx = int(4.0 - dot(weights, float4(4.0, 3.0, 2.0, 1.0)));
	//relDistance = distances2[idx] / dirShadowSplitSphereSqRadii[idx];
	return idx <= 3 ? idx : -1;
}

float Compute_Direction_Light_Shadow(float3 vPositionWs, DirectionalLightDataSimple dirLight)
{
    if (dirLight.ShadowOffset.w >= 0)
    {
        int shadowDataIdx = dirLight.ShadowOffset.w;
        int cascadeIndex = Compute_Cascade_Index(vPositionWs, shadowDataIdx);

        if (cascadeIndex < 0)
            return 1.0f;
        else
        {
            ShadowData sd = _ShadowDatasExp[shadowDataIdx + 1 + cascadeIndex];
            if (sd.slice < 0)
                return ComputeShadow_Exponential_Static(vPositionWs, sd, false);
            else
                return ComputeShadow_Exponential(vPositionWs, sd, false);
        }
    }
    else
        return 1.0f;
}

float Compute_Direction_Light_Shadow_Dynamic(float3 vPositionWs, DirectionalLightDataSimple dirLight)
{
    int shadowDataIdx = dirLight.ShadowOffset.w;
    int cascadeIndex = Compute_Cascade_Index(vPositionWs, shadowDataIdx);

    if (cascadeIndex < 0)
        return 1.0f;
    else
    {
        ShadowData sd = _ShadowDatasExp[shadowDataIdx + 1 + cascadeIndex];
        return ComputeShadow_Exponential(vPositionWs, sd, false);
    }
}

float ComputeShadow_PCF_Static(float3 vPositionWs, ShadowData sd, bool proj)
{
	float3 posTC = EvalShadow_GetTexcoords(sd, vPositionWs, proj);

	real fetchesWeights[16];
	real2 fetchesUV[16];
	float4 shadowmapSize = float4(sd.texelSizeRcp.xy, sd.textureSize.xy);
	SampleShadow_ComputeSamples_Tent_7x7(shadowmapSize, posTC.xy, fetchesWeights, fetchesUV);

	real attenuation = 0;
	float depth = 0;
	float offset = 0.01;
	float scale = 300.0f;

	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[0].xy, 0).x;
	attenuation = fetchesWeights[0] * CompareDepth(posTC.z, depth);// saturate(exp((posTC.z - depth + offset) * scale));
	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[1].xy, 0).x;
	attenuation += fetchesWeights[1] * CompareDepth(posTC.z, depth);//  saturate(exp((posTC.z - depth + offset) * scale));
	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[2].xy, 0).x;
	attenuation += fetchesWeights[2] * CompareDepth(posTC.z, depth);//  saturate(exp((posTC.z - depth + offset) * scale));
	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[3].xy, 0).x;
	attenuation += fetchesWeights[3] * CompareDepth(posTC.z, depth);//  saturate(exp((posTC.z - depth + offset) * scale));
	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[4].xy, 0).x;
	attenuation += fetchesWeights[4] * CompareDepth(posTC.z, depth);//  saturate(exp((posTC.z - depth + offset) * scale));
	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[5].xy, 0).x;
	attenuation += fetchesWeights[5] * CompareDepth(posTC.z, depth);//  saturate(exp((posTC.z - depth + offset) * scale));
	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[6].xy, 0).x;
	attenuation += fetchesWeights[6] * CompareDepth(posTC.z, depth);//  saturate(exp((posTC.z - depth + offset) * scale));
	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[7].xy, 0).x;
	attenuation += fetchesWeights[7] * CompareDepth(posTC.z, depth);//  saturate(exp((posTC.z - depth + offset) * scale));
	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[8].xy, 0).x;
	attenuation += fetchesWeights[8] * CompareDepth(posTC.z, depth);//  saturate(exp((posTC.z - depth + offset) * scale));
	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[9].xy, 0).x;
	attenuation += fetchesWeights[9] * CompareDepth(posTC.z, depth);//  saturate(exp((posTC.z - depth + offset) * scale));
	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[10].xy, 0).x;
	attenuation += fetchesWeights[10] * CompareDepth(posTC.z, depth);//  saturate(exp((posTC.z - depth + offset) * scale));
	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[11].xy, 0).x;
	attenuation += fetchesWeights[11] * CompareDepth(posTC.z, depth);//  saturate(exp((posTC.z - depth + offset) * scale));
	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[12].xy, 0).x;
	attenuation += fetchesWeights[12] * CompareDepth(posTC.z, depth);//  saturate(exp((posTC.z - depth + offset) * scale));
	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[13].xy, 0).x;
	attenuation += fetchesWeights[13] * CompareDepth(posTC.z, depth);//  saturate(exp((posTC.z - depth + offset) * scale));
	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[14].xy, 0).x;
	attenuation += fetchesWeights[14] * CompareDepth(posTC.z, depth);//  saturate(exp((posTC.z - depth + offset) * scale));
	depth = SAMPLE_TEX2D_LOD(_StaticShadowmap, fetchesUV[15].xy, 0).x;
	attenuation += fetchesWeights[15] * CompareDepth(posTC.z, depth);//  saturate(exp((posTC.z - depth + offset) * scale));

	return VALVE_SAMPLE_SHADOW(g_tShadowBuffer, posTC.xyz);// saturate(exp((posTC.z - lightDepth + 0.01) * 300.0f));
}

float ComputeShadow_PCF(float3 vPositionWs, ShadowData sd, bool proj)
{
	float3 posTC = EvalShadow_GetTexcoords(sd, vPositionWs, proj);

	real fetchesWeights[16];
	real2 fetchesUV[16];
	float4 shadowmapSize = float4(sd.texelSizeRcp.xy, sd.textureSize.xy);
	SampleShadow_ComputeSamples_Tent_7x7(shadowmapSize, posTC.xy, fetchesWeights, fetchesUV);

	real attenuation = 0;
	float depth = 0;
	float offset = 0;
	float scale = 300.0f;

	attenuation = fetchesWeights[0] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[0].xy, posTC.z));
	attenuation += fetchesWeights[1] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[1].xy, posTC.z));
	attenuation += fetchesWeights[2] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[2].xy, posTC.z));
	attenuation += fetchesWeights[3] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[3].xy, posTC.z));
	attenuation += fetchesWeights[4] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[4].xy, posTC.z));
	attenuation += fetchesWeights[5] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[5].xy, posTC.z));
	attenuation += fetchesWeights[6] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[6].xy, posTC.z));
	attenuation += fetchesWeights[7] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[7].xy, posTC.z));
	attenuation += fetchesWeights[8] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[8].xy, posTC.z));
	attenuation += fetchesWeights[9] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[9].xy, posTC.z));
	attenuation += fetchesWeights[10] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[10].xy, posTC.z));
	attenuation += fetchesWeights[11] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[11].xy, posTC.z));
	attenuation += fetchesWeights[12] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[12].xy, posTC.z));
	attenuation += fetchesWeights[13] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[13].xy, posTC.z));
	attenuation += fetchesWeights[14] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[14].xy, posTC.z));
	attenuation += fetchesWeights[15] * VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(fetchesUV[15].xy, posTC.z));

	return attenuation;
}

float Compute_Direction_Light_Shadow_PCF(float3 vPositionWs, DirectionalLightDataSimple dirLight)
{
    int shadowDataIdx = dirLight.ShadowOffset.w;
    int cascadeIndex = Compute_Cascade_Index(vPositionWs, shadowDataIdx);

    if (cascadeIndex < 0)
        return 1.0f;
    else
    {
        ShadowData sd = _ShadowDatasExp[shadowDataIdx + 1 + cascadeIndex];
        if (sd.slice < 0)
            return ComputeShadow_PCF_Static(vPositionWs, sd, false);
        else
            return ComputeShadow_PCF(vPositionWs, sd, false);
    }

}

half3 DebugDirStaticShadowDepthColor(float3 vPositionWs, DirectionalLightDataSimple dirLight)
{

    int shadowDataIdx = dirLight.ShadowOffset.w;
    int cascadeIndex = Compute_Cascade_Index(vPositionWs, shadowDataIdx);
    ShadowData sd = _ShadowDatasExp[shadowDataIdx + 1 + cascadeIndex];
    half3 depthColor = ComputeShadow_Exponential_DepthColor(vPositionWs, sd, false);

    return depthColor;
}

half3 DebugDirStaticShadowPosZ(float3 vPositionWs, DirectionalLightDataSimple dirLight)
{

    int shadowDataIdx = dirLight.ShadowOffset.w;
    int cascadeIndex = Compute_Cascade_Index(vPositionWs, shadowDataIdx);
    ShadowData sd = _ShadowDatasExp[shadowDataIdx + 1 + cascadeIndex];
    
    float3 posTC = EvalShadow_GetTexcoords(sd, vPositionWs, false);

    return posTC.z;
}

half3 DebugDirStaticShadowUV(float3 vPositionWs, DirectionalLightDataSimple dirLight)
{
    
    int shadowDataIdx = dirLight.ShadowOffset.w;
    int cascadeIndex = Compute_Cascade_Index(vPositionWs, shadowDataIdx);
    ShadowData sd = _ShadowDatasExp[shadowDataIdx + 1 + cascadeIndex];
    
    float4 posCS = EvalShadow_WorldToShadow(sd, vPositionWs, false);
    half3 posNDC = (posCS.xyz);
	// calc TCs
    float3 posTC = float3(posNDC.xy * 0.5 + 0.5, posNDC.z);

    if (sd.slice < 0)
        return half3(posTC.xy, 0); //half3(EvalShadow_GetTexcoords(sd, vPositionWs, false).xy, 0);
    else
        return half3(EvalShadow_GetTexcoords(sd, vPositionWs, false).xy, 0);
}

half3 DebugDirStaticShadowUV(float3 vPositionWs, ShadowData sd, bool proj)
{
    return half3(EvalShadow_GetTexcoords(sd, vPositionWs, proj).xy, 0);
}

half3 DebugDirShadowCascade(float3 vPositionWs, DirectionalLightDataSimple dirLight)
{    
    int shadowDataIdx = dirLight.ShadowOffset.w;
    int cascadeIndex = Compute_Cascade_Index(vPositionWs, shadowDataIdx);

    if(cascadeIndex == 0)
        return half3(1, 0, 0);
    else if (cascadeIndex == 1)
        return half3(0, 1, 0);
    else if (cascadeIndex == 2)
        return half3(0, 0, 1);
    else
        return half3(0, 1, 1);
}

float ComputeShadow_PCF_3x3_Gaussian(float3 vPositionWs, float4x4 matWorldToShadow, float4 vShadowMinMaxUv, int2 shadowOffset)
{
	float4 vPositionTextureSpace = mul(float4(vPositionWs.xyz, 1.0), matWorldToShadow);
	vPositionTextureSpace.xyz /= vPositionTextureSpace.w;

	float2 shadowMapCenter = vPositionTextureSpace.xy;

	//if ( ( frac( shadowMapCenter.x ) != shadowMapCenter.x ) || ( frac( shadowMapCenter.y ) != shadowMapCenter.y ) )
	if ((shadowMapCenter.x < 0.0f) || (shadowMapCenter.x > 1.0f) || (shadowMapCenter.y < 0.0f) || (shadowMapCenter.y > 1.0f))
		return 1.0f;

	float objDepth = (1 - vPositionTextureSpace.z);

	float4 v20Taps;
	v20Taps.x = VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(ClampShadowUv(shadowMapCenter.xy + g_vShadow3x3PCFTerms1.xy, vShadowMinMaxUv), objDepth)).x; //  1  1
	v20Taps.y = VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(ClampShadowUv(shadowMapCenter.xy + g_vShadow3x3PCFTerms1.zy, vShadowMinMaxUv), objDepth)).x; // -1  1
	v20Taps.z = VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(ClampShadowUv(shadowMapCenter.xy + g_vShadow3x3PCFTerms1.xw, vShadowMinMaxUv), objDepth)).x; //  1 -1
	v20Taps.w = VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(ClampShadowUv(shadowMapCenter.xy + g_vShadow3x3PCFTerms1.zw, vShadowMinMaxUv), objDepth)).x; // -1 -1
	float flSum = dot(v20Taps.xyzw, float4(0.25, 0.25, 0.25, 0.25));
	if ((flSum == 0.0) || (flSum == 1.0))
		return flSum;
	flSum *= g_vShadow3x3PCFTerms0.x * 4.0;

	float4 v33Taps;
	v33Taps.x = VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(ClampShadowUv(shadowMapCenter.xy + g_vShadow3x3PCFTerms2.xz, vShadowMinMaxUv), objDepth)).x; //  1  0
	v33Taps.y = VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(ClampShadowUv(shadowMapCenter.xy + g_vShadow3x3PCFTerms3.xz, vShadowMinMaxUv), objDepth)).x; // -1  0
	v33Taps.z = VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(ClampShadowUv(shadowMapCenter.xy + g_vShadow3x3PCFTerms3.zy, vShadowMinMaxUv), objDepth)).x; //  0 -1
	v33Taps.w = VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(ClampShadowUv(shadowMapCenter.xy + g_vShadow3x3PCFTerms2.zy, vShadowMinMaxUv), objDepth)).x; //  0  1
	flSum += dot(v33Taps.xyzw, g_vShadow3x3PCFTerms0.yyyy);

	flSum += VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(ClampShadowUv(shadowMapCenter.xy, vShadowMinMaxUv), objDepth)).x * g_vShadow3x3PCFTerms0.z;

	return flSum;
}

float ComputeShadow(float3 vPositionWs, float4x4 matWorldToShadow, float4 vShadowMinMaxUv, uint2 shadowOffsets)
{
	float4 vPositionTextureSpace = mul(float4(vPositionWs.xyz, 1.0), matWorldToShadow);
	vPositionTextureSpace.xyz /= vPositionTextureSpace.w;

	float2 shadowMapCenter = vPositionTextureSpace.xy;

	//if ( ( frac( shadowMapCenter.x ) != shadowMapCenter.x ) || ( frac( shadowMapCenter.y ) != shadowMapCenter.y ) )
	if ((shadowMapCenter.x < 0.0f) || (shadowMapCenter.x > 1.0f) || (shadowMapCenter.y < 0.0f) || (shadowMapCenter.y > 1.0f))
		return 1.0f;

	float objDepth = (1 - vPositionTextureSpace.z);

	float flSum = VALVE_SAMPLE_SHADOW(g_tShadowBuffer, float3(ClampShadowUv(shadowMapCenter.xy, vShadowMinMaxUv), objDepth));

	return flSum;
}

#endif // SHADOW_UTILS
