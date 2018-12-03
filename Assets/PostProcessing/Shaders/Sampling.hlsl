#ifndef UNITY_POSTFX_SAMPLING
#define UNITY_POSTFX_SAMPLING

#include "StdLib.hlsl"

#ifdef UNITY_STEREO_INSTANCING_ENABLED
#define ACTUAL_TEXTURE_ARGS(tex, samplerTex) TEXTURE2D_ARRAY_ARGS(tex, samplerTex)
#else
#define ACTUAL_TEXTURE_ARGS(tex, samplerTex) TEXTURE2D_ARGS(tex, samplerTex)
#endif

// Better, temporally stable box filtering
// [Jimenez14] http://goo.gl/eomGso
// . . . . . . .
// . A . B . C .
// . . D . E . .
// . F . G . H .
// . . I . J . .
// . K . L . M .
// . . . . . . .
half4 DownsampleBox13Tap(ACTUAL_TEXTURE_ARGS(tex, samplerTex), float2 uv, float2 texelSize, int sliceIndex = 0)
{
#ifdef UNITY_STEREO_INSTANCING_ENABLED
    half4 A = SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(-1.0, -1.0)), sliceIndex);
    half4 B = SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2( 0.0, -1.0)), sliceIndex);
    half4 C = SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2( 1.0, -1.0)), sliceIndex);
    half4 D = SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(-0.5, -0.5)), sliceIndex);
    half4 E = SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2( 0.5, -0.5)), sliceIndex);
    half4 F = SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(-1.0,  0.0)), sliceIndex);
    half4 G = SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv                                 ), sliceIndex);
    half4 H = SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2( 1.0,  0.0)), sliceIndex);
    half4 I = SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(-0.5,  0.5)), sliceIndex);
    half4 J = SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2( 0.5,  0.5)), sliceIndex);
    half4 K = SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(-1.0,  1.0)), sliceIndex);
    half4 L = SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2( 0.0,  1.0)), sliceIndex);
    half4 M = SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2( 1.0,  1.0)), sliceIndex);
#else
	half4 A = SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(-1.0, -1.0)));
	half4 B = SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(0.0, -1.0)));
	half4 C = SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(1.0, -1.0)));
	half4 D = SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(-0.5, -0.5)));
	half4 E = SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(0.5, -0.5)));
	half4 F = SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(-1.0, 0.0)));
	half4 G = SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv));
	half4 H = SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(1.0, 0.0)));
	half4 I = SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(-0.5, 0.5)));
	half4 J = SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(0.5, 0.5)));
	half4 K = SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(-1.0, 1.0)));
	half4 L = SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(0.0, 1.0)));
	half4 M = SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + texelSize * float2(1.0, 1.0)));
#endif
    half2 div = (1.0 / 4.0) * half2(0.5, 0.125);

    half4 o = (D + E + I + J) * div.x;
    o += (A + B + G + F) * div.y;
    o += (B + C + H + G) * div.y;
    o += (F + G + L + K) * div.y;
    o += (G + H + M + L) * div.y;

    return o;
}

// Standard box filtering
half4 DownsampleBox4Tap(ACTUAL_TEXTURE_ARGS(tex, samplerTex), float2 uv, float2 texelSize, int sliceIndex = 0)
{
    float4 d = texelSize.xyxy * float4(-1.0, -1.0, 1.0, 1.0);

    half4 s;

#ifdef UNITY_STEREO_INSTANCING_ENABLED
	s =  SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.xy), sliceIndex);
	s += SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.zy), sliceIndex);
	s += SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.xw), sliceIndex);
	s += SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.zw), sliceIndex);
#else
    s =  (SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.xy)));
    s += (SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.zy)));
    s += (SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.xw)));
    s += (SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.zw)));
#endif

    return s * (1.0 / 4.0);
}

// 9-tap bilinear upsampler (tent filter)
half4 UpsampleTent(ACTUAL_TEXTURE_ARGS(tex, samplerTex), float2 uv, float2 texelSize, float sampleScale, int sliceIndex = 0)
{
    float4 d = texelSize.xyxy * float4(1.0, 1.0, -1.0, 0.0) * sampleScale;

    half4 s;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
	s =  SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv - d.xy), sliceIndex);
	s += SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv - d.wy), sliceIndex) * 2.0;
	s += SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv - d.zy), sliceIndex);

	s += SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.zw), sliceIndex) * 2.0;
	s += SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv), sliceIndex) * 4.0;
	s += SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.xw), sliceIndex) * 2.0;

	s += SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.zy), sliceIndex);
	s += SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.wy), sliceIndex) * 2.0;
	s += SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.xy), sliceIndex);
#else
    s =  SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv - d.xy));
    s += SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv - d.wy)) * 2.0;
    s += SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv - d.zy));

    s += SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.zw)) * 2.0;
    s += SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv       )) * 4.0;
    s += SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.xw)) * 2.0;

    s += SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.zy));
    s += SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.wy)) * 2.0;
    s += SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.xy));
#endif

    return s * (1.0 / 16.0);
}

// Standard box filtering
half4 UpsampleBox(ACTUAL_TEXTURE_ARGS(tex, samplerTex), float2 uv, float2 texelSize, float sampleScale, int sliceIndex = 0)
{
    float4 d = texelSize.xyxy * float4(-1.0, -1.0, 1.0, 1.0) * (sampleScale * 0.5);

    half4 s;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
    s =  SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.xy), sliceIndex);
    s += SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.zy), sliceIndex);
    s += SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.xw), sliceIndex);
    s += SAMPLE_TEXTURE2D_ARRAY(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.zw), sliceIndex);
#else
	s =  (SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.xy)));
	s += (SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.zy)));
	s += (SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.xw)));
	s += (SAMPLE_TEXTURE2D(tex, samplerTex, UnityStereoTransformScreenSpaceTex(uv + d.zw)));
#endif

    return s * (1.0 / 4.0);
}

#endif // UNITY_POSTFX_SAMPLING
