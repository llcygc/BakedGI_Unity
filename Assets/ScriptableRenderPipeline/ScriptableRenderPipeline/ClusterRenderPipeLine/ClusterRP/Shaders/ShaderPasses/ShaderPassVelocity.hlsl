#if SHADERPASS != SHADERPASS_VELOCITY
#error SHADERPASS_is_not_correctly_define
#endif

// Available semantic start from TEXCOORD4
struct AttributesPass
{
    float3 previousPositionOS : TEXCOORD4; // Contain previous transform position (in case of skinning for example)
};


void VelocityPositionZBias(VertexOutputVelocity input)
{
#if defined(UNITY_REVERSED_Z)
	input.clipPos.z -= unity_MotionVectorsParams.z * input.clipPos.w;
#else
	input.clipPos.z += unity_MotionVectorsParams.z * input.clipPos.w;
#endif
}

VertexOutputVelocity Vert(VertexInputDepth v,
                        AttributesPass inputPass)
{
    VertexOutputVelocity o = (VertexOutputVelocity)0;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    // normal bias is negative since we want to apply an inset normal offset
    float3 posWorld = TransformObjectToWorld(v.vertex.xyz);

#if	_ENABLE_WIND
	posWorld.xyz = APPLY_VERTEX_OFFSET(posWorld, v.vertex.xyz, v.color, v.normal);
#endif

    float4 clipPos = TransformWorldToHClip(posWorld);


    o.clipPos = clipPos;

#if !defined(TESSELLATION_ON)
    VelocityPositionZBias(o);
#endif

#if defined(USING_STEREO_MATRICES)
    float4x4 nonJitteredViewProjMatrix = _NonJitteredViewProjMatrixStereo[unity_StereoEyeIndex];
    float4x4 prevViewProjMatrix = _PrevViewProjMatrixStereo[unity_StereoEyeIndex];
#else
    float4x4 nonJitteredViewProjMatrix = _NonJitteredViewProjMatrix;
    float4x4 prevViewProjMatrix = _PrevViewProjMatrix;
#endif
    // It is not possible to correctly generate the motion vector for tesselated geometry as tessellation parameters can change
    // from one frame to another (adaptative, lod) + in Unity we only receive information for one non tesselated vertex.
    // So motion vetor will be based on interpolate previous position at vertex level instead.
	float4 positionCS = mul(nonJitteredViewProjMatrix, float4(posWorld, 1.0));
    o.positionCS = positionCS.xyw;

    float4 previousPositionCS = float4(0.0, 0.0, 0.0, 1.0);
    // Note: unity_MotionVectorsParams.y is 0 is forceNoMotion is enabled
    bool forceNoMotion = unity_MotionVectorsParams.y == 0.0;
    if (!forceNoMotion)
    {
        bool hasDeformation = unity_MotionVectorsParams.x > 0.0; // Skin or morph target
                                                                 //Need to apply any vertex animation to the previous worldspace position, if we want it to show up in the velocity buffer
        float3 previousPositionWS = mul(unity_MatrixPreviousM, hasDeformation ? float4(inputPass.previousPositionOS, 1.0) : float4(v.vertex.xyz, 1.0)).xyz;

#if	_ENABLE_WIND
		previousPositionWS = APPLY_VERTEX_OFFSET_LASTFRAME(previousPositionWS, v.vertex.xyz, v.color, v.normal);
#endif

//#if defined(HAVE_VERTEX_MODIFICATION)
//        ApplyVertexModification(inputMesh, normalWS, previousPositionWS, _LastTime);
//#endif

        //Need this since we are using the current position from VertMesh()
        //previousPositionWS = GetCameraRelativePositionWS(previousPositionWS);

        float4 previousPositionCS = mul(prevViewProjMatrix, float4(previousPositionWS, 1.0));
        o.positionCSLastFrame = previousPositionCS.xyw;
    }

    return o;
}

void Frag(VertexOutputVelocity IN,
            out float4 outColor : SV_Target
            )
{
	half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a * _Color.a;

#if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
	alpha = _Color.a;
#endif

#if defined(_ALPHATEST_ON)
	clip(alpha - _Cutoff);
#endif

#ifdef LOD_FADE_CROSSFADE
	LODDitheringTransition(floor(IN.clipPos.xy), unity_LODFade.x);
#endif

    // TODO: How to allow overriden velocity vector from GetSurfaceAndBuiltinData ?
    float2 velocity = CalculateVelocity(float4(IN.positionCS.xy, 0.0, IN.positionCS.z), float4(IN.positionCSLastFrame.xy, 0.0, IN.positionCSLastFrame.z));

    EncodeVelocity(velocity, outColor);

    // Note: unity_MotionVectorsParams.y is 0 is forceNoMotion is enabled
    bool forceNoMotion = unity_MotionVectorsParams.y == 0.0;
    if (forceNoMotion)
        outColor = float4(0.0, 0.0, 0.0, 0.0);
}
