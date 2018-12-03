using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph
{
    public enum AdditionalCameraType
    {
        StaticShadowCamera,
        EditorPreviewCamera,
        EditorSceneViewCamera,
        EditorGameViewCamera
    }

    public class CameraManager
    {
        static Dictionary<Camera, AdditionalCameraData> s_Cameras = new Dictionary<Camera, AdditionalCameraData>();
        static List<Camera> s_Cleanup = new List<Camera>();

        public static void CleanUnused()
        {

        }
    }

    [DisallowMultipleComponent, ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class AdditionalCameraData : MonoBehaviour, ISerializationCallbackReceiver
    {
        public enum RenderingType
        {
            Unlit,
            SimpleLight,
            ClusterLight,
            StaticShadow
        }

        private Camera m_Camera;

        public RenderingType m_RenderingType = RenderingType.ClusterLight;
        public bool m_DisplayVolumetricFog = true;

        bool m_FrameSettingIsDirty = true;

        public void OnBeforeSerialize()
        {

        }

        void OnEnable()
        {
            m_Camera = GetComponent<Camera>();
            if (m_Camera == null)
                return;
        }

        public void UpdateCamera()
        {
            m_Camera = gameObject.GetComponent<Camera>();
        }

        public void OnAfterDeserialize()
        {
            m_FrameSettingIsDirty = true;
        }
    }
}
