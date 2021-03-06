using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph
{
    //-----------------------------------------------------------------------------
    // structure definition
    //-----------------------------------------------------------------------------

    // Caution: Order is important and is use for optimization in light loop
    [GenerateHLSL]
    public enum GPULightType
    {
        Directional,
        Point,
        Spot,
        ProjectorPyramid,
        ProjectorBox,

        // AreaLight
        Line, // Keep Line lights before Rectangle. This is needed because of a compiler bug (see LightLoop.hlsl)
        Rectangle,
        // Currently not supported in real time (just use for reference)
        // Sphere,
        // Disk,
    };

    // This is use to distinguish between reflection and refraction probe in LightLoop
    [GenerateHLSL]
    public enum GPUImageBasedLightingType
    {
        Reflection,
        Refraction
    };

    [GenerateHLSL]
    public struct DirectionalLightDataSimple
    {
        public Vector4 ForwardCosAngle;
        public Vector4 Color;
        public Vector4 ShadowOffset;
    }

    // These structures share between C# and hlsl need to be align on float4, so we pad them.
    [GenerateHLSL]
    public struct DirectionalLightData
    {
        public Vector3 positionWS;
        public int tileCookie; // TODO: make it a bool

        public Vector3 color;
        public int shadowIndex; // -1 if unused

        public Vector3 forward;
        public int cookieIndex; // -1 if unused

        public Vector3 right;   // Rescaled by (2 / shapeWidth)
        public float specularScale;

        public Vector3 up;      // Rescaled by (2 / shapeHeight)
        public float diffuseScale;

        public float volumetricDimmer;
        public int nonLightmappedOnly; // Use with ShadowMask feature // TODO: make it a bool

        public Vector4 shadowMaskSelector; // Use with ShadowMask feature
    };

    [GenerateHLSL]
    public struct PunctualLightGeoData
    {
        public Vector4 PositionSizeSqr;
        public Vector4 ForwardCosAngle;
    };

    [GenerateHLSL]
    public struct PunctualLightRenderingData
    {
        public Vector4 Color;
        public Vector4 ShadowBias;
        public int ShadowIndex;
        public int CookieIndex;
        public int AffectVolumetricLight;
        public int unused2;
        //public int ShadowIndex;
        //public bool dynamicShadowCasterOnly;
        //public float minRoughness;
        //bool AffectVolumetricFog;
        //public int cookieIndex;
    };

    [GenerateHLSL]
    public struct LightProxyVolumeData
    {
        int ProxyType;
        int ProxyID;
    }

    [GenerateHLSL]
    public struct AreaLightData
    {
        public Vector3 positionWS;
        public float invSqrAttenuationRaius;

        public Vector3 color;
        public int shadowIndex; //-1 if unused

        public Vector3 forward;
        public int cookieIndex; //-1 if unused

        public Vector3 right; //If spot: rescaled by cot(outerHalfAngle); if projector: rescaled by (2 / shapeLength)
        public float specularScale;

        public Vector3 up; // If spot: rescaled by cot(outerHalfAngle); if projector: rescaled by (2 / shapeLength)
        public float diffuseScale;

        public float angleScale; //SpotLight
        public float angleOffset; //SpotLight
        public float shadowDimmer;
        public bool dynamicShadowCasterOnly; // Use with shadow mask feature

        public Vector2 size; //Used by area, frustum projector and spot lights (x = cot(outerHalfAngle))
        public GPULightType lightType;
        public float minRoughness; // This is use to give a small "area" to punctual light, as if we have a light with a radius.

        public Vector4 shadowMaskSelector;
    }

    [GenerateHLSL]
    public enum EnvShapeType
    {
        None,
        Box,
        Sphere,
        Sky
    };

    [GenerateHLSL]
    public enum EnvConstants
    {
        SpecCubeLodStep = 6
    };

    // Guideline for reflection volume: In HDRenderPipeline we separate the projection volume (the proxy of the scene) from the influence volume (what pixel on the screen is affected)
    // However we add the constrain that the shape of the projection and influence volume is the same (i.e if we have a sphere shape projection volume, we have a shape influence).
    // It allow to have more coherence for the dynamic if in shader code.
    // Users can also chose to not have any projection, in this case we use the property minProjectionDistance to minimize code change. minProjectionDistance is set to huge number
    // that simulate effect of no shape projection
    [GenerateHLSL]
    public struct EnvLightData
    {
        public Vector3 positionWS;
        public EnvShapeType envShapeType;

        public Vector3 forward;
        public int envIndex;

        public Vector3 up;
        public float blendDistance; //blend transition outside the volume

        public Vector3 right;
        //User can chose if they use. This is used in case we want to force infinite projection distance (i.e no projection)
        public float minProjectionDistance;

        public Vector3 innerDistance; //equivalent to volume scale
        public float unused0;

        public Vector3 offsetLS;
        public float unused1;
    };

    // Usage of StencilBits.Lighting on 2 bits.
    // We support both deferred and forward renderer.  Here is the current usage of this 2 bits:
    // 0. Everything except case below. This include any forward opaque object. No lighting in deferred lighting path.
    // 1. All deferred opaque object that require split lighting (i.e output both specular and diffuse in two different render target). Typically Subsurface scattering material.
    // 2. All deferred opaque object.
    // 3. unused
    [GenerateHLSL]
    // Caution: Value below are hardcoded in some shader (because properties doesn't support include). If order or value is change, please update corresponding ".shader"
    public enum StencilLightingUsage
    {
        NoLighting,
        SplitLighting,
        RegularLighting
    };
}
