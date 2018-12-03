using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Viva.Rendering.RenderGraph
{
    public class RenderGraphUtils
    {
        public static RenderTexture CreateRenderTexture(Camera desCamera)
        {
            return new RenderTexture(0, 0, 24);
        }

        public static RenderTexture CreateRenderTexture(RenderTextureDescriptor rtDsc)
        {
            return new RenderTexture(rtDsc);
        }

        public static RenderTargetIdentifier CreateRenderTarget(RenderTexture rt)
        {
            return new RenderTargetIdentifier(rt);
        }

        public static Matrix4x4 GetViewProjectionMatrix(Matrix4x4 worldToViewMatrix, Matrix4x4 projectionMatrix)
        {
            // The actual projection matrix used in shaders is actually massaged a bit to work across all platforms
            // (different Z value ranges etc.)
            var gpuProj = GL.GetGPUProjectionMatrix(projectionMatrix, false);
            var gpuVP = gpuProj * worldToViewMatrix * Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f)); // Need to scale -1.0 on Z to match what is being done in the camera.wolrdToCameraMatrix API.

            return gpuVP;
        }

        public static void SetRenderTarget(CommandBuffer cmd, RenderTargetIdentifier colorBuffer, RenderTargetIdentifier depthBuffer, CubemapFace face = CubemapFace.Unknown, int depthSlice = 0, int mipLevel = 0)
        {
            cmd.SetRenderTarget(colorBuffer, depthBuffer, mipLevel, face, depthSlice);
        }
    }
}
