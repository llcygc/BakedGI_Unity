using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    public struct VolumeEffectData
    {
        public Vector4 colorExtinction;
        public Vector4 animationDir;
        public Vector4 noiseScaleContrast;
        public Vector4 noiseClampRange;
        public Matrix4x4 worldToLocalMatrix;
    }

    public class VolumetricManager
    {
        private const int MAX_VOLUME_COUNT = 5;
        public static List<VolumeEffectData> volumeEffectDatas = new List<VolumeEffectData>();
        public static List<VolumeEffect> volumeEffects = new List<VolumeEffect>();
        public static ComputeBuffer volumeEffectsBuffer;
        public static int volumeEffectCount = 0;
        public static int[] visibleVolumeIndices = new int[MAX_VOLUME_COUNT];

        public static List<BoundingSphere> boundingSpheres = new List<BoundingSphere>();

        public static CullingGroup cullingGroup;

        public static void RegisterVolumeData(VolumeEffect ve)
        {
            volumeEffects.Add(ve);
        }

        public static void PrepareCull(Camera cam)
        {
            UpdateAllVolumeDataBounds();

            cullingGroup = new CullingGroup();
            cullingGroup.targetCamera = cam;
            cullingGroup.SetBoundingSpheres(boundingSpheres.ToArray());
            cullingGroup.SetBoundingSphereCount(Mathf.Min(boundingSpheres.Count, MAX_VOLUME_COUNT));
        }

        public static void UpdateAllVolumeDataBounds()
        {
            boundingSpheres.Clear();
            if (volumeEffects.Count > 0)
            {
                foreach (var volume in volumeEffects)
                {
                    Vector3 pos = volume.transform.position;
                    Vector3 scale = volume.transform.localScale;
                    float rad = Mathf.Max(Mathf.Max(scale.x, scale.y), scale.z);
                    BoundingSphere bs = new BoundingSphere
                    {
                        position = volume.transform.position,
                        radius = rad
                    };
                    boundingSpheres.Add(bs);
                }
            }
        }

        public static void UnRegisterVolumeData(VolumeEffect ve)
        {
            volumeEffects.Remove(ve);
        }

        public static void CollectVolumeEffectsData(Camera cam)
        {
            volumeEffectDatas.Clear();
            for (int i = 0; i < volumeEffectCount; i++)
            {
                volumeEffectDatas.Add(volumeEffects[visibleVolumeIndices[i]].GetVolumeEffectData());
            }
            
            if (volumeEffectCount > 0)
            {
                if (volumeEffectsBuffer != null && volumeEffectCount != volumeEffectsBuffer.count)
                {
                    if (volumeEffectsBuffer != null)
                        volumeEffectsBuffer.Release();
                    volumeEffectCount = volumeEffectDatas.Count;
                }
                volumeEffectsBuffer = new ComputeBuffer(volumeEffectCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(VolumeEffectData)));
                volumeEffectsBuffer.SetData(volumeEffectDatas);
            }
                
        }

        public static void CullVolumeDatas(Camera cam)
        {
            Assert.IsNotNull(cullingGroup, "Culling was not prepared, please prepare cull before performing it.");

            volumeEffectCount = cullingGroup.QueryIndices(true, visibleVolumeIndices, 0);

            cullingGroup.Dispose();
            cullingGroup = null;
        }

        public static void BindParams(CommandBuffer cmd, ComputeShader cs, int kernel)
        {
            cmd.SetComputeBufferParam(cs, kernel, ClusterShaderIDs._VolumeEffectData, volumeEffectsBuffer);
            cmd.SetComputeIntParam(cs, ClusterShaderIDs._VolumeEffectCount, volumeEffectCount);
        }
    }
}
