#ifndef VEGETATION_PBR_INCLUDED
#define VEGETATION_PBR_INCLUDED

//#include "../../Lighting/LightingUtils.hlsl"

#define SPEEDTREE_Y_UP

#ifdef GEOM_TYPE_BRANCH_DETAIL
	#define GEOM_TYPE_BRANCH
#endif

#include "SpeedTreeVertex.cginc"
#include "../Lighting/LightingUtils.hlsl"

#define kDieletricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) // standard dielectric reflectivity coef at incident angle (= 4%)

// Define Input structure

struct Input
{
	half4 color;
	half3 interpolator1;
	#ifdef GEOM_TYPE_BRANCH_DETAIL
		half3 interpolator2;
	#endif
};

struct VertexOutputVegetation
{
    float4 pos : SV_POSITION;
	//UNITY_FOG_COORDS(0)
    Input data : TEXCOORD1;
    half3 normal : TEXCOORD4;
#ifdef _NORMALMAP
    half3 tangent                   : TEXCOORD5;
    half3 binormal                  : TEXCOORD6;
#endif
    half3 viewDir : TEXCOORD7;
	//SHADOW_COORDS(8)
    half4 lightmapUVOrVertexSH : TEXCOORD9;
    float3 posWorld : TEXCOORD10;
    float4 clusterPos : TEXCOORD11;
    float4 screenPos : TEXCOORD12;
    UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

struct VertexOutputVegetationDepth
{
        float2 uv : TEXCOORD0;
        half3 normal : TEXCOORD1;
        float3 posWS : TEXCOORD2;
        float4 clipPos : SV_POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
};
	
// Define uniforms

#define mainTexUV interpolator1.xy
half _Specular;

#ifdef GEOM_TYPE_BRANCH_DETAIL
	#define Detail interpolator2
	sampler2D _DetailTex;
#endif
    
#if defined(GEOM_TYPE_FROND) || defined(GEOM_TYPE_LEAF) || defined(GEOM_TYPE_FACING_LEAF)
	#define SPEEDTREE_ALPHATEST
#endif

#ifdef EFFECT_HUE_VARIATION
	#define HueVariationAmount interpolator1.z
	half4 _HueVariation;
#endif

half4 _Translucency;

void SpeedTreeVert(inout SpeedTreeVB IN, out Input OUT)
{
    OUT = (Input) 0;
	//UNITY_INITIALIZE_OUTPUT(Input, OUT);

	OUT.mainTexUV = IN.texcoord.xy;
	OUT.color = _Color;
	OUT.color.rgb *= IN.color.r; // ambient occlusion factor

	#ifdef EFFECT_HUE_VARIATION
		float hueVariationAmount = frac(unity_ObjectToWorld[0].w + unity_ObjectToWorld[1].w + unity_ObjectToWorld[2].w);
        hueVariationAmount = frac(IN.vertex.x + IN.normal.y + IN.normal.x) * 0.5 - 0.3;
		OUT.HueVariationAmount = saturate(hueVariationAmount * _HueVariation.a);
	#endif

	#ifdef GEOM_TYPE_BRANCH_DETAIL
		// The two types are always in different sub-range of the mesh so no interpolation (between detail and blend) problem.
		OUT.Detail.xy = IN.texcoord2.xy;
		if (IN.color.a == 0) // Blend
			OUT.Detail.z = IN.texcoord2.z;
		else // Detail texture
			OUT.Detail.z = 2.5f; // stay out of Blend's .z range
	#endif

	//OffsetSpeedTreeVertex(IN, unity_LODFade.x);
}

// Fragment processing

#if defined(EFFECT_BUMP) && !defined(LIGHTMAP_ON)
	#define SPEEDTREE_DATA_NORMAL			half3 Normal;
	#define SPEEDTREE_COPY_NORMAL(to, from)	to.Normal = from.Normal;
#else
	#define SPEEDTREE_DATA_NORMAL
	#define SPEEDTREE_COPY_NORMAL(to, from)
#endif

#define SPEEDTREE_COPY_FRAG(to, from)	\
	to.Albedo = from.Albedo;			\
	to.Alpha = from.Alpha;				\
	SPEEDTREE_COPY_NORMAL(to, from)

struct SpeedTreeFragOut
{
	half3 Albedo;
	half Alpha;
	SPEEDTREE_DATA_NORMAL
};

void SpeedTreeFrag(Input IN, out SpeedTreeFragOut OUT)
{
    half4 diffuseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.mainTexUV);

	OUT.Alpha = diffuseColor.a * _Color.a;
	#ifdef SPEEDTREE_ALPHATEST
		clip(OUT.Alpha - _Cutoff);
	#endif	
	
	#ifdef GEOM_TYPE_BRANCH_DETAIL
		half4 detailColor = tex2D(_DetailTex, IN.Detail.xy);
		diffuseColor.rgb = lerp(diffuseColor.rgb, detailColor.rgb, IN.Detail.z < 2.0f ? saturate(IN.Detail.z) : detailColor.a);
	#endif

