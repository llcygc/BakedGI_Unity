using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using System;

namespace Viva.Rendering.RenderGraph
{
    [GenerateHLSL]
    public enum DebugMipMapMode
    {
        None,
        MipRatio,
        MipCount,
        MipCountReduction,
        StreamingMipBudget,
        StreamingMip
    }

    [Serializable]
    public class MipMapDebugSettings
    {
        public DebugMipMapMode debugMipMapMode = DebugMipMapMode.None;

        public bool IsDebugDisplayEnabled()
        {
            return debugMipMapMode != DebugMipMapMode.None;
        }

        public void OnValidate()
        {
        }
    }
}
