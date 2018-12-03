using System.Collections.Generic;

namespace UnityEngine.Experimental.Rendering
{
    [ExecuteInEditMode]
    public class Volume : MonoBehaviour
    {
        [Tooltip("A global volume is applied to the whole scene.")]
        public bool isGlobal = false;

        [Tooltip("Volume priority in the stack. Higher number means higher priority. Negative values are supported.")]
        public float priority = 0f;

        [Tooltip("Outer distance to start blending from. A value of 0 means no blending and the volume overrides will be applied immediately upon entry.")]
        public float blendDistance = 0f;

        [Range(0f, 1f), Tooltip("Total weight of this volume in the scene. 0 means it won't do anything, 1 means full effect.")]
        public float weight = 1f;

        // Modifying sharedProfile will change the behavior of all volumes using this profile, and
        // change profile settings that are stored in the project too
        public VolumeProfile sharedProfile;

        // This property automatically instantiates the profile and makes it unique to this volume
        // so you can safely edit it via scripting at runtime without changing the original asset
        // in the project.
        // Note that if you pass in your own profile, it is your responsability to destroy it once
        // it's not in use anymore.
        public VolumeProfile profile
        {
            get
            {
                if (m_InternalProfile == null)
                {
                    m_InternalProfile = ScriptableObject.CreateInstance<VolumeProfile>();

                    if (sharedProfile != null)
                    {
                        foreach (var item in sharedProfile.components)
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

        internal VolumeProfile profileRef
        {
            get
            {
                return m_InternalProfile == null
                    ? sharedProfile
                    : m_InternalProfile;
            }
        }

        public bool HasInstantiatedProfile()
        {
            return m_InternalProfile != null;
        }

        // Needed for state tracking (see the comments in Update)
        int m_PreviousLayer;
        float m_PreviousPriority;
        VolumeProfile m_InternalProfile;

        void OnEnable()
        {
            m_PreviousLayer = gameObject.layer;
            VolumeManager.instance.Register(this, m_PreviousLayer);
        }

        void OnDisable()
        {
            VolumeManager.instance.Unregister(this, gameObject.layer);
        }

        void Update()
        {
            // Unfortunately we need to track the current layer to update the volume manager in
            // real-time as the user could change it at any time in the editor or at runtime.
            // Because no event is raised when the layer changes, we have to track it on every
            // frame :/
            int layer = gameObject.layer;
            if (layer != m_PreviousLayer)
            {
                VolumeManager.instance.UpdateVolumeLayer(this, m_PreviousLayer, layer);
                m_PreviousLayer = layer;
            }

            // Same for priority. We could use a property instead, but it doesn't play nice with the
            // serialization system. Using a custom Attribute/PropertyDrawer for a property is
            // possible but it doesn't work with Undo/Redo in the editor, which makes it useless for
            // our case.
            if (priority != m_PreviousPriority)
            {
                VolumeManager.instance.SetLayerDirty(layer);
                m_PreviousPriority = priority;
            }
        }

#if UNITY_EDITOR

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.0f, 1.0f, 1.0f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }

#endif
    }
}
