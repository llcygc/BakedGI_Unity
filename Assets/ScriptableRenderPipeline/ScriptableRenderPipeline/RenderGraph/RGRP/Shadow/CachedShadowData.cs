using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph
{
    public enum ShadowUpdateType
    {
        Dynamic,
        Static,
        Hybrid
    }

    [Serializable]
    public struct CachedShadowInfo : IComparable<CachedShadowInfo>
    {
        public Rect viewport;
        public Matrix4x4 view;
        public Matrix4x4 proj;
        public float cullingSphereSize;

        public int CompareTo(CachedShadowInfo other)
        {
            if (viewport.height != other.viewport.height)
                return viewport.height > other.viewport.height ? -1 : 1;
            if (viewport.width != other.viewport.width)
                return viewport.width > other.viewport.width ? -1 : 1;

            return 0;
        }
    }

    [RequireComponent(typeof(Light))]
    public class CachedShadowData : MonoBehaviour
    {
        public ShadowUpdateType shadowUpdateType = ShadowUpdateType.Dynamic;
        public bool UseStaticShadowmapForLastCascade = false;
        public bool NeedUpdateStaticShadowmapForLastCascade = false;
        public uint StaticShadowResolution = 4096;
        public float DepthFallOffPercent = 1.0f;
        public float ShadowNearMultiplier = 80.0f;
        public float ShadowFarMultiplier = 20.0f;
        public bool AffectVolumectricFog = true;
        //Reserved for point light : 0 - Up, 1 - Down, 2 - Left, 3 - Right, 4 - Front, 5 - Back
        public List<CachedShadowInfo> cachedShadowInfo = new List<CachedShadowInfo>();
    }
}
