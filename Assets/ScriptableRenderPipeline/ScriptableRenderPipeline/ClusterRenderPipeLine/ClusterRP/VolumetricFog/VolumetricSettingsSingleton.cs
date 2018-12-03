using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    public class VolumetricSettingsSingleton : UnityEngine.Experimental.Rendering.Singleton<VolumetricSettingsSingleton>
    {
        VolumetricSettings m_Settings { get; set; }

        public static VolumetricSettings overrideSettings
        {
            get { return instance.m_Settings; }
            set { instance.m_Settings = value; }
        }
    }
}
