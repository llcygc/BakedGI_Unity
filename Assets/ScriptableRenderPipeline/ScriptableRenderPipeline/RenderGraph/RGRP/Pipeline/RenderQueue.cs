using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph
{
    public class RGRenderQueue
    {
        public const int k_TransparentPriorityQueueRange = 100;

        public enum Priority
        {
            Background = UnityEngine.Rendering.RenderQueue.Background,
            Opaque = UnityEngine.Rendering.RenderQueue.Geometry,
            OpaqueAlphaTest = UnityEngine.Rendering.RenderQueue.AlphaTest,
            // Warning: we must not change Geometry last value to stay compatible with occlusion
            OpaqueLast = UnityEngine.Rendering.RenderQueue.GeometryLast,
            // For transparent pass we define a range of 200 value to define the priority
            // Warning: Be sure no range are overlapping
            PreVolumetricFog = 2750 - 2 * k_TransparentPriorityQueueRange,
            PreRefractionFirst = 2750 - k_TransparentPriorityQueueRange,
            PreRefraction = 2750,
            PreRefractionLast = 2750 + k_TransparentPriorityQueueRange,
            TransparentFirst = UnityEngine.Rendering.RenderQueue.Transparent - k_TransparentPriorityQueueRange,
            Transparent = UnityEngine.Rendering.RenderQueue.Transparent,
            TransparentLast = UnityEngine.Rendering.RenderQueue.Transparent + k_TransparentPriorityQueueRange,
            
            Overlay = UnityEngine.Rendering.RenderQueue.Overlay,
            AfterPostEffect = 4500
        }

        public static readonly RenderQueueRange k_RenderQueue_OpaqueNoAlphaTest = new RenderQueueRange { min = (int)Priority.Opaque, max = (int)Priority.OpaqueAlphaTest - 1 };
        public static readonly RenderQueueRange k_RenderQueue_OpaqueAlphaTest = new RenderQueueRange { min = (int)Priority.OpaqueAlphaTest, max = (int)Priority.OpaqueLast };
        public static readonly RenderQueueRange k_RenderQueue_AllOpaque = new RenderQueueRange { min = (int)Priority.Opaque, max = (int)Priority.OpaqueLast };

        public static readonly RenderQueueRange k_RenderQueue_BeforeFog = new RenderQueueRange { min = (int)Priority.PreVolumetricFog, max = (int)Priority.PreRefractionFirst - 1 };

        public static readonly RenderQueueRange k_RenderQueue_PreRefraction = new RenderQueueRange { min = (int)Priority.PreRefractionFirst, max = (int)Priority.PreRefractionLast };
        public static readonly RenderQueueRange k_RenderQueue_Transparent = new RenderQueueRange { min = (int)Priority.TransparentFirst, max = (int)Priority.TransparentLast };
        public static readonly RenderQueueRange k_RenderQueue_AllTransparent = new RenderQueueRange { min = (int)Priority.PreVolumetricFog, max = (int)Priority.Overlay - 1 };

        public static readonly RenderQueueRange k_RenderQueue_AfterGradiant = new RenderQueueRange { min = (int)Priority.Overlay, max = (int)Priority.AfterPostEffect - 1 };
        public static readonly RenderQueueRange k_RenderQueue_AfterPostEffect = new RenderQueueRange { min = (int)Priority.AfterPostEffect, max = 5000 };

        public static readonly RenderQueueRange k_RenderQueue_All = new RenderQueueRange { min = 0, max = 5000 };
    }

}
