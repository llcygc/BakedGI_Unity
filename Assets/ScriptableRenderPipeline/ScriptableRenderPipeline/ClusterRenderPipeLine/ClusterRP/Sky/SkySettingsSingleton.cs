using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph
{
    public class SkySettingsSingleton : UnityEngine.Experimental.Rendering.Singleton<SkySettingsSingleton>
    {
        SkySettings m_Settings { get; set; }

        public static SkySettings overrideSettings
        {
            get { return instance.m_Settings; }
            set { instance.m_Settings = value; }
        }
    }
}
