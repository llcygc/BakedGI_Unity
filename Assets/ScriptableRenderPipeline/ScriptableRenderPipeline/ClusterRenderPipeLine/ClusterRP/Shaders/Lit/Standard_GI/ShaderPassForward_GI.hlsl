#if SHADERPASS != SHADERPASS_FORWARD
#error SHADERPASS_is_not_correctly_define
#endif

//#include "FragData.hlsl"

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Vertex: Used for Standard and StandardSimpleLighting shaders
VertexOutput Vert(VertexInput v)
{
    VertexOutput o /*= (VertexOutput)0*/;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.uv.xy = TRANSFORM_TEX(v.texcoord, _MainTex);    
#if _DETAIL
    o.uv.zw = TRANSFORM_TEX(v.texcoord, _DetailAlbedoMap);
#else
    o.uv.zw = 0;
#endif

    o.posWS.xyz = TransformObjectToWorld(v.vertex.xyz);

#if	_ENABLE_WIND
	o.posWS.xyz = APPLY_VERTEX_OFFSET(o.posWS.xyz, v.vertex.xyz, v.color, v.normal);
#endif

    o.clipPos = TransformWorldToHClip(o.posWS.xyz);


	o.viewDir = _WorldSpaceCameraPos - o.posWS.xyz;

	v.tangent.w *= -1.0f;
    // initializes o.normal and if _NORMALMAP also o.tangent and o.binormal
    OUTPUT_NORMAL(v, o);

    // We either sample GI from lightmap or SH.
    // Lightmap UV and vertex SH coefficients use the same interpolator ("float2 lightmapUV" for lightmap or "half3 vertexSH" for SH)
    // see DECLARE_LIGHTMAP_OR_SH macro.
    // The following funcions initialize the correct variable with correct data
    OUTPUT_LIGHTMAP_UV(v.lightmapUV, unity_LightmapST, o.lightmapUVOrVertexSH);
    OUTPUT_SH(o.normal.xyz, o.lightmapUVOrVertexSH);

    o.clusterPos = ComputeClusterPos(float4(o.posWS.xyz, 1.0f), Cluster_Matrix_LinearZ); //ComputeNonStereoScreenPos(o.pos);

//#if	_ENABLE_WIND
//	o.clusterPos = v.color;
//#endif

    return o;
}

// Used for Standard shader
half4 Frag(VertexOutput IN) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(IN);

    BRDFData_Direct brdfDataDirect;
    BRDFData_Indirect brdfDataIndirect;
    half3 normalTS;
    InitializeBRDFData(IN.uv, normalTS, brdfDataDirect, brdfDataIndirect);

    half4 color = half4(0, 0, 0, brdfDataDirect.alpha);

#ifdef _EMISSION
    color.rgb += brdfDataIndirect.emission;
#endif

    InputDataSimple inputData;
#ifdef _BRDF_ANISO
    // tangent space basis -> tangent = (1, 0, 0), bitangent = (0, 1, 0) and normal = (0, 0, 1).
    // !!!! SINGLE SIDED GEOMETRY: We have to multiply TangentDir by flipFacing.
    half3 tangentTS = lerp(_BaseTangentDir, UNPACK_NORMAL_FUNC(SAMPLE_TEXTURE2D(_TangentMap, sampler_TangentMap, IN.uv.xy)), _TangentDirStrength) /** float3(IN.FacingSign, 1, 1)*/;
    InitializeInputData(IN, normalTS, tangentTS, inputData);
#else
    InitializeInputData(IN, normalTS, inputData);
#endif

    //brdfDataIndirect.bakedGI = SampleGI(IN.lightmapUVOrVertexSH, inputData.normalWS);
    color.rgb += GlobalIllumination_Trace(brdfDataDirect, inputData.normalWS, IN.tangent, inputData.viewDirectionWS, IN.posWS);

    int sliceIndex = 0;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
    sliceIndex = IN.stereoTargetEyeIndex;
#endif

#ifdef SCREEN_SHADOW
	int2 screenCoord = IN.clipPos.xy;//(IN.screenPos.xy / IN.screenPos.w) * _ScreenSize;
#ifdef _BRDF_ANISO
    color.rgb += SURFACE_LIGHTING_CLUSTER_DIRECT_ANISO_SCREENSHADOW(brdfDataDirect, int3(screenCoord.xy, sliceIndex), inputData.normalWS, inputData.tangentWS, inputData.viewDirectionWS, inputData.positionWS, inputData.clusterId);
#else
    color.rgb += SURFACE_LIGHTING_CLUSTER_DIRECT_SCREENSHADOW(brdfDataDirect, int3(screenCoord.xy, sliceIndex), inputData.normalWS, inputData.viewDirectionWS, inputData.positionWS, inputData.clusterId);
#endif
#else
#ifdef _BRDF_ANISO
    color.rgb += SURFACE_LIGHTING_CLUSTER_DIRECT_ANISO(brdfDataDirect, inputData.normalWS, inputData.tangentWS, inputData.viewDirectionWS, inputData.positionWS, inputData.clusterId);
#else
    //color.rgb += SURFACE_LIGHTING_CLUSTER_DIRECT(brdfDataDirect, inputData.normalWS, inputData.viewDirectionWS, inputData.positionWS, inputData.clusterId);
#endif
#endif

#ifdef _FORWARD_CLUSTER_FOG
    half4 scatteringColor = SAMPLE_TEXTURE3D_LOD(_VolumetricFogTexture, sampler_VolumetricFogTexture, inputData.clusterUV, 0);
    color.rgb = scatteringColor.rgb + color.rgb * scatteringColor.a;
#endif

#ifdef LOD_FADE_CROSSFADE
	LODDitheringTransition(floor(IN.clipPos.xy), unity_LODFade.x);
#endif
    return color;
}
