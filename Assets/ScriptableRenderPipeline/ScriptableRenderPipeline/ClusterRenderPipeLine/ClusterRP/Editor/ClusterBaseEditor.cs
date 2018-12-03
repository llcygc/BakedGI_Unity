using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Viva.Rendering.RenderGraph.ClusterPipeline;

namespace UnityEditor.Experimental.Rendering.ClusterPipeline
{
    using UnityObject = UnityEngine.Object;

    public class ClusterBaseEditor<T> : Editor
         where T : UnityObject
    {
        internal PropertyFetcher<T> properties { get; private set; }

        protected T m_Target
        {
            get { return target as T; }
        }

        protected T[] m_Targets
        {
            get { return targets as T[]; }
        }

        protected ClusterRenderPipeline m_ClusterPipeline
        {
            get { return RenderPipelineManager.currentPipeline as ClusterRenderPipeline; }
        }

        protected virtual void OnEnable()
        {
            properties = new PropertyFetcher<T>(serializedObject);
        }
    }
}
