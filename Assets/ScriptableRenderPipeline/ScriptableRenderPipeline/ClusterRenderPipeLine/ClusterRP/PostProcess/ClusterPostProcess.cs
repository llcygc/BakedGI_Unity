using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    public class ClusterPostProcess
    {
        private ClusterRenderPipelineResources m_Resources = null;
        private ComputeShader m_FogandAOShader = null;
        private ComputeShader m_ColorGradingShader = null;
        private ComputeShader m_StencilMaskGenShader = null;
        private ComputeShader m_ExpandMaskedBufferShader = null;
        private ComputeShader m_GradientResolveShader = null;
        private int m_FogandAOKernel;
        private int m_FogandAOStereoKernel;
        private int m_ColorGradingKernel;
        private int m_StencilMaskGenKernel;
        private int m_ExpandMaskedBufferKernal;
        private int m_GradientResolveKernel;
        private RTHandleSystem.RTHandle m_colorBuffer;
        private RTHandleSystem.RTHandle m_depthBuffer;
        private RTHandleSystem.RTHandle m_R8Buffer;
        private bool m_StencilMaskGenerated;

        public void Build(ClusterRenderPipelineResources resources)
        {
            m_Resources = resources;
            m_FogandAOShader = m_Resources.fogandAOCS;
            m_ColorGradingShader = m_Resources.colorGradingCS;
            m_StencilMaskGenShader = m_Resources.stencilMaskGenCS;
            m_ExpandMaskedBufferShader = m_Resources.expandMaskedBufferCS;

            m_StencilMaskGenerated = false;

            if (m_FogandAOShader)
            {
                m_FogandAOKernel = m_FogandAOShader.FindKernel("FogandAO_OP");
                m_FogandAOStereoKernel = m_FogandAOShader.FindKernel("FogandAO_Array");
            }

            if (m_ColorGradingShader)
                m_ColorGradingKernel = m_ColorGradingShader.FindKernel("ColorGrading");

            if (m_StencilMaskGenShader)
                m_StencilMaskGenKernel = m_StencilMaskGenShader.FindKernel("GradiantStencilMaskGen");

            if (m_ExpandMaskedBufferShader)
            {
                m_ExpandMaskedBufferKernal = m_ExpandMaskedBufferShader.FindKernel("ExpandMaskedBuffer");
            }
        }

        public void ApplyOpaqueOnlyPostProcess(CommandBuffer cmd, RGCamera rgCam, RTHandleSystem.RTHandle colorBuffer, RTHandleSystem.RTHandle depthBuffer, RTHandleSystem.RTHandle r8Buffer)
        {
            if(m_FogandAOShader)
            {
                rgCam.SetupComputeShader(m_FogandAOShader, cmd);
                int actualKernel = m_FogandAOKernel;
                int threadZ = 1;
                if (rgCam.StereoEnabled && rgCam.RenderTextureDimension == TextureDimension.Tex2DArray)
                {
                    actualKernel = m_FogandAOStereoKernel;
                    cmd.SetComputeTextureParam(m_FogandAOShader, actualKernel, ClusterShaderIDs._MainColorBufferArray, colorBuffer);
                    cmd.SetComputeTextureParam(m_FogandAOShader, actualKernel, ClusterShaderIDs._MainDepthBufferArray, depthBuffer);
                    threadZ = 2;
                }
                else
                {
                    cmd.SetComputeTextureParam(m_FogandAOShader, actualKernel, ClusterShaderIDs._MainColorBuffer, colorBuffer);
                    cmd.SetComputeTextureParam(m_FogandAOShader, actualKernel, ClusterShaderIDs._MainDepthBuffer, depthBuffer);
                }

                cmd.SetComputeTextureParam(m_FogandAOShader, actualKernel, ClusterShaderIDs._R8Buffer, r8Buffer);
                cmd.SetComputeTextureParam(m_FogandAOShader, actualKernel, ClusterShaderIDs._VolumetricFogTexture, ClusterPass.LightManager.GetFogTexture());

                int threadX = (int)Mathf.Ceil((float)rgCam.CameraWidth / 8.0f);
                int threadY = (int)Mathf.Ceil((float)rgCam.CameraHeight / 8.0f);

                cmd.DispatchCompute(m_FogandAOShader, actualKernel, threadX, threadY, threadZ);
            }
        }

        public void GenerateGradientResStencilMask(CommandBuffer cmd, RTHandleSystem.RTHandle r8Buffer, RGCamera rgCam, bool gradiant)
        {
            if(!m_StencilMaskGenerated)
            {
                int actualKernal = m_StencilMaskGenShader.FindKernel("GradiantStencilMaskGen");
                if (!gradiant)
                    actualKernal = m_StencilMaskGenShader.FindKernel("NonGradiantStencilMaskGen");
                int stencilMaskIndex = Time.frameCount % 4;
                cmd.SetComputeIntParam(m_StencilMaskGenShader, ClusterShaderIDs._MaskIndex, stencilMaskIndex);
                cmd.SetComputeTextureParam(m_StencilMaskGenShader, actualKernal, ClusterShaderIDs._R8Buffer, r8Buffer);


                int threadX = (int)Mathf.Ceil((float)rgCam.CameraWidth / 8.0f);
                int threadY = (int)Mathf.Ceil((float)rgCam.CameraHeight / 8.0f);
                cmd.SetComputeVectorParam(m_StencilMaskGenShader, ClusterShaderIDs._GroupSize, new Vector4(threadX, threadY, 0, 0));
                cmd.DispatchCompute(m_StencilMaskGenShader, actualKernal, threadX, threadY, 1);
            }
        }

        public void ExpandMaskedBuffer(CommandBuffer cmd, RTHandleSystem.RTHandle r8Buffer, RTHandleSystem.RTHandle colorBuffer, RTHandleSystem.RTHandle velocityBuffer, RTHandleSystem.RTHandle depthBuffer, RGCamera rgCam)
        {
            if (m_ExpandMaskedBufferShader)
            {
                int threadX = (int)Mathf.Ceil((float)rgCam.CameraWidth / 8.0f);
                int threadY = (int)Mathf.Ceil((float)rgCam.CameraHeight / 8.0f);

                int actualKernel = m_ExpandMaskedBufferKernal;
                int threadZ = 1;
                if (rgCam.StereoEnabled && rgCam.RenderTextureDimension == TextureDimension.Tex2DArray)
                {
                    actualKernel = m_ExpandMaskedBufferShader.FindKernel("ExpandMaskedBufferStereo");
                    cmd.SetComputeTextureParam(m_ExpandMaskedBufferShader, actualKernel, ClusterShaderIDs._MainColorBufferArray, colorBuffer);
                    cmd.SetComputeTextureParam(m_ExpandMaskedBufferShader, actualKernel, ClusterShaderIDs._MainDepthBufferArray, depthBuffer);
                    cmd.SetComputeTextureParam(m_ExpandMaskedBufferShader, actualKernel, ClusterShaderIDs._MainVelocityBufferArray, velocityBuffer);
                    cmd.SetComputeVectorParam(m_ExpandMaskedBufferShader, ClusterShaderIDs._GroupSize, new Vector4(threadX, threadY, 0, 0));
                    threadZ = 2;
                }
                else
                {
                    cmd.SetComputeTextureParam(m_ExpandMaskedBufferShader, actualKernel, ClusterShaderIDs._MainColorBuffer, colorBuffer);
                    cmd.SetComputeTextureParam(m_ExpandMaskedBufferShader, actualKernel, ClusterShaderIDs._MainDepthBuffer, depthBuffer);
                    cmd.SetComputeTextureParam(m_ExpandMaskedBufferShader, actualKernel, ClusterShaderIDs._MainVelocityBuffer, velocityBuffer);
                }

                cmd.SetComputeTextureParam(m_ExpandMaskedBufferShader, actualKernel, ClusterShaderIDs._R8Buffer, r8Buffer);

                cmd.DispatchCompute(m_ExpandMaskedBufferShader, actualKernel, threadX, threadY, threadZ);
            }
        }

        public void ApplyFinalPostProcess(CommandBuffer cmd)
        {
            if(m_ColorGradingShader)
            {

            }
        }
    }
}
