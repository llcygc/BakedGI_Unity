using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Viva.Rendering.RenderGraph.ClusterPipeline;

using UnityObject = UnityEngine.Object;

namespace UnityEditor.Experimental.Rendering.ClusterPipeline
{
    static class ClusterAssetFactory
    {
        static string s_RenderPipelineResourcesPath
        {
            get { return ClusterEditorUtils.GetClusterRenderPipelinePath() + "RenderPipelineResources/ClusterRenderPipelineResources.asset"; }
        }

        [MenuItem("RenderPipeline/ClusterRenderPipeline/Create Pipeline Asset", false, 16)]
        static void CreateHDRenderPipeline()
        {
            var instance = ScriptableObject.CreateInstance<ClusterRenderPipeLineAsset>();
            AssetDatabase.CreateAsset(instance, ClusterEditorUtils.GetClusterRenderPipelinePath() + "ClusterRenderPipelineAsset.asset");

            // If it exist, load renderPipelineResources
            instance.renderPipelineResources = AssetDatabase.LoadAssetAtPath<ClusterRenderPipelineResources>(s_RenderPipelineResourcesPath);
        }

        [MenuItem("RenderPipeline/ClusterRenderPipeline/Create Resources Asset", false, 15)]
        static void CreateRenderPipelineResources()
        {
            string ClusterRenderPipelinePath = ClusterEditorUtils.GetClusterRenderPipelinePath();
            //string PostProcessingPath = ClusterEditorUtils.GetPostProcessingPath();
            string CorePath = ClusterEditorUtils.GetCorePath();

            var instance = ScriptableObject.CreateInstance<ClusterRenderPipelineResources>();
            instance.clusterLightAssignmentCS = Load<ComputeShader>(ClusterRenderPipelinePath + "Shaders/ClusterCompute.compute");
            instance.volumetricLightingCS = Load<ComputeShader>(ClusterRenderPipelinePath + "Shaders/VolumetricEffect/VolumetricLightCompute.compute");
            instance.volumetricFogMediaCS = Load<ComputeShader>(ClusterRenderPipelinePath + "Shaders/VolumetricEffect/VolumetricFogDensityCompute.compute");
            instance.volumetricFogAccumulateCS = Load<ComputeShader>(ClusterRenderPipelinePath + "Shaders/VolumetricEffect/VolulmetricFogCompute.compute");

            instance.shadowCasterShader = Load<Shader>(ClusterRenderPipelinePath + "Shaders/ShadowMapDepth.shader");
            // Sky
            instance.blitCubemap = Load<Shader>(ClusterRenderPipelinePath + "Sky/BlitCubemap.shader");
            instance.buildProbabilityTables = Load<ComputeShader>(ClusterRenderPipelinePath + "Sky/BuildProbabilityTables.compute");
            instance.debugDisplayLatlongShader = Load<Shader>(ClusterRenderPipelinePath + "Debug/DebugDisplayLatlong.Shader");
            instance.computeGgxIblSampleData = Load<ComputeShader>(ClusterRenderPipelinePath + "Sky/ComputeGgxIblSampleData.compute");
            instance.GGXConvolve = Load<Shader>(ClusterRenderPipelinePath + "Sky/GGXConvolve.shader");
            instance.opaqueAtmosphericScattering = Load<Shader>(ClusterRenderPipelinePath + "Sky/OpaqueAtmosphericScattering.shader");

            // Skybox/Cubemap is a builtin shader, must use Sahder.Find to access it. It is fine because we are in the editor
            instance.skyboxCubemap = Shader.Find("Skybox/Cubemap");

            // Shadow
            instance.shadowClearShader = Load<Shader>(CorePath + "Shadow/ShadowClear.shader");
            instance.shadowBlurMoments = Load<ComputeShader>(CorePath + "Shadow/ShadowBlurMoments.compute");
            instance.debugShadowMapShader = Load<Shader>(CorePath + "Shadow/DebugDisplayShadowMap.shader");

            instance.cameraMotionVectors = Load<Shader>(ClusterRenderPipelinePath + "RenderPipelineResources/CameraMotionVectors.shader");
            instance.copyDepthBuffer = Load<Shader>(ClusterRenderPipelinePath + "RenderPipelineResources/CopyDepthBuffer.shader");
            instance.blit = Load<Shader>(ClusterRenderPipelinePath + "RenderPipelineResources/Blit.shader");

            instance.debugFullScreenShader = Load<Shader>(ClusterRenderPipelinePath + "Debug/DebugFullScreen.Shader");
            instance.debugColorPickerShader = Load<Shader>(ClusterRenderPipelinePath + "Debug/DebugColorPicker.Shader");

            instance.fogandAOCS = Load<ComputeShader>(ClusterRenderPipelinePath + "Shaders/PostProcess/FogandAO_CS.compute");
            instance.colorGradingCS = Load<ComputeShader>(ClusterRenderPipelinePath + "Shaders/PostProcess/ColorGrading_CS.compute");
            instance.stencilMaskGenCS = Load<ComputeShader>(ClusterRenderPipelinePath + "Shaders/PostProcess/GradiantStencilMaskGen.compute");
            instance.expandMaskedBufferCS = Load<ComputeShader>(ClusterRenderPipelinePath + "Shaders/PostProcess/ExpandMaskedBuffer.compute");
            instance.stencilMaskBlit = Load<Shader>(ClusterRenderPipelinePath + "Shaders/PostProcess/StencilMaskBlit.shader");
            instance.screenSpaceShadowCS = Load<ComputeShader>(ClusterRenderPipelinePath + "Shaders/PostProcess/ScreenSpaceShadow.compute");
            instance.screenSpaceShadowShader = Load<Shader>(ClusterRenderPipelinePath + "Shaders/PostProcess/ScreenSpaceShadow.shader");

            instance.TextureBlurCS = Load<ComputeShader>(ClusterRenderPipelinePath + "Shaders/TextureBlur.compute");

            AssetDatabase.CreateAsset(instance, s_RenderPipelineResourcesPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Note: move this to a static using once we can target C#6+
        static T Load<T>(string path)
            where T : UnityObject
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
