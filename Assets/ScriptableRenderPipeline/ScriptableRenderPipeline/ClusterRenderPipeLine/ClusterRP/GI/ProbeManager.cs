using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    public class Probe
    {
        public Vector3 position;
        public Vector4 scaleOffset;
        public RenderTexture RadianceTexture;
        public RenderTexture NormalTexture;
        //RenderTexture DistanceTexture;

        public Probe(Vector3 pos)
        {
            position = pos;

            RenderTextureDescriptor radDesc = new RenderTextureDescriptor(1024, 1024, RenderTextureFormat.RGB111110Float);
            radDesc.dimension = TextureDimension.Cube;
            RadianceTexture = new RenderTexture(radDesc);

            RenderTextureDescriptor normalDesc = new RenderTextureDescriptor(1024, 1024, RenderTextureFormat.ARGBHalf);
            normalDesc.dimension = TextureDimension.Cube;
            NormalTexture = new RenderTexture(normalDesc);
        }

        public void Dispose()
        {
            RadianceTexture.Release();
            RadianceTexture = null;

            NormalTexture.Release();
            NormalTexture = null;
        }
    }

    public class ProbeManager
    {
        public struct ProbeData
        {
            Vector3 position;
            Vector4 scaleOffset;
        }
        //>>> System.Lazy<T> is broken in Unity (legacy runtime) so we'll have to do it ourselves :|
        static readonly ProbeManager s_Instance = new ProbeManager();
        public static ProbeManager instance { get { return s_Instance; } }

        public bool isDynamic;
        public bool needRender;
        public bool showDebug;
        public Vector3 ProbeVolumeDimension;
        public List<Probe> Probes = new List<Probe>();

        private float NearPlane = 0.3f;
        private float FarPlane = 1000.0f;

        ComputeBuffer ProbeDataBuffer;

        RenderTexture radianceMapOctan;
        RenderTexture normalMapOctan;

        public void AllocateProbes(Vector3 dimenson, Transform probeVolume)
        {
            if (dimenson.x > 0 && dimenson.y > 0 && dimenson.z > 0)
            {
                needRender = true;
                Probes.Clear();

                for (int i = 0; i < dimenson.x; i++)
                    for (int j = 0; j < dimenson.y; j++)
                        for (int k = 0; k < dimenson.z; k++)
                        {
                            Vector3 coord = new Vector3((float)i / (dimenson.x - 1) - 0.5f, (float)j / (dimenson.y - 1) - 0.5f, (float)k / (dimenson.z - 1) - 0.5f);
                            coord = probeVolume.localToWorldMatrix.MultiplyPoint(coord);
                            Gizmos.color = new Color(0.0f, 1.0f, 1.0f, 1.0f);
                            Gizmos.DrawSphere(coord, 0.3f);

                            Probe newProbe = new Probe(coord);
                        }
            }
            else
            {
                UnityEngine.Debug.LogError("Probe count must be greater than 1 in every dimension.");
            }
        }

        public void UpdateProbeSettings(bool dynamic, bool debug, float near, float far)
        {
            isDynamic = dynamic;
            showDebug = debug;
            NearPlane = near;
            FarPlane = far;
        }

        public void Render(ScriptableRenderContext context, CommandBuffer cmd)
        {
            if (needRender || isDynamic)
            {
                needRender = false;



                ReprojectCubeToOctan();
            }
        }

        public void ShowDebug(bool show)
        {
            showDebug = show;
        }

        private void RenderCubeMaps()
        {

        }

        private void ReprojectCubeToOctan()
        {

        }

        public void PushGlobalParams(CommandBuffer cmd)
        {

        }
    }
}
