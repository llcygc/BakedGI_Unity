using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph.ClusterPipeline { 

    [Serializable]
    public class VolumetricSettings : VolumeComponent
    {
        //public enum FogMediaResolution
        //{
        //    _128 = 128,
        //    _256 = 256,
        //    _512 = 512,
        //    _1024 = 1024
        //}

        //public FogMediaResolution fogMediaResolution = FogMediaResolution._256;
        
        public GradientParameter GlobalFogMediaGradient = new GradientParameter(new Gradient());

        public BoolParameter enalbeVelocityPass = new BoolParameter(false);
        public Vector2Parameter MinMaxHeight = new Vector2Parameter(new Vector2(0, 100));
        public Vector2Parameter MinMaxDistance = new Vector2Parameter(new Vector2(0, 500));
        public MinFloatParameter BaseDensity = new MinFloatParameter(0.0f, 0.0f);
        public MinFloatParameter ScatterIntensity = new MinFloatParameter(1.0f, 1.0f);
        public NoInterpClampedFloatParameter OcclussionFactor = new NoInterpClampedFloatParameter(0.2f, 0.0f, 1.0f);

        public ColorParameter AmbientColor = new ColorParameter(new Color(0, 0, 0));
        public ColorParameter BackFaceColor = new ColorParameter(new Color(0, 0, 0));

        public BoolParameter UseNoise = new BoolParameter(false);
        public Vector3Parameter NoiseScale = new Vector3Parameter(new Vector3(1, 1, 1));
        public FloatRangeParameter NoiseDensityClamp = new FloatRangeParameter(new Vector2(0, 1), 0.0f, 1.0f);
        public Vector3Parameter Veclocity = new Vector3Parameter(new Vector3(0.3f, 0, 0));

        public NoInterpClampedFloatParameter PhaseFunction = new NoInterpClampedFloatParameter(0.0f, 0.0f, 1.0f);

        public BoolParameter MediaVelocity = new BoolParameter(false);
        public BoolParameter LightVelocity = new BoolParameter(false);
        public NoInterpClampedFloatParameter TemporalBlendWeight = new NoInterpClampedFloatParameter(0.9f, 0.0f, 1.0f);
        private float[] fogMediaArray;

        public float[] GetFogMediaArrayStatic()
        {
            if (fogMediaArray == null)
            {
                fogMediaArray = new float[256 * 4];
                CalculateFogMediaArray();
            }

            return fogMediaArray;
        }

        public float[] GetFogMediaArrayDynamic()
        {
            if (fogMediaArray == null)
                fogMediaArray = new float[256 * 4];

            CalculateFogMediaArray();

            return fogMediaArray;
        }

        public void CalculateFogMediaArray()
        {
            GradientAlphaKey[] alphaKeys = GlobalFogMediaGradient.value.alphaKeys;
            GradientColorKey[] colorKeys = GlobalFogMediaGradient.value.colorKeys;

            int index = 0;
            float occlusion = 1.0f;

            while (index < 256)
            {
                float pos = index / 255.0f;

                Color actualColor = GlobalFogMediaGradient.value.Evaluate(pos);
                occlusion *= (1 - Mathf.Clamp(actualColor.a - AmbientColor.value.a, 0, 1) * OcclussionFactor);

                fogMediaArray[index * 4] = actualColor.r * occlusion;
                fogMediaArray[index * 4 + 1] = actualColor.g * occlusion;
                fogMediaArray[index * 4 + 2] = actualColor.b * occlusion;
                fogMediaArray[index * 4 + 3] = actualColor.a;
                index++;
            }
        }
    }

    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public sealed class GradientParameter : VolumeParameter<Gradient>
    {
        public GradientParameter(Gradient value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public sealed class FloatArrayParameter : VolumeParameter<float[]>
    {
        public FloatArrayParameter(float[] value, bool overrideState = false)
            : base(value, overrideState) { }
    }
}
