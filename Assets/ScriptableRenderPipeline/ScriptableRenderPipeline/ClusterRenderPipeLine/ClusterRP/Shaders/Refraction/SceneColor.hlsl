#ifndef REFRACTION_INCLUDED
#define REFRACTION_INCLUDED

#ifdef UNITY_STEREO_INSTANCING_ENABLED
UNITY_DECLARE_TEX2DARRAY(MainColorBufferStereo);
#else
UNITY_DECLARE_TEX2D(MainColorBuffer);
#endif

inline half4 GetSceneColor(float2 uv)
{
#ifdef UNITY_STEREO_INSTANCING_ENABLED 
    return UNITY_SAMPLE_TEX2DARRAY(MainColorBufferStereo, float3(uv, (float)unity_StereoEyeIndex));
#else 
    return UNITY_SAMPLE_TEX2D(MainColorBuffer, uv);
#endif 
}

#endif // UNITY_MATERIAL_INCLUDED