	#ifdef EFFECT_HUE_VARIATION
		half3 shiftedColor = lerp(diffuseColor.rgb, _HueVariation.rgb, IN.HueVariationAmount);
		half maxBase = max(diffuseColor.r, max(diffuseColor.g, diffuseColor.b));
		half newMaxBase = max(shiftedColor.r, max(shiftedColor.g, shiftedColor.b));
		maxBase /= newMaxBase;
		maxBase = maxBase * 0.5f + 0.5f;
		// preserve vibrance
		shiftedColor.rgb *= maxBase;
        diffuseColor.rgb = saturate(shiftedColor);
	#endif
	
        OUT.Albedo = diffuseColor.rgb *IN.color.rgb;
	#if defined(EFFECT_BUMP) && !defined(LIGHTMAP_ON)
		OUT.Normal = UNPACK_NORMAL_FUNC(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.mainTexUV), 1.0f);
		#ifdef GEOM_TYPE_BRANCH_DETAIL
			half3 detailNormal = UNPACK_NORMAL_FUNC(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.Detail.xy), 1.0f);
			OUT.Normal = lerp(OUT.Normal, detailNormal, IN.Detail.z < 2.0f ? saturate(IN.Detail.z) : detailColor.a);
		#endif
	#endif
	
}

inline void InitializeVegetationBRDFData(VertexOutputVegetation IN, out half3 normalTS, out BRDFData_Direct brdfDataDirect, out BRDFData_Indirect brdfDataIndirect)
{
    SpeedTreeFragOut data;
    SpeedTreeFrag(IN.data, data);


   /* o.specular.xyz = kDieletricSpec.rgb;

    o.albedo = data.Albedo;
    o.alpha = data.Alpha;
    o.metallic = 0;

    o.smoothness = _Specular;
    o.occlusion = 1.0;

#if defined(EFFECT_BUMP)
    o.normalTS = data.Normal;
#else
    o.normalTS = half3(0, 0, 1);
#endif*/

    brdfDataDirect.diffuse = data.Albedo;

    half metallic = 0;
    half3 specular = half3(0.0h, 0.0h, 0.0h);

    half smoothness = _Specular;

#if defined(EFFECT_BUMP)
    normalTS = data.Normal;
#else
    normalTS = half3(0.0h, 0.0h, 1.0h);
#endif
    
    half oneMinusReflectivity = OneMinusReflectivityMetallic(metallic);
    half reflectivity = 1.0 - oneMinusReflectivity;

    brdfDataDirect.specular = kDieletricSpec.rgb;

    brdfDataDirect.occlusion = 1.0f;// specGlossAO.g;;
    brdfDataDirect.alpha = data.Alpha;

    brdfDataIndirect.bakedGI = 0;
    brdfDataIndirect.grazingTerm = saturate(smoothness + reflectivity);
    brdfDataIndirect.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
    brdfDataDirect.roughness = PerceptualRoughnessToRoughness(brdfDataIndirect.perceptualRoughness);
    brdfDataDirect.roughness2 = brdfDataDirect.roughness * brdfDataDirect.roughness;

}

void InitializeVegetationInputData(VertexOutputVegetation IN, half3 normalTS, out InputDataSimple inputData)
{
    inputData.positionWS = IN.posWorld.xyz;

    half3 viewDir = IN.viewDir;

#ifdef _NORMALMAP
    inputData.normalWS = TangentToWorldNormal(normalTS, IN.tangent.xyz, IN.binormal.xyz, IN.normal.xyz);
#else
    inputData.normalWS = normalize(IN.normal);
#endif

    inputData.viewDirectionWS = SafeNormalize(viewDir);
    inputData.clusterId = ComputeClusterID(IN.clusterPos);
}

SurfaceData SpeedTreeFragSetup(VertexOutputVegetation i)
{
        SpeedTreeFragOut data;
        SpeedTreeFrag(i.data, data);
        SurfaceData o = (SurfaceData) 0;

        o.specular.xyz = kDieletricSpec.rgb;
        
        o.albedo = data.Albedo;
        o.alpha = data.Alpha;
        o.metallic = 0;

        o.smoothness = _Specular;
        o.occlusion = 1.0;

#if defined(EFFECT_BUMP)
        o.normalTS = data.Normal;
#else
        o.normalTS = half3(0, 0, 1);
#endif

        return o;
}

inline half4 VertexGIForward(SpeedTreeVB v, float3 posWorld, half3 normalWorld)
{
	half4 ambientOrLightmapUV = 0;
#if UNITY_SHOULD_SAMPLE_SH
#ifdef VERTEXLIGHT_ON
	// Approximated illumination from non-important point lights
	ambientOrLightmapUV.rgb = Shade4PointLights(
		unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
		unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb, unity_LightColor[3].rgb,
		unity_4LightAtten0, posWorld, normalWorld);
#endif

	ambientOrLightmapUV.rgb = ShadeSHPerVertex(normalWorld, ambientOrLightmapUV.rgb);
#endif

#ifdef DYNAMICLIGHTMAP_ON
	ambientOrLightmapUV.zw = v.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif

	return ambientOrLightmapUV;
}

#endif // SPEEDTREE_COMMON_INCLUDED
