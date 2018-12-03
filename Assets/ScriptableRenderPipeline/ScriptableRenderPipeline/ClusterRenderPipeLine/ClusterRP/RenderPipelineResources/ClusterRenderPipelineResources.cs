using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
#endif

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    public class ClusterRenderPipelineResources : ScriptableObject
    {
        // Debug
        public Shader debugDisplayLatlongShader;
        public Shader debugViewMaterialGBufferShader;
        public Shader debugViewTilesShader;
        public Shader debugFullScreenShader;

        //Cluster light assignment resources
        public ComputeShader clusterLightAssignmentCS;

        //Volumetric light/fog resources
        public ComputeShader volumetricLightingCS;
        public ComputeShader volumetricFogMediaCS;
        public ComputeShader volumetricFogAccumulateCS;

        //Lighting resources
        //public Shader clusterLightingShader;
        
        // Lighting resources
        public Shader deferredShader;
        public ComputeShader subsurfaceScatteringCS;
        public ComputeShader gaussianPyramidCS;
        public ComputeShader depthPyramidCS;
        public ComputeShader copyChannelCS;
        public ComputeShader applyDistortionCS;

        // Lighting tile pass resources
        public ComputeShader clearDispatchIndirectShader;
        public ComputeShader buildDispatchIndirectShader;
        public ComputeShader buildScreenAABBShader;
        public ComputeShader buildPerTileLightListShader;     // FPTL
        public ComputeShader buildPerBigTileLightListShader;
        public ComputeShader buildPerVoxelLightListShader;    // clustered
        public ComputeShader buildMaterialFlagsShader;
        public ComputeShader deferredComputeShader;
        public ComputeShader deferredDirectionalShadowComputeShader;

        // SceneSettings
        // These shaders don't need to be reference by RenderPipelineResource as they are not use at runtime (only to draw in editor)
        // public Shader drawSssProfile;
        // public Shader drawTransmittanceGraphShader;

        public Shader cameraMotionVectors;
        public Shader shadowCasterShader;
        public Shader copyDepthBuffer;
        public Shader blit;
        public Shader debugColorPickerShader;

        // Sky
        public Shader blitCubemap;
        public ComputeShader buildProbabilityTables;
        public ComputeShader computeGgxIblSampleData;
        public Shader GGXConvolve;
        public Shader opaqueAtmosphericScattering;

        public Shader skyboxCubemap;

        // Utilities / Core
        public ComputeShader encodeBC6HCS;
        public Shader cubeToPanoShader;
        public Shader blitCubeTextureFace;

        // Shadow
        public Shader shadowClearShader;
        public ComputeShader shadowBlurMoments;
        public Shader debugShadowMapShader;
        public Shader screenSpaceShadowShader;
        public ComputeShader screenSpaceShadowCS;
        public ComputeShader TextureBlurCS;

        //PostEffect
        public ComputeShader fogandAOCS;
        public ComputeShader colorGradingCS;
        public ComputeShader stencilMaskGenCS;
        public ComputeShader gradientResolveCS;
        public ComputeShader expandMaskedBufferCS;
        public ComputeShader toneMappingCS;
        public Shader stencilMaskBlit;
        public Texture2D stencilMaskTexture;

        public int applyDistortionKernel { get; private set; }

#if UNITY_EDITOR
        [ContextMenu("Generate Stencil Mask")]
        void GenerateStencilMask()
        {
            if(stencilMaskGenCS)
            {
                int width = 1344;
                int height = 1512;

                float widthf = 1344;
                float heightf = 1512;

                Vector2 res = new Vector2(widthf, heightf);
                Color[] texColors = new Color[width * height];

                Vector2 groupSize = res / 8.0f;

                int zoneIndex = 0;

                Vector2 test = new Vector2(-1.0f, 0.0f);

                if (test.magnitude > 1.0f)
                    UnityEngine.Debug.Log("Edge outside");
                else
                    UnityEngine.Debug.Log("Edge inside");

                for (int i = 0; i < width; i++)
                    for(int j = 0; j < height; j++)
                    {
                        Vector2 zoneCoord = new Vector2(i, j) / res;
                        zoneCoord.x = Mathf.Floor((float)i / 8.0f);
                        zoneCoord.y = Mathf.Floor((float)j / 8.0f);
                        zoneCoord /= groupSize;
                        zoneCoord.x = zoneCoord.x * 2.0f - 1.0f;
                        zoneCoord.y = zoneCoord.y * 2.0f - 1.0f;
                        float length = zoneCoord.magnitude;

                        if(length > 1.0f )
                            zoneIndex = 0;
                        else
                            zoneIndex = 1;

                        int index = j * width + i;

                        switch (zoneIndex)
                        {
                            case 0:
                                texColors[index] = Color.white;
                                break;
                            case 1:
                                texColors[index] = Color.black;
                                break;
                            default:
                                texColors[index] = Color.white;
                                break;
                        }

                    }
                

                var stencilMask2D = new Texture2D(width, height, TextureFormat.RGBA32, false);
                stencilMask2D.SetPixels(texColors);

                var stencilMask2D_R8 = new Texture2D(width, height, TextureFormat.R8, false);
                Graphics.ConvertTexture(stencilMask2D, stencilMask2D_R8);

                var srpPath = Directory.GetFiles(Application.dataPath, "SRPMARKER", SearchOption.AllDirectories).FirstOrDefault();
                if (srpPath != null)
                {
                    srpPath = new Uri(Application.dataPath).MakeRelativeUri(new Uri(Directory.GetParent(srpPath).ToString())).ToString();
                    srpPath = Path.Combine(srpPath, "ScriptableRenderPipeline/ClusterRenderPipeLine/ClusterRP/RenderPipelineResources");

                    string assetPath = srpPath + "/stencilMask.asset";
                    AssetDatabase.CreateAsset(stencilMask2D_R8, assetPath);
                }

                //stencilMaskBuffer.Release();
            }
        }
#endif

        void OnEnable()
        {
            applyDistortionKernel = -1;

            if (applyDistortionCS != null)
                applyDistortionKernel = applyDistortionCS.FindKernel("KMain");
        }
    }
}
