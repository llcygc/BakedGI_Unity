using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph
{
    public abstract class IRenderGraphNode
    {
        protected RenderNodeContext context;
        protected CommandBuffer cmd;

        public RenderNodeContext NodeContext
        {
            get { return context; }
            set { context = value; }
        }

        public virtual void Execute(ScriptableRenderContext renderContext)
        {
        }
    }
}
