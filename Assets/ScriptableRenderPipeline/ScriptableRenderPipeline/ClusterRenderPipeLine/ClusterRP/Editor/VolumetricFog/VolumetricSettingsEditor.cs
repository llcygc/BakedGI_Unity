using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Viva.Rendering.RenderGraph.ClusterPipeline;

namespace UnityEditor.Experimental.Rendering.ClusterPipeline
{
    [CanEditMultipleObjects]
    [VolumeComponentEditor(typeof(VolumetricSettings))]
    public class VolumetricSettingsEditor : VolumeComponentEditor
    {
        public class Styles
        {
            public GUIContent fogMediaResolution = new GUIContent("Heigh resolution", "Resolution for heigh fog density gradiant.");
            public GUIContent minmaxHeight = new GUIContent("Min-Max Height", "Height range of the gradient data");
            public GUIContent minmaxDistance = new GUIContent("Min-Max Distance", "Fog volume range to the near clip plane of the camera");
            public GUIContent baseDensity = new GUIContent("Base Fog Density", "Max Density of the global fog. Actual Fog density will multiply by the alpha of the gradient data.");
            public GUIContent scatterIntensity = new GUIContent("Scatter Intensity", "Scattering of the fog media");
            public GUIContent occlusionFactor = new GUIContent("Fake Occlusion Factor", "Fake the absorb and out scatter by the volume media");

            public GUIContent fogMediaGradient = new GUIContent("Fog media gradient", "Gradient for height fog params");
            public GUIContent ambientColor = new GUIContent("Ambient Color", "Ambient Color");
            public GUIContent velocity = new GUIContent("Flow Velocity", "The flow velocity of fog");
            public GUIContent useNoiseDensity = new GUIContent("Use noise", "If use a perlin noise as the density of the fog");
            public GUIContent noiseClampRange = new GUIContent("Noise Clamp Range", "Clamp noise density to the range");
            public GUIContent noiseScale = new GUIContent("Noise Scale", "The scale of the perlin noise in XYZ directions");
            public GUIContent phg = new GUIContent("Phase function factor", "The k factor of phase function");
            public GUIContent backColor = new GUIContent("Back Face Color", "The color when you look back to the light direction");
            public GUIContent blendWeight = new GUIContent("Temporal Blend Weight", "The blend weight to the last frame volumetric data");

            public GUIContent mediaVelocity = new GUIContent("Enable Media Velocity", "Compute media velocity for temporal velocity");
            public GUIContent lightVelocity = new GUIContent("Enable Light Velocity", "Compute light velocity for temporal velocity");
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

        //private SerializedDataParameter fogMediaRes;
        private SerializedDataParameter fogGradiant;
        private SerializedDataParameter fogMinMaxHeight;
        private SerializedDataParameter fogMinMaxDistance;
        private SerializedDataParameter baseDensity;
        private SerializedDataParameter scatterIntensity;
        private SerializedDataParameter ambientColor;
        private SerializedDataParameter useNoise;
        private SerializedDataParameter noiseScale;
        private SerializedDataParameter noiseClampRange;
        private SerializedDataParameter velocity;
        private SerializedDataParameter backfaceColor;
        private SerializedDataParameter phaseFunction;
        private SerializedDataParameter blendWeight;
        private SerializedDataParameter mediaVelocity;
        private SerializedDataParameter lightVelocity;
        private SerializedDataParameter occlusionFactor;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<VolumetricSettings>(serializedObject);
            //fogMediaRes = serializedObject.FindProperty("fogMediaResolution");
            fogGradiant = Unpack(o.Find(x => x.GlobalFogMediaGradient));
            fogMinMaxHeight = Unpack(o.Find(x => x.MinMaxHeight));
            fogMinMaxDistance = Unpack(o.Find(x => x.MinMaxDistance));
            baseDensity = Unpack(o.Find(x => x.BaseDensity));
            scatterIntensity = Unpack(o.Find(x => x.ScatterIntensity));
            ambientColor = Unpack(o.Find(x => x.AmbientColor));
            noiseScale = Unpack(o.Find(x => x.NoiseScale));
            useNoise = Unpack(o.Find(x => x.UseNoise));
            noiseClampRange = Unpack(o.Find(x => x.NoiseDensityClamp));
            velocity = Unpack(o.Find(x => x.Veclocity));
            backfaceColor = Unpack(o.Find(x => x.BackFaceColor));
            phaseFunction = Unpack(o.Find(x => x.PhaseFunction));
            blendWeight = Unpack(o.Find(x => x.TemporalBlendWeight));
            mediaVelocity = Unpack(o.Find(x => x.MediaVelocity));
            lightVelocity = Unpack(o.Find(x => x.LightVelocity));
            occlusionFactor = Unpack(o.Find(x => x.OcclussionFactor));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(fogMinMaxHeight, styles.minmaxHeight);
            PropertyField(fogMinMaxDistance, styles.minmaxDistance);
            PropertyField(baseDensity, styles.baseDensity);
            PropertyField(scatterIntensity, styles.scatterIntensity);
            PropertyField(occlusionFactor, styles.occlusionFactor);
            //EditorGUI.BeginChangeCheck();
                PropertyField(fogGradiant, styles.fogMediaGradient);
            //if (EditorGUI.EndChangeCheck())
            //{
            //    (target as VolumetricSettings).CalculateFogMediaArray();
            //    EditorUtility.SetDirty(target);
            //}

            EditorGUILayout.Space();
            PropertyField(phaseFunction, styles.phg);
            PropertyField(ambientColor, styles.ambientColor);
            //PropertyField(backfaceColor, styles.backColor);

            EditorGUILayout.Space();
            PropertyField(useNoise, styles.useNoiseDensity);
            PropertyField(noiseClampRange, styles.noiseClampRange);
            PropertyField(noiseScale, styles.noiseScale);
            PropertyField(velocity, styles.velocity);
            PropertyField(blendWeight, styles.blendWeight);

            EditorGUILayout.Space();
            PropertyField(mediaVelocity, styles.mediaVelocity);
            PropertyField(lightVelocity, styles.lightVelocity);
        }

        //public void CalculateFogMediaArray()
        //{
        //    Gradient targeGradient = (Gradient)fogGradiant.value.gra;
        //    GradientAlphaKey[] alphaKeys = fogGradiant.value.serializedObject as Gradient.alphaKeys;
        //    GradientColorKey[] colorKeys = fogGradiant.value.colorKeys;

        //    int index = 0;
        //    float occlusion = 1.0f;

        //    while (index < 256)
        //    {
        //        float pos = index / 255.0f;

        //        Color actualColor = GlobalFogMediaGradient.value.Evaluate(pos);
        //        occlusion *= (1 - Mathf.Clamp(actualColor.a - AmbientColor.value.a, 0, 1) * OcclussionFactor);

        //        fogMediaArray[index * 4] = actualColor.r * occlusion;
        //        fogMediaArray[index * 4 + 1] = actualColor.g * occlusion;
        //        fogMediaArray[index * 4 + 2] = actualColor.b * occlusion;
        //        fogMediaArray[index * 4 + 3] = actualColor.a;
        //        FogMediaFloatArray.value[index * 4] = actualColor.r * occlusion;
        //        FogMediaFloatArray.value[index * 4 + 1] = actualColor.g * occlusion;
        //        FogMediaFloatArray.value[index * 4 + 2] = actualColor.b * occlusion;
        //        FogMediaFloatArray.value[index * 4 + 3] = actualColor.a;
        //        index++;
        //    }
        //}
    }
}
