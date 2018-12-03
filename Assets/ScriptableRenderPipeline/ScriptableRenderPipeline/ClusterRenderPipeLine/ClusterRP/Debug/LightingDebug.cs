using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    [GenerateHLSL]
    public enum DebugLightingMode
    {
        None,
        DiffuseLighting,
        SpecularLighting,
        LuxMeter,
        VisualizeCascade,
        IndirectDiffuseOcclusionFromSsao,
        IndirectDiffuseGtaoFromSsao,
        IndirectSpecularOcclusionFromSsao,
        IndirectSpecularGtaoFromSsao,
        EnvironmentProxyVolume,
        EnvironmentSampleCoordinates,
    }

    public enum ShadowMapDebugMode
    {
        None,
        VisualizeAtlas,
        VisualizeShadowMap
    }

    [Serializable]
    public class LightingDebugSettings
    {
        public bool IsDebugDisplayEnabled()
        {
            return debugLightingMode != DebugLightingMode.None || overrideSmoothness || overrideAlbedo || overrideNormal;
        }

        public DebugLightingMode    debugLightingMode = DebugLightingMode.None;
        public ShadowMapDebugMode   shadowDebugMode = ShadowMapDebugMode.None;
        public bool                 shadowDebugUseSelection = false;
        public uint                 shadowMapIndex = 0;
        public uint                 shadowAtlasIndex = 0;
        public float                shadowMinValue = 0.0f;
        public float                shadowMaxValue = 1.0f;

        public bool                 overrideSmoothness = false;
        public float                overrideSmoothnessValue = 0.5f;
        public bool                 overrideAlbedo = false;
        public Color                overrideAlbedoValue = new Color(0.5f, 0.5f, 0.5f);
        public bool                 overrideNormal = false;

        public bool                 displaySkyReflection = false;
        public float                skyReflectionMipmap = 0.0f;

        public float                environmentProxyDepthScale = 20;

        public ClusterPass.LightManager.TileClusterDebug tileClusterDebug = ClusterPass.LightManager.TileClusterDebug.None;
        public ClusterPass.LightManager.TileClusterCategoryDebug tileClusterDebugByCategory = ClusterPass.LightManager.TileClusterCategoryDebug.Punctual;
    }
}
