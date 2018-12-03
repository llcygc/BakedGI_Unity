using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    //class ShadowManager : IDisposable
    //{
    //    ShadowSettings m_shadowSettings;
    //    public ShadowSettings shadowSettings
    //    {
    //        get { return m_shadowSettings; }
    //        set { m_shadowSettings = value; }
    //    }

    //    public void UpdateCullingParams(ref ScriptableCullingParameters cullingParams)
    //    {
    //        cullingParams.shadowDistance = Mathf.Min(cullingParams.shadowDistance, m_shadowSettings.maxShadowDistance);
    //    }

    //    public void Dispose()
    //    {

    //    }
    //}

    class ShadowSetup : IDisposable
    {
        //Shadow related stuff
        const int k_MaxShadowDataSlots = 64;
        const int k_MaxPayloadSlotsPerShadowData = 4;
        ShadowmapBase[] m_Shadowmaps;
        CachedShadowManager m_ShadowMgr;
        static ComputeBuffer s_ShadowDataBuffer;
        static ComputeBuffer s_ShadowPayloadBuffer;

        public static GPUShadowType ClusterShadowLightType(Light l)
        {
            var ald = l.GetComponent<ClusterAdditionalLightData>();

            if (ald == null)
                return ShadowRegistry.ShadowLightType(l);

            GPUShadowType shadowType = GPUShadowType.Unknown;

            switch(ald.lightTypeExtent)
            {
                case LightTypeExtent.Punctual:
                    shadowType = ShadowRegistry.ShadowLightType(l);
                    break;
                
                //Area and projector not supported yet
            }

            return shadowType;
        }

        public ShadowSetup(ClusterRenderPipelineResources resources, ShadowInitParameters shadowInit, CachedShadowSettings shadowSettings, out CachedShadowManager shadowManager)
        {
            s_ShadowDataBuffer = new ComputeBuffer(k_MaxShadowDataSlots, System.Runtime.InteropServices.Marshal.SizeOf(typeof(ShadowData)));
            s_ShadowPayloadBuffer = new ComputeBuffer(k_MaxShadowDataSlots * k_MaxPayloadSlotsPerShadowData, System.Runtime.InteropServices.Marshal.SizeOf(typeof(ShadowPayload)));

            ShadowAtlas.AtlasInit expInit;
            expInit.baseInit.width = (uint)shadowInit.shadowAtlasWidth;
            expInit.baseInit.height = (uint)shadowInit.shadowAtlasHeight;
            expInit.baseInit.slices = 1;
            expInit.baseInit.shadowmapBits = 32;
            expInit.baseInit.shadowmapFormat = RenderTextureFormat.RFloat;
            expInit.baseInit.samplerState = SamplerState.Default();
            expInit.baseInit.comparisonSamplerState = ComparisonSamplerState.Default();
            expInit.baseInit.clearColor = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
            expInit.baseInit.maxPayloadCount = 0;
            expInit.baseInit.shadowSupport = ShadowmapBase.ShadowSupport.Directional | ShadowmapBase.ShadowSupport.Point | ShadowmapBase.ShadowSupport.Spot;
            expInit.shaderKeyword = null;
            expInit.shadowClearShader = resources.shadowClearShader;
            expInit.shadowBlurMoments = resources.TextureBlurCS;

            m_Shadowmaps = new ShadowmapBase[] { new ShadowAtlasExp(ref expInit) };

            ShadowContext.SyncDel syncer = (ShadowContext sc) =>
            {
                //update buffers
                uint offset, count;
                ShadowData[] sds;
                sc.GetShadowDatas(out sds, out offset, out count);
                Debug.Assert(offset == 0);
                s_ShadowDataBuffer.SetData(sds);
                ShadowPayload[] payloads;
                sc.GetPayloads(out payloads, out offset, out count);
                Debug.Assert(offset == 0);
                s_ShadowPayloadBuffer.SetData(payloads);
            };

            ShadowContext.BindDel binder = (ShadowContext sc, CommandBuffer cb, ComputeShader computeShader, int computeKernel) =>
            {
                uint offset, count;
                RenderTargetIdentifier[] tex;
                sc.GetTex2DArrays(out tex, out offset, out count);

                if (computeShader)
                {
                    //bind buffers
                    cb.SetComputeBufferParam(computeShader, computeKernel, ClusterShaderIDs._ShadowDatasExp, s_ShadowDataBuffer);
                    cb.SetComputeBufferParam(computeShader, computeKernel, ClusterShaderIDs._ShadowPayloads, s_ShadowPayloadBuffer);
                    //bind textures
                    cb.SetComputeTextureParam(computeShader, computeKernel, ClusterShaderIDs._ShadowmapExp_VSM_0, tex[0]);
                    cb.SetComputeTextureParam(computeShader, computeKernel, ClusterShaderIDs._ShadowmapExp_VSM_1, tex[1]);
                    cb.SetComputeTextureParam(computeShader, computeKernel, ClusterShaderIDs._ShadowmapExp_VSM_2, tex[2]);
                    cb.SetComputeTextureParam(computeShader, computeKernel, ClusterShaderIDs._ShadowmapExp_PCF, tex[3]);
                }
                else
                {
                    //bind buffers
                    cb.SetGlobalBuffer(ClusterShaderIDs._ShadowDatasExp, s_ShadowDataBuffer);
                    cb.SetGlobalBuffer(ClusterShaderIDs._ShadowPayloads, s_ShadowPayloadBuffer);
                    //bind textures
                    cb.SetGlobalTexture(ClusterShaderIDs._ShadowmapExp, tex[0]);
                    //cb.SetGlobalTexture(ClusterShaderIDs._ShadowmapPCF, tex[0]);
                }
            };

            ShadowContext.CtxtInit scInit;
            scInit.storage.maxShadowDataSlots = k_MaxShadowDataSlots;
            scInit.storage.maxPayloadSlots = k_MaxShadowDataSlots * k_MaxPayloadSlotsPerShadowData;
            scInit.storage.maxTex2DArraySlots = 4;
            scInit.storage.maxTexCubeArraySlots = 4;
            scInit.storage.maxComparisonSamplerSlots = 1;
            scInit.storage.maxSamplerSlots = 4;
            scInit.dataSyncer = syncer;
            scInit.resourceBinder = binder;

            m_ShadowMgr = new CachedShadowManager(shadowSettings, ref scInit, m_Shadowmaps);
            // set global overrides - these need to match the override specified in ShadowDispatch.hlsl
            bool useGlobalOverrides = true;
            m_ShadowMgr.SetGlobalShadowOverride(GPUShadowType.Point, ShadowAlgorithm.Custom, ShadowVariant.V0, ShadowPrecision.High, useGlobalOverrides);
            m_ShadowMgr.SetGlobalShadowOverride(GPUShadowType.Spot, ShadowAlgorithm.Custom, ShadowVariant.V0, ShadowPrecision.High, useGlobalOverrides);
            m_ShadowMgr.SetGlobalShadowOverride(GPUShadowType.Directional, ShadowAlgorithm.Custom, ShadowVariant.V0, ShadowPrecision.High, useGlobalOverrides);

            m_ShadowMgr.SetShadowLightTypeDelegate(ClusterShadowLightType);

            shadowManager = m_ShadowMgr;
        }
        
        public void Dispose()
        {
            if (m_Shadowmaps != null)
            {
                (m_Shadowmaps[0] as ShadowAtlas).Dispose();
                m_Shadowmaps = null;
            }
            m_ShadowMgr = null;

            if (s_ShadowDataBuffer != null)
                s_ShadowDataBuffer.Release();
            if (s_ShadowPayloadBuffer != null)
                s_ShadowPayloadBuffer.Release();
        }
    }

    namespace ClusterPass
    {

        [GenerateHLSL]
        public enum LightVolumeType
        {
            Cone,
            Sphere,
            Box,
            Count
        }

        [GenerateHLSL]
        public enum LightCategory
        {
            Punctual,
            Area,
            Env,
            Count
        }

        [GenerateHLSL]
        public enum LightFeatureFlags
        {
            // Light bit mask must match LightDefinitions.s_LightFeatureMaskFlags value
            Punctual = 1 << 12,
            Area = 1 << 13,
            Directional = 1 << 14,
            Env = 1 << 15,
            Sky = 1 << 16,
            SSRefraction = 1 << 17,
            SSReflection = 1 << 18,
            // If adding more light be sure to not overflow LightDefinitions.s_LightFeatureMaskFlags
        }

        [GenerateHLSL]
        public class LightDefinitions
        {
            public static int s_MaxNrLightsPerCamera = 1024;
            public static int s_MaxNrBigTileLightsPlusOne = 512;
            public static float s_ViewPortScaleZ = 1.0f;     // may be overkill but the footprint is 2 bits per pixel using uint16.

            // enable unity's original left-hand shader camera space (right-hand internally in unity).
            public static int s_UseLeftHandCameraSpace = 1;

            //public static int s_TileSizeFptl = 16;
            //public static int s_TileSizeClustered = 32;

            //feature variants
            public static int s_NumFeatureVariants = 27;

            //Following define the maximun number of bits use in each feature category
            public static uint s_LightFeatureMaskFlags = 0XFFF000;
            public static uint s_LightFeatureMaskFlagsOpaque = 0XFFF000 & ~((uint)LightFeatureFlags.SSReflection); //Opaque don's support screen space refraction
            public static uint s_LightFeatureMaskFlagsTransparent = 0XFFF000 & ~((uint)LightFeatureFlags.SSReflection); //Transparent don't support screen space reflection
            public static uint s_MaterialFeatureMaskFlags = 0X000FFF; //don't use all bits just to be safe from signed float conversions
        }

        public class VolumetricRenderManager
        {
        }

        public class LightManager
        {
            public enum TileClusterDebug : int
            {
                None,
                Tile,
                Cluster,
                MaterialFeatureVariants
            };

            public enum TileClusterCategoryDebug : int
            {
                Punctual = 1,
                Area = 2,
                AreaAndPunctual = 3,
                Environment = 4,
                EnvironmentAndPunctual = 5,
                EnvironmentAndArea = 6,
                EnvironmentAndAreaAndPunctual = 7,
                Decal = 8
            };

            public const int k_MaxDirectionalLightsOnScreen = 4;
            public const int k_MaxPunctualLightsOnScreen = 512;
            public const int k_MaxAreaLightsOnScreen = 64;
            public const int k_MaxLightsOnScreen = k_MaxDirectionalLightsOnScreen + k_MaxDirectionalLightsOnScreen + k_MaxAreaLightsOnScreen;
            public const int k_MaxEvnLightOnScreen = 64;
            public const int k_MaxShadowOnScreen = 16;
            public const int k_MaxCascadeCount = 4; //Should be not less than m_Settings.directionalLightCascadeCount;

            private int CULLING_CLUSTER_X = 0;
            private int CULLING_CLUSTER_Y = 0;

            private int LIGHTFOG_CLUSTER_X = 0;
            private int LIGHTFOG_CLUSTER_Y = 0;

            private int ALIGNED_RES_X = 0;
            private int ALIGNED_RES_Y = 0;

            private int CLUSTER_DEPTH = 0;

            private Matrix4x4 m_ClusterMatrixLinearZLastFrame;

            //TO DO-------------------
            //Original parameters need to move to more suitable places
            float m_blendWeight;
            uint HaltonIndex = 0;
            //x - Max height; y - Min height; z - Density param count; w - Not used yet
            //Depth slice constant/compute buffer
            ComputeBuffer m_clusterDepthSliceBuffer;
            float[] m_DepthSlices;
            private bool needUpdateMedia = false;

            //Cluster lighting params and buffers
            // Static keyword is required here else we get a "DestroyBuffer can only be call in main thread"
            static private ComputeBuffer s_DirectionalLightsSimpleBuffer = null;
            //Punctual and area lights data
            static private ComputeBuffer s_LightsGeoBuffer = null; // Only for point and spot beacuse now thats all we need
            static private ComputeBuffer s_LightsCullingVolumeBuffer = null; //TO DO: Use for more general and complex local lights, use light proxy mesh other than simple shape parameters for culling
            static private ComputeBuffer s_LightsRenderingBuffer = null; //Actual data for rendering like color, distance fall off, shadow, etc

            static private ComputeBuffer s_ClusterStartOffsetIndexBuffer = null;
            static private ComputeBuffer s_ClusterLightLinkListBuffer = null;

            static private ComputeBuffer s_LightIndexBuffer;

            private Vector4[] m_DepthSliceArray;

            //Shadow related
            static private ComputeBuffer s_ShadowData = null;
            private bool m_enableBakeShadowMask = false;

            //Volumetric light/fog resources
            private RenderTexture m_ClusterVolumetricLightingBuffer;
            private RenderTexture m_ClusterVolumetricLightingBufferLastFrame;
            static private RenderTexture m_ClusterFogBuffer;

            private bool isFirstTime = false;

            static Texture2DArray m_DefaultTexture2DArray;

            TextureCacheCubemap m_CubeReflTexArray;
            int m_CubeReflTexArraySize = 128;
            TextureCache2D m_CookieTexArray;
            int m_CookieTexArraySize = 16;
            TextureCacheCubemap m_CubeCookieTexArray;
            int m_CubeCookieTexArraySize = 16;

            Material m_DebugViewTilesMaterial;

            public class LightList
            {
                public List<DirectionalLightDataSimple> directionalLightsSimple;
                public List<PunctualLightGeoData> punctualLightsGeo;
                public List<PunctualLightRenderingData> punctualLightsRenderieng;
                public List<AreaLightData> areaLights;
                public List<ShadowData> shadows;
                public Vector4[] directionalShadowSplitSphereSqr;

                public void Clear()
                {
                    directionalLightsSimple.Clear();
                    punctualLightsGeo.Clear();
                    punctualLightsRenderieng.Clear();
                    areaLights.Clear();
                    shadows.Clear();
                }

                public void Allocate()
                {
                    directionalLightsSimple = new List<DirectionalLightDataSimple>();
                    punctualLightsGeo = new List<PunctualLightGeoData>();
                    punctualLightsRenderieng = new List<PunctualLightRenderingData>();
                    areaLights = new List<AreaLightData>();
                    shadows = new List<ShadowData>();
                    directionalShadowSplitSphereSqr = new Vector4[k_MaxCascadeCount];
                }
            }

            private LightList m_LightList;
            private int m_punctualLightCount = 0;
            private int m_areaLightCount = 0;
            private int m_directionalLightCount = 0;
            private bool enableShadowMask = false; //Track if any light require shadow mask. In this case we will need to enable the keyword shadow mask
            private float m_maxShadowDistance = 0.0f;
            private bool m_justResized = true;

            //Shadow related stuff
            FrameId m_FrameId = new FrameId();
            CachedShadowManager m_ShadowMgr;
            ShadowSetup m_ShadowSetup; // doesn't actually have to reside here, it would be enough to pass the IShadowManager in from the outside
            List<int> m_ShadowRequests = new List<int>();
            Dictionary<int, int> m_ShadowIndices = new Dictionary<int, int>();

            FrameClusterConfigration m_clusterSettings;
            ClusterRenderPipelineResources m_Resources = null;

            private ComputeShader clusterLightAssignmetShader { get { return m_Resources.clusterLightAssignmentCS; } }
            private ComputeShader volumetricLightingShader { get { return m_Resources.volumetricLightingCS; } }
            private ComputeShader volumetricFogMediaShader { get { return m_Resources.volumetricFogMediaCS; } }
            private ComputeShader volumetricFogAccumulateShader { get { return m_Resources.volumetricFogAccumulateCS; } }

            private int kernelHandleLA;
            private int kernelHandleVM;
            private int kernelHandleVM_Local;
            private int kernelHandleVL;
            private int kernelHandleRP;
            private int kernelHandleVF;

            // This is a workaround for global properties not being accessible from compute.
            // When activeComputeShader is set, all calls to SetGlobalXXX will set the property on the select compute shader instead of the global scope.
            private ComputeShader activeComputeShader;
            private int activeComputeKernel;
            private CommandBuffer activeCommandBuffer;
            private void SetGlobalPropertyRedirect(ComputeShader computeShader, int computeKernel, CommandBuffer commandBuffer)
            {
                activeComputeShader = computeShader;
                activeComputeKernel = computeKernel;
                activeCommandBuffer = commandBuffer;
            }

            Light m_CurrentSunLight;
            int m_CurrentSunLightShadowIndex = -1;
            public Light GetCurrentSunLight() { return m_CurrentSunLight; }

            public LightManager()
            {

            }

            public void NewFrame()
            {

            }

            public bool AllocateResolutionDependentResources(int clusterWidth, int clusterHeight, FrameClusterConfigration clusterSettings, bool useVolumetricLighting = false, bool useVolumetricFog = false)
            {
                //Debug.Log("Cluster Resources allocated!");
                //UnityEngine.Debug.Log("Cluster Frustum Width:" + clusterWidth.ToString());
                m_clusterSettings = clusterSettings;

                CULLING_CLUSTER_X = clusterWidth / (int)m_clusterSettings.cullingClusterSize;
                CULLING_CLUSTER_Y = clusterHeight / (int)m_clusterSettings.cullingClusterSize;

                ALIGNED_RES_X = clusterWidth / 128;
                ALIGNED_RES_Y = clusterHeight / 128;

                CLUSTER_DEPTH = (int)m_clusterSettings.clusterDepthSlices;

                int cullingClusterCount = CULLING_CLUSTER_X * CULLING_CLUSTER_Y * CLUSTER_DEPTH;
                int lightLinkListCount = cullingClusterCount * (int)m_clusterSettings.maxLightsPerCluster;

                uint[] clusterStartOffsetIndex = new uint[cullingClusterCount];
                uint[] clusterLightLinkList = new uint[lightLinkListCount];

                for (int i = 0; i < cullingClusterCount; i++)
                    clusterStartOffsetIndex[i] = 0xffffffff;

                for (int i = 0; i < lightLinkListCount; i++)
                    clusterLightLinkList[i] = 0xffffffff;

                //Cluster culling resources
                if (s_ClusterStartOffsetIndexBuffer != null)
                    s_ClusterStartOffsetIndexBuffer.Release();
                s_ClusterStartOffsetIndexBuffer = new ComputeBuffer(cullingClusterCount, sizeof(uint), ComputeBufferType.Raw);
                s_ClusterStartOffsetIndexBuffer.SetData(clusterStartOffsetIndex);

                if (s_ClusterLightLinkListBuffer != null)
                    s_ClusterLightLinkListBuffer.Release();
                s_ClusterLightLinkListBuffer = new ComputeBuffer(lightLinkListCount, sizeof(uint), ComputeBufferType.Counter);
                s_ClusterLightLinkListBuffer.SetData(clusterLightLinkList);

                if (s_ClusterStartOffsetIndexBuffer == null || s_ClusterLightLinkListBuffer == null)
                    return false;

                if (useVolumetricLighting)
                {
                    m_justResized = true;

                    LIGHTFOG_CLUSTER_X = clusterWidth / (int)m_clusterSettings.lightFogClusterSize;
                    LIGHTFOG_CLUSTER_Y = clusterHeight / (int)m_clusterSettings.lightFogClusterSize;

                    //Light & fog resources
                    if (m_ClusterVolumetricLightingBuffer != null)
                        m_ClusterVolumetricLightingBuffer.Release();
                    m_ClusterVolumetricLightingBuffer = new RenderTexture(LIGHTFOG_CLUSTER_X, LIGHTFOG_CLUSTER_Y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    m_ClusterVolumetricLightingBuffer.dimension = TextureDimension.Tex3D;
                    m_ClusterVolumetricLightingBuffer.name = "VolumetricLightingTexture";
                    m_ClusterVolumetricLightingBuffer.hideFlags = HideFlags.HideAndDontSave;
                    m_ClusterVolumetricLightingBuffer.useMipMap = false;
                    m_ClusterVolumetricLightingBuffer.filterMode = FilterMode.Trilinear;
                    m_ClusterVolumetricLightingBuffer.wrapMode = TextureWrapMode.Clamp;
                    m_ClusterVolumetricLightingBuffer.enableRandomWrite = true;
                    m_ClusterVolumetricLightingBuffer.volumeDepth = (int)m_clusterSettings.clusterDepthSlices;
                    m_ClusterVolumetricLightingBuffer.Create();

                    if (m_ClusterVolumetricLightingBufferLastFrame != null)
                        m_ClusterVolumetricLightingBufferLastFrame.Release();
                    m_ClusterVolumetricLightingBufferLastFrame = new RenderTexture(LIGHTFOG_CLUSTER_X, LIGHTFOG_CLUSTER_Y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    m_ClusterVolumetricLightingBufferLastFrame.dimension = TextureDimension.Tex3D;
                    m_ClusterVolumetricLightingBufferLastFrame.name = "VolumetricLightingTextureLastFrame";
                    m_ClusterVolumetricLightingBufferLastFrame.hideFlags = HideFlags.HideAndDontSave;
                    m_ClusterVolumetricLightingBufferLastFrame.useMipMap = false;
                    m_ClusterVolumetricLightingBufferLastFrame.filterMode = FilterMode.Trilinear;
                    m_ClusterVolumetricLightingBufferLastFrame.wrapMode = TextureWrapMode.Clamp;
                    m_ClusterVolumetricLightingBufferLastFrame.enableRandomWrite = true;
                    m_ClusterVolumetricLightingBufferLastFrame.volumeDepth = (int)m_clusterSettings.clusterDepthSlices;
                    m_ClusterVolumetricLightingBufferLastFrame.Create();

                    if (m_ClusterVolumetricLightingBuffer == null || m_ClusterVolumetricLightingBufferLastFrame == null)
                        return false;

                    m_ClusterVolumetricLightingBuffer.SetGlobalShaderProperty("_VolumetricLightingTexture");

                    if (useVolumetricFog)
                    {
                        if (m_ClusterFogBuffer != null)
                            m_ClusterFogBuffer.Release();
                        m_ClusterFogBuffer = new RenderTexture(LIGHTFOG_CLUSTER_X, LIGHTFOG_CLUSTER_Y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                        m_ClusterFogBuffer.dimension = TextureDimension.Tex3D;
                        m_ClusterFogBuffer.name = "VolumetricFogTexture";
                        m_ClusterFogBuffer.hideFlags = HideFlags.HideAndDontSave;
                        m_ClusterFogBuffer.useMipMap = false;
                        m_ClusterFogBuffer.filterMode = FilterMode.Trilinear;
                        m_ClusterFogBuffer.wrapMode = TextureWrapMode.Clamp;
                        m_ClusterFogBuffer.enableRandomWrite = true;
                        m_ClusterFogBuffer.volumeDepth = (int)m_clusterSettings.clusterDepthSlices;
                        m_ClusterFogBuffer.Create();

                        if (m_ClusterFogBuffer == null)
                            return false;

                        m_ClusterFogBuffer.SetGlobalShaderProperty("_VolumetricFogTexture");
                    }
                }

                return true;
            }

            private ClusterAdditionalLightData GetClusterAdditionalLightData(VisibleLight light)
            {
                if (light.light == null)
                    return ClusterUtils.s_DefaultClusterAdditionLightData;
                else
                {
                    var add = light.light.GetComponent<ClusterAdditionalLightData>();
                    if (add == null)
                    {
                        add = ClusterUtils.s_DefaultClusterAdditionLightData;
                    }
                    return add;
                }
            }

            public bool PrepareLightsDataForGPU(CommandBuffer cmd, CachedShadowSettings shadowSettings, CullResults cullResults, Camera cam, bool enableLightCullingMask)
            {
                //If any light requires it, we need to enalbe bake shadow mask feature
                m_enableBakeShadowMask = false;

                m_LightList.Clear();

                Vector3 camPosWS = cam.transform.position;

                //Note: Light with null intensity/color are culled by the C++, no need to test it here
                if (cullResults.visibleLights.Count != 0 || cullResults.visibleReflectionProbes.Count != 0)
                {
                    //0. deal with shadows
                    {
                        m_FrameId.frameCount++;
                        //get the indices for all lights that want to have shadows
                        //----!!!ShadowCache need to be done
                        m_ShadowRequests.Clear();
                        m_ShadowRequests.Capacity = cullResults.visibleLights.Count;
                        int lcnt = cullResults.visibleLights.Count;
                        for (int i = 0; i < lcnt; i++)
                        {
                            VisibleLight vl = cullResults.visibleLights[i];
                            if (vl.light == null || vl.light.shadows == LightShadows.None)
                                continue;

                            AdditionalShadowData asd = vl.light.GetComponent<AdditionalShadowData>();
                            if (asd != null && asd.shadowDimmer > 0.0f && vl.light.type != LightType.Point)
                                m_ShadowRequests.Add(i);
                        }
                        //pass this list to a routine that assigns shadows based on some heuristic
                        uint shadowRequestCount = (uint)m_ShadowRequests.Count;
                        //TODO: Do not call ToArray here to avoid GC, refractor API
                        int[] shadowRequests = m_ShadowRequests.ToArray();
                        int[] shadowDataIndices;
                        m_ShadowMgr.UpdateShadowSettings(shadowSettings);
                        m_ShadowMgr.ProcessShadowRequests(m_FrameId, cullResults, cam, false/*ShaderConfig.s_CameraRelativeRendering != 0*/, cullResults.visibleLights,
                            ref shadowRequestCount, shadowRequests, out shadowDataIndices);

                        //update the visibleLights with the shadow information
                        m_ShadowIndices.Clear();
                        for (uint i = 0; i < shadowRequestCount; i++)
                            m_ShadowIndices.Add(shadowRequests[i], shadowDataIndices[i]);
                    }

                    float oldSpecularGlobalDimmer = 0;//m_TileSettings.specularGlobalDimmer;
                                                      // Change some parameters in case of "special" rendering (can be preview, reflection, etc.)
                    if (cam.cameraType == CameraType.Reflection)
                    {
                        //m_TileSettings.specularGlobalDimmer = 0.0f;
                    }

                    // 1. Count the number of lights and sort all lights by category, type and volume - This is required for the fptl/cluster shader code
                    // If we reach maximum of lights available on screen, then we discard the light.
                    // Lights are processed in order, so we don't discards light based on their importance but based on their ordering in visible lights list.
                    int directionalLightCount = 0;
                    int punctualLightCount = 0;
                    int areaLightCount = 0;

                    int lightCount = Math.Min(cullResults.visibleLights.Count, k_MaxAreaLightsOnScreen);
                    var sortKeys = new uint[lightCount];
                    int sortCount = 0;

                    for (int lightIndex = 0, numLights = cullResults.visibleLights.Count; (lightIndex < numLights) && (sortCount < lightCount); ++lightIndex)
                    {
                        var light = cullResults.visibleLights[lightIndex];

                        //We only process light with additional data
                        var additionalData = GetClusterAdditionalLightData(light);

                        LightCategory lightCategory = LightCategory.Count;
                        GPULightType gpuLightType = GPULightType.Point;
                        LightVolumeType lightVolumeType = LightVolumeType.Count;

                        if(additionalData.lightTypeExtent == LightTypeExtent.Punctual)
                        {
                            lightCategory = LightCategory.Punctual;

                            switch (light.lightType)
                            {
                                case LightType.Spot:
                                    if (punctualLightCount >= k_MaxPunctualLightsOnScreen)
                                        continue;
                                    switch(additionalData.spotLightShape)
                                    {
                                        case SpotLightShape.Cone:
                                            gpuLightType = GPULightType.Spot;
                                            lightVolumeType = LightVolumeType.Cone;
                                            break;
                                        case SpotLightShape.Pyramid:
                                            gpuLightType = GPULightType.ProjectorPyramid;
                                            lightVolumeType = LightVolumeType.Cone;
                                            break;
                                        case SpotLightShape.Box:
                                            gpuLightType = GPULightType.ProjectorBox;
                                            lightVolumeType = LightVolumeType.Box;
                                            break;
                                        default:
                                            Debug.Assert(false, "Encountered an unknown SpotLightShape.");
                                            break;
                                    }
                                    break;

                                case LightType.Directional:
                                    if (directionalLightCount >= k_MaxDirectionalLightsOnScreen)
                                        continue;
                                    gpuLightType = GPULightType.Directional;
                                    lightVolumeType = LightVolumeType.Count;
                                    break;

                                case LightType.Point:
                                    if (punctualLightCount >= k_MaxPunctualLightsOnScreen)
                                        continue;
                                    gpuLightType = GPULightType.Point;
                                    lightVolumeType = LightVolumeType.Sphere;
                                    break;

                                default:
                                    Debug.Assert(false, "Encountered an unknown LightType.");
                                    break;

                            }
                        }
                        else
                        {
                            lightCategory = LightCategory.Area;

                            switch(additionalData.lightTypeExtent)
                            {
                                case LightTypeExtent.Rectangle:
                                    if (areaLightCount >= k_MaxAreaLightsOnScreen)
                                        continue;
                                    gpuLightType = GPULightType.Rectangle;
                                    lightVolumeType = LightVolumeType.Box;
                                    break;
                                case LightTypeExtent.Line:
                                    if (areaLightCount >= k_MaxAreaLightsOnScreen)
                                        continue;
                                    gpuLightType = GPULightType.Line;
                                    lightVolumeType = LightVolumeType.Box;
                                    break;

                                default:
                                    Debug.Assert(false, "Encountered an unknown LightType.");
                                    break;
                            }
                        }

                        uint shadow = m_ShadowIndices.ContainsKey(lightIndex) ? 1u : 0;
                        sortKeys[sortCount++] = (uint)lightCategory << 27 | (uint)gpuLightType << 22 | (uint)lightVolumeType << 17 | shadow << 16 | (uint)lightIndex;
                    }

                    CoreUtils.QuickSort(sortKeys, 0, sortCount - 1); //Call our own quicksort instead of Array.Sort(sortKeys, 0, sortCount) so we don't allocate memory (note the SortCount-1 that is different from original call).

                    // TODO: Refractor shadow management
                    // The good way of managing shadow:
                    // Here we sort everyone and we decide which light is important or not (this is the responsibility of the light loop)
                    // We allocate shadow slot based on maximum shadow allowed on screen and attribute slot by bigger solid angle
                    // Then we ask to the ShadowRender to render the shadow, not the reverse as it is today (i.e render shadow than expect they will be use...)
                    // The light manager is in charge, not the shadow pass
                    // For now we will still apply the maximun of shadow here but we don't apply the sorting by priority + slot allocation yet
                    // m_current

                    //Go through all lights, convet them to GPU format.
                    //Create simultaneously data for culling (LightVolumeData and rendering)
                    var worldToView = WorldToCamera(cam);

                    for(int sortIndex = 0; sortIndex < sortCount; ++sortIndex )
                    {
                        uint sortKey = sortKeys[sortIndex];
                        LightCategory lightCategory = (LightCategory)((sortKey >> 27) & 0X1F);
                        GPULightType gpuLightType = (GPULightType)((sortKey >> 22) & 0X1F);
                        LightVolumeType lightVolumeType = (LightVolumeType)((sortKey >> 17) & 0x1F);
                        int lightIndex = (int)(sortKey & 0XFFFF);

                        var light = cullResults.visibleLights[lightIndex];

                        m_enableBakeShadowMask = m_enableBakeShadowMask || IsBakedShadowMaskLight(light.light);

                        var additionalLightData = GetClusterAdditionalLightData(light);
                        var additionalShadowData = light.light == null ? null : light.light.GetComponent<AdditionalShadowData>();

                        if(gpuLightType == GPULightType.Directional)
                        {
                            if(GetDirectionalLightDataSimple(shadowSettings, gpuLightType, light, additionalLightData, additionalShadowData, lightIndex))
                            {
                                directionalLightCount++;

                                // We make the light position camera-relative as late as possible in order
                                // to allow the preceding code to work with the absolute world space coordinates.
                                //if(ShaderConfig.s_CameraRelativeRendering != 0)
                                //{
                                //    // Caution: 'DirectionalLightData.positionWS' is camera-relative after this point.
                                //    int n = m_LightList.directionalLights.Count;
                                //    DirectionalLightData lightData = m_LightList.directionalLights[n - 1];
                                //    lightData.positionWS -= camPosWS;
                                //    m_LightList.directionalLights[n - 1] = lightData;
                                //}
                            }
                            continue;
                        }

                        //Punctual, area, projector lights - the rendering side.
                        if(GetPunctualLightData(cmd, shadowSettings, cam, gpuLightType, light, additionalLightData, additionalShadowData, lightIndex))
                        {
                            switch(lightCategory)
                            {
                                case LightCategory.Punctual:
                                    punctualLightCount++;
                                    break;
                                case LightCategory.Area:
                                    areaLightCount++;
                                    break;
                                default:
                                    Debug.Assert(false, "TODO: encountered an unknown LightCategory.");
                                    break;
                            }

                            // Then culling side. Must be call in this order as we pass the created Light data to the function
                            GetLightVolumeDataAndBound(); //TO DO: Currently only use analytic methods for culling because we use point and spot light only
                        }
                    }

                    //Setup per object light information for Light culling mask
                    if (enableLightCullingMask)
                    {
                        int[] perObjectLightIndexMap = cullResults.GetLightIndexMap();
                        int dirLightCount = 0;
                        // Disable all directional lights from the perobject light indices
                        // Pipeline handles them globally
                        for (int i = 0; i < cullResults.visibleLights.Count; ++i)
                        {
                            VisibleLight light = cullResults.visibleLights[i];
                            if (light.lightType == LightType.Directional)
                            {
                                perObjectLightIndexMap[i] = -1;
                                ++dirLightCount;
                            }
                            else
                                perObjectLightIndexMap[i] -= dirLightCount;
                        }

                        cullResults.SetLightIndexMap(perObjectLightIndexMap);

                        int lightIndicesCount = cullResults.GetLightIndicesCount();
                        if (lightIndicesCount > 0)
                        {
                            if (s_LightIndexBuffer == null)
                            {
                                s_LightIndexBuffer = new ComputeBuffer(lightIndicesCount, sizeof(int));
                            }
                            else if (s_LightIndexBuffer.count < lightIndicesCount)
                            {
                                s_LightIndexBuffer.Release();
                                s_LightIndexBuffer = new ComputeBuffer(lightIndicesCount, sizeof(int));
                            }

                            cullResults.FillLightIndices(s_LightIndexBuffer);
                        }
                    }
                }

                UpdateBufferData();

                return true;
            }

            public bool GetDirectionalLightDataSimple(CachedShadowSettings shadowSettings, GPULightType gpuLightType, VisibleLight light, ClusterAdditionalLightData additionalData, AdditionalShadowData additionalShadowData, int lightIndex)
            {
                var directionalLightDataSimple = new DirectionalLightDataSimple();
                
                Vector3 spotDir = light.light.transform.forward.normalized;
                directionalLightDataSimple.ForwardCosAngle = new Vector4(spotDir.x, spotDir.y, spotDir.z, -2.0f);
                var actualColor = light.finalColor;
                directionalLightDataSimple.Color = new Vector4(actualColor.r, actualColor.g, actualColor.b, 1.0f);

                int shadowIdx = -1;
                directionalLightDataSimple.ShadowOffset.w = shadowIdx;
                if (m_ShadowIndices.TryGetValue(lightIndex, out shadowIdx))
                    directionalLightDataSimple.ShadowOffset.w = shadowIdx;

                m_CurrentSunLight = m_CurrentSunLight == null ? light.light : m_CurrentSunLight;

                m_LightList.directionalLightsSimple.Add(directionalLightDataSimple);

                return true;
            }
            
            public bool GetPunctualLightData(CommandBuffer cmd, CachedShadowSettings shadowSettings, Camera cam, GPULightType gpuLightType, VisibleLight light, ClusterAdditionalLightData additionalData, AdditionalShadowData additionalShadowData, int lightIndex)
            {
                var punctualLightGeoData = new PunctualLightGeoData();
                var punctualLightRenderingData = new PunctualLightRenderingData();

                float fInnerConePercent = additionalData == null ? 0.0f : additionalData.m_InnerSpotPercent / 100.0f;
                float fPhiDot = Mathf.Clamp(Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad), 0.0f, 1.0f); // outer cone
                float fThetaDot = Mathf.Clamp(Mathf.Cos(light.spotAngle * 0.5f * fInnerConePercent * Mathf.Deg2Rad), 0.0f, 1.0f); // inner cone

                Vector4 pos = light.localToWorld.GetColumn(3);
                Vector4 dir = light.localToWorld.GetColumn(2);
                punctualLightGeoData.PositionSizeSqr = new Vector4(pos.x, pos.y, pos.z, light.range * light.range);
                punctualLightGeoData.ForwardCosAngle = new Vector4(dir.x, dir.y, dir.z, light.lightType == LightType.Spot ? fPhiDot : -2.0f);

                punctualLightRenderingData.Color = new Vector4(light.finalColor.r, light.finalColor.g, light.finalColor.b, light.lightType == LightType.Spot ? 1.0f / Mathf.Max(0.01f, fThetaDot - fPhiDot) : 1.0f);

                int shadowIdx = -1;
                punctualLightRenderingData.ShadowIndex = shadowIdx;
                int cookieIdx = -1;
                if (m_ShadowIndices.TryGetValue(lightIndex, out shadowIdx))
                    punctualLightRenderingData.ShadowIndex = shadowIdx;

                punctualLightRenderingData.AffectVolumetricLight = 1;

                if (light.light != null)
                {
                    CachedShadowData csd = light.light.GetComponent<CachedShadowData>();
                    if (csd && !csd.AffectVolumectricFog)
                    {
                        punctualLightRenderingData.AffectVolumetricLight = 0;
                    }
                
                    if (light.light.cookie != null)
                    {
                        switch(light.lightType)
                        {
                            case LightType.Spot:
                                cookieIdx = m_CookieTexArray.FetchSlice(cmd, light.light.cookie);
                                break;
                        }
                    }
                }

                punctualLightRenderingData.CookieIndex = cookieIdx;

                m_LightList.punctualLightsGeo.Add(punctualLightGeoData);
                m_LightList.punctualLightsRenderieng.Add(punctualLightRenderingData);

                return true;
            }

            public bool GetAreaLightData(CommandBuffer cmd, ShadowSettings shadowSettings, Camera cam, GPULightType gpuLightType, VisibleLight light, ClusterAdditionalLightData additionalData, AdditionalShadowData additionalShadowData, int lightIndex)
            {
                var lightData = new AreaLightData();

                lightData.lightType = gpuLightType;
                lightData.positionWS = light.light.transform.position;
                // Setting 0 for invSqrAttenuationRadius mean we have no range attenuation, but still have inverse square attenuation.
                lightData.invSqrAttenuationRaius = additionalData.applyRangeAttenuation ? 1.0f / (light.range * light.range) : 0.0f;
                lightData.color = GetLightColor(light);

                lightData.forward = light.light.transform.forward; // Note: Light direction is oriented backward (-Z)
                lightData.up = light.light.transform.up;
                lightData.right = light.light.transform.right;

                lightData.size = new Vector2(additionalData.shapeLength, additionalData.shapeWidth);

                if(lightData.lightType == GPULightType.ProjectorBox)
                {
                    //Rescale for cookies and windowing
                    lightData.right *= 2.0f / additionalData.shapeLength;
                    lightData.up *= 2.0f / additionalData.shapeWidth;
                }
                else if(lightData.lightType == GPULightType.ProjectorPyramid)
                {
                    //Get width and height for the current frustum
                    var spotAngle = light.spotAngle;

                    float frustumHeight;
                    float frustumWidth;
                    if(additionalData.aspectRatio >= 1.0f)
                    {
                        frustumHeight = 2.0f * Mathf.Tan(spotAngle * 0.5f * Mathf.Deg2Rad);
                        frustumWidth = frustumHeight * additionalData.aspectRatio;
                    }
                    else
                    {
                        frustumWidth = 2.0f * Mathf.Tan(spotAngle * 0.5f * Mathf.Deg2Rad);
                        frustumHeight = frustumWidth / additionalData.aspectRatio;
                    }

                    lightData.size = new Vector2(frustumWidth, frustumHeight);

                    //Rescale for cookies and windowing
                    lightData.right *= 2.0f / frustumWidth;
                    lightData.up *= 2.0f / frustumHeight;
                }

                if(lightData.lightType == GPULightType.Spot)
                {
                    var spotAngle = light.spotAngle;

                    var innerConePerfect = additionalData.GetInnerSpotPercent01();
                    var cosSpotOuterHalfAngle = Mathf.Clamp(Mathf.Cos(spotAngle * 0.5f * Mathf.Deg2Rad), 0.0f, 1.0f);
                    var sinSpotOuterHalfAngle = Mathf.Sqrt(1.0f - cosSpotOuterHalfAngle * cosSpotOuterHalfAngle);
                    var cosSpotInnerHalfAngle = Mathf.Clamp(Mathf.Cos(spotAngle * 0.5f * innerConePerfect * Mathf.Deg2Rad), 0.0f, 1.0f);

                    var val = Mathf.Max(0.001f, (cosSpotInnerHalfAngle - cosSpotOuterHalfAngle));
                    lightData.angleScale = 1.0f / val;
                    lightData.angleOffset = -cosSpotOuterHalfAngle * lightData.angleScale;

                    //Rescale for cookies and windowing
                    float cotOuterHalfAngle = cosSpotOuterHalfAngle / sinSpotOuterHalfAngle;
                    lightData.up *= cotOuterHalfAngle;
                    lightData.right *= cotOuterHalfAngle;
                }
                else
                {
                    lightData.angleScale = 0.0f;
                    lightData.angleOffset = 1.0f;
                }

                float distanceToCamera = (lightData.positionWS - cam.transform.position).magnitude;
                float distanceFade = ComputeLinearDistanceFade(distanceToCamera, additionalData.fadeDistance);
                float lightScale = additionalData.lightDimmer * distanceFade;

                lightData.diffuseScale = 0.0f;
                lightData.specularScale = 0.0f;

                if (lightData.diffuseScale <= 0.0f && lightData.specularScale <= 0.0f)
                    return false;

                lightData.cookieIndex = -1;
                lightData.shadowIndex = -1;

                if(light.light.cookie != null)
                {
                    //TODO: add texture atlas supprot for cookie textures
                    switch(light.lightType)
                    {
                        case LightType.Spot:
                            lightData.cookieIndex = m_CookieTexArray.FetchSlice(cmd, light.light.cookie);
                            break;
                        case LightType.Point:
                            lightData.cookieIndex = m_CubeCookieTexArray.FetchSlice(cmd, light.light.cookie);
                            break;
                    }
                }
                else if(light.lightType == LightType.Spot && additionalData.spotLightShape != SpotLightShape.Cone)
                {
                    lightData.cookieIndex = m_CookieTexArray.FetchSlice(cmd, Texture2D.whiteTexture);
                }

                if(additionalShadowData)
                {
                    float shadowDistanceFade = ComputeLinearDistanceFade(distanceToCamera, additionalShadowData.shadowFadeDistance);
                    lightData.shadowDimmer = additionalShadowData.shadowDimmer * shadowDistanceFade;
                }
                else
                {
                    lightData.shadowDimmer = 1.0f;
                }

                int shadowIdx;
                if (m_ShadowIndices.TryGetValue(lightIndex, out shadowIdx))
                    lightData.shadowIndex = shadowIdx;

                // Value of max smoothness is from artists point of view, need to convert from perceptual smoothness to roughness
                lightData.minRoughness = (1.0f - additionalData.maxSmoothness) * (1.0f - additionalData.maxSmoothness);
                lightData.shadowMaskSelector = Vector4.zero;

                if(IsBakedShadowMaskLight(light.light))
                {
                    lightData.shadowMaskSelector[light.light.bakingOutput.occlusionMaskChannel] = 1.0f;
                    //TODO: make this option per light, not global
                    lightData.dynamicShadowCasterOnly = QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask;
                }
                else
                {
                    lightData.shadowMaskSelector.x = -1.0f;
                    lightData.dynamicShadowCasterOnly = false;
                }

                m_LightList.areaLights.Add(lightData);

                return true;
            }

            public void GetLightVolumeDataAndBound()
            {

            }

            public int GetCurrentShadowCount()
            {
                return m_ShadowRequests.Count;
            }

            public int GetShadowAtlasCount()
            {
                return (m_ShadowMgr == null) ? 0 : (int)m_ShadowMgr.GetShadowMapCount();
            }

            float ComputeLinearDistanceFade(float distanceToCamera, float fadeDistance)
            {
                float scale;
                float bias;
                GetScaleBiasForLinearDistanceFade(fadeDistance, out scale, out bias);

                return 1.0f - Mathf.Clamp01(distanceToCamera * scale + bias);
            }

            void GetScaleBiasForLinearDistanceFade(float fadeDistance, out float scale, out float bias)
            {
                float distanceFadeNear = 0.9f * fadeDistance;
                scale = 1.0f / (fadeDistance - distanceFadeNear);
                bias = -distanceFadeNear / (fadeDistance - distanceFadeNear);
            }

            private void InitShadowSystem(ClusterRenderPipelineResources resources, ShadowInitParameters shadowInit, CachedShadowSettings shadowSettings)
            {
                m_ShadowSetup = new ShadowSetup(resources, shadowInit, shadowSettings, out m_ShadowMgr);
            }

            //shadowInit - shadow atlas resolution; shadowsettings - shadow distance etc
            public void Build(ClusterRenderPipelineResources renderPipelineResources, FrameClusterConfigration clusterSettings, ShadowInitParameters shadowInit, CachedShadowSettings shadowSettings)
            {
                m_Resources = renderPipelineResources;
                m_clusterSettings = clusterSettings;

                m_DebugViewTilesMaterial = CoreUtils.CreateEngineMaterial(m_Resources.debugViewTilesShader);

                m_LightList = new LightList();
                m_LightList.Allocate();
                
                s_DirectionalLightsSimpleBuffer = new ComputeBuffer(k_MaxDirectionalLightsOnScreen, System.Runtime.InteropServices.Marshal.SizeOf(typeof(DirectionalLightDataSimple)));
                s_LightsGeoBuffer = new ComputeBuffer(k_MaxPunctualLightsOnScreen, System.Runtime.InteropServices.Marshal.SizeOf(typeof(PunctualLightGeoData)));
                s_LightsRenderingBuffer = new ComputeBuffer(k_MaxPunctualLightsOnScreen, System.Runtime.InteropServices.Marshal.SizeOf(typeof(PunctualLightRenderingData)));
                s_ShadowData = new ComputeBuffer(k_MaxCascadeCount + k_MaxShadowOnScreen, System.Runtime.InteropServices.Marshal.SizeOf(typeof(ShadowData)));

                m_CookieTexArray = new TextureCache2D("Cookie");
                m_CookieTexArray.AllocTextureArray(5, 256, 256, TextureFormat.R8, false);
#if UNITY_EDITOR
                UnityEditor.SceneView.onSceneGUIDelegate -= OnSceneGUI;
                UnityEditor.SceneView.onSceneGUIDelegate += OnSceneGUI;
#endif

                InitShadowSystem(renderPipelineResources, shadowInit, shadowSettings);
            }

            public void ClusterLightCompute(RGCamera rgCam, CommandBuffer cmd)
            {
                kernelHandleLA = clusterLightAssignmetShader.FindKernel("LightAssignment");

                rgCam.SetupComputeShader(clusterLightAssignmetShader, cmd);

                cmd.SetComputeIntParam(clusterLightAssignmetShader, ClusterShaderIDs._PunctualLightCount, m_LightList.punctualLightsGeo.Count);
                //if (isFirstTime)
                //{
                if (s_ClusterStartOffsetIndexBuffer == null)
                    Debug.Log("ClusterStartOffsetIndexIn is null!!!!!");
                cmd.SetComputeBufferParam(clusterLightAssignmetShader, kernelHandleLA, "ClusterStartOffsetIndex", s_ClusterStartOffsetIndexBuffer);
                //}

                s_ClusterLightLinkListBuffer.SetCounterValue(0);

                if ((!rgCam.StereoEnabled) || rgCam.StereoCamDist > 0)
                {
                    int depthArrayCount = (CLUSTER_DEPTH + 1) * 4;
                    float n = rgCam.NearClipPlane;
                    float f = rgCam.FarClipPlane;
                    
                    bool needRecalculateDepthSlice = (m_DepthSlices == null) || (m_DepthSlices.Length != depthArrayCount) || (!m_DepthSlices[0].Equals(n / f));

                    if (needRecalculateDepthSlice)
                    {
                        m_DepthSlices = new float[depthArrayCount];
                        for (int i = 0; i <= CLUSTER_DEPTH; i++)
                        {
                            m_DepthSlices[i * 4] = Mathf.Pow(f / n, (float)i / CLUSTER_DEPTH - 1);
                            m_DepthSlices[i * 4 + 1] = 0;
                            m_DepthSlices[i * 4 + 2] = 0;
                            m_DepthSlices[i * 4 + 3] = 0;
                        }
                        
                        cmd.SetComputeFloatParams(clusterLightAssignmetShader, "ClusterDepthSlicesArray", m_DepthSlices);
                    }

                    //Set light and culling params to CS
                    cmd.SetComputeVectorParam(clusterLightAssignmetShader, "CameraPos", rgCam.ClusterCameraPos);
                    cmd.SetComputeVectorParam(clusterLightAssignmetShader, "CullingClusterParams", new Vector4(CULLING_CLUSTER_X, CULLING_CLUSTER_Y, CLUSTER_DEPTH, CULLING_CLUSTER_X * CULLING_CLUSTER_Y * CLUSTER_DEPTH * m_clusterSettings.maxLightsPerCluster));
                    cmd.SetComputeBufferParam(clusterLightAssignmetShader, kernelHandleLA, "ClusterLightsLinkList", s_ClusterLightLinkListBuffer);
                    cmd.SetComputeBufferParam(clusterLightAssignmetShader, kernelHandleLA, "AllLightList", s_LightsGeoBuffer);

                    cmd.DispatchCompute(clusterLightAssignmetShader, kernelHandleLA, CULLING_CLUSTER_X, CULLING_CLUSTER_Y, 1);

                }
            }

            public void VolumetricLightCompute(RGCamera rgCam, CommandBuffer cmd, VolumetricSettings vSettings, bool useStaticShadowMap)
            {
                if (useStaticShadowMap)
                    kernelHandleVL = volumetricLightingShader.FindKernel("VolumetricLighting");
                else
                    kernelHandleVL = volumetricLightingShader.FindKernel("VolumetricLighting_Dynamic");

                kernelHandleVM = volumetricFogMediaShader.FindKernel("VolumetricFogMedia");
                kernelHandleVM_Local = volumetricFogMediaShader.FindKernel("VolumetricFogMedia_WithLocalVolume");
                VolumetricManager.CollectVolumeEffectsData(rgCam.camera);
                if (VolumetricManager.volumeEffectCount > 0)
                {
                    kernelHandleVM = volumetricFogMediaShader.FindKernel("VolumetricFogMedia_WithLocalVolume");
                    VolumetricManager.BindParams(cmd, volumetricFogMediaShader, kernelHandleVM);
                }

                rgCam.SetupComputeShader(volumetricLightingShader, cmd);
                rgCam.SetupComputeShader(volumetricFogMediaShader, cmd);

                cmd.SetComputeBufferParam(volumetricLightingShader, kernelHandleVL, ClusterShaderIDs._DirectionalLightDatasSimple, s_DirectionalLightsSimpleBuffer);
                cmd.SetComputeIntParam(volumetricLightingShader, ClusterShaderIDs._DirectionalLightCount, m_LightList.directionalLightsSimple.Count);

                cmd.SetComputeBufferParam(volumetricLightingShader, kernelHandleVL, "ShadowData", s_ShadowData);

                cmd.SetComputeBufferParam(volumetricLightingShader, kernelHandleVL, "ClusterStartOffsetIndexIn", s_ClusterStartOffsetIndexBuffer);
                cmd.SetComputeTextureParam(volumetricLightingShader, kernelHandleVL, "VolumetricLightingTexture", m_ClusterVolumetricLightingBuffer);

                cmd.SetComputeFloatParams(volumetricLightingShader, "ClusterDepthSlicesArray", m_DepthSlices);
                cmd.SetComputeFloatParams(volumetricFogAccumulateShader, "ClusterDepthSlicesArray", m_DepthSlices);
                cmd.SetComputeFloatParams(volumetricFogMediaShader, "ClusterDepthSlicesArray", m_DepthSlices);

                cmd.SetComputeTextureParam(volumetricFogMediaShader, kernelHandleVM, "VolumetricLightingTexture", m_ClusterVolumetricLightingBuffer);

                if (isFirstTime)
                {
                    m_ClusterMatrixLinearZLastFrame = rgCam.MVPMatrixLinearZ;
                    isFirstTime = false;
                }

                cmd.SetComputeFloatParam(volumetricLightingShader, "blendWeight", m_blendWeight);

                cmd.SetComputeVectorParam(volumetricLightingShader, "CameraPos", rgCam.ClusterCameraPos);
                cmd.SetComputeVectorParam(volumetricLightingShader, "CullingClusterParams", new Vector4(CULLING_CLUSTER_X, CULLING_CLUSTER_Y, CLUSTER_DEPTH, CULLING_CLUSTER_X * CULLING_CLUSTER_Y));
                cmd.SetComputeVectorParam(volumetricLightingShader, "FogClusterParams", new Vector4(LIGHTFOG_CLUSTER_X, LIGHTFOG_CLUSTER_Y, CLUSTER_DEPTH, LIGHTFOG_CLUSTER_X * LIGHTFOG_CLUSTER_Y));

                //WindZone wind = FindObjectOfType<WindZone>() as WindZone;
                Vector4 flow = new Vector4(Time.timeSinceLevelLoad * 3, 0, 0, 0);
                Vector4 globalFogParam = new Vector4(vSettings.MinMaxHeight.value.x, vSettings.MinMaxHeight.value.y, vSettings.BaseDensity.value, vSettings.ScatterIntensity.value);
                Vector4 haltonSeq = new Vector4(HaltonSampler.RadicalInverse(2, HaltonIndex), HaltonSampler.RadicalInverse(3, HaltonIndex), HaltonSampler.RadicalInverse(5, HaltonIndex), 0);
                cmd.SetComputeVectorParam(volumetricLightingShader, "HaltonSequence", haltonSeq);

                cmd.SetComputeBufferParam(volumetricLightingShader, kernelHandleVL, "ClusterLightsLinkListIn", s_ClusterLightLinkListBuffer);
                cmd.SetComputeBufferParam(volumetricLightingShader, kernelHandleVL, "AllLightList", s_LightsGeoBuffer);
                cmd.SetComputeBufferParam(volumetricLightingShader, kernelHandleVL, "AllLightsRenderingData", s_LightsRenderingBuffer);

                Vector4 VeclocityandPhase = new Vector4(vSettings.Veclocity.value.x, vSettings.Veclocity.value.y, vSettings.Veclocity.value.z, Mathf.Min(vSettings.PhaseFunction.value, 0.9999f));
                Vector4 NoiseScale = new Vector4(vSettings.NoiseScale.value.x, vSettings.NoiseScale.value.y, vSettings.NoiseScale.value.z, vSettings.UseNoise.value ? 1 : 0);
                cmd.SetComputeMatrixParam(volumetricLightingShader, "Cluster_Matrix_LinearZ_LastFrame", m_ClusterMatrixLinearZLastFrame);
                cmd.SetComputeVectorParam(volumetricLightingShader, "GlobalFogParams", globalFogParam);
                cmd.SetComputeVectorParam(volumetricLightingShader, "ambientColor", vSettings.AmbientColor.value);
                cmd.SetComputeVectorParam(volumetricLightingShader, "velocityPhg", VeclocityandPhase);
                cmd.SetComputeVectorParam(volumetricLightingShader, "backfaceColor", vSettings.BackFaceColor.value);
#if UNITY_EDITOR
                if(Application.isPlaying)
                {
                    cmd.SetComputeFloatParams(volumetricLightingShader, "FogMediaGradient", vSettings.GetFogMediaArrayDynamic());
                    cmd.SetComputeFloatParams(volumetricFogMediaShader, "FogMediaGradient", vSettings.GetFogMediaArrayDynamic());
                }
                else
                {
                    cmd.SetComputeFloatParams(volumetricLightingShader, "FogMediaGradient", vSettings.GetFogMediaArrayDynamic());
                    cmd.SetComputeFloatParams(volumetricFogMediaShader, "FogMediaGradient", vSettings.GetFogMediaArrayDynamic());
                }
#else
                cmd.SetComputeFloatParams(volumetricLightingShader, "FogMediaGradient", vSettings.GetFogMediaArrayDynamic());
                cmd.SetComputeFloatParams(volumetricFogMediaShader, "FogMediaGradient", vSettings.GetFogMediaArrayDynamic());
#endif

                cmd.SetComputeVectorParam(volumetricFogMediaShader, "CameraPos", rgCam.ClusterCameraPos);
                cmd.SetComputeMatrixParam(volumetricFogMediaShader, "Cluster_Matrix_LinearZ_LastFrame", m_ClusterMatrixLinearZLastFrame);
                cmd.SetComputeVectorParam(volumetricFogMediaShader, "FogClusterParams", new Vector4(LIGHTFOG_CLUSTER_X, LIGHTFOG_CLUSTER_Y, CLUSTER_DEPTH, LIGHTFOG_CLUSTER_X * LIGHTFOG_CLUSTER_Y));
                cmd.SetComputeVectorParam(volumetricFogMediaShader, "GlobalFogParams", globalFogParam);
                cmd.SetComputeVectorParam(volumetricFogMediaShader, "HaltonSequence", haltonSeq);
                cmd.SetComputeVectorParam(volumetricFogMediaShader, "ambientColor", vSettings.AmbientColor.value);
                cmd.SetComputeVectorParam(volumetricFogMediaShader, "noiseScale", NoiseScale);
                cmd.SetComputeVectorParam(volumetricFogMediaShader, "velocityPhg", VeclocityandPhase);
                cmd.SetComputeVectorParam(volumetricFogMediaShader, "noiseClampRange", vSettings.NoiseDensityClamp.value);
                cmd.SetComputeFloatParam(volumetricFogMediaShader, "_TimeFog", Time.timeSinceLevelLoad);
                
                cmd.DispatchCompute(volumetricFogMediaShader, kernelHandleVM, ALIGNED_RES_X * 2, ALIGNED_RES_Y * 2, 8);
                cmd.DispatchCompute(volumetricLightingShader, kernelHandleVL, ALIGNED_RES_X * 2, ALIGNED_RES_Y * 2, 8);

                cmd.SetComputeVectorParam(volumetricFogAccumulateShader, "FogClusterParams", new Vector4(LIGHTFOG_CLUSTER_X, LIGHTFOG_CLUSTER_Y, CLUSTER_DEPTH, LIGHTFOG_CLUSTER_X * LIGHTFOG_CLUSTER_Y));
                
                m_ClusterMatrixLinearZLastFrame = rgCam.MVPMatrixLinearZ;

                if (HaltonIndex == int.MaxValue)
                    HaltonIndex = 0;
                else
                    HaltonIndex++;
            }

            public void VolumetricLightComputePost(RGCamera rgCam, CommandBuffer cmd, VolumetricSettings vSettings)
            {
                kernelHandleRP = volumetricLightingShader.FindKernel("VolumetricReprojection");
                kernelHandleVF = 0;

#if (UNITY_EDITOR)
                kernelHandleVF = volumetricFogAccumulateShader.FindKernel("VolumetricFog");
#else
                kernelHandleVF = volumetricFogAccumulateShader.FindKernel("VolumetricFog");//VolumetricFogShader.FindKernel("VolumetricFog_1080");
#endif
                cmd.SetComputeTextureParam(volumetricLightingShader, kernelHandleRP, "VolumetricLightingTexture", m_ClusterVolumetricLightingBuffer);
                cmd.SetComputeTextureParam(volumetricLightingShader, kernelHandleRP, "VolumetricLightingTextureLastFrame", m_ClusterVolumetricLightingBufferLastFrame);
                if(!m_justResized)
                {
                    cmd.DispatchCompute(volumetricLightingShader, kernelHandleRP, ALIGNED_RES_X * 2, ALIGNED_RES_Y * 2, 8);
                }
                m_justResized = false;

                cmd.SetComputeTextureParam(volumetricFogAccumulateShader, kernelHandleVF, "VolumetricLightingTexture", m_ClusterVolumetricLightingBuffer);
                cmd.SetComputeTextureParam(volumetricFogAccumulateShader, kernelHandleVF, "VolumetricFogTexture", m_ClusterFogBuffer);
                cmd.SetComputeTextureParam(volumetricFogAccumulateShader, kernelHandleVF, "VolumetricLightingTextureLastFrame", m_ClusterVolumetricLightingBufferLastFrame);
                cmd.DispatchCompute(volumetricFogAccumulateShader, kernelHandleVF, ALIGNED_RES_X, ALIGNED_RES_Y, 1);
            }

            public void PostBlurExpShadows(CommandBuffer cmd, int blurMethod)
            {
                m_ShadowMgr.PostBlurExpShadows(cmd, blurMethod);
            }

            public void PushGlobalParams(RGCamera rgCam, CommandBuffer cmd, ComputeShader computeShader, int kernelIndex, bool forceClustered = false)
            {
                using (new ProfilingSample(cmd, "Pubsh Global Parameters"))
                {
                    //Shadows
                    m_ShadowMgr.SyncData();

                    SetGlobalPropertyRedirect(computeShader, kernelIndex, cmd);
                    BindGlobalParams(cmd, rgCam, forceClustered);
                    SetGlobalPropertyRedirect(null, 0, null);
                }
            }

            public void UpdateCullingParameters(ref ScriptableCullingParameters cullingParams, bool enableLightCullingMask)
            {
                m_ShadowMgr.UpdateCullingParameters(ref cullingParams);
                //if (!enableLightCullingMask)
                //    cullingParams.cullingFlags |= CullFlag.DisablePerObjectCulling;
            }

            private void UpdateBufferData()
            {
                s_DirectionalLightsSimpleBuffer.SetData(m_LightList.directionalLightsSimple);
                s_LightsGeoBuffer.SetData(m_LightList.punctualLightsGeo);
                s_LightsRenderingBuffer.SetData(m_LightList.punctualLightsRenderieng);
            }

            private void BindGlobalParams(CommandBuffer cmd, RGCamera rgCam, bool foreceCluster)
            {
                m_ShadowMgr.BindResources(cmd, activeComputeShader, activeComputeKernel);

                cmd.SetGlobalBuffer(ClusterShaderIDs._DirectionalLightDatasSimple, s_DirectionalLightsSimpleBuffer);
                Shader.SetGlobalInt(ClusterShaderIDs._DirectionalLightCount, m_LightList.directionalLightsSimple.Count);

                cmd.SetGlobalTexture(ClusterShaderIDs._CookieTextures, m_CookieTexArray.GetTexCache());
                cmd.SetGlobalBuffer("ClusterStartOffsetIndexIn", s_ClusterStartOffsetIndexBuffer);
                cmd.SetGlobalBuffer("ClusterLightsLinkListIn", s_ClusterLightLinkListBuffer);
                cmd.SetGlobalBuffer(ClusterShaderIDs._LightIndexBuffer, s_LightIndexBuffer);

                cmd.SetGlobalInt(ClusterShaderIDs._PunctualLightCount, m_LightList.punctualLightsRenderieng.Count);
                cmd.SetGlobalBuffer("AllLightList", s_LightsGeoBuffer);
                cmd.SetGlobalBuffer("AllLightsRenderingData", s_LightsRenderingBuffer);
                //cmd.SetGlobalMatrixArray("ShadowMatrix", LightsShadowData);
                cmd.SetGlobalBuffer("ShadowData", s_ShadowData);
                cmd.SetGlobalVector("CullingClusterParams", new Vector4(CULLING_CLUSTER_X, CULLING_CLUSTER_Y, CLUSTER_DEPTH, CULLING_CLUSTER_X * CULLING_CLUSTER_Y * CLUSTER_DEPTH * m_clusterSettings.maxLightsPerCluster));
                cmd.SetGlobalVector(ClusterShaderIDs._FogClusterParams, new Vector4(LIGHTFOG_CLUSTER_X, LIGHTFOG_CLUSTER_Y, CLUSTER_DEPTH, LIGHTFOG_CLUSTER_X * LIGHTFOG_CLUSTER_Y));
                //cmd.SetGlobalVector("HaltonSequence", haltonSeq);
                cmd.SetGlobalMatrix("Cluster_Matrix_LinearZ_LastFrame", m_ClusterMatrixLinearZLastFrame);
                
            }

            public void SetupScreenSpaceShadowParams(CommandBuffer cmd, ComputeShader cs, int kernel)
            {
                cmd.SetComputeBufferParam(cs, kernel, ClusterShaderIDs._DirectionalLightDatasSimple, s_DirectionalLightsSimpleBuffer);
                cmd.SetComputeBufferParam(cs, kernel, ClusterShaderIDs._ShadowDatasExp, s_ShadowData);
            }

            public void RenderShadows(ScriptableRenderContext renderContext, CommandBuffer cmd, CullResults cullResults)
            {
                m_ShadowMgr.RenderShadows(m_FrameId, renderContext, cmd, cullResults, cullResults.visibleLights);
            }

            static Matrix4x4 WorldToCamera(Camera camera)
            {
                return GetFlipMatrix() * camera.worldToCameraMatrix;
            }

            static Matrix4x4 GetFlipMatrix()
            {
                Matrix4x4 flip = Matrix4x4.identity;
                bool isLeftHand = ((int)LightDefinitions.s_UseLeftHandCameraSpace) != 0;
                if (isLeftHand) flip.SetColumn(2, new Vector4(0.0f, 0.0f, -1.0f, 0.0f));
                return flip;
            }

            public Vector3 GetLightColor(VisibleLight light)
            {
                return new Vector3(light.finalColor.r, light.finalColor.g, light.finalColor.b);
            }

            public bool IsBakedShadowMaskLight(Light light)
            {
                if (light)
                    return light.bakingOutput.lightmapBakeType == LightmapBakeType.Mixed &&
                        light.bakingOutput.mixedLightingMode == MixedLightingMode.Shadowmask &&
                        light.bakingOutput.occlusionMaskChannel != -1; // We need to have an occlusion mask channel assign, else we have no shadow mask
                else
                    return false;
            }

            public void CleanUp()
            {
                s_ClusterLightLinkListBuffer.Release();
                s_ClusterStartOffsetIndexBuffer.Release();

                if (s_DirectionalLightsSimpleBuffer != null)
                    s_DirectionalLightsSimpleBuffer.Release();
                s_DirectionalLightsSimpleBuffer = null;
                //s_LightsCullingVolumeBuffer.Release();

                if (s_LightsGeoBuffer != null)
                    s_LightsGeoBuffer.Release();
                s_LightsGeoBuffer = null;

                if (s_LightsRenderingBuffer != null)
                    s_LightsRenderingBuffer.Release();
                s_LightsRenderingBuffer = null;

                if (s_LightIndexBuffer != null)
                    s_LightIndexBuffer.Release();
                s_LightIndexBuffer = null;
                
                m_ShadowSetup.Dispose();

                if(m_CookieTexArray != null)
                {
                    m_CookieTexArray.Release();
                    m_CookieTexArray = null;
                }
            }

            static public RenderTexture GetFogTexture()
            {
                return m_ClusterFogBuffer;
            }

            public void RenderDebugOverlay(RGCamera rgCam, CommandBuffer cmd, DebugDisplaySettings debugDisplaySettings, ref float x, ref float y, float overlaySize, float width)
            {
                LightingDebugSettings lightingDebug = debugDisplaySettings.lightingDebugSettings;

                using (new ProfilingSample(cmd, "Tiled/cluster Lighting Debug"/*, CustomSamplerId.TPTiledLightingDebug.GetSampler()*/))
                {
                    if (lightingDebug.tileClusterDebug != ClusterPass.LightManager.TileClusterDebug.None)
                    {

                        int w = rgCam.CameraWidth;
                        int h = rgCam.CameraHeight;
                        int numTilesX = (w + 15) / 16;
                        int numTilesY = (h + 15) / 16;
                        int numTiles = numTilesX * numTilesY;

                        bool bUseClustered = true;

                        // lightCategories
                        m_DebugViewTilesMaterial.SetInt(ClusterShaderIDs._ViewTilesFlags, (int)lightingDebug.tileClusterDebugByCategory);
                        //m_DebugViewTilesMaterial.SetVector(ClusterShaderIDs._MousePixelCoord, ClusterUtils.GetMouseCoordinates(rgCam));
                        //m_DebugViewTilesMaterial.SetVector(ClusterShaderIDs._MouseClickPixelCoord, ClusterUtils.GetMouseClickCoordinates(rgCam));
                        //m_DebugViewTilesMaterial.SetBuffer(ClusterShaderIDs.g_vLightListGlobal, bUseClustered ? s_PerVoxelLightLists : s_LightList);
                        m_DebugViewTilesMaterial.EnableKeyword(bUseClustered ? "USE_CLUSTERED_LIGHTLIST" : "USE_FPTL_LIGHTLIST");
                        m_DebugViewTilesMaterial.DisableKeyword(!bUseClustered ? "USE_CLUSTERED_LIGHTLIST" : "USE_FPTL_LIGHTLIST");
                        m_DebugViewTilesMaterial.EnableKeyword("SHOW_LIGHT_CATEGORIES");
                        m_DebugViewTilesMaterial.DisableKeyword("SHOW_FEATURE_VARIANTS");

                        CoreUtils.DrawFullScreen(cmd, m_DebugViewTilesMaterial, (RenderTargetIdentifier)BuiltinRenderTextureType.CameraTarget);
                    }
                }

                //                using (new ProfilingSample(cmd, "Display Shadows"))
                //                {
                //                    if (lightingDebug.shadowDebugMode == ShadowMapDebugMode.VisualizeShadowMap)
                //                    {
                //                        int index = (int)lightingDebug.shadowMapIndex;

                //#if UNITY_EDITOR
                //                        if (lightingDebug.shadowDebugUseSelection)
                //                        {
                //                            index = -1;
                //                            if (UnityEditor.Selection.activeObject is GameObject)
                //                            {
                //                                GameObject go = UnityEditor.Selection.activeObject as GameObject;
                //                                Light light = go.GetComponent<Light>();
                //                                if (light != null)
                //                                {
                //                                    index = m_ShadowMgr.GetShadowRequestIndex(light);
                //                                }
                //                            }
                //                        }
                //#endif

                //                        if (index != -1)
                //                        {
                //                            uint faceCount = m_ShadowMgr.GetShadowRequestFaceCount((uint)index);
                //                            for (uint i = 0; i < faceCount; ++i)
                //                            {
                //                                m_ShadowMgr.DisplayShadow(cmd, m_DebugShadowMapMaterial, index, i, x, y, overlaySize, overlaySize, lightingDebug.shadowMinValue, lightingDebug.shadowMaxValue, hdCamera.camera.cameraType != CameraType.SceneView);
                //                                HDUtils.NextOverlayCoord(ref x, ref y, overlaySize, overlaySize, hdCamera.actualWidth);
                //                            }
                //                        }
                //                    }
                //                    else if (lightingDebug.shadowDebugMode == ShadowMapDebugMode.VisualizeAtlas)
                //                    {
                //                        m_ShadowMgr.DisplayShadowMap(cmd, m_DebugShadowMapMaterial, lightingDebug.shadowAtlasIndex, lightingDebug.shadowSliceIndex, x, y, overlaySize, overlaySize, lightingDebug.shadowMinValue, lightingDebug.shadowMaxValue, hdCamera.camera.cameraType != CameraType.SceneView);
                //                        HDUtils.NextOverlayCoord(ref x, ref y, overlaySize, overlaySize, hdCamera.actualWidth);
                //                    }
                //                }
            }
#if UNITY_EDITOR
            private Vector2 m_mousePosition = Vector2.zero;

            private void OnSceneGUI(UnityEditor.SceneView sceneView)
            {
                m_mousePosition = Event.current.mousePosition;
            }
#endif
        }

        [Serializable]
        public class ClusterSettings
        {
            public enum ClusterSize
            {
                _4 = 4,
                _8 = 8,
                _16 = 16,
                _32 = 32,
                _64 = 64
            }

            public enum DepthSlices
            {
                _64 = 64,
                _128 = 128
            }

            public enum FogMethod
            {
                HeightFog,
                VolumetricFog
            }

            public enum FogPass
            {
                Forward,
                PostEffect
            }

            public ClusterSize cullingClusterSize = ClusterSize._32;
            public ClusterSize lightFogClusterSize = ClusterSize._16;
            public DepthSlices clusterDepthSlices = DepthSlices._128;
            public int maxLightsPerCluster = 20;
        }

    }

}
