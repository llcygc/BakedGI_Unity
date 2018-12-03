using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph
{
    [Serializable]
    public class CommonSettings : VolumeComponent
    {
        public BoolParameter enableDepthPrepass = new BoolParameter(true);
        public BoolParameter hasRefraction = new BoolParameter(true);
        public BoolParameter enableAsyncCompute = new BoolParameter(true);
        public BoolParameter enableScreenSpaceShadow = new BoolParameter(false);
        public BoolParameter enableLightCullingMask = new BoolParameter(false);
        public MSAAParameter msaaSamples = new MSAAParameter(MSAASamples.None);
    }

    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public sealed class MSAAParameter : VolumeParameter<MSAASamples>
    {
        public MSAAParameter(MSAASamples value, bool overrideState = false)
            : base(value, overrideState) { }
    }
}
