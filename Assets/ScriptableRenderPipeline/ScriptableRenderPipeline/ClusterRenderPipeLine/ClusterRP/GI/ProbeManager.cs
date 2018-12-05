using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    public class Probe : IDisposable
    {
        public Vector3 position;
        public Vector4 scaleOffset;
        public RenderTexture RadianceTexture;
        public RenderTexture NormalTexture;
        //RenderTexture DistanceTexture;

        public Probe(Vector3 pos)
        {
            position = pos;

            RenderTextureDescriptor radDesc = new RenderTextureDescriptor(1024, 1024, RenderTextureFormat.RGB111110Float);
            radDesc.dimension = TextureDimension.Cube;
            RadianceTexture = new RenderTexture(radDesc);

            RenderTextureDescriptor normalDesc = new RenderTextureDescriptor(1024, 1024, RenderTextureFormat.ARGBHalf);
            normalDesc.dimension = TextureDimension.Cube;
            NormalTexture = new RenderTexture(normalDesc);////
        }

        public void Dispose()
        {
            RadianceTexture.Release();
            RadianceTexture = null;

            NormalTexture.Release();
            NormalTexture = null;
        }
    }

    public class ProbeManager : IDisposable
    {
        public struct ProbeData
        {
            Vector3 position;
            Vector4 scaleOffset;
        }

        private const uint PROBE_RES = 1024;
        private const uint MAX_LIGHTS_COUNT = 64;

        ShaderPassName[] m_GIPassNames = { ClusterShaderPassNames.s_ForwardBaseName, ClusterShaderPassNames.s_ClusterForwardName, ClusterShaderPassNames.s_SRPDefaultUnlitName };
        private ClusterPass.LightManager m_ProbeLightManager = new ClusterPass.LightManager();

        //>>> System.Lazy<T> is broken in Unity (legacy runtime) so we'll have to do it ourselves :|
        static readonly ProbeManager s_Instance = new ProbeManager();
        public static ProbeManager instance { get { return s_Instance; } }

        public bool isDynamic;
        public bool needRender;
        public bool showDebug;
        public Vector3 ProbeVolumeDimension;
        public List<Probe> Probes = new List<Probe>();
        public List<GameObject> DebugSpheres = new List<GameObject>();

        private TextureCacheCubemap probeCache;
        private float NearPlane = 0.3f;
        private float FarPlane = 1000.0f;

        ComputeBuffer ProbeDataBuffer;

        CullResults m_cullResults;
        CachedShadowSettings m_ShadowSettings;
        ShadowInitParameters m_ShadowInitParams;

        RenderTexture radianceMapOctan;
        RenderTexture normalMapOctan;
        RenderTexture probeDepth;

        RenderTexture radianceCubeArray;

        Material ProbeDebugMaterial;
        Shader ProbeDebugShader;

        public void AllocateProbes(Vector3Int dimension, Transform probeVolume)
        {
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

                    RenderTextureDescriptor radCubeArrayDesc = new RenderTextureDescriptor(1024, 1024, RenderTextureFormat.RGB111110Float);
                    radCubeArrayDesc.dimension = TextureDimension.CubeArray;
                    radCubeArrayDesc.volumeDepth = destCount * 6;
                    radianceCubeArray = new RenderTexture(radCubeArrayDesc);


                    RenderTextureDescriptor depthDesc = new RenderTextureDescriptor(1024, 1024, RenderTextureFormat.Depth);
                    depthDesc.dimension = TextureDimension.CubeArray;
                    depthDesc.volumeDepth = 6 * destCount;
                     probeDepth = new RenderTexture(depthDesc);
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
                                Probe newProbe = new Probe(coord);
                                Probes.Add(newProbe);
                                GameObject debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                                MeshRenderer mesh = debugSphere.GetComponent<MeshRenderer>();
                                if (mesh)
                                {
                                    Material probeMat = new Material(ProbeDebugShader);
                                    probeMat.SetFloat("_ProbeID", index);
                                    mesh.sharedMaterial = probeMat;
                                }
                                debugSphere.SetActive(showDebug);
                                debugSphere.transform.position = coord;
                                DebugSpheres.Add(debugSphere);
                            }
                            else
                            {
                                Probes[index].position = coord;
                                if(DebugSpheres[index] == null)
                                {
                                    DebugSpheres[index] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                                }

                                MeshRenderer mesh = DebugSpheres[i].GetComponent<MeshRenderer>();
                                if (mesh)
                                {
                                    Material probeMat = mesh.sharedMaterial;
                                    if (probeMat.shader.name != "Unlit / GI_ProbeDebug")
                                        probeMat = new Material(ProbeDebugShader);

                                    probeMat.SetFloat("_ProbeID", index);
                                    mesh.sharedMaterial = probeMat;
                                }

                                DebugSpheres[index].transform.position = coord;
                                DebugSpheres[index].SetActive(showDebug);
                            }
                        }
            }
            else
            {
                UnityEngine.Debug.LogError("Probe count must be greater than 1 in every dimension.");
            }
        }

        public void UpdateProbeSettings(bool dynamic, bool debug, float near, float far)
        {
            isDynamic = dynamic;
            showDebug = debug;
            NearPlane = near;
            FarPlane = far;

            for(int i = 0; i < DebugSpheres.Count; i++)
            {
                DebugSpheres[i].SetActive(showDebug);
            }
        }

        public void Build(ClusterRenderPipelineResources resources, FrameClusterConfigration clusterConifg, ShadowInitParameters shadowInitParameters, CachedShadowSettings shadowSettings)
        {
            m_ProbeLightManager.Build(resources, clusterConifg, shadowInitParameters, shadowSettings);
            m_ProbeLightManager.AllocateResolutionDependentResources((int)PROBE_RES, (int)PROBE_RES, clusterConifg);
            m_ShadowSettings = shadowSettings;
            m_ShadowInitParams = shadowInitParameters;

            ProbeDebugMaterial = new Material(Shader.Find("Unlit/GI_ProbeDebug"));
            ProbeDebugShader = Shader.Find("Unlit/GI_ProbeDebug");
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

                Quaternion[] rotations = { Quaternion.Euler(90, 0, 0), Quaternion.Euler(0, 90, 0), Quaternion.Euler(0, 0, 0), Quaternion.Euler(-90, 0, 0), Quaternion.Euler(0, -90, 0), Quaternion.Euler(0, 180, 0) };
                CubemapFace[] faces = { CubemapFace.PositiveY, CubemapFace.PositiveX, CubemapFace.PositiveZ, CubemapFace.NegativeY, CubemapFace.NegativeX, CubemapFace.NegativeZ };

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

                        renderContext.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                        renderContext.SetupCameraProperties(probeCamera, rgCam.StereoEnabled); // Need to recall SetupCameraProperties after m_ShadowPass.Render
                        m_ProbeLightManager.RenderShadows(renderContext, cmd, m_cullResults);
                        m_ProbeLightManager.PostBlurExpShadows(cmd, 1);
                        m_ProbeLightManager.ClusterLightCompute(rgCam, cmd);
                        rgCam.SetupGlobalParams(cmd, 0, 0);

                        //RenderTargetIdentifier[] rendertargets = { Probes[i].RadianceTexture, Probes[i].NormalTexture };
                        cmd.SetRenderTarget(radianceCubeArray, probeDepth, 0, CubemapFace.Unknown, i * 6 + (int)faces[j]);
                        cmd.ClearRenderTarget(true, true, probeCamera.backgroundColor.linear);
                        RendererConfiguration settings = RendererConfiguration.PerObjectReflectionProbes | RendererConfiguration.PerObjectLightmaps | RendererConfiguration.PerObjectLightProbe;
                        RenderRendererList(m_cullResults, rgCam.camera, renderContext, cmd, m_GIPassNames, RGRenderQueue.k_RenderQueue_AllOpaque, settings);
                        renderContext.DrawSkybox(probeCamera);
                        renderContext.Submit();
                    }
                }
                
                ReprojectCubeToOctan();

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

        private void ReprojectCubeToOctan()
        {

        }

        public void PushGlobalParams(CommandBuffer cmd)
        {
            if(Probes.Count > 0)
                cmd.SetGlobalTexture("GI_ProbeTexture", radianceCubeArray);
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
        }
    }
}
