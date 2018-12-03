using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Viva.Rendering.RenderGraph;

namespace UnityEditor.Experimental.Rendering.ClusterPipeline
{
    public class StandardUpgrader : MaterialUpgrader
    {
        public static void UpdateStandardMaterialKeywords(Material material)
        {
            material.SetFloat("_WorkflowMode", 1.0f);
            ClusterShaderHelper.SetKeyword(material, "_OCCLUSIONMAP", material.GetTexture("_OcclusionMap"));
            ClusterShaderHelper.SetKeyword(material, "_METALLICSPECGLOSSMAP", material.GetTexture("_MetallicGlossMap"));
            ClusterShaderHelper.SetKeyword(material, "_NORMALMAP", material.GetTexture("_BumpMap") || material.GetTexture("_DetailNormalMap"));
            ClusterShaderHelper.SetKeyword(material, "_DETAIL_MASK", material.GetTexture("_DetailMask"));
            ClusterShaderHelper.SetKeyword(material, "_DETAIL", material.GetTexture("_DetailAlbedoMap") || material.GetTexture("_DetailNormalMap"));
            ClusterShaderHelper.SetKeyword(material, "_DETAIL_MULX2", material.GetTexture("_DetailAlbedoMap") || material.GetTexture("_DetailNormalMap"));
            ClusterShaderHelper.SetMaterialBlendMode(material);
        }

        public static void UpdateStandardSpecularMaterialKeywords(Material material)
        {
            material.SetFloat("_WorkflowMode", 0.0f);
            ClusterShaderHelper.SetKeyword(material, "_OCCLUSIONMAP", material.GetTexture("_OcclusionMap"));
            ClusterShaderHelper.SetKeyword(material, "_METALLICSPECGLOSSMAP", material.GetTexture("_SpecGlossMap"));
            ClusterShaderHelper.SetKeyword(material, "_NORMALMAP", material.GetTexture("_BumpMap") || material.GetTexture("_DetailNormalMap"));
            ClusterShaderHelper.SetKeyword(material, "_SPECULAR_SETUP", true);
            ClusterShaderHelper.SetKeyword(material, "_DETAIL_MASK", material.GetTexture("_DetailMask"));
            ClusterShaderHelper.SetKeyword(material, "_DETAIL", material.GetTexture("_DetailAlbedoMap") || material.GetTexture("_DetailNormalMap"));
            ClusterShaderHelper.SetKeyword(material, "_DETAIL_MULX2", material.GetTexture("_DetailAlbedoMap") || material.GetTexture("_DetailNormalMap"));
            ClusterShaderHelper.SetMaterialBlendMode(material);
        }

        public StandardUpgrader(string oldShaderName)
        {
            if (oldShaderName.Contains("Specular"))
                RenameShader(oldShaderName, RenderGraphShaderNames.s_RenderGraphStandardShader, UpdateStandardSpecularMaterialKeywords);
            else
                RenameShader(oldShaderName, RenderGraphShaderNames.s_RenderGraphStandardShader, UpdateStandardMaterialKeywords);
        }
    }

    public class VegetationUpgrader : MaterialUpgrader
    {
        public static void UpdateVegetationMaterialKeywords(Material material)
        {
            //#pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
            //#pragma shader_feature _NORMALMAP
            //#pragma shader_feature _ALPHATEST_ON 
            //#pragma shader_feature _ALPHAPREMULTIPLY_ON
            //#pragma shader_feature _EMISSION
            //#pragma shader_feature _METALLICSPECGLOSSMAP
            //#pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            //#pragma shader_feature _OCCLUSIONMAP

            //#define ENABLE_WIND

            //#pragma shader_feature EFFECT_BUMP
            //#pragma shader_feature SPEEDTREE_ALPHATEST
            //#pragma shader_feature EFFECT_HUE_VARIATION
            ClusterShaderHelper.SetKeyword(material, "ENABLE_WIND", true);
        }

        public VegetationUpgrader(string oldShaderName)
        {
            RenameShader(oldShaderName, RenderGraphShaderNames.s_RenderGraphVegetationShader, UpdateVegetationMaterialKeywords);
        }
    }

    public class ParticleUpgrader : MaterialUpgrader
    {
        public static void UpdateParticleMaterialKeywords(Material material)
        {

        }

        public ParticleUpgrader(string oldShaderName)
        {
            RenameShader(oldShaderName, RenderGraphShaderNames.s_RenderGraphParticleShader, UpdateParticleMaterialKeywords);
        }
    }


}
