using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph
{
    [Serializable]
    public class ClusterShadowSettings : VolumeComponent
    {
        public MinFloatParameter MaxShadowDistance = new MinFloatParameter(500.0f, 0.0f);
        public MinIntParameter MaxShadowCasters = new MinIntParameter(5, 0);
        public Vecot2IntParameter ShadowMapResolution = new Vecot2IntParameter(new Vector2Int(2048, 2048));
        public BoolParameter HasStaticShadow = new BoolParameter(false);
        public Vecot2IntParameter StaticShadowMapResolution = new Vecot2IntParameter(new Vector2Int(4096, 4096));
        public TextureParameter staticShadowmap = new TextureParameter(null);
    }

    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public sealed class Vecot2IntParameter : VolumeParameter<Vector2Int>
    {
        public Vecot2IntParameter(Vector2Int value, bool overrideState = false)
            : base(value, overrideState) { }
    }
}
