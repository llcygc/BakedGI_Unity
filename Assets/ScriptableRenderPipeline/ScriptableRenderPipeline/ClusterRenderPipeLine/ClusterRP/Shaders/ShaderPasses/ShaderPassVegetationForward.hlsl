#if SHADERPASS != SHADERPASS_FORWARD_VEGETATION
#error SHADERPASS_is_not_correctly_define
#endif

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Vertex: Used for Standard and StandardSimpleLighting shaders
VertexOutputVegetation Vert(SpeedTreeVB v)
{
    VertexOutputVegetation o = (VertexOutputVegetation)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    SpeedTreeVert(v, o.data);

    OUTPUT_NORMAL(v, o);
    o.posWorld = TransformObjectToWorld(v.vertex.xyz);//mul(unity_ObjectToWorld, v.vertex).xyz;
    o.pos = TransformWorldToHClip(o.posWorld.xyz);
    
    o.viewDir = SafeNormalize(_WorldSpaceCameraPos - o.posWorld.xyz);
    OUTPUT_LIGHTMAP_UV(v.lightmapUV, unity_LightmapST, o.lightmapUVOrVertexSH);
    OUTPUT_SH(o.normal, o.lightmapUVOrVertexSH);

    half3 lodPosition;
#if defined(GEOM_TYPE_BRANCH) || defined(GEOM_TYPE_FROND)
    lodPosition = v.texcoord1.xyz;
#elif defined(GEOM_TYPE_LEAF)
    lodPosition = float3(v.texcoord1.w, v.texcoord3.x, v.texcoord3.y);
#endif

    //o.lightmapUVOrVertexSH.rgb = v.vertex.xyz;

    o.clusterPos = ComputeClusterPos(float4(o.posWorld, 1.0f), Cluster_Matrix_LinearZ);
    o.screenPos = o.pos * 0.5f;
    o.screenPos.xy = float2(o.screenPos.x, o.screenPos.y * _ProjectionParams.x) + o.screenPos.w;
    o.screenPos.zw = o.pos.zw;
    //UNITY_TRANSFER_FOG(o, o.pos);
    return o;
}

// Used for Standard shader
half4 Frag(VertexOutputVegetation IN, float facing : VFACE) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(IN);

    BRDFData_Direct brdfDataDirect;
    BRDFData_Indirect brdfDataIndirect;
    half3 normalTS;
    InitializeVegetationBRDFData(IN, normalTS, brdfDataDirect, brdfDataIndirect);
    half4 color = half4(0, 0, 0, brdfDataDirect.alpha);

    InputDataSimple inputData;
    InitializeVegetationInputData(IN, normalTS, inputData);

    brdfDataIndirect.bakedGI = SampleGI(IN.lightmapUVOrVertexSH, inputData.normalWS);
    color.rgb += GlobalIllumination(brdfDataIndirect, brdfDataDirect, inputData.normalWS, inputData.viewDirectionWS);

    int sliceIndex = 0;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
    sliceIndex = IN.stereoTargetEyeIndex;
#endif

    int2 screenCoord = (IN.screenPos.xy / IN.screenPos.w) * _ScreenSize;

#ifdef SCREEN_SHADOW
    color.rgb += SURFACE_LIGHTING_CLUSTER_DIRECT_SCREENSHADOW(brdfDataDirect, int3(screenCoord.xy, sliceIndex), inputData.normalWS, inputData.viewDirectionWS, inputData.positionWS, inputData.clusterId);
#else
    color.rgb += SURFACE_LIGHTING_CLUSTER_DIRECT(brdfDataDirect, inputData.normalWS, inputData.viewDirectionWS, inputData.positionWS, inputData.clusterId);
#endif 

    return color;
}
