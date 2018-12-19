﻿#if RGRP_V_2

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph
{
    public abstract class IRGRenderer
    {
        public virtual void SetUp()
        {

        }
        // Update is called once per frame
        public virtual void Update()
        {

        }

        public virtual void Execute(ScriptableRenderContext renderContext)
        {

        }
    }
}
#endif
