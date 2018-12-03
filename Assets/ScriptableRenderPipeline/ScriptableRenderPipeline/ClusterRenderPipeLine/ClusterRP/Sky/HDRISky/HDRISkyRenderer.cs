using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Viva.Rendering.RenderGraph
{
    public class HDRISkyRenderer : SkyRenderer
    {
        Material m_SkyHDRIMaterial; // Renders a cubemap into a render texture (can be cube or 2D)
        MaterialPropertyBlock m_PropertyBlock;
        HDRISkySettings m_HdriSkyParams;

        public HDRISkyRenderer(HDRISkySettings hdriSkyParams)
        {
            m_HdriSkyParams = hdriSkyParams;
            m_PropertyBlock = new MaterialPropertyBlock();
        }

        public override void Build()
        {
            m_SkyHDRIMaterial = CoreUtils.CreateEngineMaterial("Hidden/ClusterRenderPipeline/Sky/SkyHDRI");
        }

        public override void Cleanup()
        {
            CoreUtils.Destroy(m_SkyHDRIMaterial);
        }

        public override void SetRenderTargets(BuiltinSkyParameters builtinParams, bool stereo)
        {
            if (stereo)
            {
                int depthSlice = stereo ? -1 : 0;
                builtinParams.commandBuffer.SetRenderTarget(builtinParams.colorBuffer, builtinParams.depthBuffer, 0, CubemapFace.Unknown, depthSlice);
            }
            else
            {
                if (builtinParams.depthBuffer == BuiltinSkyParameters.nullRT)
                {
                    CoreUtils.SetRenderTarget(builtinParams.commandBuffer, builtinParams.colorBuffer);
                }
                else
                {
                    CoreUtils.SetRenderTarget(builtinParams.commandBuffer, builtinParams.colorBuffer, builtinParams.depthBuffer);
                }
            }
        }

        public override void RenderSky(BuiltinSkyParameters builtinParams, bool stereo, bool renderForCubemap)
        {
            m_SkyHDRIMaterial.SetTexture(RenderGraphShaderIDs._Cubemap, m_HdriSkyParams.skyHDRI);
            m_SkyHDRIMaterial.SetVector(RenderGraphShaderIDs._SkyParam, new Vector4(m_HdriSkyParams.exposure, m_HdriSkyParams.multiplier, m_HdriSkyParams.rotation, 0.0f));

            // This matrix needs to be updated at the draw call frequency.
            if (stereo)
            {
                m_PropertyBlock.SetMatrixArray(RenderGraphShaderIDs._PixelCoordToViewDirWS, builtinParams.pixelCoordToViewDirMatrix);
                builtinParams.commandBuffer.DrawProcedural(Matrix4x4.identity, m_SkyHDRIMaterial, renderForCubemap ? 0 : 1, MeshTopology.Triangles, 3, 1, m_PropertyBlock);
                //CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_SkyHDRIMaterial, m_PropertyBlock, renderForCubemap ? 0 : 1);
            }
            else
            {
                m_PropertyBlock.SetMatrixArray(RenderGraphShaderIDs._PixelCoordToViewDirWS, builtinParams.pixelCoordToViewDirMatrix);
                CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_SkyHDRIMaterial, m_PropertyBlock, renderForCubemap ? 0 : 2);
            }
        }

        public override bool IsSkyValid()
        {
            return m_HdriSkyParams != null && m_SkyHDRIMaterial != null; 
        }
    }
}
