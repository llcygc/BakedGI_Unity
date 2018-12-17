using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.Rendering.ClusterPipeline;
#endif
namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    [ExecuteInEditMode]
    public class GI_Settings : MonoBehaviour
    {
        public enum ProbeDebugMode
        {
            Radiance = 0,
            Normal,
            Distance
        };
        public bool ShowDebug;
        public ProbeDebugMode DebugMode;
        public bool IsDynamic;

        public float NearPlane = 0.3f;
        public float FarPlane = 1000.0f;

        public Vector3Int ProbeDimensions = new Vector3Int(4, 4, 4);

        private void OnEnable()
        {
            AllocateProbes();
            UpdateProbeSettings();
        }
        // Use this for initialization
        void Start()
        {
        }

        // Update is called once per frame
        void Update()
        {

        }

        private void OnDisable()
        {
            ProbeManager.instance.Dispose();
        }

        public void AllocateProbes()
        {
            ProbeManager.instance.AllocateProbes(ProbeDimensions, this.gameObject.transform);
        }

        public void UpdateProbeSettings()
        {
            ProbeManager.instance.UpdateProbeSettings(IsDynamic, ShowDebug, NearPlane, FarPlane, DebugMode);
        }

#if UNITY_EDITOR

        private void OnDrawGizmos()
        {
            if (!ShowDebug)
            {
                Gizmos.color = new Color(0.0f, 1.0f, 1.0f, 0.8f);
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

                Gizmos.matrix = Matrix4x4.identity;
                if (ProbeDimensions.x > 0 && ProbeDimensions.y > 0 && ProbeDimensions.z > 0)
                {
                    for (int i = 0; i < ProbeDimensions.x; i++)
                        for (int j = 0; j < ProbeDimensions.y; j++)
                            for (int k = 0; k < ProbeDimensions.z; k++)
                            {
                                Vector3 coord = new Vector3((float)i / (ProbeDimensions.x - 1) - 0.5f, (float)j / (ProbeDimensions.y - 1) - 0.5f, (float)k / (ProbeDimensions.z - 1) - 0.5f);
                                coord = transform.localToWorldMatrix.MultiplyPoint(coord);
                                Gizmos.color = new Color(0.0f, 1.0f, 1.0f, 1.0f);
                                Gizmos.DrawSphere(coord, 0.3f);
                            }
                }
            }

            ProbeManager.instance.SetUpDebug(Vector3.zero, Vector3.zero, false);
        }

        private void OnDrawGizmosSelected()
        {
            Camera cam = Camera.current;
            RaycastHit hit;

            Vector3 mousePosition = Event.current.mousePosition;
            mousePosition.y = SceneView.currentDrawingSceneView.camera.pixelHeight - mousePosition.y;

            Ray ray = cam.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out hit, 1000.0f))
            {
                Handles.color = Color.green;
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                Handles.DrawLine(hit.point + hit.normal * 2.0f, hit.point);
                Handles.DrawWireDisc(hit.point, hit.normal, 0.5f);
                ProbeManager.instance.SetUpDebug(hit.point, hit.normal, true);
            }
            else
                ProbeManager.instance.SetUpDebug(Vector3.zero, Vector3.zero, false);
        }

#endif
    }
}
