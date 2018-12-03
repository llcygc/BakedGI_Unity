using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Viva.Rendering.RenderGraph
{
    public abstract class RenderGraphAsset : ScriptableObject
    {
        public abstract IRenderGraphController InternalCreateController();
        public abstract void Execute();
    }
}
