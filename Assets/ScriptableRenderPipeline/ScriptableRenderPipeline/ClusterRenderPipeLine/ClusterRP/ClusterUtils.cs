using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    public class ClusterUtils
    {

        static public ClusterAdditionalLightData s_DefaultClusterAdditionLightData { get { return ComponentSingleton<ClusterAdditionalLightData>.instance; } }
        static MaterialPropertyBlock s_PropertyBlock = new MaterialPropertyBlock();

        public static List<T> FindComponentsofType<T>()
        {
            List<T> results = new List<T>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.isLoaded)
                {
                    var allGameObjects = s.GetRootGameObjects();
                    for (int j = 0; j < allGameObjects.Length; j++)
                    {
                        var go = allGameObjects[j];
                        results.AddRange(go.GetComponentsInChildren<T>());
                    }
                }
            }

            return results;
        }

        public static List<RenderPipelineMaterial> GetRenderPipelineMaterialList()
        {
            var baseType = typeof(RenderPipelineMaterial);
            var assembly = baseType.Assembly;

            var types = assembly.GetTypes()
                .Where(t => t.IsSubclassOf(baseType))
                .Select(Activator.CreateInstance)
                .Cast<RenderPipelineMaterial>()
                .ToList();

            // Note: If there is a need for an optimization in the future of this function, user can
            // simply fill the materialList manually by commenting the code abode and returning a
            // custom list of materials they use in their game.
            //
            // return new List<RenderPipelineMaterial>
            // {
            //    new Lit(),
            //    new Unlit(),
            //    ...
            // };

            return types;
        }

        public static Material GetBlitMaterial()
        {
            ClusterRenderPipeline clusterPipeline = RenderPipelineManager.currentPipeline as ClusterRenderPipeline;
            if (clusterPipeline != null)
            {
                return clusterPipeline.GetBlitMaterial();
            }

            return null;
        }

        public static void DrawFullScreen(CommandBuffer commandBuffer, RGCamera camera, Material material,
                                            RenderTargetIdentifier colorBuffer,
                                            MaterialPropertyBlock properties = null, int shaderPassId = 0)
        {
            CoreUtils.SetRenderTarget(commandBuffer, colorBuffer);
            commandBuffer.SetGlobalVector(ClusterShaderIDs._ScreenToTargetScale, camera.doubleBufferedViewportScale);
            commandBuffer.DrawProcedural(Matrix4x4.identity, material, shaderPassId, MeshTopology.Triangles, 3, 1, properties);
        }

        public static void BlitCameraTexture(CommandBuffer cmd, RGCamera rgCam, RTHandleSystem.RTHandle source, RTHandleSystem.RTHandle destination, float mipLevel = 0.0f, bool bilinear = false)
        {
            SetRenderTarget(cmd, rgCam, destination);
            BlitTexture(cmd, source, destination, rgCam.viewportScaleBias, mipLevel, bilinear);
        }

        // This set of RenderTarget management methods is supposed to be used when rendering into a camera dependent render texture.
        // This will automatically set the viewport based on the camera size and the RTHandle scaling info.
        public static void SetRenderTarget(CommandBuffer cmd, RGCamera camera, RTHandleSystem.RTHandle buffer, ClearFlag clearFlag, Color clearColor, int miplevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown, int depthSlice = 0)
        {
            cmd.SetRenderTarget(buffer, miplevel, cubemapFace, depthSlice);
            SetViewportAndClear(cmd, camera, buffer, clearFlag, clearColor);
        }

        public static void SetRenderTarget(CommandBuffer cmd, RGCamera camera, RTHandleSystem.RTHandle buffer, ClearFlag clearFlag = ClearFlag.None, int miplevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown, int depthSlice = 0)
        {
            SetRenderTarget(cmd, camera, buffer, clearFlag, CoreUtils.clearColorAllBlack, miplevel, cubemapFace, depthSlice);
        }

        public static void SetRenderTarget(CommandBuffer cmd, RGCamera camera, RTHandleSystem.RTHandle colorBuffer, RTHandleSystem.RTHandle depthBuffer, int miplevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown, int depthSlice = 0)
        {
            SetRenderTarget(cmd, camera, colorBuffer, depthBuffer, ClearFlag.None, CoreUtils.clearColorAllBlack, miplevel, cubemapFace, depthSlice);
        }

        public static void SetRenderTarget(CommandBuffer cmd, RGCamera camera, RTHandleSystem.RTHandle colorBuffer, RTHandleSystem.RTHandle depthBuffer, ClearFlag clearFlag, int miplevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown, int depthSlice = 0)
        {
            SetRenderTarget(cmd, camera, colorBuffer, depthBuffer, clearFlag, CoreUtils.clearColorAllBlack, miplevel, cubemapFace, depthSlice);
        }

        public static void SetRenderTarget(CommandBuffer cmd, RGCamera camera, RTHandleSystem.RTHandle colorBuffer, RTHandleSystem.RTHandle depthBuffer, ClearFlag clearFlag, Color clearColor, int miplevel = 0, CubemapFace cubemapFace = CubemapFace.Unknown, int depthSlice = 0)
        {
            CoreUtils.SetRenderTarget(cmd, colorBuffer, depthBuffer, miplevel, cubemapFace, depthSlice);
            SetViewportAndClear(cmd, camera, colorBuffer, clearFlag, clearColor);
        }

        public static void SetRenderTarget(CommandBuffer cmd, RGCamera camera, RenderTargetIdentifier[] colorBuffers, RTHandleSystem.RTHandle depthBuffer)
        {
            CoreUtils.SetRenderTarget(cmd, colorBuffers, depthBuffer, ClearFlag.None, CoreUtils.clearColorAllBlack);
            SetViewport(cmd, camera, depthBuffer);
        }

        public static void SetRenderTarget(CommandBuffer cmd, RGCamera camera, RenderTargetIdentifier[] colorBuffers, RTHandleSystem.RTHandle depthBuffer, ClearFlag clearFlag = ClearFlag.None)
        {
            CoreUtils.SetRenderTarget(cmd, colorBuffers, depthBuffer); // Don't clear here, viewport needs to be set before we do.
            SetViewportAndClear(cmd, camera, depthBuffer, clearFlag, CoreUtils.clearColorAllBlack);
        }

        public static void SetRenderTarget(CommandBuffer cmd, RGCamera camera, RenderTargetIdentifier[] colorBuffers, RTHandleSystem.RTHandle depthBuffer, ClearFlag clearFlag, Color clearColor)
        {
            cmd.SetRenderTarget(colorBuffers, depthBuffer);
            SetViewport(cmd, camera, depthBuffer);
            CoreUtils.ClearRenderTarget(cmd, clearFlag, clearColor);
        }

        private static void SetViewportAndClear(CommandBuffer cmd, RGCamera camera, RTHandleSystem.RTHandle buffer, ClearFlag clearFlag, Color clearColor)
        {
            // Clearing a partial viewport currently does not go through the hardware clear.
            // Instead it goes through a quad rendered with a specific shader.
            // When enabling wireframe mode in the scene view, unfortunately it overrides this shader thus breaking every clears.
            // That's why in the editor we don't set the viewport before clearing (it's set to full screen by the previous SetRenderTarget) but AFTER so that we benefit from un-bugged hardware clear.
            // We consider that the small loss in performance is acceptable in the editor.
            // A refactor of wireframe is needed before we can fix this properly (with not doing anything!)
#if !UNITY_EDITOR
            SetViewport(cmd, camera, buffer);
#endif
            CoreUtils.ClearRenderTarget(cmd, clearFlag, clearColor);
#if UNITY_EDITOR
            SetViewport(cmd, camera, buffer);
#endif
        }

        // Scaling viewport is done for auto-scaling render targets.
        // In the context of HDRP, every auto-scaled RT is scaled against the maximum RTHandles reference size (that can only grow).
        // When we render using a camera whose viewport is smaller than the RTHandles reference size (and thus smaller than the RT actual size), we need to set it explicitly (otherwise, native code will set the viewport at the size of the RT)
        // For auto-scaled RTs (like for example a half-resolution RT), we need to scale this viewport accordingly.
        // For non scaled RTs we just do nothing, the native code will set the viewport at the size of the RT anyway.
        public static void SetViewport(CommandBuffer cmd, RGCamera camera, RTHandleSystem.RTHandle target)
        {
            if (target.useScaling)
            {
                Debug.Assert(camera != null, "Missing HDCamera when setting up Render Target with auto-scale and Viewport.");
                Vector2Int scaledViewportSize = target.GetScaledSize(new Vector2Int(camera.CameraWidth, camera.CameraHeight));
                cmd.SetViewport(new Rect(0.0f, 0.0f, scaledViewportSize.x, scaledViewportSize.y));
            }
        }

        public static void BlitQuad(CommandBuffer cmd, Texture source, Vector4 scaleBiasTex, Vector4 scaleBiasRT, int mipLevelTex, bool bilinear)
        {
            s_PropertyBlock.SetTexture(ClusterShaderIDs._BlitTexture, source);
            s_PropertyBlock.SetVector(ClusterShaderIDs._BlitScaleBias, scaleBiasTex);
            s_PropertyBlock.SetVector(ClusterShaderIDs._BlitScaleBiasRt, scaleBiasRT);
            s_PropertyBlock.SetFloat(ClusterShaderIDs._BlitMipLevel, mipLevelTex);
            cmd.DrawProcedural(Matrix4x4.identity, GetBlitMaterial(), bilinear ? 2 : 3, MeshTopology.Quads, 4, 1, s_PropertyBlock);
        }

        public static void BlitTexture(CommandBuffer cmd, RTHandleSystem.RTHandle source, RTHandleSystem.RTHandle destination, Vector4 scaleBias, float mipLevel, bool bilinear)
        {
            s_PropertyBlock.SetTexture(ClusterShaderIDs._BlitTexture, source);
            s_PropertyBlock.SetVector(ClusterShaderIDs._BlitScaleBias, scaleBias);
            s_PropertyBlock.SetFloat(ClusterShaderIDs._BlitMipLevel, mipLevel);
            cmd.DrawProcedural(Matrix4x4.identity, GetBlitMaterial(), bilinear ? 1 : 0, MeshTopology.Triangles, 3, 1, s_PropertyBlock);
        }
    }
}
