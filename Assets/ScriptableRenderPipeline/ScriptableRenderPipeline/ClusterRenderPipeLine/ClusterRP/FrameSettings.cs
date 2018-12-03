using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    public struct FrameRTConfigration
    {
        public int frameWidth;
        public int frameHeight;
        public TextureDimension textureDimension;
        public MSAASamples msaaSamples;
#if UNITY_EDITOR
        public int volumetricWidth;
        public int volumetricHeight;
        public bool volumetricNeedReallocate;
#endif
    }

    public struct FrameClusterConfigration
    {
        public uint cullingClusterSize;
        public uint lightFogClusterSize;
        public uint clusterDepthSlices;
        public uint maxLightsPerCluster;
    }

    public struct FrameConfigration
    {

        public bool enableHDR;
        public bool enableDepthPrePass;
        public bool enablePostprocess;
        public bool enableClusterLighting;
        public bool enableVolumetricFog;
        public bool enableVolumetricLighting;
        public bool enableVolumetricDisplay;
        public bool enableHalfResParticle;
        public bool enableShadows;
        public bool enableSky;
        public bool enableAsyncCompute;
        public bool enableScreenSpaceShadow;
        public bool enableStaticShadowmap;
        public bool enableGradientResolution;
        public bool enableLightCullingMask;
        public bool hasRefraction;
        public bool rtConfigChanged;
        public bool clusterConfigChanged;

        public FrameRTConfigration rtConfig;
        public FrameClusterConfigration clusterConfig;

        public static void GetFrameConfigration(RGCamera rgCam, PostProcessLayer postProcessLayer, VolumetricSettings volumetricSettings, ref FrameConfigration frameConfig)
        {
            AdditionalCameraData acd = rgCam.camera.GetComponent<AdditionalCameraData>();
            
            frameConfig.enableHDR = true;
            frameConfig.enablePostprocess = CoreUtils.IsPostProcessingActive(postProcessLayer);
            if (acd)
            {
                switch (acd.m_RenderingType)
                {
                    case AdditionalCameraData.RenderingType.ClusterLight:
                        frameConfig.enableSky = true;
                        frameConfig.enableShadows = true;
                        frameConfig.enableAsyncCompute = true;
                        frameConfig.enableClusterLighting = true;
                        frameConfig.enableVolumetricDisplay = acd.m_DisplayVolumetricFog;
                        break;
                    case AdditionalCameraData.RenderingType.Unlit:
                        frameConfig.enableShadows = false;
                        frameConfig.enableAsyncCompute = false;
                        frameConfig.enableSky = true;
                        frameConfig.enableDepthPrePass = false;
                        frameConfig.enableClusterLighting = false;
                        frameConfig.enableVolumetricDisplay = false;
                        break;
                    case AdditionalCameraData.RenderingType.SimpleLight:
                        frameConfig.enableAsyncCompute = false;
                        frameConfig.enableSky = true;
                        frameConfig.enableDepthPrePass = false;
                        frameConfig.enableVolumetricFog = false;
                        frameConfig.enableClusterLighting = false;
                        frameConfig.enableVolumetricDisplay = false;
                        break;
                    case AdditionalCameraData.RenderingType.StaticShadow:
                        frameConfig.enableSky = false;
                        frameConfig.enableShadows = false;
                        frameConfig.enableAsyncCompute = false;
                        frameConfig.enableDepthPrePass = false;
                        frameConfig.enableVolumetricFog = false;
                        frameConfig.enableClusterLighting = false;
                        frameConfig.enableVolumetricDisplay = false;
                        break;
                    default:
                        break;
                }
            }
#if UNITY_EDITOR
            else if(rgCam.camera.cameraType == CameraType.SceneView)
            {
                frameConfig.enableSky = true;
                frameConfig.enableShadows = true;
                frameConfig.enableAsyncCompute = true;
                frameConfig.enableClusterLighting = true;
                frameConfig.enableVolumetricDisplay = true;
            }
#endif

            bool msaaChanged = false;
            var commonSettings = VolumeManager.instance.stack.GetComponent<CommonSettings>();
            if(commonSettings)
            {
                frameConfig.enableAsyncCompute = commonSettings.enableAsyncCompute;
                frameConfig.hasRefraction = commonSettings.hasRefraction;
                frameConfig.enableScreenSpaceShadow = commonSettings.enableScreenSpaceShadow;
                frameConfig.enableDepthPrePass = commonSettings.enableDepthPrepass;
                msaaChanged = frameConfig.rtConfig.msaaSamples != commonSettings.msaaSamples;
                frameConfig.enableLightCullingMask = commonSettings.enableLightCullingMask;
                frameConfig.rtConfig.msaaSamples = commonSettings.msaaSamples;
            }

            frameConfig.enableGradientResolution = rgCam.camera.stereoEnabled && !frameConfig.hasRefraction;

            frameConfig.enableVolumetricLighting = false;
            frameConfig.enableVolumetricFog = false;
            if (volumetricSettings/* && volumetricSettings.BaseDensity != 0*/)
            {
                frameConfig.enableVolumetricLighting = true;
                frameConfig.enableVolumetricFog = true;
            }

#if UNITY_EDITOR
            if(rgCam.camera.cameraType == CameraType.Preview || rgCam.camera.name.Contains("Preview"))
            {
                frameConfig.enableDepthPrePass = false;
                frameConfig.enablePostprocess = false;
                frameConfig.enableClusterLighting = true;
                frameConfig.enableVolumetricFog = false;
                frameConfig.enableVolumetricLighting = false;
                frameConfig.enableHalfResParticle = false;
                frameConfig.enableShadows = false;
                frameConfig.enableSky = false;
                frameConfig.enableAsyncCompute = false;
                frameConfig.enableScreenSpaceShadow = false;
                frameConfig.enableStaticShadowmap = false;
                frameConfig.rtConfig.msaaSamples = MSAASamples.None;
            }
#endif

            if (rgCam.CameraWidth != frameConfig.rtConfig.frameWidth || rgCam.CameraHeight != frameConfig.rtConfig.frameHeight || rgCam.RenderTextureDimension != frameConfig.rtConfig.textureDimension || msaaChanged)
            {
                frameConfig.rtConfigChanged = true;
                frameConfig.rtConfig.frameWidth = rgCam.CameraWidth;
                frameConfig.rtConfig.frameHeight = rgCam.CameraHeight;
                frameConfig.rtConfig.textureDimension = rgCam.RenderTextureDimension;
#if UNITY_EDITOR
                if (frameConfig.enableVolumetricFog || frameConfig.enableVolumetricLighting)
                {
                    if (frameConfig.rtConfig.volumetricWidth != rgCam.CameraWidth || frameConfig.rtConfig.volumetricHeight != rgCam.CameraHeight)
                    {
                        frameConfig.rtConfig.volumetricWidth = rgCam.CameraWidth;
                        frameConfig.rtConfig.volumetricHeight = rgCam.CameraHeight;
                        frameConfig.rtConfig.volumetricNeedReallocate = true;
                    }
                    else
                        frameConfig.rtConfig.volumetricNeedReallocate = false;
                }
#endif
            }
            else
                frameConfig.rtConfigChanged = false;

            if (frameConfig.rtConfigChanged)
                frameConfig.clusterConfigChanged = true;
            else
                frameConfig.clusterConfigChanged = false;

        }
    }
}
