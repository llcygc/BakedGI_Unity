using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    public class Probe
    {
        public Vector3 position;
        public Vector4 scaleOffset;
        public Vector4 probeID;
        //RenderTexture DistanceTexture;

        public Probe(Vector3 pos, Vector4 id)
        {
            position = pos;
            probeID = id;
            scaleOffset = new Vector4(1, 1, 0, 0);
        }
    }

    public class ProbeManager : IDisposable
    {
        public struct ProbeData
        {
            public Vector3 position;
            public Vector4 scaleOffset;
            public Vector4 probeID;
        }

        private const int PROBE_RES = 512;
        private const uint MAX_LIGHTS_COUNT = 64;

        ShaderPassName[] m_RadiancePassNames = { new ShaderPassName("ClusterGI_Rad") };
        ShaderPassName[] m_NormalPassNames = { new ShaderPassName("ClusterGI") };
        ShaderPassName[] m_DepthPassNames = { new ShaderPassName("ClusterGI_Depth") };

        private ClusterPass.LightManager m_ProbeLightManager = new ClusterPass.LightManager();

        //>>> System.Lazy<T> is broken in Unity (legacy runtime) so we'll have to do it ourselves :|
        static readonly ProbeManager s_Instance = new ProbeManager();
        public static ProbeManager instance { get { return s_Instance; } }

        public bool isDynamic;
        public bool needRender;
        public bool showDebug;
        public GI_Settings.ProbeDebugMode debugMode;
        public Vector3 ProbeVolumeDimension;
        public List<Probe> Probes = new List<Probe>();
        public List<ProbeData> ProbeDatas = new List<ProbeData>();
        public List<GameObject> DebugSpheres = new List<GameObject>();

        private TextureCacheCubemap probeCache;
        private float NearPlane = 0.1f;
        private float FarPlane = 1000.0f;
        private ComputeShader CubetoOctanShader;
        private int CubetoOctanKernel;
        private Vector3 debugPos;
        private Vector3 debugDir;
        private bool doDebugTrace = false;

        ComputeBuffer ProbeDataBuffer;

        CullResults m_cullResults;
        CachedShadowSettings m_ShadowSettings;
        ShadowInitParameters m_ShadowInitParams;

        RenderTexture radianceMapOctan;
        RenderTexture normalMapOctan;
        RenderTexture depthMapOctan;
        RenderTexture probeDepth;

        RenderTexture radianceCubeArray;
        RenderTexture normalMapArray;
        RenderTexture depthMapArray;

        Material ProbeDebugMaterial;
        Shader ProbeDebugShader;

        Matrix4x4[] RotationMatrices = new Matrix4x4[6];
        Matrix4x4 ProjectionMatrix;

        Quaternion[] rotations = { Quaternion.Euler(90, 0, 0), Quaternion.Euler(0, 90, 0), Quaternion.Euler(0, 0, 0), Quaternion.Euler(-90, 0, 0), Quaternion.Euler(0, -90, 0), Quaternion.Euler(0, 180, 0) };
        CubemapFace[] faces = { CubemapFace.PositiveY, CubemapFace.PositiveX, CubemapFace.PositiveZ, CubemapFace.NegativeY, CubemapFace.NegativeX, CubemapFace.NegativeZ };

        public void AllocateProbes(Vector3Int dimension, Transform probeVolume)
        {
            ProbeVolumeDimension = dimension;
            int destCount = dimension.x * dimension.y * dimension.z;
            int origCount = Probes.Count();
            if (dimension.x > 0 && dimension.y > 0 && dimension.z > 0)
            {
                needRender = true;
                //Probes.Clear();
                if (destCount < origCount)
                {
                    Probes.Clear();
                    DebugSpheres.Clear();
                    Probes.Capacity = destCount;
                    DebugSpheres.Capacity = destCount;
                }

                if (destCount != origCount)
                {
                    if (radianceCubeArray != null)
                    {
                        radianceCubeArray.Release();
                        radianceCubeArray = null;
                    }

                    if (probeDepth != null)
                    {
                        probeDepth.Release();
                        probeDepth = null;
                    }

                    if (normalMapArray != null)
                    {
                        normalMapArray.Release();
                        normalMapArray = null;
                    }

                    if (depthMapArray != null)
                    {
                        depthMapArray.Release();
                        depthMapArray = null;
                    }

                    RenderTextureDescriptor radCubeArrayDesc = new RenderTextureDescriptor(PROBE_RES, PROBE_RES, RenderTextureFormat.RGB111110Float);
                    radCubeArrayDesc.dimension = TextureDimension.CubeArray;
                    radCubeArrayDesc.volumeDepth = destCount * 6;
                    radianceCubeArray = new RenderTexture(radCubeArrayDesc);

                    RenderTextureDescriptor normCubeArrayDesc = new RenderTextureDescriptor(PROBE_RES, PROBE_RES, RenderTextureFormat.RGB111110Float);
                    normCubeArrayDesc.dimension = TextureDimension.CubeArray;
                    normCubeArrayDesc.volumeDepth = destCount * 6;
                    normalMapArray = new RenderTexture(normCubeArrayDesc);

                    RenderTextureDescriptor dephtArrayDesc = new RenderTextureDescriptor(PROBE_RES, PROBE_RES, RenderTextureFormat.RHalf);
                    dephtArrayDesc.dimension = TextureDimension.CubeArray;
                    dephtArrayDesc.volumeDepth = destCount * 6;
                    depthMapArray = new RenderTexture(dephtArrayDesc);

                    RenderTextureDescriptor depthDesc = new RenderTextureDescriptor(PROBE_RES, PROBE_RES, RenderTextureFormat.Depth);
                    depthDesc.dimension = TextureDimension.CubeArray;
                    depthDesc.volumeDepth = 6 * destCount;
                    probeDepth = new RenderTexture(depthDesc);

                    if (radianceMapOctan != null)
                    {
                        radianceMapOctan.Release();
                        radianceMapOctan = null;
                    }

                    if (normalMapOctan != null)
                    {
                        normalMapOctan.Release();
                        normalMapOctan = null;
                    }

                    if (depthMapOctan != null)
                    {
                        depthMapOctan.Release();
                        depthMapOctan = null;
                    }

                    RenderTextureDescriptor radianceMapOctanDesc = new RenderTextureDescriptor(PROBE_RES, PROBE_RES, RenderTextureFormat.RGB111110Float);
                    radianceMapOctanDesc.dimension = TextureDimension.Tex2DArray;
                    radianceMapOctanDesc.volumeDepth = destCount;
                    radianceMapOctanDesc.enableRandomWrite = true;
                    radianceMapOctan = new RenderTexture(radianceMapOctanDesc);
                    radianceMapOctan.Create();

                    RenderTextureDescriptor normalMapOctanDesc = new RenderTextureDescriptor(PROBE_RES, PROBE_RES, RenderTextureFormat.RGB111110Float);
                    normalMapOctanDesc.dimension = TextureDimension.Tex2DArray;
                    normalMapOctanDesc.volumeDepth = destCount;
                    normalMapOctanDesc.enableRandomWrite = true;
                    normalMapOctan = new RenderTexture(normalMapOctanDesc);
                    normalMapOctan.Create();

                    RenderTextureDescriptor depthMapOctanDesc = new RenderTextureDescriptor(PROBE_RES, PROBE_RES, RenderTextureFormat.RHalf);
                    depthMapOctanDesc.dimension = TextureDimension.Tex2DArray;
                    depthMapOctanDesc.volumeDepth = destCount;
                    depthMapOctanDesc.enableRandomWrite = true;
                    depthMapOctan = new RenderTexture(depthMapOctanDesc);
                    depthMapOctan.Create();
                }

                for (int i = 0; i < dimension.x; i++)
                    for (int j = 0; j < dimension.y; j++)
                        for (int k = 0; k < dimension.z; k++)
                        {
                            int index = i * dimension.y * dimension.z + j * dimension.z + k;
                            Vector3 coord = new Vector3((float)i / (dimension.x - 1) - 0.5f, (float)j / (dimension.y - 1) - 0.5f, (float)k / (dimension.z - 1) - 0.5f);
                            coord = probeVolume.localToWorldMatrix.MultiplyPoint(coord);
                            if (index >= origCount)
                            {
                                Probe newProbe = new Probe(coord, new Vector4(i, j, k, 0));
                                Probes.Add(newProbe);
                                GameObject debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                                MeshRenderer mesh = debugSphere.GetComponent<MeshRenderer>();
                                if (mesh)
                                {
                                    Material probeMat = new Material(ProbeDebugShader);
                                    probeMat.SetFloat("_DebugProbeID", index);
                                    mesh.sharedMaterial = probeMat;
                                }
                                debugSphere.SetActive(showDebug);
                                debugSphere.transform.position = coord;
                                DebugSpheres.Add(debugSphere);
                            }
                            else
                            {
                                Probes[index].position = coord;
                                Probes[index].probeID = new Vector4(i, j, k, 0);
                                if (DebugSpheres.Count < i + 1)
                                {
                                    DebugSpheres.Add(GameObject.CreatePrimitive(PrimitiveType.Sphere));
                                }
                                else if (DebugSpheres[index] == null)
                                {
                                    DebugSpheres[index] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                                }

                                MeshRenderer mesh = DebugSpheres[i].GetComponent<MeshRenderer>();
                                if (mesh)
                                {
                                    Material probeMat = mesh.sharedMaterial;
                                    if (probeMat.shader.name != "Unlit/GI_ProbeDebug")
                                        probeMat = new Material(ProbeDebugShader);

                                    probeMat.SetFloat("_ProbeID", index);
                                    mesh.sharedMaterial = probeMat;
                                }

                                DebugSpheres[index].transform.position = coord;
                                DebugSpheres[index].SetActive(showDebug);
                            }
                        }

                ProbeDatas.Clear();
                for (int i = 0; i < Probes.Count; i++)
                {
                    ProbeData pData = new ProbeData()
                    {
                        position = Probes[i].position,
                        probeID = Probes[i].probeID,
                        scaleOffset = Probes[i].scaleOffset
                    };
                    ProbeDatas.Add(pData);
                }

                CoreUtils.SafeRelease(ProbeDataBuffer);
                ProbeDataBuffer = new ComputeBuffer(Probes.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(ProbeData)));
                ProbeDataBuffer.SetData(ProbeDatas);
            }
            else
            {
                UnityEngine.Debug.LogError("Probe count must be greater than 1 in every dimension.");
            }
        }

        public void UpdateProbeSettings(bool dynamic, bool debug, float near, float far, GI_Settings.ProbeDebugMode probeDebugMode)
        {
            isDynamic = dynamic;
            showDebug = debug;
            NearPlane = near;
            FarPlane = far;
            debugMode = probeDebugMode;

            for (int i = 0; i < DebugSpheres.Count; i++)
            {
                DebugSpheres[i].SetActive(showDebug);
            }

            ProjectionMatrix = Matrix4x4.Perspective(90.0f, 1, NearPlane, FarPlane);
        }

        public void Build(ClusterRenderPipelineResources resources, FrameClusterConfigration clusterConifg, ShadowInitParameters shadowInitParameters, CachedShadowSettings shadowSettings)
        {
            CubetoOctanShader = resources.CubetoOctanShader;
            if (CubetoOctanShader)
                CubetoOctanKernel = CubetoOctanShader.FindKernel("CubetoOctan");

            m_ProbeLightManager.Build(resources, clusterConifg, shadowInitParameters, shadowSettings);
            m_ProbeLightManager.AllocateResolutionDependentResources((int)PROBE_RES, (int)PROBE_RES, clusterConifg);
            m_ShadowSettings = shadowSettings;
            m_ShadowInitParams = shadowInitParameters;

            ProbeDebugMaterial = new Material(Shader.Find("Unlit/GI_ProbeDebug"));
            ProbeDebugShader = Shader.Find("Unlit/GI_ProbeDebug");

            for (int i = 0; i < 6; i++)
            {
                RotationMatrices[i] = Matrix4x4.Rotate(rotations[i]);
                ProjectionMatrix = Matrix4x4.Perspective(90.0f, 1, NearPlane, FarPlane);
            }
        }

        public void Render(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            if (needRender || isDynamic)
            {
                needRender = false;

                GameObject probeCamObj = new GameObject("Probe Camera");
                probeCamObj.hideFlags = HideFlags.HideAndDontSave;
                Camera probeCamera = probeCamObj.AddComponent<Camera>();
                probeCamera.enabled = false;
                probeCamera.renderingPath = RenderingPath.Forward;
                probeCamera.nearClipPlane = NearPlane;
                probeCamera.farClipPlane = FarPlane;
                probeCamera.depthTextureMode = DepthTextureMode.None;
                probeCamera.clearFlags = CameraClearFlags.SolidColor | CameraClearFlags.Depth;
                probeCamera.backgroundColor = Color.black;
                probeCamera.orthographic = false;
                probeCamera.hideFlags = HideFlags.HideAndDontSave;
                probeCamera.allowMSAA = false;
                probeCamera.stereoTargetEye = StereoTargetEyeMask.None;
                probeCamera.fieldOfView = 90.0f;
                probeCamera.aspect = 1;
                probeCamObj.SetActive(false);


                for (int i = 0; i < Probes.Count; i++)
                {
                    probeCamObj.transform.position = Probes[i].position;
                    for (int j = 0; j < 6; j++)
                    {
                        probeCamObj.transform.rotation = rotations[j];
                        m_ProbeLightManager.NewFrame();
                        RGCamera rgCam = RGCamera.Get(probeCamera, null, 128, false);

                        ScriptableCullingParameters cullingParams;

                        if (!CullResults.GetCullingParameters(probeCamera, false, out cullingParams))
                        {
                            continue;
                        }

                        CullResults.Cull(ref cullingParams, renderContext, ref m_cullResults);
                        m_ProbeLightManager.UpdateCullingParameters(ref cullingParams, false);
                        m_ProbeLightManager.PrepareLightsDataForGPU(cmd, m_ShadowSettings, m_cullResults, probeCamera, false);

                        bool enableStaticShadowmap = false;
                        if (m_ShadowSettings.StaticShadowmap)
                        {
                            enableStaticShadowmap = true;
                            cmd.SetGlobalTexture(ClusterShaderIDs._StaticShadowmapExp, m_ShadowSettings.StaticShadowmap);
                        }

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
                        {
                            m_ShadowSettings.MaxShadowDistance = 1000.0f;
                            m_ShadowSettings.MaxShadowCasters = 5;
                            m_ShadowSettings.ShadowmapRes = new Vector2Int(m_ShadowInitParams.shadowAtlasWidth, m_ShadowInitParams.shadowAtlasHeight);
                            m_ShadowSettings.StaticShadowmapRes = Vector2Int.one;
                            m_ShadowSettings.StaticShadowmap = null;
                        }

                        m_ProbeLightManager.RenderShadows(renderContext, cmd, m_cullResults);
                        m_ProbeLightManager.PostBlurExpShadows(cmd, 1);
                        
                        m_ProbeLightManager.ClusterLightCompute(rgCam, cmd);
                        rgCam.SetupGlobalParams(cmd, 0, 0);

                        renderContext.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                        renderContext.SetupCameraProperties(probeCamera, rgCam.StereoEnabled); // Need to recall SetupCameraProperties after m_ShadowPass.Render

                        //RenderTargetIdentifier[] rendertargets = { Probes[i].RadianceTexture, Probes[i].NormalTexture };
                        cmd.SetRenderTarget(radianceCubeArray, probeDepth, 0, CubemapFace.Unknown, i * 6 + (int)faces[j]);
                        cmd.ClearRenderTarget(true, true, probeCamera.backgroundColor.linear);
                        RendererConfiguration settings = RendererConfiguration.PerObjectReflectionProbes | RendererConfiguration.PerObjectLightmaps | RendererConfiguration.PerObjectLightProbe;
                        RenderRendererList(m_cullResults, rgCam.camera, renderContext, cmd, m_RadiancePassNames, RGRenderQueue.k_RenderQueue_AllOpaque, settings);
                        renderContext.DrawSkybox(probeCamera);

                        //RenderTargetIdentifier[] rendertargets = { Probes[i].RadianceTexture, Probes[i].NormalTexture };
                        cmd.SetRenderTarget(normalMapArray, probeDepth, 0, CubemapFace.Unknown, i * 6 + (int)faces[j]);
                        cmd.ClearRenderTarget(true, true, new Color(0, 0, 0));
                        RenderRendererList(m_cullResults, rgCam.camera, renderContext, cmd, m_NormalPassNames, RGRenderQueue.k_RenderQueue_AllOpaque);

                        //RenderTargetIdentifier[] rendertargets = { Probes[i].RadianceTexture, Probes[i].NormalTexture };
                        cmd.SetRenderTarget(depthMapArray, probeDepth, 0, CubemapFace.Unknown, i * 6 + (int)faces[j]);
                        cmd.ClearRenderTarget(true, true, new Color(1, 1, 1));
                        RenderRendererList(m_cullResults, rgCam.camera, renderContext, cmd, m_DepthPassNames, RGRenderQueue.k_RenderQueue_AllOpaque);

                        renderContext.Submit();
                    }
                }
                
                ReprojectCubeToOctan(renderContext, cmd);

                UnityEngine.Object.DestroyImmediate(probeCamera);
                UnityEngine.Object.DestroyImmediate(probeCamObj);
            }
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

        public void DrawDebugMap(ScriptableRenderContext context, CommandBuffer cmd)
        {

        }

        public void ShowDebug(bool show)
        {
            showDebug = show;
        }

        private void RenderCubeMaps()
        {

        }

        public void SetUpDebug(Vector3 pos, Vector3 dir, bool hasHit)
        {
            debugPos = pos;
            debugDir = dir;
            doDebugTrace = hasHit;
        }

        private void ReprojectCubeToOctan(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            if (CubetoOctanShader && CubetoOctanKernel >= 0)
            {
                cmd.Clear();
                cmd.SetComputeTextureParam(CubetoOctanShader, CubetoOctanKernel, "GI_ProbeTexture", radianceCubeArray);
                cmd.SetComputeTextureParam(CubetoOctanShader, CubetoOctanKernel, "GI_NormalTexture", normalMapArray);
                cmd.SetComputeTextureParam(CubetoOctanShader, CubetoOctanKernel, "GI_DepthTexture", depthMapArray);

                cmd.SetComputeTextureParam(CubetoOctanShader, CubetoOctanKernel, "RadMapOctan", radianceMapOctan);
                cmd.SetComputeTextureParam(CubetoOctanShader, CubetoOctanKernel, "NormalMapOctan", normalMapOctan);
                cmd.SetComputeTextureParam(CubetoOctanShader, CubetoOctanKernel, "DistMapOctan", depthMapOctan);

                cmd.SetComputeVectorParam(CubetoOctanShader, "CubeOctanResolution", new Vector4(PROBE_RES, PROBE_RES, PROBE_RES, PROBE_RES));
                int threadDim = PROBE_RES / 8;
                cmd.DispatchCompute(CubetoOctanShader, CubetoOctanKernel, threadDim, threadDim, Probes.Count);
                renderContext.ExecuteCommandBuffer(cmd);
                renderContext.Submit();
                cmd.Clear();
            }
        }

        public void PushGlobalParams(CommandBuffer cmd)
        {
            if(Probes.Count > 0)
            {
                cmd.SetGlobalTexture("GI_ProbeTexture", radianceCubeArray);
                cmd.SetGlobalTexture("GI_NormalTexture", normalMapArray);
                cmd.SetGlobalTexture("GI_DepthTexture", depthMapArray);

                cmd.SetGlobalTexture("RadMapOctan", radianceMapOctan);
                cmd.SetGlobalTexture("NormalMapOctan", normalMapOctan);
                cmd.SetGlobalTexture("DistMapOctan", depthMapOctan);

                cmd.SetGlobalFloat("GI_DebugMode", (float)debugMode);
                cmd.SetGlobalVector("ProbeDimenson", ProbeVolumeDimension);
                int count = (int)ProbeVolumeDimension.x * (int)ProbeVolumeDimension.y * (int)ProbeVolumeDimension.z;
                cmd.SetGlobalVector("ProbeMin", Probes[0].position);
                cmd.SetGlobalVector("ProbeMax", Probes[count - 1].position);
                cmd.SetGlobalBuffer("ProbeDataBuffer", ProbeDataBuffer);
                cmd.SetGlobalMatrixArray("ProbeRotationMatrix", RotationMatrices);
                cmd.SetGlobalMatrix("ProbeProjMatrix", ProjectionMatrix);
                cmd.SetGlobalVector("CubeOctanResolution", new Vector4(PROBE_RES, PROBE_RES, PROBE_RES, PROBE_RES));
                cmd.SetGlobalVector("ProbeProjectonParam", new Vector4(NearPlane, FarPlane, 0, 0));

                cmd.SetGlobalVector("DebugPos", debugPos);
                cmd.SetGlobalVector("DebugDir", debugDir);
                cmd.SetGlobalVector("DebugParam", new Vector4(doDebugTrace ? 1 : 0, 1000.0f, 0, 0));
            }
        }

        public void Dispose()
        {
            m_ProbeLightManager.CleanUp();
            Probes.Clear();
            for (int i = 0; i < DebugSpheres.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(DebugSpheres[i]);
            }
            DebugSpheres.Clear();
            radianceCubeArray.Release();
            probeDepth.Release();
            normalMapArray.Release();
            radianceMapOctan.Release();
            normalMapOctan.Release();
            depthMapOctan.Release();
        }
    }
}
