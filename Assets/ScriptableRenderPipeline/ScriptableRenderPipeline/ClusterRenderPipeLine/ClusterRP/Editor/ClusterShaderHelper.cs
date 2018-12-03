using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor.Experimental.Rendering.ClusterPipeline
{
    public static class ClusterShaderHelper
    {
        public enum UpgradeBlendMode
        {
            Opaque,
            Cutout,
            Alpha,
            Premultiply
        }

        public enum SpecularSource
        {
            SpecularTextureAndColor,
            NoSpecular
        }

        public enum GlossinessSource
        {
            BaseAlpha,
            SpecularAlpha
        }

        public enum ReflectionSource
        {
            NoReflection,
            Cubemap,
            ReflectionProbe
        }

        public struct UpgradeParams
        {
            public UpgradeBlendMode blendMode;
            public SpecularSource specularSource;
            public GlossinessSource glosinessSource;
        }

        public static void SetVegetationType(Material material)
        {
            //#pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
            //#pragma shader_feature _NORMALMAP
            //#pragma shader_feature _ALPHATEST_ON 

            //#pragma shader_feature EFFECT_BUMP
            //#pragma shader_feature SPEEDTREE_ALPHATEST
            //#pragma shader_feature EFFECT_HUE_VARIATION
            bool isLeaf = material.IsKeywordEnabled("GEOM_TYPE_LEAF");

        }

        public static void SetMaterialBlendMode(Material material)
        {
            bool enableTransparent = material.IsKeywordEnabled("_ALPHABLEND_ON");
            bool enableAlphaTest = material.IsKeywordEnabled("_ALPHATEST_ON");
            bool enablePremultiply = material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON");

            UpgradeBlendMode mode = UpgradeBlendMode.Opaque;
            float blendValue = 0;
            float surfaceTypeValue = 0;
            if (enableAlphaTest)
                mode = UpgradeBlendMode.Cutout;
            else if (enableTransparent)
            {
                surfaceTypeValue = 1;
                blendValue = 0;
                mode = UpgradeBlendMode.Alpha;
            }
            else if (enablePremultiply)
            {
                surfaceTypeValue = 1;
                blendValue = 1;
                mode = UpgradeBlendMode.Premultiply;
            }

            material.SetFloat("_Blend", blendValue);
            material.SetFloat("_Surface", surfaceTypeValue);

            switch (mode)
            {
                case UpgradeBlendMode.Opaque:
                    material.SetOverrideTag("RenderType", "");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetInt("_ZWrite", 1);
                    material.SetFloat("_AlphaClip", 0.0f);
                    SetKeyword(material, "_ALPHATEST_ON", false);
                    SetKeyword(material, "_ALPHABLEND_ON", false);
                    SetKeyword(material, "_ALPHAPREMULTIPLY_ON", false);
                    material.renderQueue = -1;
                    break;

                case UpgradeBlendMode.Cutout:
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetInt("_ZWrite", 1);
                    material.SetFloat("_AlphaClip", 1.0f);
                    SetKeyword(material, "_ALPHATEST_ON", true);
                    SetKeyword(material, "_ALPHABLEND_ON", false);
                    SetKeyword(material, "_ALPHAPREMULTIPLY_ON", false);
                    material.renderQueue = (int)RenderQueue.AlphaTest;
                    break;

                case UpgradeBlendMode.Alpha:
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.SetFloat("_AlphaClip", 0.0f);
                    SetKeyword(material, "_ALPHATEST_ON", false);
                    SetKeyword(material, "_ALPHABLEND_ON", true);
                    SetKeyword(material, "_ALPHAPREMULTIPLY_ON", false);
                    material.renderQueue = (int)RenderQueue.Transparent;
                    break;

                case UpgradeBlendMode.Premultiply:
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.SetFloat("_AlphaClip", 0.0f);
                    SetKeyword(material, "_ALPHATEST_ON", false);
                    SetKeyword(material, "_ALPHABLEND_ON", false);
                    SetKeyword(material, "_ALPHAPREMULTIPLY_ON", true);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    break;
            }
        }

        public static void SetKeyword(Material material, string keyword, bool enable)
        {
            if (enable)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }
    }
}

