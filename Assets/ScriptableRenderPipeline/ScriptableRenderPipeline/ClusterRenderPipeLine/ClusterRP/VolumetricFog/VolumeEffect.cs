using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    [ExecuteInEditMode]
    public class VolumeEffect : MonoBehaviour
    {
        [ColorUsage(false, true)]
        public Color color = Color.white;
        public float extinction = 100.0f;

        [Range(0, 1)]
        public float edgeBlend = 0.0f;
        public Vector4 animationDir;
        public Vector4 noiseScaleContrast = Vector4.one;
        public Vector4 noiseClampRange;

        private void OnEnable()
        {
            VolumetricManager.RegisterVolumeData(this);
        }

        private void OnDisable()
        {
            VolumetricManager.UnRegisterVolumeData(this);
        }

        public VolumeEffectData GetVolumeEffectData()
        {
            VolumeEffectData vData = new VolumeEffectData();
            vData.colorExtinction = new Vector4(color.r, color.g, color.b, extinction);
            vData.animationDir = animationDir;
            noiseScaleContrast.w = edgeBlend;
            vData.noiseScaleContrast = noiseScaleContrast;
            vData.noiseClampRange = noiseClampRange;
            vData.worldToLocalMatrix = this.gameObject.transform.worldToLocalMatrix;
            Matrix4x4 transMatrix = Matrix4x4.Translate(Vector3.one / 2);
            //vData.worldToLocalMatrix = vData.worldToLocalMatrix * transMatrix;
            return vData;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.0f, 1.0f, 1.0f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            //Gizmos.DrawWireCube(this.transform.position, this.transform.localScale);
        }
    }
}
