using UnityEngine;
using UnityEditor;
using System;
using Viva.Rendering.RenderGraph;
using UnityEditor.Experimental.Rendering;

namespace UnityEditor.Experimental.Rendering.ClusterPipeline
{
    [VolumeComponentEditor(typeof(CommonSettings))]
    [CanEditMultipleObjects]
    public class CommonSettingsEditor
        : VolumeComponentEditor
    {
        private class Styles
        {
            public readonly GUIContent asyncCompute = new GUIContent("Enable Async Compute");
            public readonly GUIContent hasRefraction = new GUIContent("Has Refraction");
            public readonly GUIContent depthPrepass = new GUIContent("Enable Depth Prepass");
            public readonly GUIContent screenSpaceShadow = new GUIContent("Enalbe Screen Space Shadow");
            public readonly GUIContent maxShadowDistance = new GUIContent("Maximum shadow distance");
            public readonly GUIContent nearPlaneOffset = new GUIContent("Shadow near plane offset");
            public readonly GUIContent lightCullingMask = new GUIContent("Enable light culling mask");
            public readonly GUIContent msaaSamples = new GUIContent("MSAA samples");
            public readonly GUIContent opaqueSortFlag = new GUIContent("Opaque objects sort flag");
        }

        private static Styles s_Styles = null;
        private static Styles styles
        {
            get
            {
                if (s_Styles == null)
                    s_Styles = new Styles();
                return s_Styles;
            }
        }

        private SerializedDataParameter m_enableDepthPrepass;
        private SerializedDataParameter m_hasRefraction;
        private SerializedDataParameter m_enableAsyncCompute;
        private SerializedDataParameter m_enableScreenSpaceShadow;
        private SerializedDataParameter m_enableLightCullingMask;
        
        private SerializedDataParameter m_MSAASamples;
        private SerializedDataParameter m_OpaqueSortFlag;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<CommonSettings>(serializedObject);

            m_enableDepthPrepass = Unpack(o.Find(x => x.enableDepthPrepass));
            m_hasRefraction = Unpack(o.Find(x => x.hasRefraction));
            m_enableAsyncCompute = Unpack(o.Find(x => x.enableAsyncCompute));
            m_enableScreenSpaceShadow = Unpack(o.Find(x => x.enableScreenSpaceShadow));
            m_enableLightCullingMask = Unpack(o.Find(x => x.enableLightCullingMask));
            m_MSAASamples = Unpack(o.Find(x => x.msaaSamples));//serializedObject.FindProperty("m_Settings.m_MsaaSamples");
            //m_OpaqueSortFlag = Unpack(o.Find(x => x.opaqueSortFlag));//serializedObject.FindProperty("m_Settings.m_OpaqueSortFlag");
        }

        public override void OnInspectorGUI()
        {
            PropertyField(m_enableAsyncCompute, styles.asyncCompute);
            PropertyField(m_hasRefraction, styles.hasRefraction);
            PropertyField(m_enableDepthPrepass, styles.depthPrepass);
            PropertyField(m_enableScreenSpaceShadow, styles.screenSpaceShadow);
            PropertyField(m_enableLightCullingMask, styles.lightCullingMask);

            PropertyField(m_MSAASamples, styles.msaaSamples);
        }
    }
}
