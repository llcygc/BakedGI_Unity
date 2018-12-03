#ifndef BSDF_INCLUDED
#define BSDF_INCLUDED

#include "CoreRP/ShaderLibrary/BSDF.hlsl"

half3 BRDF_PBS_Direct_Term(half3 diffColor, half3 specColor, half nv, half3 normal, half3 viewDir, half3 lightDir, half roughness, half roughness2)
{
    
#ifndef _SPECULARHIGHLIGHTS_OFF
    half3 halfDir = SafeNormalize(lightDir + viewDir);

    half NoH = saturate(dot(normal, halfDir));
    half LoH = saturate(dot(lightDir, halfDir));    
    half NdotL = saturate(dot(normal, lightDir));

    // GGX Distribution multiplied by combined approximation of Visibility and Fresnel
    // See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
    // https://community.arm.com/events/1155
    half d = NoH * NoH * (roughness2 - 1.h) + 1.00001h;

    half LoH2 = LoH * LoH;
    half specularTerm = roughness2 / ((d * d) * max(0.1h, LoH2) * (roughness + 0.5h) * 4);

    // on mobiles (where half actually means something) denominator have risk of overflow
    // clamp below was added specifically to "fix" that, but dx compiler (we convert bytecode to metal/gles)
    // sees that specularTerm have only non-negative terms, so it skips max(0,..) in clamp (leaving only min(100,...))
#if defined (SHADER_API_MOBILE)
    specularTerm = specularTerm - HALF_MIN;
    specularTerm = clamp(specularTerm, 0.0, 100.0); // Prevent FP16 overflow on mobiles
#endif

    half3 color = specularTerm * specColor + diffColor;
    return color * NdotL;
#else
    return brdfData.diffuse;
#endif
}

void BSDF_Direct_Term(half3 normal, half3 viewDir, half3 lightDir, half roughness, half roughness2, out float diffTerm, out float specTerm)
{

#ifndef _SPECULARHIGHLIGHTS_OFF
    half3 halfDir = SafeNormalize(lightDir + viewDir);

    half NoH = saturate(dot(normal, halfDir));
    half LoH = saturate(dot(lightDir, halfDir));
    half NdotL = saturate(dot(normal, lightDir));

    // GGX Distribution multiplied by combined approximation of Visibility and Fresnel
    // See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
    // https://community.arm.com/events/1155
    half d = NoH * NoH * (roughness2 - 1.h) + 1.00001h;

    half LoH2 = LoH * LoH;
    half specularTerm = roughness2 / ((d * d) * max(0.1h, LoH2) * (roughness + 0.5h) * 4);

    // on mobiles (where half actually means something) denominator have risk of overflow
    // clamp below was added specifically to "fix" that, but dx compiler (we convert bytecode to metal/gles)
    // sees that specularTerm have only non-negative terms, so it skips max(0,..) in clamp (leaving only min(100,...))
#if defined (SHADER_API_MOBILE)
    specularTerm = specularTerm - HALF_MIN;
    specularTerm = clamp(specularTerm, 0.0, 100.0); // Prevent FP16 overflow on mobiles
#endif
    specTerm = specularTerm * NdotL;
    diffTerm = NdotL;
#else
    specTerm = 0;
    diffTerm = NdotL;// brdfData.diffuse;
#endif

}

half BRDF_PBS_Direct_SpecTerm(half nv, half3 normal, half3 viewDir, half3 lightDir, half roughness)
{
    half3 halfDir = SafeNormalize(lightDir + viewDir);
    half nh = saturate(dot(normal, halfDir));
    half lh = saturate(dot(lightDir, halfDir));

	// GGX Distribution multiplied by combined approximation of Visibility and Fresnel
	// See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
	// https://community.arm.com/events/1155
    half a = roughness;
    half a2 = a * a;

    half d = nh * nh * (a2 - 1.h) + 1.00001h;
#ifdef UNITY_COLORSPACE_GAMMA
	// Tighter approximation for Gamma only rendering mode!
	// DVF = sqrt(DVF);
	// DVF = (a * sqrt(.25)) / (max(sqrt(0.1), lh)*sqrt(roughness + .5) * d);
	half specularTerm = a / (max(0.32h, lh) * (1.5h + roughness) * d);
#else
    half specularTerm = a2 / (max(0.1h, lh * lh) * (roughness + 0.5h) * (d * d) * 4);
#endif

	// on mobiles (where half actually means something) denominator have risk of overflow
	// clamp below was added specifically to "fix" that, but dx compiler (we convert bytecode to metal/gles)
	// sees that specularTerm have only non-negative terms, so it skips max(0,..) in clamp (leaving only min(100,...))
#if defined (SHADER_API_MOBILE)
	specularTerm = specularTerm - 1e-4h;
#endif

#if defined (SHADER_API_MOBILE)
	specularTerm = clamp(specularTerm, 0.0, 100.0); // Prevent FP16 overflow on mobiles
#endif
#if defined(_SPECULARHIGHLIGHTS_OFF)
	specularTerm = 0.0;
#endif

    return specularTerm; // half4(color, 1);
}

void BSDF_Aniso_Direct_Term(half3 normal, half3 tangent, half3 viewDir, half3 lightDir, half roughness, half roughness2, out float diffTerm, out float specTerm)
{
#ifndef _SPECULARHIGHLIGHTS_OFF
    half3 halfDir = SafeNormalize(lightDir + viewDir);

    half NoH = saturate(dot(normal, halfDir));
    half LoH = saturate(dot(lightDir, halfDir));
    half NdotL = saturate(dot(normal, lightDir));
    half NdotV = saturate(dot(normal, viewDir));

    float3 X = tangent.xyz;
    float3 Y = cross(normal, tangent);

    float roughnessX = RoughnessToPerceptualRoughness(roughness);
    float roughnessY = 1.0f;

    half roughnessAniso = max(roughnessX, roughnessY);
    half perceptualRoughness = roughnessAniso;// SmoothnessToPerceptualRoughness(1 - roughnessAniso); //(smoothness);
    roughnessAniso = PerceptualRoughnessToRoughness(perceptualRoughness);

    float mx = roughnessX * roughnessX;
    float my = roughnessY * roughnessY;
    float XdotH = dot(X, halfDir);
    float YdotH = dot(Y, halfDir);
    float d = XdotH * XdotH / (mx * mx) + YdotH * YdotH / (my * my) + NoH * NoH;

    // GGX Distribution multiplied by combined approximation of Visibility and Fresnel
    // See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
    // https://community.arm.com/events/1155
    //half d = NoH * NoH * (roughness2 - 1.h) + 1.00001h;

    half LoH2 = LoH * LoH;

    half D = 1 / (PI * mx*my * d*d); // Normalization is: 1/(pi*mx*my)
    half V = V_SmithJointGGX(NdotL, NdotV, roughnessAniso); // <- As V is based on NdotL it is prone to artifacts so it is currently skipped.

    specTerm = V * D * PI; // Torrance-Sparrow model
    // specularTerm * nl can be NaN on Metal in some cases, use max() to make sure it's a sane value
    specTerm = max(0, specTerm * NdotL);
	diffTerm = NdotL;// DisneyDiffuse(NdotV, NdotL, LoH, perceptualRoughness) * NdotL;
#else
    specTerm = 0;
    diffTerm = NdotL;// brdfData.diffuse;
#endif

}

#endif // TILED_SHADING_CORE_INCLUDED
