using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Viva.Rendering.RenderGraph
{
    public struct RenderNodeGraph
    {
        IRenderGraphNode rootNode;
    }

    public abstract class IRenderPipline
    {
        RenderNodeGraph renderGraph;
        RenderNodeContext globalContext;

        public virtual void Render()
        {

        }
    }

}
