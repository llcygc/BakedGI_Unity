using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Viva.Rendering.RenderGraph
{
    public struct RendererNode
    {
        IRenderGraphNode rootNode;
    }

    public abstract class IRenderer
    {
        public virtual void Build()
        {

        }

        public virtual void Render()
        {

        }
    }
}
