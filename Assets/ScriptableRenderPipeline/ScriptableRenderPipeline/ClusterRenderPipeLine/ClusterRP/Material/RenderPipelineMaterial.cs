using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;
using UnityEngine;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    public class RenderPipelineMaterial : UnityEngine.Object
    {
        // GBuffer management
        public virtual int GetMaterialGBufferCount() { return 0; }
        public virtual void GetMaterialGBufferDescription(out RenderTextureFormat[] RTFormat, out bool[] sRGBFlag)
        {
            RTFormat = null;
            sRGBFlag = null;
        }

        // Regular interface
        public virtual void Build(ClusterRenderPipeLineAsset hdAsset) {}
        public virtual void Cleanup() {}

        // Following function can be use to initialize GPU resource (once or each frame) and bind them
        public virtual void RenderInit(CommandBuffer cmd) {}
        public virtual void Bind() {}
    }
}
