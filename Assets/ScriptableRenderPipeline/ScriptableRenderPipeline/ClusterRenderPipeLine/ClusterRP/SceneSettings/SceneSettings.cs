using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    [ExecuteInEditMode]
    public class SceneSettings : MonoBehaviour
    {
        public SceneSettingsProfile sharedProfile;
        private SceneSettingsProfile m_InternalProfile;
        public SceneSettingsProfile profile
        {
            get
            {
                if(m_InternalProfile == null)
                {
                    m_InternalProfile = ScriptableObject.CreateInstance<SceneSettingsProfile>();

                    if(sharedProfile != null)
                    {
                        foreach(var item in sharedProfile.components)
                        {
                            var itemCopy = Instantiate(item);
                            m_InternalProfile.components.Add(itemCopy);
                        }
                    }
                }
                return m_InternalProfile;
            }
            set
            {
                m_InternalProfile = value;
            }
        }

        internal SceneSettingsProfile profileRef
        {
            get
            {
                return m_InternalProfile == null ? sharedProfile : m_InternalProfile;
            }
        }

        public bool HasInstantiatedProfile()
        {
            return m_InternalProfile != null;
        }

        public CommonSettings commonSettings
        {
            set { m_CommonSettings = value; }
            get { return m_CommonSettings; }
        }

        public SkySettings skySettings
        {
            set { m_SkySettings = value; }
            get { return m_SkySettings; }
        }

        public VolumetricSettings volumetricSettings
        {
            set { m_VolumetricSettings = value; }
            get { return m_VolumetricSettings; }
        }

        [SerializeField] private CommonSettings m_CommonSettings = null;
        [SerializeField] private SkySettings    m_SkySettings = null;
        [SerializeField] private VolumetricSettings m_VolumetricSettings = null;

        // Use this for initialization
        void OnEnable()
        {
            SceneSettingsManager.instance.AddSceneSettings(this);

            ClusterRenderPipeline clusterPipeline = RenderPipelineManager.currentPipeline as ClusterRenderPipeline;

            if (clusterPipeline != null)
            {
                clusterPipeline.OnSceneLoad();
            }
        }

        void OnDisable()
        {
            SceneSettingsManager.instance.RemoveSceneSettings(this);
        }

        void OnValidate()
        {
            // If the setting is already the one currently used we need to tell the manager to reapply it.
            if (SceneSettingsManager.instance.GetCurrentSceneSetting())
            {
                SceneSettingsManager.instance.UpdateCurrentSceneSetting();
            }
        }
    }
}
