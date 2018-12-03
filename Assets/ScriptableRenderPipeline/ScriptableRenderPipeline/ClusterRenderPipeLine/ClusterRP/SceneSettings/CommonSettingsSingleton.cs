using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph
{
    public class CommonSettingsSingleton : UnityEngine.Experimental.Rendering.Singleton<CommonSettingsSingleton>
    {
        private CommonSettings settings { get; set; }

        public static CommonSettings overrideSettings
        {
            get { return instance.settings; }
            set { instance.settings = value; }
        }
    }
}
