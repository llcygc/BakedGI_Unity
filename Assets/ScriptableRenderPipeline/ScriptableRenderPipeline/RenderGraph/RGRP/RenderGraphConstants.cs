using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Viva.Rendering.RenderGraph
{
    public static class RenderGraphShaderIDs
    {
        public static readonly int _SkyTexture = Shader.PropertyToID("_SkyTexture");
        public static readonly int _SkyTextureMipCount = Shader.PropertyToID("_SkyTextureMipCount");

        public static readonly int _Cubemap = Shader.PropertyToID("_Cubemap");
        public static readonly int _SkyParam = Shader.PropertyToID("_SkyParam");
        public static readonly int _PixelCoordToViewDirWS = Shader.PropertyToID("_PixelCoordToViewDirWS");

        public static readonly int _GlobalFog_Extinction = Shader.PropertyToID("_GlobalFog_Extinction");
        public static readonly int _GlobalFog_Asymmetry = Shader.PropertyToID("_GlobalFog_Asymmetry");
        public static readonly int _GlobalFog_Scattering = Shader.PropertyToID("_GlobalFog_Scattering");
    }
}
