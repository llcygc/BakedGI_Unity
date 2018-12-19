#if RGRP_V_2
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph
{
    public class ComputeNode : IRenderGraphNode
    {
        public bool AsyncNode;
        // Start is called before the first frame update
        void SetUp()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        public override void Execute(ScriptableRenderContext renderContext)
        {

        }
    }
}

#endif