using System.Collections;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Linq;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.PostProcessing;

using Viva.Rendering.RenderGraph;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_2017_2_OR_NEWER
using XRSettings = UnityEngine.XR.XRSettings;
#elif UNITY_5_6_OR_NEWER
    using XRSettings = UnityEngine.VR.VRSettings;
#endif

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    public class ClusterRenderPipeline : RenderPipeline
    {
        enum ForwardPass
        {
            Opaque,
            PreRefraction,
            Transparent
        }

        //Debug Panel Info
        public struct DebugPanelInfo
        {
            public int blurMethod;
            public int opaqueSortFlag;
            public int opaqueDrawOrder;
            public bool enableSSShadow;
            public bool enableDepthPrepass;
            public bool enableAsyncCompute;
            public bool enalbeSinglePassVL;
            public bool enableTAA;
        }

        public bool haveDebugPanel = false;
        public DebugPanelInfo m_debugPanelInfo = new DebugPanelInfo();
        public int m_visibleLightsCount;
        public int m_shadowCasterCount;

        private readonly ClusterRenderPipeLineAsset m_Asset;
        static Dictionary<Camera, AdditionalCameraData> s_Cameras = new Dictionary<Camera, AdditionalCameraData>();

        //Cluster params
        private const int ALIGNED_CLUSTER_RES = 128;
        
        private RenderTexture m_CameraStencilBufferCopy;

        private RTHandleSystem.RTHandle m_CameraColorBuffer;
        private RTHandleSystem.RTHandle m_CameraColorBufferCopy;
        private RTHandleSystem.RTHandle m_CameraDepthStencilBuffer;
        private RTHandleSystem.RTHandle m_VelocityBuffer;
        private RTHandleSystem.RTHandle m_CameraDepthBufferCopy;
        private RTHandleSystem.RTHandle m_R8Buffer;
        private RTHandleSystem.RTHandle m_ScreenSpaceShadowBuffer;
        private RTHandleSystem.RTHandle m_HistoryBuffer;

        private RenderTargetIdentifier m_CameraStencilBufferCopyRT;

        // The pass "SRPDefaultUnlit" is a fall back to legacy unlit rendering and is required to support unity 2d + unity UI that render in the scene.
        ShaderPassName[] m_SinglePassName = new ShaderPassName[1];
        ShaderPassName[] m_ForwardPassNames = { ClusterShaderPassNames.s_ForwardBaseName, ClusterShaderPassNames.s_ClusterForwardName, ClusterShaderPassNames.s_SRPDefaultUnlitName };
        ShaderPassName[] m_ForwardAndForwardOnlyPassNames = { new ShaderPassName(), new ShaderPassName(), ClusterShaderPassNames.s_SRPDefaultUnlitName };
        ShaderPassName[] m_ForwardOnlyPassNames = { new ShaderPassName(), ClusterShaderPassNames.s_SRPDefaultUnlitName };
        ShaderPassName[] m_DepthOnlyAndDepthForwardOnlyPassNames = { ClusterShaderPassNames.s_DepthForwardOnlyName, ClusterShaderPassNames.s_DepthOnlyName };
        ShaderPassName[] m_DepthForwardOnlyPassNames = { ClusterShaderPassNames.s_DepthForwardOnlyName };
        ShaderPassName[] m_DepthOnlyPassNames = { ClusterShaderPassNames.s_DepthOnlyName };
        ShaderPassName[] m_ForwardErrorPassNames = { ClusterShaderPassNames.s_AlwaysName, ClusterShaderPassNames.s_ForwardBaseName, ClusterShaderPassNames.s_DeferredName, ClusterShaderPassNames.s_PrepassBaseName, ClusterShaderPassNames.s_VertexName, ClusterShaderPassNames.s_VertexLMRGBMName, ClusterShaderPassNames.s_VertexLMName };
        ShaderPassName[] m_MotionVectorPassName = { ClusterShaderPassNames.s_MotionVectorsName };

        private CopyTextureSupport m_CopyTextureSupport;

        private Material m_CopyDepthMaterial;
        private Material m_CameraMotionVectorMaterial;
        private Material m_GradientStencilMaskBlitMaterial;
        private Material m_ScreenSpaceShadowMaterial;
        private ComputeShader m_ScreenSpaceShadowCS;

        private static readonly string kMSAADepthKeyword = "_MSAA_DEPTH";

        public int GetCurrentShadowCount() { return m_LightManager.GetCurrentShadowCount(); }
        public int GetShadowAtlasCount() { return m_LightManager.GetShadowAtlasCount(); }

        //Shadow map params
        private CachedShadowSettings m_ShadowSettings = new CachedShadowSettings();

        //Frame configration for every camra
        private FrameConfigration m_FrameConifgDefault = new FrameConfigration();
        private FrameConfigration m_FrameConfig = new FrameConfigration();

        //Lights params
        private ClusterPass.LightManager m_LightManager = new ClusterPass.LightManager();
        private readonly SkyManager m_SkyManager = new SkyManager();
        private Light m_CurrentSunLight;

        //Debugging
        DebugDisplaySettings m_DebugDisplaySettings = new DebugDisplaySettings();
        public DebugDisplaySettings debugDisplaySettings { get { return m_DebugDisplaySettings; } }
        static DebugDisplaySettings s_NeutralDebugDisplaySettings = new DebugDisplaySettings();
        DebugDisplaySettings m_CurrentDebugDisplaySettings;
        RTHandleSystem.RTHandle m_DebugColorPickerBuffer;
        RTHandleSystem.RTHandle m_DebugFullScreenTempBuffer;
        bool m_FullScreenDebugPushed;
        bool m_ValidAPI;

        Material m_DebugDisplayLatlong;
        Material m_DebugFullScreen;
        Material m_DebugColorPicker;
        Material m_Blit;
        Material m_ErrorMaterial;

        bool m_forceStencilMask = false;

        public Material GetBlitMaterial() { return m_Blit; }

        readonly PostProcessRenderContext m_PostProcessContext;
        private ClusterPostProcess m_PostProcessCompute = new ClusterPostProcess();

        // Stencil usage in HDRenderPipeline.
        // Currently we use only 2 bits to identify the kind of lighting that is expected from the render pipeline
        // Usage is define in LightDefinitions.cs
        [Flags]
        public enum StencilBitMask
        {
            Clear = 0,             // 0x0
            LightingMask = 7,             // 0x7  - 3 bit
            ObjectVelocity = 128,           // 0x80 - 1 bit
            All = 255            // 0xFF - 8 bit
        }

        uint m_frameCount;
        float m_LastTime, m_Time;
        public float m_cpuTime;
        public int m_shadowBlurMethod;

        RenderStateBlock m_DepthStateOpaque;
        RenderStateBlock m_DepthStateOpaqueWithPrepass;

        SubsurfaceScatteringSettings m_InternalSSSAsset;
        public SubsurfaceScatteringSettings sssSettings
        {
            get
            {
                // If no SSS asset is set, build / reuse an internal one for simplicity
                var asset = m_Asset.sssSettings;

                if (asset == null)
                {
                    if (m_InternalSSSAsset == null)
                        m_InternalSSSAsset = ScriptableObject.CreateInstance<SubsurfaceScatteringSettings>();

                    asset = m_InternalSSSAsset;
                }

                return asset;
            }
        }
        
        SkySettings m_SkySettings = null;
        VolumetricSettings m_VolumetricSettings = null;

        public SkySettings skySettingsToUse
        {
            get
            {
                if (SkySettingsSingleton.overrideSettings)
                    return SkySettingsSingleton.overrideSettings;

                return m_SkySettings;
            }
        }

        public ClusterRenderPipeline(ClusterRenderPipeLineAsset asset)
        {
            m_Asset = asset;

            m_ValidAPI = true;

            if (!SetRenderingFeatures())
            {
                m_ValidAPI = false;

                return;
            }

            SetupDefaultFrameConfig(m_Asset, ref m_FrameConifgDefault);
            m_FrameConfig = m_FrameConifgDefault;
            SetupShadowSettings(m_Asset, ref m_ShadowSettings);

            m_CopyTextureSupport = SystemInfo.copyTextureSupport;

            m_CopyDepthMaterial = CoreUtils.CreateEngineMaterial(asset.renderPipelineResources.copyDepthBuffer);
            m_CameraMotionVectorMaterial = CoreUtils.CreateEngineMaterial(asset.renderPipelineResources.cameraMotionVectors);
            m_GradientStencilMaskBlitMaterial = CoreUtils.CreateEngineMaterial(asset.renderPipelineResources.stencilMaskBlit);
            m_ScreenSpaceShadowMaterial = CoreUtils.CreateEngineMaterial(asset.renderPipelineResources.screenSpaceShadowShader);
            m_ScreenSpaceShadowCS = asset.renderPipelineResources.screenSpaceShadowCS;

            int frameWidth = m_FrameConfig.rtConfig.frameWidth;
            int frameHeight = m_FrameConfig.rtConfig.frameHeight;
            MSAASamples frameMsaaSamples = m_FrameConfig.rtConfig.msaaSamples;

            m_CameraColorBuffer = RTHandles.Alloc(width: frameWidth, height: frameHeight, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGBHalf, sRGB: false, msaaSamples: frameMsaaSamples, enableRandomWrite: true, name: "CameraColor");
            m_CameraDepthStencilBuffer = RTHandles.Alloc(width: frameWidth, height: frameHeight, depthBufferBits: DepthBits.Depth24, colorFormat: RenderTextureFormat.Depth, filterMode: FilterMode.Point, bindTextureMS: false, msaaSamples: frameMsaaSamples, name: "CameraDepthStencil");
            m_VelocityBuffer = RTHandles.Alloc(width: frameWidth, height: frameHeight, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.RGHalf, sRGB: false, name: "Velocity");
            m_R8Buffer = RTHandles.Alloc(width: frameWidth, height: frameHeight, filterMode: FilterMode.Bilinear, colorFormat: RenderTextureFormat.R8, sRGB: false, msaaSamples: frameMsaaSamples, enableRandomWrite: true,name: "R8Buffer");
            m_ScreenSpaceShadowBuffer = RTHandles.Alloc(width: frameWidth, height: frameHeight, filterMode: FilterMode.Bilinear, colorFormat: RenderTextureFormat.R8, sRGB: false, msaaSamples: frameMsaaSamples, enableRandomWrite: true, name: "ShadowBuffer");

            if (UnityEngine.Debug.isDebugBuild)
            {
                m_DebugColorPickerBuffer = RTHandles.Alloc(width: frameWidth, height: frameHeight, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGBHalf, sRGB: false, name: "DebugColorPicker");
                m_DebugFullScreenTempBuffer = RTHandles.Alloc(width: frameWidth, height: frameHeight, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGBHalf, sRGB: false, name: "DebugFullScreen");
            }

            if (NeedDepthBufferCopy())
            {
                m_CameraDepthBufferCopy = RTHandles.Alloc(width: frameWidth, height: frameHeight, depthBufferBits: DepthBits.Depth24, colorFormat: RenderTextureFormat.Depth, filterMode: FilterMode.Point, bindTextureMS: false, msaaSamples: frameMsaaSamples, name: "CameraDepthCopy");
                m_CameraColorBufferCopy = RTHandles.Alloc(width: frameWidth, height: frameHeight, filterMode: FilterMode.Bilinear, colorFormat: RenderTextureFormat.ARGBHalf, sRGB: false, msaaSamples: frameMsaaSamples, enableRandomWrite: false, name: "CameraColorCopy");
            }

            m_LightManager.Build(asset.renderPipelineResources, m_FrameConfig.clusterConfig, asset.shadowInitParams, m_ShadowSettings);
            ProbeManager.instance.Build(asset.renderPipelineResources, m_FrameConfig.clusterConfig, asset.shadowInitParams, m_ShadowSettings);
            m_SkyManager.Build(asset.renderPipelineResources);
            m_SkyManager.skySettings = skySettingsToUse;

            m_PostProcessContext = new PostProcessRenderContext();
            m_PostProcessCompute.Build(asset.renderPipelineResources);

            m_DebugDisplaySettings.RegisterDebug();
            //m_DebugFullScreenTempRT = HDShaderIDs._DebugFullScreenTexture;

            InitializeRenderStateBlocks();

            InitializeDebugMaterials();

            UnityEngine.Debug.Log("Cluster render pipeline has been created!!!!!");
            //RegisterDebug();
        }

        void SetupDefaultFrameConfig(ClusterRenderPipeLineAsset asset, ref FrameConfigration defaultConfig)
        {
            defaultConfig.enableDepthPrePass = true;
            defaultConfig.enableHDR = true;
            defaultConfig.enablePostprocess = false;
            defaultConfig.enableScreenSpaceShadow = false;
            defaultConfig.enableVolumetricLighting = false;
            defaultConfig.enableVolumetricDisplay = false;
            defaultConfig.enableStaticShadowmap = false;
            defaultConfig.enableVolumetricFog = false;
            defaultConfig.enableHalfResParticle = false;
            defaultConfig.enableShadows = true;
            defaultConfig.enableSky = true;
            defaultConfig.enableClusterLighting = true;
            defaultConfig.enableAsyncCompute = true;
            defaultConfig.enableLightCullingMask = false;
            defaultConfig.rtConfigChanged = false;
            defaultConfig.clusterConfigChanged = false;
            defaultConfig.hasRefraction = false;
            //Render target settings
            defaultConfig.rtConfig.frameWidth = 1;
            defaultConfig.rtConfig.frameHeight = 1;
            defaultConfig.rtConfig.textureDimension = TextureDimension.Tex2D;
            defaultConfig.rtConfig.msaaSamples = MSAASamples.None;
            //Cluster settings
            defaultConfig.clusterConfig.cullingClusterSize = (uint)m_Asset.clusterSettings.cullingClusterSize;
            defaultConfig.clusterConfig.lightFogClusterSize = (uint)m_Asset.clusterSettings.lightFogClusterSize;
            defaultConfig.clusterConfig.clusterDepthSlices = (uint)m_Asset.clusterSettings.clusterDepthSlices;
            defaultConfig.clusterConfig.maxLightsPerCluster = (uint)m_Asset.clusterSettings.maxLightsPerCluster;
        }

        void SetupShadowSettings(ClusterRenderPipeLineAsset asset, ref CachedShadowSettings defaultShadowSettings)
        {
            defaultShadowSettings.MaxShadowDistance = 1000.0f;
            defaultShadowSettings.MaxShadowCasters = 5;
            defaultShadowSettings.ShadowmapRes = new Vector2Int(asset.shadowInitParams.shadowAtlasWidth, asset.shadowInitParams.shadowAtlasHeight);
            defaultShadowSettings.StaticShadowmapRes = Vector2Int.one;
            defaultShadowSettings.StaticShadowmap = null;
        }

        void InitializeDebugMaterials()
        {
            //m_DebugViewMaterialGBuffer = CoreUtils.CreateEngineMaterial(m_Asset.renderPipelineResources.debugViewMaterialGBufferShader);
            //m_DebugViewMaterialGBufferShadowMask = CoreUtils.CreateEngineMaterial(m_Asset.renderPipelineResources.debugViewMaterialGBufferShader);
            //m_DebugViewMaterialGBufferShadowMask.EnableKeyword("SHADOWS_SHADOWMASK");
            m_DebugDisplayLatlong = CoreUtils.CreateEngineMaterial(m_Asset.renderPipelineResources.debugDisplayLatlongShader);
            m_DebugFullScreen = CoreUtils.CreateEngineMaterial(m_Asset.renderPipelineResources.debugFullScreenShader);
            m_DebugColorPicker = CoreUtils.CreateEngineMaterial(m_Asset.renderPipelineResources.debugColorPickerShader);
            m_Blit = CoreUtils.CreateEngineMaterial(m_Asset.renderPipelineResources.blit);
            m_ErrorMaterial = CoreUtils.CreateEngineMaterial("Hidden/InternalErrorShader");
        }

        void InitializeRenderStateBlocks()
        {
            m_DepthStateOpaque = new RenderStateBlock
            {
                depthState = new DepthState(true, CompareFunction.LessEqual),
                mask = RenderStateMask.Depth
            };

            // When doing a prepass, we don't need to write the depth anymore.
            // Moreover, we need to use DepthEqual because for alpha tested materials we don't do the clip in the shader anymore (otherwise HiZ does not work on PS4)
            m_DepthStateOpaqueWithPrepass = new RenderStateBlock
            {
                depthState = new DepthState(false, CompareFunction.Equal),
                mask = RenderStateMask.Depth
            };
        }

        public override void Dispose()
        {
            base.Dispose();

            m_LightManager.CleanUp();

            RTHandles.Release(m_CameraColorBuffer);
            RTHandles.Release(m_CameraDepthStencilBuffer);
            RTHandles.Release(m_VelocityBuffer);

            if (NeedDepthBufferCopy())
                RTHandles.Release(m_CameraDepthBufferCopy);

            SupportedRenderingFeatures.active = new SupportedRenderingFeatures();
            ProbeManager.instance.Dispose();
        }

        bool IsSupportedPlatform()
        {
            // Note: If you add new platform in this function, think about adding support when building the player to in HDRPCustomBuildProcessor.cs

            if (!SystemInfo.supportsComputeShaders)
                return false;

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12 ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.PlayStation4 ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.XboxOne ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.XboxOneD3D12 ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan ||
                SystemInfo.graphicsDeviceType == (GraphicsDeviceType)22 /*GraphicsDeviceType.Switch*/)
            {
                return true;
            }

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal)
            {
                string os = SystemInfo.operatingSystem;

                // Metal support depends on OS version:
                // macOS 10.11.x doesn't have tessellation / earlydepthstencil support, early driver versions were buggy in general
                // macOS 10.12.x should usually work with AMD, but issues with Intel/Nvidia GPUs. Regardless of the GPU, there are issues with MTLCompilerService crashing with some shaders
                // macOS 10.13.x is expected to work, and if it's a driver/shader compiler issue, there's still hope on getting it fixed to next shipping OS patch release
                //
                // Has worked experimentally with iOS in the past, but it's not currently supported
                //

                if (os.StartsWith("Mac"))
                {
                    // TODO: Expose in C# version number, for now assume "Mac OS X 10.10.4" format with version 10 at least
                    int startIndex = os.LastIndexOf(" ");
                    var parts = os.Substring(startIndex + 1).Split('.');
                    int a = Convert.ToInt32(parts[0]);
                    int b = Convert.ToInt32(parts[1]);
                    // In case in the future there's a need to disable specific patch releases
                    // int c = Convert.ToInt32(parts[2]);

                    if (a >= 10 && b >= 13)
                        return true;
                }
            }

            return false;
        }

        bool SetRenderingFeatures()
        {
            // Set subshader pipeline tag
            Shader.globalRenderPipeline = "ClusterRenderPipeline";

            // HD use specific GraphicsSettings
            GraphicsSettings.lightsUseLinearIntensity = true;
            GraphicsSettings.lightsUseColorTemperature = true;
            // HD should always use the new batcher
            //GraphicsSettings.useScriptableRenderPipelineBatching = true;

            SupportedRenderingFeatures.active = new SupportedRenderingFeatures()
            {
                reflectionProbeSupportFlags = SupportedRenderingFeatures.ReflectionProbeSupportFlags.Rotation,
                defaultMixedLightingMode = SupportedRenderingFeatures.LightmapMixedBakeMode.IndirectOnly,
                supportedMixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeMode.IndirectOnly | SupportedRenderingFeatures.LightmapMixedBakeMode.Shadowmask,
                supportedLightmapBakeTypes = LightmapBakeType.Baked | LightmapBakeType.Mixed | LightmapBakeType.Realtime,
                supportedLightmapsModes = LightmapsMode.NonDirectional | LightmapsMode.CombinedDirectional,
                rendererSupportsLightProbeProxyVolumes = true,
                rendererSupportsMotionVectors = true,
                rendererSupportsReceiveShadows = false,
                rendererSupportsReflectionProbes = true
            };

            //Lightmapping.SetDelegate(GlobalIlluminationUtils.hdLightsDelegate);

#if UNITY_EDITOR
            //SceneViewDrawMode.SetupDrawMode();

            if (UnityEditor.PlayerSettings.colorSpace == ColorSpace.Gamma)
            {
                UnityEngine.Debug.LogError("Cluster Render Pipeline doesn't support Gamma mode, change to Linear mode");
            }
#endif

            if (!IsSupportedPlatform())
            {
                CoreUtils.DisplayUnsupportedAPIMessage();

                return false;
            }

            return true;
        }

        CullResults m_cullResults;
        public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
        {
            base.Render(renderContext, cameras);
            RenderPipeline.BeginFrameRendering(cameras);

            float startTime = Time.realtimeSinceStartup;

            {
                float t = Time.realtimeSinceStartup;
                uint c = (uint)Time.frameCount;

                bool newFrame;

                if(Application.isPlaying)
                {
                    newFrame = m_frameCount != c;
                    m_frameCount = c;
                }
                else
                {
                    newFrame = (t - m_Time) > 0.0166f;
                    if (newFrame) m_frameCount++;
                }

                if (newFrame)
                {
                    RGCamera.CleanUnUsed();
                    // Make sure both are never 0.
                    m_LastTime = (m_Time > 0) ? m_Time : t;
                    m_Time = t;
                }
            }

            //TODO: Render only visible probes
            var isReflection = cameras.Any(c => c.cameraType == CameraType.Reflection);

            if (!isReflection && cameras.Any(c => c.cameraType != CameraType.Preview))
            {
                CommandBuffer probeCmd = CommandBufferPool.Get("Probe Rendering");
                ProbeManager.instance.Render(renderContext, probeCmd);
                CommandBufferPool.Release(probeCmd);
            }
            //if(!isReflection)
            //    Reflections

            // This is the main command buffer used for the frame.

            //foreach (var material in m_MaterialList)
            //    material.RenderInit(cmd);

            // Do anything we need to do upon a new frame.

            foreach (var cam in cameras)
            {
                if (cam == null)
                    continue;
                
                RenderPipeline.BeginCameraRendering(cam);

                m_LightManager.NewFrame();

                // If we render a reflection view or a preview we should not display any debug information
                // This need to be call before ApplyDebugDisplaySettings()
                if (cam.cameraType == CameraType.Reflection || cam.cameraType == CameraType.Preview)
                {
                    // Neutral allow to disable all debug settings
                    m_CurrentDebugDisplaySettings = s_NeutralDebugDisplaySettings;
                }
                else
                {
                    m_CurrentDebugDisplaySettings = m_DebugDisplaySettings;
                }


                PostProcessLayer postProcessLayer = cam.GetComponent<PostProcessLayer>();

                RGCamera rgCam = RGCamera.Get(cam, postProcessLayer, ALIGNED_CLUSTER_RES, m_FrameConfig.enablePostprocess);

                if (haveDebugPanel)
                {
                    //m_FrameConfig.enableScreenSpaceShadow = false;// m_debugPanelInfo.enableSSShadow;
                    m_FrameConfig.enableDepthPrePass = m_FrameConfig.enableScreenSpaceShadow || m_debugPanelInfo.enableDepthPrepass;
                    m_FrameConfig.enableAsyncCompute = m_debugPanelInfo.enableAsyncCompute && (m_FrameConfig.enableDepthPrePass || m_debugPanelInfo.enableTAA);

                    if(postProcessLayer)
                    {
                        if (m_debugPanelInfo.enableTAA)
                            postProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.TemporalAntialiasing;
                        else
                            postProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.None;
                    }
                }

#if UNITY_EDITOR
                if(cam.cameraType == CameraType.Game && cam.name == "SHADER_FORGE_BLIT_CAMERA")
                {
                    renderContext.Submit();
                    continue;
                }
#endif                

                var cmd = CommandBufferPool.Get("");

                using (new ProfilingSample(cmd, "Volume Update"))
                {
                    // Temporary hack:
                    // For scene view, by default, we use the "main" camera volume layer mask if it exists
                    // Otherwise we just remove the lighting override layers in the current sky to avoid conflicts
                    // This is arbitrary and should be editable in the scene view somehow.
                    LayerMask layerMask = -1;
                    //if (additionalCameraData != null)
                    //{
                    //    layerMask = additionalCameraData.volumeLayerMask;
                    //}
                    //else
                    //{
                    //    // Temporary hack:
                    //    // For scene view, by default, we use the "main" camera volume layer mask if it exists
                    //    // Otherwise we just remove the lighting override layers in the current sky to avoid conflicts
                    //    // This is arbitrary and should be editable in the scene view somehow.
                    //    if (camera.cameraType == CameraType.SceneView)
                    //    {
                    //        var mainCamera = Camera.main;
                    //        bool needFallback = true;
                    //        if (mainCamera != null)
                    //        {
                    //            var mainCamAdditionalData = mainCamera.GetComponent<HDAdditionalCameraData>();
                    //            if (mainCamAdditionalData != null)
                    //            {
                    //                layerMask = mainCamAdditionalData.volumeLayerMask;
                    //                needFallback = false;
                    //            }
                    //        }

                    //        if (needFallback)
                    //        {
                    //            // If the override layer is "Everything", we fall-back to "Everything" for the current layer mask to avoid issues by having no current layer
                    //            // In practice we should never have "Everything" as an override mask as it does not make sense (a warning is issued in the UI)
                    //            if (m_Asset.renderPipelineSettings.lightLoopSettings.skyLightingOverrideLayerMask == -1)
                    //                layerMask = -1;
                    //            else
                    //                layerMask = (-1 & ~m_Asset.renderPipelineSettings.lightLoopSettings.skyLightingOverrideLayerMask);
                    //        }
                    //    }
                    //}
                    VolumeManager.instance.Update(cam.transform, layerMask);
                }

                m_VolumetricSettings = VolumeManager.instance.stack.GetComponent<VolumetricSettings>();

                FrameConfigration.GetFrameConfigration(rgCam, postProcessLayer, m_VolumetricSettings, ref m_FrameConfig);
                //cmd.BeginSample("ClusterForwardRendering:Render");
                m_FrameConfig.enableAsyncCompute = false;

                if (m_FrameConfig.enableScreenSpaceShadow)
                    cmd.EnableShaderKeyword("SCREEN_SHADOW");
                else
                    cmd.DisableShaderKeyword("SCREEN_SHADOW");

                cmd.EnableShaderKeyword("SOFTPARTICLES_ON");

                //if (m_FrameConfig.enableLightCullingMask)
                //    cmd.EnableShaderKeyword("LIGHT_CULLING_MASK");
                //else
                //    cmd.DisableShaderKeyword("LIGHT_CULLING_MASK");

                PushGlobalParams(rgCam, cmd, sssSettings);
                Resize(rgCam, postProcessLayer, m_FrameConfig.enableVolumetricFog || m_FrameConfig.enableVolumetricLighting);
                
                ScriptableCullingParameters cullingParams;

                Camera cullingCam = cam;
#if UNITY_EDITOR
                Camera tempCam = null;
                if (Selection.activeGameObject)
                    tempCam = Selection.activeGameObject.GetComponent<Camera>() as Camera;
                if (StaticOcclusionCullingVisualization.showGeometryCulling && StaticOcclusionCullingVisualization.previewOcclusionCamera)
                {
                    cullingCam = StaticOcclusionCullingVisualization.previewOcclusionCamera;
                }
#endif
                if (!CullResults.GetCullingParameters(cam, rgCam.StereoEnabled, out cullingParams))
                {
                    renderContext.Submit();
                    continue;
                }

                m_LightManager.UpdateCullingParameters(ref cullingParams, m_FrameConfig.enableLightCullingMask);

#if UNITY_EDITOR
                // emit scene view UI
                if (cam.cameraType == CameraType.SceneView)
                {
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(cam);
                }
#endif

                using (new ProfilingSample(cmd, "CullResults.Cull"))
                {
                    VolumetricManager.PrepareCull(cam);
                    CullResults.Cull(ref cullingParams, renderContext, ref m_cullResults);
                    VolumetricManager.CullVolumeDatas(cam);
                }

#if UNITY_EDITOR
                var additionalCameraData = cam.GetComponent<AdditionalCameraData>();
                if (additionalCameraData && additionalCameraData.m_RenderingType == AdditionalCameraData.RenderingType.StaticShadow)
                {
                    RenderStaticShadowmap(cam);
                    renderContext.SetupCameraProperties(cam);
                    RenderForwardOpaqueShadow(m_cullResults, rgCam, cam, renderContext, cmd);
                    CommandBufferPool.Release(cmd);
                    renderContext.Submit();
                    continue;
                }
#endif
                using (new ProfilingSample(cmd, "Prepare Lights Data and Others"))
                {
                    //Update Sky params
                    m_SkyManager.skySettings = skySettingsToUse;
                    m_SkyManager.Resize(cam.nearClipPlane, cam.farClipPlane);

                    PrepareLightsData(rgCam, cmd, renderContext);

                    if (cam.cameraType != CameraType.SceneView)
                    {
                        m_visibleLightsCount = m_cullResults.visibleLights.Count;
                        m_shadowCasterCount = m_LightManager.GetCurrentShadowCount();
                    }
                }
                
                using (new ProfilingSample(cmd, "Render all shadows and cluster light"))
                {
                    //Render Shadows
                    if (m_FrameConfig.enableShadows)
                    {
                        var clusterShadowSettings = VolumeManager.instance.stack.GetComponent<ClusterShadowSettings>();
                        if (clusterShadowSettings)
                        {
                            m_ShadowSettings.MaxShadowDistance = clusterShadowSettings.MaxShadowDistance;
                            m_ShadowSettings.MaxShadowCasters = clusterShadowSettings.MaxShadowCasters;
                            m_ShadowSettings.ShadowmapRes = clusterShadowSettings.ShadowMapResolution;
                            m_ShadowSettings.StaticShadowmapRes = clusterShadowSettings.StaticShadowMapResolution;
                            m_ShadowSettings.StaticShadowmap = clusterShadowSettings.staticShadowmap.value;
                        }
                        else
                            SetupShadowSettings(m_Asset, ref m_ShadowSettings);

                        GPUFence clusterLightAssignmentComplete = new GPUFence();
                        {
                            GPUFence startFence = cmd.CreateGPUFence();
                            renderContext.ExecuteCommandBuffer(cmd);
                            cmd.Clear();
                            clusterLightAssignmentComplete = ClusterLightAssignmentComputeAsync(rgCam, m_cullResults, renderContext, startFence);
                        }

                        if (m_ShadowSettings.StaticShadowmap)
                        {
                            m_FrameConfig.enableStaticShadowmap = true;
                            cmd.SetGlobalTexture(ClusterShaderIDs._StaticShadowmapExp, m_ShadowSettings.StaticShadowmap);
                        }
                        else
                            m_FrameConfig.enableStaticShadowmap = false;

                        RenderShadow(renderContext, cmd, m_cullResults);

                        cmd.WaitOnGPUFence(clusterLightAssignmentComplete);
                        m_LightManager.PushGlobalParams(rgCam, cmd, null, 0);
                        renderContext.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                    }
                }
                //renderContext.SetupCameraProperties(mainCamera); // Need to recall SetupCameraProperties after m_ShadowPass.Render
                //m_SkyManager.UpdateEnvironment(rgCam, m_CurrentSunLight, cmd);

                //if (cam.actualRenderingPath == RenderingPath.DeferredShading)
                //{
                //    RenderGBuffer(m_cullResults, cam, renderContext, cmd);
                //    RenderDeferredLighting(rgCam, cmd);
                //}
                //else if (cam.actualRenderingPath == RenderingPath.Forward)
                {
                    renderContext.SetupCameraProperties(cam, rgCam.StereoEnabled); // Need to recall SetupCameraProperties after m_ShadowPass.Render
                    PushGlobalParams(rgCam, cmd, sssSettings);

                    if (rgCam.StereoEnabled)
                        renderContext.StartMultiEye(cam);

                    int depthSlice = rgCam.StereoEnabled && XRSettings.eyeTextureDesc.dimension == TextureDimension.Tex2DArray ? -1 : 0;
                    if (rgCam.TaaEnabled)
                        CoreUtils.SetRenderTarget(cmd, m_VelocityBuffer, m_CameraDepthStencilBuffer, ClearFlag.Depth, Color.clear, 0, CubemapFace.Unknown, depthSlice);
                    else if (m_FrameConfig.enableDepthPrePass)
                        CoreUtils.SetRenderTarget(cmd, m_CameraDepthStencilBuffer, ClearFlag.Depth, Color.clear, 0, CubemapFace.Unknown, depthSlice);
                    else if(rgCam.StereoEnabled || m_forceStencilMask)
                    {
                        CoreUtils.SetRenderTarget(cmd, m_CameraDepthStencilBuffer, ClearFlag.Depth, Color.clear, 0, CubemapFace.Unknown, depthSlice);
                        cmd.SetGlobalTexture(ClusterShaderIDs._R8Buffer, m_R8Buffer);
                        cmd.DrawProcedural(Matrix4x4.identity, m_GradientStencilMaskBlitMaterial, 0, MeshTopology.Triangles, 3, 1, null);
                    }

                    m_LightManager.PostBlurExpShadows(cmd, m_shadowBlurMethod);
                    renderContext.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    GPUFence volumetricLightComputeComplete = new GPUFence();
                    if (m_FrameConfig.enableVolumetricDisplay && m_FrameConfig.enableVolumetricLighting && m_FrameConfig.enableVolumetricFog && m_FrameConfig.enableAsyncCompute)
                    {
                        {
                            GPUFence startFence = cmd.CreateGPUFence();
                            renderContext.ExecuteCommandBuffer(cmd);
                            cmd.Clear();
                            volumetricLightComputeComplete = VolumetricLightComputeAsync(rgCam, renderContext, startFence);
                        }
                    }

                    if (rgCam.TaaEnabled)
                        RenderVelocityPrepass(m_cullResults, rgCam, renderContext, cmd);
                    else if (m_FrameConfig.enableDepthPrePass)
                        RenderDepthPrepass(m_cullResults, rgCam, renderContext, cmd);

                    CopyDepthBufferIfNeeded(cmd, renderContext);

                    GPUFence volumetricFogComputeComplete = new GPUFence();
                    if (m_FrameConfig.enableVolumetricDisplay && m_FrameConfig.enableVolumetricLighting && m_FrameConfig.enableVolumetricFog && m_FrameConfig.enableAsyncCompute)
                    {
                        cmd.WaitOnGPUFence(volumetricLightComputeComplete);
                        {
                            GPUFence startFence = cmd.CreateGPUFence();
                            renderContext.ExecuteCommandBuffer(cmd);
                            cmd.Clear();
                            volumetricFogComputeComplete = VolumetricFogComputeAsync(rgCam, renderContext, startFence);
                        }
                    }

                    if(m_FrameConfig.enableScreenSpaceShadow)
                        RenderScreenSpaceShadow(rgCam, renderContext, cmd);

                    if (rgCam.StereoEnabled)
                        renderContext.StopMultiEye(cam);

                    ProbeManager.instance.PushGlobalParams(cmd);
                    RenderForwardOpaque(m_cullResults, rgCam, cam, renderContext, cmd);

                    if (rgCam.TaaEnabled)
                        RenderCameraVelocity(m_cullResults, rgCam, renderContext, cmd);

                    CopyDepthBufferIfNeeded(cmd, renderContext);

                    if (m_FrameConfig.enableVolumetricDisplay && m_FrameConfig.enableVolumetricLighting && m_FrameConfig.enableVolumetricFog && m_FrameConfig.enableAsyncCompute)
                        cmd.WaitOnGPUFence(volumetricFogComputeComplete);
                    else if (m_FrameConfig.enableVolumetricDisplay && m_FrameConfig.enableVolumetricLighting && m_FrameConfig.enableVolumetricFog)
                    {
                        m_LightManager.VolumetricLightCompute(rgCam, cmd, m_VolumetricSettings, m_FrameConfig.enableStaticShadowmap);
                        m_LightManager.VolumetricLightComputePost(rgCam, cmd, m_VolumetricSettings);
                    }
                }

                if (rgCam.camera.clearFlags == CameraClearFlags.Skybox)
                    renderContext.DrawSkybox(rgCam.camera);
                //RenderSky(rgCam, cmd);
                renderContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                RenderForwardPreFog(m_cullResults, rgCam, renderContext, cmd);

                if (cam.cameraType != CameraType.Reflection && m_FrameConfig.enableVolumetricDisplay && m_FrameConfig.enableVolumetricFog)
                {
                    OpaqueOnlyPostProcess(cmd, rgCam, renderContext);
                }
                
                RenderForwardPreRefraction(m_cullResults, rgCam, renderContext, cmd);

                CopyColorBufferIfNeeded(cmd, renderContext);
                //cmd.SetGlobalTexture(m_FrameRTDimension == TextureDimension.Tex2DArray ? ClusterShaderIDs._MainColorBufferArray : ClusterShaderIDs._MainColorBuffer, GetColorTexture());
                
                RenderForwardTransparent(renderContext, rgCam, cmd);
                
                EndForwardRendering(rgCam, cam, cmd, renderContext, postProcessLayer);

                RenderDebug(rgCam, cmd);

#if UNITY_EDITOR
                if (cam.cameraType == CameraType.SceneView)
                {
                    cmd.DisableShaderKeyword(kMSAADepthKeyword);
                    m_CopyDepthMaterial.SetTexture(ClusterShaderIDs._InputDepth, m_CameraDepthStencilBuffer);
                    cmd.Blit(null, BuiltinRenderTextureType.CameraTarget, m_CopyDepthMaterial);
                }
#endif

#if UNITY_EDITOR
                // We need to make sure the viewport is correctly set for the editor rendering. It might have been changed by debug overlay rendering just before.
                cmd.SetViewport(new Rect(0.0f, 0.0f, rgCam.CameraWidth, rgCam.CameraHeight));
#endif


                //cmd.EndSample("ClusterForwardRendering:Render");
                renderContext.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                renderContext.Submit();
            }

            float endTime = Time.realtimeSinceStartup;
            m_cpuTime = endTime - startTime;
        }

        bool IsConsolePlatform()
        {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.PlayStation4 ||
                    SystemInfo.graphicsDeviceType == GraphicsDeviceType.XboxOne ||
                    SystemInfo.graphicsDeviceType == GraphicsDeviceType.XboxOneD3D12;
        }

        bool NeedDepthBufferCopy()
        {
            // For now we consider only PS4 to be able to read from a bound depth buffer.
            // TODO: test/implement for other platforms.
            return !IsConsolePlatform();
        }

        RenderTargetIdentifier GetDepthTexture()
        {
            return NeedDepthBufferCopy() ? m_CameraDepthBufferCopy : m_CameraDepthStencilBuffer;
        }

        RenderTargetIdentifier GetColorTexture()
        {
            return NeedDepthBufferCopy() ? m_CameraColorBufferCopy : m_CameraColorBuffer;
        }

        void CopyDepthBufferIfNeeded(CommandBuffer cmd, ScriptableRenderContext renderContext)
        {
            using (new ProfilingSample(cmd, NeedDepthBufferCopy() ? "Copy DepthBuffer" : "Set DepthBuffer"))
            {
                if (NeedDepthBufferCopy())
                {
                    using (new ProfilingSample(cmd, "Copy depth-stencil buffer"))
                    {
                        cmd.CopyTexture(m_CameraDepthStencilBuffer, m_CameraDepthBufferCopy);
                    }
                }

                cmd.SetGlobalTexture(ClusterShaderIDs._MainDepthTexture, GetDepthTexture());
                cmd.SetGlobalTexture(ClusterShaderIDs._CameraDepthTexture, GetDepthTexture());
                renderContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
        }

        void CopyColorBufferIfNeeded(CommandBuffer cmd, ScriptableRenderContext renderContext)
        {
            using (new ProfilingSample(cmd, NeedDepthBufferCopy() ? "Copy ColorBuffer" : "Set ColorBuffer"))
            {
                if (NeedDepthBufferCopy())
                {
                    cmd.CopyTexture(m_CameraColorBuffer, m_CameraColorBufferCopy);
                }

                cmd.SetGlobalTexture(m_FrameConfig.rtConfig.textureDimension == TextureDimension.Tex2DArray ? ClusterShaderIDs._MainColorBufferArray : ClusterShaderIDs._MainColorBuffer, GetColorTexture());
                cmd.SetGlobalTexture(ClusterShaderIDs._CameraColorTexture, GetColorTexture());
                renderContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
        }

        private void RenderForwardSimple()
        {

        }

        public void OnSceneLoad()
        {
            // Recreate the textures which went NULL
            //m_MaterialList.ForEach(material => material.Build(m_Asset.renderPipelineResources));
        }

        private void RenderSky(RGCamera rgCam, CommandBuffer cmd)
        {
            m_SkyManager.RenderSky(rgCam, m_CurrentSunLight, m_CameraColorBuffer, m_CameraDepthStencilBuffer, cmd/*, m_DebugDisplaySettings*/);
            m_SkyManager.RenderOpaqueAtmosphericScattering(cmd);
        }

        private void Resize(RGCamera rgCam, PostProcessLayer postProcessLayer, bool useVolumetricLighting = true, bool useVolumetricFog = true)
        {
            if (m_FrameConfig.rtConfigChanged)
            {
                UnityEngine.Debug.Log("RT Resources allocated!");
                UnityEngine.Debug.Log("Scene Name: " + rgCam.camera.scene.name);
                UnityEngine.Debug.Log("Camera Name: " + rgCam.camera.name);
                UnityEngine.Debug.Log("Resolution: " + rgCam.CameraWidth.ToString() + "x" + rgCam.CameraHeight.ToString());
                UnityEngine.Debug.Log("Stereo Enabled: " + rgCam.StereoEnabled.ToString());
                ReallocateAllRenderTargets();
            }

            if (m_FrameConfig.clusterConfigChanged)
            {
#if UNITY_EDITOR
                useVolumetricLighting &= m_FrameConfig.rtConfig.volumetricNeedReallocate;
#endif
                //UnityEngine.Debug.Log(rgCam.camera.gameObject.name + " Resolution: " + rgCam.CameraWidth.ToString() + "x" + rgCam.CameraHeight.ToString());
                AllocateResolutionDependentClusterResources(rgCam, useVolumetricLighting, useVolumetricFog);
            }
        }

        private void ReallocateAllRenderTargets()
        {
            RTHandles.Release(m_CameraColorBuffer);
            RTHandles.Release(m_CameraDepthStencilBuffer);

            FrameRTConfigration rtC = m_FrameConfig.rtConfig;
            int texArraySlices = 1;
            if (rtC.textureDimension == TextureDimension.Tex2DArray)
                texArraySlices = 2;

            m_CameraColorBuffer = RTHandles.Alloc(width: rtC.frameWidth, height: rtC.frameHeight, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGBHalf, sRGB: false, enableRandomWrite: true, dimension: rtC.textureDimension, slices: texArraySlices, msaaSamples: rtC.msaaSamples, name: "CameraColor");
            m_CameraDepthStencilBuffer = RTHandles.Alloc(width: rtC.frameWidth, height: rtC.frameHeight, filterMode: FilterMode.Point, depthBufferBits: DepthBits.Depth24, colorFormat: RenderTextureFormat.Depth, enableRandomWrite: false, dimension: rtC.textureDimension, slices: texArraySlices, msaaSamples: rtC.msaaSamples, name: "CameraDepthStencil");

            //UnityEngine.Debug.Log("RT Dimension: " + texArraySlices.ToString());

            if (m_VelocityBuffer != null && m_VelocityBuffer.rt)
            {
                RTHandles.Release(m_VelocityBuffer);
                m_VelocityBuffer = RTHandles.Alloc(width: rtC.frameWidth, height: rtC.frameHeight, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.RGHalf, enableRandomWrite: true, dimension: rtC.textureDimension, slices: texArraySlices, sRGB: false, msaaSamples: rtC.msaaSamples, name: "Velocity");
            }

            if(m_R8Buffer != null && m_R8Buffer.rt)
            {
                RTHandles.Release(m_R8Buffer);
                m_R8Buffer = RTHandles.Alloc(width: rtC.frameWidth, height: rtC.frameHeight, filterMode: FilterMode.Bilinear, colorFormat: RenderTextureFormat.R8, enableRandomWrite: true, dimension: TextureDimension.Tex2D, slices: 1, msaaSamples: MSAASamples.None, name: "R8Buffer");
            }

            if (UnityEngine.Debug.isDebugBuild)
            {
                RTHandles.Release(m_DebugColorPickerBuffer);
                RTHandles.Release(m_DebugFullScreenTempBuffer);
                m_DebugColorPickerBuffer = RTHandles.Alloc(width: rtC.frameWidth, height: rtC.frameHeight, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGBHalf, sRGB: false, name: "DebugColorPicker");
                m_DebugFullScreenTempBuffer = RTHandles.Alloc(width: rtC.frameWidth, height: rtC.frameHeight, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGBHalf, sRGB: false, name: "DebugFullScreen");
            }

            if(m_ScreenSpaceShadowBuffer != null && m_ScreenSpaceShadowBuffer.rt)
            {

                RTHandles.Release(m_ScreenSpaceShadowBuffer);
                m_ScreenSpaceShadowBuffer = RTHandles.Alloc(width: rtC.frameWidth, height: rtC.frameHeight, filterMode: FilterMode.Bilinear, colorFormat: RenderTextureFormat.R8, enableRandomWrite: true, dimension: rtC.textureDimension, slices: texArraySlices, msaaSamples: MSAASamples.None, name: "ScreenSpaceShadow");
            }


            if (NeedDepthBufferCopy())
            {
                if (m_CameraDepthBufferCopy != null && m_CameraDepthBufferCopy.rt)
                    RTHandles.Release(m_CameraDepthBufferCopy);

                if (m_CameraColorBufferCopy != null && m_CameraColorBufferCopy.rt)
                    RTHandles.Release(m_CameraColorBufferCopy);

                m_CameraDepthBufferCopy = RTHandles.Alloc(width: rtC.frameWidth, height: rtC.frameHeight, filterMode: FilterMode.Point, depthBufferBits: DepthBits.Depth24, colorFormat: RenderTextureFormat.Depth, enableRandomWrite: false, dimension: rtC.textureDimension, slices: texArraySlices, msaaSamples: rtC.msaaSamples, name: "CameraDepthCopy");
                m_CameraColorBufferCopy = RTHandles.Alloc(width: rtC.frameWidth, height: rtC.frameHeight, filterMode: FilterMode.Point, depthBufferBits: DepthBits.Depth24, colorFormat: RenderTextureFormat.ARGBHalf, enableRandomWrite: false, dimension: rtC.textureDimension, slices: texArraySlices, msaaSamples: rtC.msaaSamples, name: "CameraColorCopy");
            }
        }

        private void PrepareLightsData(RGCamera rgCam, CommandBuffer cmd, ScriptableRenderContext renderContext)
        {
            if (m_FrameConfig.enableClusterLighting)
            {
                // Note: Legacy Unity behave like this for ShadowMask
                // When you select ShadowMask in Lighting panel it recompile shaders on the fly with the SHADOW_MASK keyword.
                // However there is no C# function that we can query to know what mode have been select in Lighting Panel and it will be wrong anyway. Lighting Panel setup what will be the next bake mode. But until light is bake, it is wrong.
                // Currently to know if you need shadow mask you need to go through all visible lights (of CullResult), check the LightBakingOutput struct and look at lightmapBakeType/mixedLightingMode. If one light have shadow mask bake mode, then you need shadow mask features (i.e extra Gbuffer).
                // It mean that when we build a standalone player, if we detect a light with bake shadow mask, we generate all shader variant (with and without shadow mask) and at runtime, when a bake shadow mask light is visible, we dynamically allocate an extra GBuffer and switch the shader.
                // So the first thing to do is to go through all the light: PrepareLightsForGPU
                bool enableBakeShadowMask = m_LightManager.PrepareLightsDataForGPU(cmd, m_ShadowSettings, m_cullResults, rgCam.camera, m_FrameConfig.enableLightCullingMask);
                //ConfigureForShadowMask(enableBakeShadowMask, cmd);
                //ClusterLightAssignmentCompute(rgCam, m_cullResults, cmd, renderContext);
            }
        }

        private void UpdateSkyEnvironment(RGCamera rgCam, CommandBuffer cmd)
        {

        }

        private GPUFence ClusterLightAssignmentComputeAsync(RGCamera rgCam, CullResults cullResults, ScriptableRenderContext renderContext, GPUFence startFence)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Cluster light assignment async");
            cmd.WaitOnGPUFence(startFence);
            m_LightManager.ClusterLightCompute(rgCam, cmd);
            RenderGradientResolution(rgCam, cmd, renderContext);
            GPUFence completeFence = cmd.CreateGPUFence();
            renderContext.ExecuteCommandBufferAsync(cmd, ComputeQueueType.Background);
            CommandBufferPool.Release(cmd);
            return completeFence;
        }

        private GPUFence VolumetricLightComputeAsync(RGCamera rgCam, ScriptableRenderContext renderContext, GPUFence startFence)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Volumetric light compute async");
            cmd.WaitOnGPUFence(startFence);
            m_LightManager.VolumetricLightCompute(rgCam, cmd, m_VolumetricSettings, m_FrameConfig.enableStaticShadowmap);
            GPUFence completeFence = cmd.CreateGPUFence();
            renderContext.ExecuteCommandBufferAsync(cmd, ComputeQueueType.Background);
            CommandBufferPool.Release(cmd);
            return completeFence;
        }

        private GPUFence VolumetricFogComputeAsync(RGCamera rgCam, ScriptableRenderContext renderContext, GPUFence startFence)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Volumetric fog compute async");
            cmd.WaitOnGPUFence(startFence);
            m_LightManager.VolumetricLightComputePost(rgCam, cmd, m_VolumetricSettings);
            GPUFence completeFence = cmd.CreateGPUFence();
            renderContext.ExecuteCommandBufferAsync(cmd, ComputeQueueType.Background);
            CommandBufferPool.Release(cmd);
            return completeFence;
        }

        private void OpaqueOnlyPostProcess(CommandBuffer cmd, RGCamera rgCam, ScriptableRenderContext renderContext)
        {
            m_PostProcessCompute.ApplyOpaqueOnlyPostProcess(cmd, rgCam, m_CameraColorBuffer, m_CameraDepthStencilBuffer, m_R8Buffer);
            renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        private void PushGlobalParams(RGCamera rgCam, CommandBuffer cmd, SubsurfaceScatteringSettings sssSettings)
        {
            using (new ProfilingSample(cmd, "Push Global Parameters"))
            {
                rgCam.SetupGlobalParams(cmd, m_Time, m_LastTime);
                if (rgCam.StereoEnabled)
                    rgCam.SetupGlobalStereoParams(cmd);

                // TODO: cmd.SetGlobalInt() does not exist, so we are forced to use Shader.SetGlobalInt() instead.

                //if (m_SkyManager.IsSkyValid())
                //{
                //    m_SkyManager.SetGlobalSkyTexture();
                //    Shader.SetGlobalInt(ClusterShaderIDs._EnvLightSkyEnabled, 1);
                //}
                //else
                //{
                //    Shader.SetGlobalInt(ClusterShaderIDs._EnvLightSkyEnabled, 0);
                //}

                //// Broadcast SSS parameters to all shaders.
                //Shader.SetGlobalInt(ClusterShaderIDs._EnableSSSAndTransmission, m_CurrentDebugDisplaySettings.renderingDebugSettings.enableSSSAndTransmission ? 1 : 0);
                //Shader.SetGlobalInt(ClusterShaderIDs._TexturingModeFlags, sssParameters.texturingModeFlags);
                //Shader.SetGlobalInt(ClusterShaderIDs._TransmissionFlags, sssParameters.transmissionFlags);
                //Shader.SetGlobalInt(ClusterShaderIDs._UseDisneySSS, sssParameters.useDisneySSS ? 1 : 0);
                //cmd.SetGlobalVectorArray(ClusterShaderIDs._ThicknessRemaps, sssParameters.thicknessRemaps);
                //cmd.SetGlobalVectorArray(ClusterShaderIDs._ShapeParams, sssParameters.shapeParams);
                //cmd.SetGlobalVectorArray(ClusterShaderIDs._HalfRcpVariancesAndWeights, sssParameters.halfRcpVariancesAndWeights);
                //cmd.SetGlobalVectorArray(ClusterShaderIDs._TransmissionTints, sssParameters.transmissionTints);
            }
        }

        private void RenderGradientResolution(RGCamera rgCam, CommandBuffer cmd, ScriptableRenderContext renderContext)
        {
            if(rgCam.StereoEnabled || m_forceStencilMask)
            {
                m_PostProcessCompute.GenerateGradientResStencilMask(cmd, m_R8Buffer, rgCam, m_FrameConfig.enableGradientResolution);
            }
        }

        private void RenderDepthPrepass(CullResults cullResults, RGCamera rgCam, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            // In case of deferred renderer, we can have forward opaque material. These materials need to be render in the depth buffer to correctly build the light list.
            // And they will tag the stencil to not be lit during the deferred lighting pass.

            // Guidelines: In deferred by default there is no opaque in forward. However it is possible to force an opaque material to render in forward
            // by using the pass "ForwardOnly". In this case the .shader should not have "Forward" but only a "ForwardOnly" pass.
            // It must also have a "DepthForwardOnly" and no "DepthOnly" pass as forward material (either deferred or forward only rendering) have always a depth pass.

            // In case of forward only rendering we have a depth prepass. In case of deferred renderer, it is optional
            //bool addFullDepthPrepass = m_Asset.renderSettings.ShouldUseForwardRenderingOnly() || m_Asset.renderSettings.useDepthPrepassWithDeferredRendering;
            bool addAlphaTestOnly = !m_Asset.renderSettings.ShouldUseForwardRenderingOnly() && m_Asset.renderSettings.useDepthPrepass /*&& m_Asset.renderSettings.renderAlphaTestOnlyInDeferredPrepass*/;

            using (new ProfilingSample(cmd, addAlphaTestOnly ? "Depth PrePass alpha test" : "Depth Prepass"))
            {
                RenderStateBlock stateBlock = rgCam.StereoEnabled || m_forceStencilMask ? RGRenderStateBlock.GradientStencilStateBlock : RGRenderStateBlock.NoStencilStateBlock;
                //cmd.ClearRenderTarget(true, false, Color.black);
                cmd.SetGlobalTexture(ClusterShaderIDs._R8Buffer, m_R8Buffer);
                if(rgCam.StereoEnabled || m_forceStencilMask)
                {                    
                    cmd.DrawProcedural(Matrix4x4.identity, m_GradientStencilMaskBlitMaterial, 0, MeshTopology.Triangles, 3, 1, null);
                }
                RenderRendererList(cullResults, rgCam.camera, renderContext, cmd, m_DepthOnlyPassNames, RGRenderQueue.k_RenderQueue_AllOpaque, 0, stateBlock);
            }
        }

        private void RenderScreenSpaceShadow(RGCamera rgCam, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            if(m_ScreenSpaceShadowMaterial)
            {                
                using (new ProfilingSample(cmd, "Screen space shadow"))
                {
                    // These flags are still required in SRP or the engine won't compute previous model matrices...
                    // If the flag hasn't been set yet on this camera, motion vectors will skip a frame.
                    rgCam.camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

                    int depthSlice = rgCam.StereoEnabled && XRSettings.eyeTextureDesc.dimension == TextureDimension.Tex2DArray ? -1 : 0;

                    CoreUtils.SetRenderTarget(cmd, m_ScreenSpaceShadowBuffer, m_CameraDepthStencilBuffer, ClearFlag.None, 0, CubemapFace.Unknown, depthSlice);
                    cmd.SetGlobalTexture(ClusterShaderIDs._CameraDepthTexture, GetDepthTexture());
                    cmd.DrawProcedural(Matrix4x4.identity, m_ScreenSpaceShadowMaterial, rgCam.StereoEnabled ? 0 : 1, MeshTopology.Triangles, 3, 1, null);
                    cmd.SetGlobalTexture(ClusterShaderIDs._ScreenSpaceShadowBuffer, m_ScreenSpaceShadowBuffer);
                    renderContext.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    //PushFullScreenDebugTexture(rgCam, cmd, m_VelocityBuffer, FullScreenDebugMode.MotionVectors);
                }
            }
        }

        private void RenderVelocityPrepass(CullResults cullResults, RGCamera rgCam, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            if (!rgCam.TaaEnabled)
                return;

            using (new ProfilingSample(cmd, "Velocity Buffer Prepass"))
            {
                rgCam.camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

                int depthSlice = rgCam.StereoEnabled && XRSettings.eyeTextureDesc.dimension == TextureDimension.Tex2DArray ? -1 : 0;

                RenderStateBlock stateBlock = rgCam.StereoEnabled || m_forceStencilMask ? RGRenderStateBlock.GradientVelocityStencilStateBlock : RGRenderStateBlock.VelocityStencilStateBlock;

                if(rgCam.StereoEnabled || m_forceStencilMask)
                {
                    cmd.SetGlobalTexture(ClusterShaderIDs._R8Buffer, m_R8Buffer);
                    cmd.DrawProcedural(Matrix4x4.identity, m_GradientStencilMaskBlitMaterial, 0, MeshTopology.Triangles, 3, 1, null);
                }

                //CoreUtils.ClearRenderTarget(cmd, ClearFlag.None, CoreUtils.clearColorAllBlack);
                RenderRendererList(cullResults, rgCam.camera, renderContext, cmd, m_MotionVectorPassName, RGRenderQueue.k_RenderQueue_AllOpaque, RendererConfiguration.PerObjectMotionVectors, stateBlock);
            }
        }

        private void RenderCameraVelocity(CullResults cullResults, RGCamera rgCam, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            if (!rgCam.TaaEnabled)
                return;

            using (new ProfilingSample(cmd, "Camera Velocity"))
            {
                // These flags are still required in SRP or the engine won't compute previous model matrices...
                // If the flag hasn't been set yet on this camera, motion vectors will skip a frame.
                rgCam.camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

                int depthSlice = rgCam.StereoEnabled && XRSettings.eyeTextureDesc.dimension == TextureDimension.Tex2DArray ? -1 : 0;

                CoreUtils.SetRenderTarget(cmd, m_VelocityBuffer, m_CameraDepthStencilBuffer, ClearFlag.None, 0, CubemapFace.Unknown, depthSlice);
                //HDUtils.DrawFullScreen(cmd, hdCamera, m_CameraMotionVectorsMaterial, m_VelocityBuffer, m_CameraDepthStencilBuffer, null, 0);
                cmd.SetGlobalVector(ClusterShaderIDs._ScreenToTargetScale, new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
                cmd.DrawProcedural(Matrix4x4.identity, m_CameraMotionVectorMaterial, 0, MeshTopology.Triangles, 3, 1, null);
                PushFullScreenDebugTexture(rgCam, cmd, m_VelocityBuffer, FullScreenDebugMode.MotionVectors);
                CoreUtils.SetRenderTarget(cmd, m_CameraColorBuffer, m_CameraDepthStencilBuffer, ClearFlag.None, 0, CubemapFace.Unknown, depthSlice);
            }
        }

        private void RenderGBuffer(CullResults cullResults, Camera cam, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {

        }

        private void RenderDeferredLighting(RGCamera rgCam, CommandBuffer cmd)
        {
            //if (m_Asset.renderSettings.ShouldUseForwardRenderingOnly())
            //    return;

            //m_MRTCache2[0] = m_CameraColorBufferRT;
            //m_MRTCache2[1] = m_CameraSssDiffuseLightingBufferRT;
            //var depthTexture = GetDepthTexture();

            //var options = new LightManager.LightingPassOptions();

            //if (m_CurrentDebugDisplaySettings.renderingDebugSettings.enableSSSAndTransmission)
            //{
            //    // Output split lighting for materials asking for it (masked in the stencil buffer)
            //    options.outputSplitLighting = true;

            //    m_LightManager.RenderDeferredLighting(hdCamera, cmd, m_CurrentDebugDisplaySettings, m_MRTCache2, m_CameraDepthStencilBufferRT, depthTexture, m_DeferredShadowBuffer, options);
            //}

            //// Output combined lighting for all the other materials.
            //options.outputSplitLighting = false;

            //m_LightManager.RenderDeferredLighting(hdCamera, cmd, m_CurrentDebugDisplaySettings, m_MRTCache2, m_CameraDepthStencilBufferRT, depthTexture, m_DeferredShadowBuffer, options);
        }

        private void RenderShadow(ScriptableRenderContext renderContext, CommandBuffer cmd, CullResults cullResults)
        {
            m_LightManager.RenderShadows(renderContext, cmd, cullResults);
            renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        private void RenderStaticShadowmap(Camera cam)
        {

        }

        private void RenderForwardOpaque(CullResults cullResults, RGCamera rgCam, Camera cam, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            using (new ProfilingSample(cmd, "Render Forward"))
            {
                if (rgCam.StereoEnabled)
                    renderContext.StartMultiEye(cam);

                int depthSlice = rgCam.StereoEnabled && XRSettings.eyeTextureDesc.dimension == TextureDimension.Tex2DArray ? -1 : 0;

                cmd.SetRenderTarget(m_CameraColorBuffer, m_CameraDepthStencilBuffer, 0, CubemapFace.Unknown, depthSlice);

                // Clear RenderTarget to avoid tile initialization on mobile GPUs
                // https://community.arm.com/graphics/b/blog/posts/mali-performance-2-how-to-correctly-handle-framebuffers
                if (cam.clearFlags != CameraClearFlags.Nothing)
                {
                    bool clearDepth = (cam.clearFlags != CameraClearFlags.Nothing) && !m_FrameConfig.enableDepthPrePass && !rgCam.StereoEnabled;
                    bool clearColor = (cam.clearFlags == CameraClearFlags.Color || cam.clearFlags == CameraClearFlags.Skybox);
                    cmd.ClearRenderTarget(clearDepth, clearColor, cam.backgroundColor.linear);
                }

                RendererConfiguration settings = RendererConfiguration.PerObjectReflectionProbes | RendererConfiguration.PerObjectLightmaps | RendererConfiguration.PerObjectLightProbe;
                if (m_FrameConfig.enableLightCullingMask)
                    settings |= RendererConfiguration.ProvideLightIndices;
                RenderStateBlock block = rgCam.StereoEnabled || m_forceStencilMask ? RGRenderStateBlock.GradientStencilStateBlock : RGRenderStateBlock.NoStencilStateBlock;
                RenderRendererList(m_cullResults, rgCam.camera, renderContext, cmd, m_ForwardPassNames, RGRenderQueue.k_RenderQueue_AllOpaque, settings, block);
            }
        }

        private void RenderForwardPreFog(CullResults cullResults, RGCamera rgCam, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            RendererConfiguration settings = RendererConfiguration.PerObjectReflectionProbes | RendererConfiguration.PerObjectLightmaps | RendererConfiguration.PerObjectLightProbe;
            RenderStateBlock block = rgCam.StereoEnabled || m_forceStencilMask ? RGRenderStateBlock.GradientStencilStateBlock : RGRenderStateBlock.NoStencilStateBlock;
            RenderRendererList(m_cullResults, rgCam.camera, renderContext, cmd, m_ForwardPassNames, RGRenderQueue.k_RenderQueue_BeforeFog, settings, block);
        }

        private void RenderForwardPreRefraction(CullResults cullResults, RGCamera rgCam, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            int depthSlice = rgCam.StereoEnabled && XRSettings.eyeTextureDesc.dimension == TextureDimension.Tex2DArray ? -1 : 0;
            cmd.SetRenderTarget(m_CameraColorBuffer, m_CameraDepthStencilBuffer, 0, CubemapFace.Unknown, depthSlice);
            RendererConfiguration settings = RendererConfiguration.PerObjectReflectionProbes | RendererConfiguration.PerObjectLightmaps | RendererConfiguration.PerObjectLightProbe;
            RenderStateBlock block = rgCam.StereoEnabled || m_forceStencilMask ? RGRenderStateBlock.GradientStencilStateBlock : RGRenderStateBlock.NoStencilStateBlock;
            RenderRendererList(m_cullResults, rgCam.camera, renderContext, cmd, m_ForwardPassNames, RGRenderQueue.k_RenderQueue_PreRefraction, settings, block);
        }

        private void RenderForwardOpaqueShadow(CullResults cullResults, RGCamera rgCam, Camera cam, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            using (new ProfilingSample(cmd, "Render Static Shadowmap"))
            {
                // Clear RenderTarget to avoid tile initialization on mobile GPUs
                // https://community.arm.com/graphics/b/blog/posts/mali-performance-2-how-to-correctly-handle-framebuffers
                cmd.ClearRenderTarget(true, true, Color.black);

                renderContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var opaqueDrawSettings = new DrawRendererSettings(cam, ClusterShaderPassNames.s_ShadowCasterPixelName);
                opaqueDrawSettings.sorting.flags = SortFlags.CommonOpaque;
                RendererConfiguration settings = RendererConfiguration.PerObjectReflectionProbes | RendererConfiguration.PerObjectLightmaps | RendererConfiguration.PerObjectLightProbe;
                opaqueDrawSettings.rendererConfiguration = settings;

                var opaqueFilterSettings = new FilterRenderersSettings(true)
                {
                    renderQueueRange = RenderQueueRange.opaque
                };

                renderContext.DrawRenderers(m_cullResults.visibleRenderers, ref opaqueDrawSettings, opaqueFilterSettings);
            }
        }

        private void RenderForwardTransparent(ScriptableRenderContext renderContext, RGCamera rgCam, CommandBuffer cmd)
        {
            if (rgCam.StereoEnabled)
                renderContext.StartMultiEye(rgCam.camera);

            int depthSlice = rgCam.StereoEnabled && XRSettings.eyeTextureDesc.dimension == TextureDimension.Tex2DArray ? -1 : 0;

            cmd.SetRenderTarget(m_CameraColorBuffer, m_CameraDepthStencilBuffer, 0, CubemapFace.Unknown, depthSlice);

            RendererConfiguration settings = RendererConfiguration.PerObjectReflectionProbes | RendererConfiguration.PerObjectLightmaps | RendererConfiguration.PerObjectLightProbe;
            RenderStateBlock stateBlock = (rgCam.StereoEnabled || m_forceStencilMask) ? RGRenderStateBlock.GradientStencilStateBlock : RGRenderStateBlock.NoStencilStateBlock;
            RenderRendererList(m_cullResults, rgCam.camera, renderContext, cmd, m_ForwardPassNames, RGRenderQueue.k_RenderQueue_Transparent, settings, stateBlock);

            if (m_FrameConfig.enableGradientResolution)
            {
                m_PostProcessCompute.ExpandMaskedBuffer(cmd, m_R8Buffer, m_CameraColorBuffer, m_VelocityBuffer, m_CameraDepthStencilBuffer, rgCam);
                renderContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
            
            RenderRendererList(m_cullResults, rgCam.camera, renderContext, cmd, m_ForwardPassNames, RGRenderQueue.k_RenderQueue_AfterGradiant, settings, stateBlock);

            if (rgCam.StereoEnabled)
                renderContext.StopMultiEye(rgCam.camera);
                
        }

        private void RenderForwardAfterPostProcess(ScriptableRenderContext renderContext, RGCamera rgCam, CommandBuffer cmd)
        {
            if (rgCam.StereoEnabled && XRSettings.eyeTextureDesc.dimension == TextureDimension.Tex2DArray)
                cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, 0, CubemapFace.Unknown, -1);
            
            RenderRendererList(m_cullResults, rgCam.camera, renderContext, cmd, m_ForwardPassNames, RGRenderQueue.k_RenderQueue_AfterPostEffect, 0, RGRenderStateBlock.NoStencilStateBlock);
        }

        private void EndForwardRendering(RGCamera rgCam, Camera mainCam, CommandBuffer cmd, ScriptableRenderContext renderContext, PostProcessLayer postProcessLayer)
        {
            if (rgCam.StereoEnabled)
                renderContext.StartMultiEye(rgCam.camera);

            if (m_FrameConfig.enablePostprocess)
            {
                RenderPostProcess(rgCam, cmd, postProcessLayer);
            }

            RenderForwardAfterPostProcess(renderContext, rgCam, cmd);
            
            if(!m_FrameConfig.enablePostprocess)
            {
                if (rgCam.StereoEnabled && XRSettings.eyeTextureDesc.dimension == TextureDimension.Tex2DArray)
                {
                    cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, 0, CubemapFace.Unknown, -1);
                    cmd.Blit(m_CameraColorBuffer, BuiltinRenderTextureType.CurrentActive);
                }
                else
                    cmd.Blit(m_CameraColorBuffer, BuiltinRenderTextureType.CameraTarget);
            }


            renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            if (rgCam.StereoEnabled)
            {
                renderContext.StopMultiEye(mainCam);
                renderContext.StereoEndRender(mainCam);
            }
        }
        
        private void AllocateResolutionDependentClusterResources(RGCamera rgCam, bool useVolumetricLighting, bool useVolumetricFog)
        {
            m_LightManager.AllocateResolutionDependentResources(rgCam.ClusterFrustumWidth, rgCam.ClusterFrustumHeight, m_FrameConfig.clusterConfig, useVolumetricLighting, useVolumetricFog);
        }

        private void InitAndClearBuffer(RGCamera rgCam, bool enableBakeShadowMask, CommandBuffer cmd)
        {

        }

        private void RenderPostProcess(RGCamera rgCam, CommandBuffer cmd, PostProcessLayer layer)
        {
            using (new ProfilingSample(cmd, "Post-processing"))
            {
                if (layer == null)
                    return;

                RenderTargetIdentifier source = m_CameraColorBuffer;

                bool tempHACK = !IsConsolePlatform();

                if (tempHACK)
                {
                    // TEMPORARY:
                    // Since we don't render to the full render textures, we need to feed the post processing stack with the right scale/bias.
                    // This feature not being implemented yet, we'll just copy the relevant buffers into an appropriately sized RT.
                    cmd.ReleaseTemporaryRT(ClusterShaderIDs._CameraDepthTexture);
                    cmd.ReleaseTemporaryRT(ClusterShaderIDs._CameraMotionVectorsTexture);
                    cmd.ReleaseTemporaryRT(ClusterShaderIDs._CameraColorTexture);

                    cmd.GetTemporaryRT(ClusterShaderIDs._CameraDepthTexture, rgCam.CameraWidth, rgCam.CameraHeight, m_CameraDepthStencilBuffer.rt.depth, FilterMode.Point, m_CameraDepthStencilBuffer.rt.format);
                    m_CopyDepthMaterial.SetTexture(ClusterShaderIDs._InputDepth, m_CameraDepthStencilBuffer);
                    cmd.Blit(null, ClusterShaderIDs._CameraDepthTexture, m_CopyDepthMaterial);
                    if (m_VelocityBuffer != null)
                    {
                        cmd.GetTemporaryRT(ClusterShaderIDs._CameraMotionVectorsTexture, rgCam.CameraWidth, rgCam.CameraHeight, 0, FilterMode.Point, m_VelocityBuffer.rt.format);
                        cmd.Blit(m_VelocityBuffer, ClusterShaderIDs._CameraMotionVectorsTexture);
                    }

                    cmd.GetTemporaryRT(ClusterShaderIDs._CameraColorTexture, rgCam.CameraWidth, rgCam.CameraHeight, 0, FilterMode.Point, m_CameraColorBuffer.rt.format);
                    cmd.Blit(m_CameraColorBuffer, ClusterShaderIDs._CameraColorTexture);
                    source = ClusterShaderIDs._CameraColorTexture;
                }
                else
                {
                    cmd.SetGlobalTexture(ClusterShaderIDs._CameraDepthTexture, m_CameraDepthStencilBuffer);
                    cmd.SetGlobalTexture(ClusterShaderIDs._CameraMotionVectorsTexture, m_VelocityBuffer);
                }

                var context = rgCam.postProcessRenderContext;
                context.Reset();
                context.source = source;
                context.destination = BuiltinRenderTextureType.CameraTarget;
                context.command = cmd;
                context.camera = rgCam.camera;
                context.sourceFormat = RenderTextureFormat.ARGBHalf;
                context.flip = rgCam.camera.targetTexture == null;
                if (rgCam.StereoEnabled && m_FrameConfig.rtConfig.textureDimension == TextureDimension.Tex2DArray)
                    context.flip = false;

                layer.Render(context);
            }
        }

        private void RenderRendererList(CullResults cullResults,
                                            Camera cam,
                                            ScriptableRenderContext renderContext,
                                            CommandBuffer cmd,
                                            ShaderPassName passName,
                                            RenderQueueRange inRenderQueueRange,
                                            RendererConfiguration rendererConfig = 0,
                                            RenderStateBlock? stateBlock = null,
                                            Material overrideMaterial = null)
        {
            m_SinglePassName[0] = passName;
            RenderRendererList(cullResults, cam, renderContext, cmd, m_SinglePassName, inRenderQueueRange, rendererConfig, stateBlock, overrideMaterial);
        }

        private void RenderRendererList(CullResults cullResults,
                                            Camera cam,
                                            ScriptableRenderContext renderContext,
                                            CommandBuffer cmd,
                                            ShaderPassName[] passNames,
                                            RenderQueueRange inRenderQueueRange,
                                            RendererConfiguration rendererConfig = 0,
                                            RenderStateBlock? stateBlock = null,
                                            Material overrideMaterial = null)
        {
            //if (!m_CurrentDebugDisplaySettings.renderingDebugSettings.displayOpaqueObjects)
            //    return;

            // This is done here because DrawRenderers API lives outside command buffers so we need to make call this before doing any DrawRenders
            renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            var drawSettings = new DrawRendererSettings(cam, ClusterShaderPassNames.s_EmptyName)
            {
                rendererConfiguration = rendererConfig,
                sorting = { flags = SortFlags.CommonOpaque }
            };

            for (int i = 0; i < passNames.Length; ++i)
            {
                drawSettings.SetShaderPassName(i, passNames[i]);
            }

            if (overrideMaterial != null)
                drawSettings.SetOverrideMaterial(overrideMaterial, 0);

            var filterSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = inRenderQueueRange
            };

            if (stateBlock == null)
                renderContext.DrawRenderers(cullResults.visibleRenderers, ref drawSettings, filterSettings);
            else
                renderContext.DrawRenderers(cullResults.visibleRenderers, ref drawSettings, filterSettings, stateBlock.Value);

        }


        public void PushFullScreenDebugTexture(RGCamera rgCam, CommandBuffer cmd, RTHandleSystem.RTHandle textureID, FullScreenDebugMode debugMode)
        {
            if(debugMode == m_CurrentDebugDisplaySettings.fullScreenDebugMode)
            {
                m_FullScreenDebugPushed = true;
                ClusterUtils.BlitCameraTexture(cmd, rgCam, textureID, m_DebugFullScreenTempBuffer);
            }
        }

        private void RenderDebug(RGCamera rgCam, CommandBuffer cmd)
        {
            if (rgCam.camera.cameraType == CameraType.Reflection || rgCam.camera.cameraType == CameraType.Preview)
                return;

            using (new ProfilingSample(cmd, "RenderDebug"))
            {
                if(m_CurrentDebugDisplaySettings.fullScreenDebugMode != FullScreenDebugMode.None && m_FullScreenDebugPushed)
                {
                    m_FullScreenDebugPushed = false;
                    cmd.SetGlobalTexture(ClusterShaderIDs._DebugFullScreenTexture, m_DebugFullScreenTempBuffer);
                    m_DebugFullScreen.SetFloat(ClusterShaderIDs._FullScreenDebugMode, (float)m_CurrentDebugDisplaySettings.fullScreenDebugMode);
                    m_DebugFullScreen.SetFloat(ClusterShaderIDs._RequireToFlipInputTexture, rgCam.camera.cameraType != CameraType.SceneView ? 1.0f : 0.0f);
                    ClusterUtils.DrawFullScreen(cmd, rgCam, m_DebugFullScreen, (RenderTargetIdentifier)BuiltinRenderTextureType.CameraTarget);
                }

                // Then overlays
                float x = 0;
                float overlayRatio = m_CurrentDebugDisplaySettings.debugOverlayRatio;
                float overlaySize = Math.Min(rgCam.CameraWidth, rgCam.CameraHeight) * overlayRatio;
                float y = rgCam.CameraHeight - overlaySize;

                m_LightManager.RenderDebugOverlay(rgCam, cmd, m_CurrentDebugDisplaySettings, ref x, ref y, overlaySize, rgCam.CameraWidth);
            }
        }

        private void CopyTexture(CommandBuffer cmd, RenderTargetIdentifier sourceRT, RenderTargetIdentifier destRT, Material copyMaterial, bool forceBlit = false)
        {
            if (m_CopyTextureSupport != CopyTextureSupport.None && !forceBlit)
                cmd.CopyTexture(sourceRT, destRT);
            else
                cmd.Blit(sourceRT, destRT, copyMaterial);
        }
    }

}
