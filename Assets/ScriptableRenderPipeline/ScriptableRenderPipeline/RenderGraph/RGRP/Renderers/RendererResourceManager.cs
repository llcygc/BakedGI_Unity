#if RGRP_V_2

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Viva.Rendering.RenderGraph
{
    public class RendererResourceManager
    {
        private List<RendererResource> RendererResources;

        RendererResource FetchResource(int i)
        {
            return RendererResources[i];
        }
    }
}

#endif