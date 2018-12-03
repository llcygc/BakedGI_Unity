using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    [ExecuteInEditMode]
    public class WindData : MonoBehaviour
    {
        [Range(0.0f, 1.0f)]
        public float scale = 1.0f;
        private WindZone windZone;

        private Vector4 windData;
        private Vector4 windDataLastFrame;
        // Use this for initialization
        void Start()
        {
            windZone = this.GetComponent<WindZone>();
            if (windZone)
            {
                float strength = WindStrength(Time.time * scale, windZone.windMain, windZone.windPulseMagnitude, windZone.windPulseFrequency);
                Vector3 dir = windZone.transform.forward * strength;
                windData = windDataLastFrame = new Vector4(dir.x, dir.y, dir.z, windZone.windTurbulence);
            }
        }

        // Update is called once per frame
        void Update()
        {
            if(windZone)
            {
                float strength = WindStrength(Time.time * scale, windZone.windMain, windZone.windPulseMagnitude, windZone.windPulseFrequency);
                Vector3 dir = windZone.transform.forward * strength;
                windData = new Vector4(dir.x, dir.y, dir.z, windZone.windTurbulence);
                Shader.SetGlobalVector("_WindDirectionStrength", windData);
                Shader.SetGlobalVector("_WindDirectionStrengthLastFrame", windDataLastFrame);
                windDataLastFrame = windData;
            }
        }

        float WindStrength(float time, float mainStrength, float gustStrength, float frequency)
        {
            time *= frequency;
            float result = Mathf.Cos(time * Mathf.PI) * Mathf.Cos(3 * time * Mathf.PI) * Mathf.Cos(5 * time * Mathf.PI) * Mathf.Cos(7 * time * Mathf.PI) * mainStrength + Mathf.Sin(25 * time * Mathf.PI) * gustStrength;
            return result;
        }
    }
}
