using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    public class GI_Settings : MonoBehaviour
    {
        public bool ShowDebug;
        public bool IsDynamic;
        public float NearPlane;
        public float FarPlane;

        public Vector3Int ProbeDimensions = new Vector3Int(4, 4, 4);
        
        // Use this for initialization
        void Start()
        {
            AllocateProbes();
            ProbeManager.instance.UpdateProbeSettings(IsDynamic, ShowDebug, NearPlane, FarPlane);
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void AllocateProbes()
        {
            ProbeManager.instance.AllocateProbes(ProbeDimensions, this.gameObject.transform);
        }

#if UNITY_EDITOR

        private void OnDrawGizmos()
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

#endif
    }
}
