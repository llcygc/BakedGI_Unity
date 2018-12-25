using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    public class ClusterDeferredRender : IRGRenderer 
    {
        private RGCamera m_RGCam;
        private ShaderPassName[] deferredPass = { new ShaderPassName("ClusterDeferred") };

        public ComputeShader DeferredLightingCS;
        private const MSAASamples m_kMSAASamples = MSAASamples.None;

        //RT0: Normal(RGB) and Translucency Luminance/AO(A)
        private RTHandleSystem.RTHandle GBuffer_RT0;
        //RT1: Albedo(RGB) and SSS Profile(A)
        private RTHandleSystem.RTHandle GBuffer_RT1;
        //RT2: Rougness(R) and Specular YCbCr/Transmittance CbCr
        private RTHandleSystem.RTHandle GBuffer_RT2;

        private RTHandleSystem.RTHandle ColorBuffer;
        private RTHandleSystem.RTHandle DepthStencilBuffer;

        private RTHandleSystem.RTHandle ColorBufferCopy;
        private RTHandleSystem.RTHandle DepthStencilBufferCopy;

        private ClusterPass.LightManager m_LightManager = new ClusterPass.LightManager();

        private int m_iFrameHeight = 1;
        private int m_iFrameWidth = 1;

        private GeometryNode ShadowRenderNode;
        private ComputeNode ShadowPostNode;
        private ComputeNode LightCullingNode;
        private GeometryNode DepthPrepassNode;
        private ComputeNode ScreenSpaceShadowNode;
        private GeometryNode DeferredGBufferNode;
        private ComputeNode DeferredLightingNode;
        private GeometryNode ForwardBeforeFogNode;
        private ComputeNode VolumetricLightingNode;
        private ComputeNode VolumetricFogNode;
        private GeometryNode ForwardAfterFogNode;

        public override void SetUp()
        {
            GBuffer_RT0 = RTHandles.Alloc(width: m_iFrameWidth, height: m_iFrameHeight, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGB32, sRGB: false, msaaSamples: m_kMSAASamples, enableRandomWrite: false, name: "GBUFFER_0");
            GBuffer_RT1 = RTHandles.Alloc(width: m_iFrameWidth, height: m_iFrameHeight, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGB32, sRGB: false, msaaSamples: m_kMSAASamples, enableRandomWrite: false, name: "GBUFFER_1");
            GBuffer_RT2 = RTHandles.Alloc(width: m_iFrameWidth, height: m_iFrameHeight, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGB32, sRGB: false, msaaSamples: m_kMSAASamples, enableRandomWrite: false, name: "GBUFFER_2");

            ColorBuffer = RTHandles.Alloc(width: m_iFrameWidth, height: m_iFrameHeight, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.RGB111110Float, sRGB: false, msaaSamples: m_kMSAASamples, enableRandomWrite: false, name: "MainColorBuffer");
            DepthStencilBuffer = RTHandles.Alloc(width: m_iFrameWidth, height: m_iFrameHeight, filterMode: FilterMode.Point, depthBufferBits: DepthBits.Depth24, colorFormat: RenderTextureFormat.Depth, sRGB: false, msaaSamples: m_kMSAASamples, enableRandomWrite: false, name: "MainDepthBuffer");
        }
        // Update is called once per frame
        public override void Update(Camera cam)
        {
            PostProcessLayer postProcessLayer = cam.GetComponent<PostProcessLayer>();
            m_RGCam = RGCamera.Get(cam, postProcessLayer, 128, true);
        }

        public override void Execute(ScriptableRenderContext renderContext)
        {

        }
    }
}

