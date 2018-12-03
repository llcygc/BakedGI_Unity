using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Viva.Rendering.RenderGraph
{
    public enum ContextType
    {
        ComputeContext,
        GraphicContext,
        Hybrid
    }

    public enum RenderViewType
    {
        Normal,
        VR_MultiPass,
        VR_SinglePass,
        VR_SinglePassStereo,
        AR
    }

    public enum RenderResourceType
    {
        Texture,
        ComputeBuffer,
        GraphicShader,
        ComputeShader
    }

    public class RenderResourceContent
    {

    }

    public class RenderContextResources
    {
        RenderResourceType type;
        RenderResourceContent content;
        string name;

        public void AllocateResource()
        {

        }

        public void ReleaseResource()
        {

        }
    }

    public struct RenderNodeContext
    {
        public RenderContextResources[] resources;
        public RenderViewType viewType;
        public ContextType type;
        public RenderTargetIdentifier colorBuffer;
        public RenderTargetIdentifier depthBuffer;
        public CubemapFace rtCubeMapFace;
        public int rtMipLevel;
        public int rtDepthSlice;

        public void Dispose()
        {
            foreach (RenderContextResources res in resources)
                res.ReleaseResource();
        }
    }
}