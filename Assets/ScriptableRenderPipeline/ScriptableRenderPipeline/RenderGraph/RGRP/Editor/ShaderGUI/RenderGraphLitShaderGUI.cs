using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace UnityEditor.Experimental.Rendering.ClusterPipeline
{
    class RenderGraphLitShaderGUI : RenderGraphShaderGUI
    {
        public enum WorkflowMode
        {
            Specular = 0,
            Metallic
        }

        public enum BRDFMode
        {
            Standard = 0,
            Anisotropic,
            Lambert
        }

        public enum SmoothnessMapChannel
        {
            SpecularMetallicAlpha,
            AlbedoAlpha,
        }

        public enum DoubleSidedNormalMode
        {
            Flip,
            Mirror,
            None
        }

        public enum VertexAnimationMode
        {
            None,
            Hierarchy,
            Single,
            Procedural
        }

        private static class Styles
        {
            public static GUIContent twoSidedText = new GUIContent("Two Sided", "Render front and back faces");
            public static GUIContent doubleSidedNormalModeText = new GUIContent("Normal mode", "This will modify the normal base on the selected mode. Mirror: Mirror the normal with vertex normal plane, Flip: Flip the normal");
            public static GUIContent alphaClipText = new GUIContent("Alpha Clip", "Enable Alpha Clip");
            public static GUIContent albedoText = new GUIContent("Albedo", "Albedo (RGB) and Transparency (A)");
            public static GUIContent clipThresholdText = new GUIContent("Clip Threshold", "Threshold for alpha clip");
            public static GUIContent specularMapText = new GUIContent("Specular", "Specular (RGB) and Smoothness (A)");
            public static GUIContent metallicMapText = new GUIContent("Metallic", "Metallic (R) and Smoothness (A)");
            public static GUIContent smoothnessText = new GUIContent("Smoothness", "Smoothness value");
            public static GUIContent smoothnessScaleText = new GUIContent("Smoothness", "Smoothness scale factor");
            public static GUIContent metallicScaleText = new GUIContent("Metallic", "metallic scale factor");
            public static GUIContent smoothnessMapChannelText = new GUIContent("Source", "Smoothness texture and channel");
            public static GUIContent highlightsText = new GUIContent("Specular Highlights", "Specular Highlights");
            public static GUIContent reflectionsText = new GUIContent("Reflections", "Glossy Reflections");
            public static GUIContent normalMapText = new GUIContent("Normal Map", "Normal Map");
            public static GUIContent tangentMapText = new GUIContent("Tangent Map", "Tangent Map for anisotropic");
            public static GUIContent baseTangentDirText = new GUIContent("Base Tangent Dir", "Base tangent direction");
            public static GUIContent tangentStrengthText = new GUIContent("Tangent Strength", "Tangent dir strength");
            public static GUIContent occlusionText = new GUIContent("Occlusion", "Occlusion (G)");
            public static GUIContent emissionText = new GUIContent("Color", "Emission (RGB)");
            public static GUIContent detailMaskText = new GUIContent("Detail Mask", "Detial mask for detail maps");
            public static GUIContent detailAlbedoText = new GUIContent("Detail Albedo", "Detial map for Albedo");
            public static GUIContent detailNormalText = new GUIContent("Detial Normal", "Detial map for normal");
            public static GUIContent bumpScaleNotSupported = new GUIContent("Bump scale is not supported on mobile platforms");
            public static GUIContent fixNow = new GUIContent("Fix now");

            public static string primaryMapsText = "Main Maps";
            public static string detailMapsText = "Detail Maps";
            public static string vertexAnimationText = "Vertex Animation Enabled";
            public static string secondaryMapsText = "Secondary Maps";
            public static string forwardText = "Forward Rendering Options";
            public static string workflowModeText = "Workflow Mode";
            public static string brdfModeText = "BRDF Mode";
            public static string surfaceType = "Surface Type";
            public static string blendingMode = "Blending Mode";
            public static string advancedText = "Advanced Options";
            public static readonly string[] workflowNames = Enum.GetNames(typeof(WorkflowMode));
            public static readonly string[] brdfNames = Enum.GetNames(typeof(BRDFMode));
            public static readonly string[] surfaceNames = Enum.GetNames(typeof(SurfaceType));
            public static readonly string[] blendNames = Enum.GetNames(typeof(BlendMode));
            public static readonly string[] doubleSizeModeNames = Enum.GetNames(typeof(DoubleSidedNormalMode));
            public static readonly string[] vertexAnimationNames = Enum.GetNames(typeof(VertexAnimationMode));
            public static readonly string[] metallicSmoothnessChannelNames = { "Metallic Alpha", "Albedo Alpha" };
            public static readonly string[] specularSmoothnessChannelNames = { "Specular Alpha", "Albedo Alpha" };
        }

        private MaterialProperty workflowMode;
        private MaterialProperty brdfMode;
        private MaterialProperty surfaceType;
        private MaterialProperty blendMode;
        private MaterialProperty culling;
        private MaterialProperty alphaClip;

        private MaterialProperty albedoColor;
        private MaterialProperty albedoMap;
        private MaterialProperty alphaThreshold;

        private MaterialProperty smoothness;
        private MaterialProperty smoothnessScale;
        private MaterialProperty smoothnessMapChannel;

        private MaterialProperty metallic;
        private MaterialProperty metallicScale;
        private MaterialProperty specColor;
        private MaterialProperty metallicGlossMap;
        private MaterialProperty specGlossMap;
        private MaterialProperty highlights;
        private MaterialProperty reflections;

        private MaterialProperty tangentMap;
        private MaterialProperty tangentDir;
        private MaterialProperty tangentDirStrength;

        private MaterialProperty doubleSideMode;
        private const string kDoubleSidedNormalMode = "_DoubleSidedNormalMode";
        private MaterialProperty doubleSidedEnable = null;
        private const string kDoubleSidedEnable = "_DoubleSidedEnable";

        private MaterialProperty detailMask;
        private MaterialProperty detailAlbedo;
        private MaterialProperty detailNormal;
        private MaterialProperty detailNormalScale;

        private MaterialProperty bumpScale;
        private MaterialProperty bumpMap;
        private MaterialProperty occlusionStrength;
        private MaterialProperty occlusionMap;
        private MaterialProperty emissionColorForRendering;
        private MaterialProperty emissionMap;

        private MaterialProperty vertAnimation;
        private MaterialProperty trunkStiffness;
        private MaterialProperty branchStiffness;
        private MaterialProperty branchFrequency;
        private MaterialProperty branchAmplify;
        private MaterialProperty detailFrequency;
        private MaterialProperty detailAmplify;

        public override void FindProperties(MaterialProperty[] properties)
        {
            workflowMode = FindProperty("_WorkflowMode", properties);
            brdfMode = FindProperty("_BRDFMode", properties);
            surfaceType = FindProperty("_Surface", properties);
            blendMode = FindProperty("_Blend", properties);
            culling = FindProperty("_Cull", properties);
            alphaClip = FindProperty("_AlphaClip", properties);
            albedoColor = FindProperty("_Color", properties);
            albedoMap = FindProperty("_MainTex", properties);
            alphaThreshold = FindProperty("_Cutoff", properties);

            smoothness = FindProperty("_Glossiness", properties);
            smoothnessScale = FindProperty("_GlossMapScale", properties, false);
            smoothnessMapChannel = FindProperty("_SmoothnessTextureChannel", properties, false);

            metallic = FindProperty("_Metallic", properties);
            metallicScale = FindProperty("_MetallicScale", properties);
            specColor = FindProperty("_SpecColor", properties);
            metallicGlossMap = FindProperty("_MetallicGlossMap", properties);
            specGlossMap = FindProperty("_SpecGlossMap", properties);
            highlights = FindProperty("_SpecularHighlights", properties);
            reflections = FindProperty("_GlossyReflections", properties);

            tangentMap = FindProperty("_TangentMap", properties);
            tangentDir = FindProperty("_BaseTangentDir", properties);
            tangentDirStrength = FindProperty("_TangentDirStrength", properties);

            detailMask = FindProperty("_DetailMask", properties);
            detailAlbedo = FindProperty("_DetailAlbedoMap", properties);
            detailNormal = FindProperty("_DetailNormalMap", properties);
            detailNormalScale = FindProperty("_DetailNormalMapScale", properties);

            bumpScale = FindProperty("_BumpScale", properties);
            bumpMap = FindProperty("_BumpMap", properties);
            occlusionStrength = FindProperty("_OcclusionStrength", properties);
            occlusionMap = FindProperty("_OcclusionMap", properties);
            emissionColorForRendering = FindProperty("_EmissionColor", properties);
            emissionMap = FindProperty("_EmissionMap", properties);

            doubleSidedEnable = FindProperty(kDoubleSidedEnable, properties);
            doubleSideMode = FindProperty("_DoubleSidedNormalMode", properties);

            vertAnimation = FindProperty("_VertexAnimation", properties);
            trunkStiffness = FindProperty("_TrunkStiffness", properties);
            branchStiffness = FindProperty("_BranchStiffness", properties);
            branchFrequency = FindProperty("_BranchFrequency", properties);
            branchAmplify = FindProperty("_BranchAmplify", properties);
            detailFrequency = FindProperty("_DetailFrequency", properties);
            detailAmplify = FindProperty("_DetailAmplify", properties);
    }

        public override void MaterialChanged(Material material)
        {
            material.shaderKeywords = null;
            SetupMaterialBlendMode(material);
            SetMaterialKeywords(material);
        }

        public override void ShaderPropertiesGUI(Material material)
        {
            // Use default labelWidth
            EditorGUIUtility.labelWidth = 0f;

            // Detect any changes to the material
            EditorGUI.BeginChangeCheck();
            {
                DoPopup(Styles.workflowModeText, workflowMode, Styles.workflowNames);
                DoPopup(Styles.brdfModeText, brdfMode, Styles.brdfNames);
                DoPopup(Styles.surfaceType, surfaceType, Styles.surfaceNames);
                if ((SurfaceType)material.GetFloat("_Surface") == SurfaceType.Transparent)
                    DoPopup(Styles.blendingMode, blendMode, Styles.blendNames);

                EditorGUI.BeginChangeCheck();
                bool twoSidedEnabled = EditorGUILayout.Toggle(Styles.twoSidedText, doubleSidedEnable.floatValue == 1.0f);
                if (EditorGUI.EndChangeCheck())
                {
                    doubleSidedEnable.floatValue = twoSidedEnabled ? 1.0f : 0;
                }

                if (twoSidedEnabled)
                    DoPopup("Double Side Mode", doubleSideMode, Styles.doubleSizeModeNames);

                EditorGUI.BeginChangeCheck();
                bool alphaClipEnabled = EditorGUILayout.Toggle(Styles.alphaClipText, alphaClip.floatValue == 1);
                if (EditorGUI.EndChangeCheck())
                    alphaClip.floatValue = alphaClipEnabled ? 1 : 0;

                // Primary properties
                GUILayout.Label(Styles.primaryMapsText, EditorStyles.boldLabel);
                DoAlbedoArea(material);
                DoMetallicSpecularArea();
                DoNormalArea();
                bool isAnisotropic = (BRDFMode)material.GetFloat("_BRDFMode") == BRDFMode.Anisotropic;
                if (isAnisotropic)
                    DoAnisotropicArea();

                bool needocclusionmap = true;
                if ((WorkflowMode)workflowMode.floatValue == WorkflowMode.Metallic && metallicGlossMap.textureValue != null)
                    needocclusionmap = false;
                if(needocclusionmap)
                    m_MaterialEditor.TexturePropertySingleLine(Styles.occlusionText, occlusionMap, occlusionMap.textureValue != null ? occlusionStrength : null);

                DoEmissionArea(material);
                EditorGUI.BeginChangeCheck();
                m_MaterialEditor.TextureScaleOffsetProperty(albedoMap);
                if (EditorGUI.EndChangeCheck())
                    albedoMap.textureScaleAndOffset = albedoMap.textureScaleAndOffset; // Apply the main texture scale and offset to the emission texture as well, for Enlighten's sake

                EditorGUILayout.Space();

                GUILayout.Label(Styles.detailMapsText, EditorStyles.boldLabel);
                DoDetailArea();
                EditorGUI.BeginChangeCheck();
                m_MaterialEditor.TextureScaleOffsetProperty(detailAlbedo);
                if (EditorGUI.EndChangeCheck())
                    detailAlbedo.textureScaleAndOffset = detailAlbedo.textureScaleAndOffset; // Apply the main texture scale and offset to the emission texture as well, for Enlighten's sake
                
                EditorGUILayout.Space();

                DoPopup(Styles.vertexAnimationText, vertAnimation, Styles.vertexAnimationNames);
                if(vertAnimation.floatValue > 0)
                    DoWindArea();

                EditorGUILayout.Space();

                m_MaterialEditor.ShaderProperty(highlights, Styles.highlightsText);
                m_MaterialEditor.ShaderProperty(reflections, Styles.reflectionsText);
            }
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var obj in blendMode.targets)
                    MaterialChanged((Material)obj);
            }

            EditorGUILayout.Space();

            // NB renderqueue editor is not shown on purpose: we want to override it based on blend mode
            GUILayout.Label(Styles.advancedText, EditorStyles.boldLabel);
            m_MaterialEditor.EnableInstancingField();
            m_MaterialEditor.DoubleSidedGIField();
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            // _Emission property is lost after assigning Standard shader to the material
            // thus transfer it before assigning the new shader
            if (material.HasProperty("_Emission"))
            {
                material.SetColor("_EmissionColor", material.GetColor("_Emission"));
            }

            base.AssignNewShaderToMaterial(material, oldShader, newShader);

            if (oldShader == null || !oldShader.name.Contains("Legacy Shaders/"))
            {
                SetupMaterialBlendMode(material);
                return;
            }

            SurfaceType surfaceType = SurfaceType.Opaque;
            BlendMode blendMode = BlendMode.Alpha;
            if (oldShader.name.Contains("/Transparent/Cutout/"))
            {
                surfaceType = SurfaceType.Opaque;
                material.SetFloat("_AlphaClip", 1);
            }
            else if (oldShader.name.Contains("/Transparent/"))
            {
                // NOTE: legacy shaders did not provide physically based transparency
                // therefore Fade mode
                surfaceType = SurfaceType.Transparent;
                blendMode = BlendMode.Alpha;
            }
            material.SetFloat("_Surface", (float)surfaceType);
            material.SetFloat("_Blend", (float)blendMode);

            if (oldShader.name.Equals("Standard (Specular setup)"))
            {
                material.SetFloat("_WorkflowMode", (float)WorkflowMode.Specular);
                Texture texture = material.GetTexture("_SpecGlossMap");
                if (texture != null)
                    material.SetTexture("_MetallicSpecGlossMap", texture);
            }
            else
            {
                material.SetFloat("_WorkflowMode", (float)WorkflowMode.Metallic);
                Texture texture = material.GetTexture("_MetallicGlossMap");
                if (texture != null)
                    material.SetTexture("_MetallicSpecGlossMap", texture);
            }

            MaterialChanged(material);
        }

        void DoAlbedoArea(Material material)
        {
            m_MaterialEditor.TexturePropertySingleLine(Styles.albedoText, albedoMap, albedoColor);
            if (material.GetFloat("_AlphaClip") == 1)
            {
                m_MaterialEditor.ShaderProperty(alphaThreshold, Styles.clipThresholdText.text, MaterialEditor.kMiniTextureFieldLabelIndentLevel + 1);
            }
        }

        void DoNormalArea()
        {
            m_MaterialEditor.TexturePropertySingleLine(Styles.normalMapText, bumpMap, bumpMap.textureValue != null ? bumpScale : null);
            if (bumpScale.floatValue != 1 && UnityEditorInternal.InternalEditorUtility.IsMobilePlatform(EditorUserBuildSettings.activeBuildTarget))
                if (m_MaterialEditor.HelpBoxWithButton(Styles.bumpScaleNotSupported, Styles.fixNow))
                    bumpScale.floatValue = 1;
        }

        void DoAnisotropicArea()
        {
            m_MaterialEditor.TexturePropertySingleLine(Styles.tangentMapText, tangentMap, tangentMap.textureValue != null ? tangentDirStrength : null);
            if(tangentMap.textureValue != null)
            {
                m_MaterialEditor.VectorProperty(tangentDir, "Base Tangent Dir");

            }
        }

        void DoEmissionArea(Material material)
        {
            // Emission for GI?
            if (m_MaterialEditor.EmissionEnabledProperty())
            {
                bool hadEmissionTexture = emissionMap.textureValue != null;

                // Texture and HDR color controls
                m_MaterialEditor.TexturePropertyWithHDRColor(Styles.emissionText, emissionMap, emissionColorForRendering, false);

                // If texture was assigned and color was black set color to white
                float brightness = emissionColorForRendering.colorValue.maxColorComponent;
                if (emissionMap.textureValue != null && !hadEmissionTexture && brightness <= 0f)
                    emissionColorForRendering.colorValue = Color.white;

                // LW does not support RealtimeEmissive. We set it to bake emissive and handle the emissive is black right.
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                if (brightness <= 0f)
                    material.globalIlluminationFlags |= MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }

        void DoDetailArea()
        {
            m_MaterialEditor.TexturePropertySingleLine(Styles.detailMaskText, detailMask);
            m_MaterialEditor.TexturePropertySingleLine(Styles.detailAlbedoText, detailAlbedo);
            m_MaterialEditor.TexturePropertySingleLine(Styles.detailNormalText, detailNormal, detailNormal.textureValue != null ? detailNormalScale : null);
            if (detailNormalScale.floatValue != 1 && UnityEditorInternal.InternalEditorUtility.IsMobilePlatform(EditorUserBuildSettings.activeBuildTarget))
                if (m_MaterialEditor.HelpBoxWithButton(Styles.bumpScaleNotSupported, Styles.fixNow))
                    detailNormalScale.floatValue = 1;
        }

        void DoMetallicSpecularArea()
        {
            string[] metallicSpecSmoothnessChannelName;
            bool hasGlossMap = false;
            if ((WorkflowMode)workflowMode.floatValue == WorkflowMode.Metallic)
            {
                hasGlossMap = metallicGlossMap.textureValue != null;
                metallicSpecSmoothnessChannelName = Styles.metallicSmoothnessChannelNames;
                m_MaterialEditor.TexturePropertySingleLine(Styles.metallicMapText, metallicGlossMap,
                    hasGlossMap ? metallicScale : metallic);
            }
            else
            {
                hasGlossMap = specGlossMap.textureValue != null;
                metallicSpecSmoothnessChannelName = Styles.specularSmoothnessChannelNames;
                m_MaterialEditor.TexturePropertySingleLine(Styles.specularMapText, specGlossMap,
                    hasGlossMap ? null : specColor);
            }

            bool showSmoothnessScale = hasGlossMap;
            if (smoothnessMapChannel != null)
            {
                int smoothnessChannel = (int)smoothnessMapChannel.floatValue;
                if (smoothnessChannel == (int)SmoothnessMapChannel.AlbedoAlpha)
                    showSmoothnessScale = true;
            }

            int indentation = 2; // align with labels of texture properties
            m_MaterialEditor.ShaderProperty(showSmoothnessScale ? smoothnessScale : smoothness, showSmoothnessScale ? Styles.smoothnessScaleText : Styles.smoothnessText, indentation);
            if (showSmoothnessScale)
                m_MaterialEditor.ShaderProperty(occlusionStrength, "Occlusion", indentation);

            int prevIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 3;
            if (smoothnessMapChannel != null)
                DoPopup(Styles.smoothnessMapChannelText.text, smoothnessMapChannel, metallicSpecSmoothnessChannelName);
            EditorGUI.indentLevel = prevIndentLevel;
        }

        void DoWindArea()
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Space();
            if (vertAnimation.floatValue == 1)
            {
                m_MaterialEditor.ShaderProperty(trunkStiffness, "Trunk Stiffness");
                EditorGUILayout.Space();
            }
                

            if (vertAnimation.floatValue == 1 || vertAnimation.floatValue == 2)
            {
                m_MaterialEditor.ShaderProperty(branchStiffness, "Branch Stiffness");
                m_MaterialEditor.ShaderProperty(branchFrequency, "Branch Frequency");
                m_MaterialEditor.ShaderProperty(branchAmplify, "Branch Amplify");
                EditorGUILayout.Space();
            }
                
            m_MaterialEditor.ShaderProperty(detailFrequency, "Detail Frequency");
            m_MaterialEditor.ShaderProperty(detailAmplify, "Detail Amplify");
            EditorGUI.indentLevel--;
        }

        static SmoothnessMapChannel GetSmoothnessMapChannel(Material material)
        {
            int ch = (int)material.GetFloat("_SmoothnessTextureChannel");
            if (ch == (int)SmoothnessMapChannel.AlbedoAlpha)
                return SmoothnessMapChannel.AlbedoAlpha;

            return SmoothnessMapChannel.SpecularMetallicAlpha;
        }

        static void SetMaterialKeywords(Material material)
        {
            // Note: keywords must be based on Material value not on MaterialProperty due to multi-edit & material animation
            // (MaterialProperty value might come from renderer material property block)
            bool isSpecularWorkFlow = (WorkflowMode)material.GetFloat("_WorkflowMode") == WorkflowMode.Specular;
            bool hasGlossMap = false;
            if (isSpecularWorkFlow)
                hasGlossMap = material.GetTexture("_SpecGlossMap");
            else
                hasGlossMap = material.GetTexture("_MetallicGlossMap");

            bool doubleSidedEnable = material.GetFloat(kDoubleSidedEnable) == 1.0f;
            if (doubleSidedEnable)
            {
                DoubleSidedNormalMode doubleSidedNormalMode = (DoubleSidedNormalMode)material.GetFloat(kDoubleSidedNormalMode);
                switch (doubleSidedNormalMode)
                {
                    case DoubleSidedNormalMode.Mirror: // Mirror mode (in tangent space)
                        material.SetVector("_DoubleSidedConstants", new Vector4(1.0f, 1.0f, -1.0f, 0.0f));
                        break;

                    case DoubleSidedNormalMode.Flip: // Flip mode (in tangent space)
                        material.SetVector("_DoubleSidedConstants", new Vector4(-1.0f, -1.0f, -1.0f, 0.0f));
                        break;

                    case DoubleSidedNormalMode.None: // None mode (in tangent space)
                        material.SetVector("_DoubleSidedConstants", new Vector4(1.0f, 1.0f, 1.0f, 0.0f));
                        break;
                }
            }
            
            //#if defined(_ENABLE_WIND_SINGLE) || defined(_ENABLE_WIND_HIERARCHY) || defined(_ENABLE_WIND_PROCEDURAL)
            VertexAnimationMode vertAnimMode = (VertexAnimationMode)material.GetFloat("_VertexAnimation");
            switch(vertAnimMode)
            {
                case VertexAnimationMode.None:
                    CoreUtils.SetKeyword(material, "_ENABLE_WIND_SINGLE", false);
                    CoreUtils.SetKeyword(material, "_ENABLE_WIND_HIERARCHY", false);
                    CoreUtils.SetKeyword(material, "_ENABLE_WIND_PROCEDURAL", false);
                    break;
                case VertexAnimationMode.Hierarchy:
                    CoreUtils.SetKeyword(material, "_ENABLE_WIND_SINGLE", false);
                    CoreUtils.SetKeyword(material, "_ENABLE_WIND_HIERARCHY", true);
                    CoreUtils.SetKeyword(material, "_ENABLE_WIND_PROCEDURAL", false);
                    break;
                case VertexAnimationMode.Single:
                    CoreUtils.SetKeyword(material, "_ENABLE_WIND_SINGLE", true);
                    CoreUtils.SetKeyword(material, "_ENABLE_WIND_HIERARCHY", false);
                    CoreUtils.SetKeyword(material, "_ENABLE_WIND_PROCEDURAL", false);
                    break;
                case VertexAnimationMode.Procedural:
                    CoreUtils.SetKeyword(material, "_ENABLE_WIND_SINGLE", false);
                    CoreUtils.SetKeyword(material, "_ENABLE_WIND_HIERARCHY", false);
                    CoreUtils.SetKeyword(material, "_ENABLE_WIND_PROCEDURAL", true);
                    break;
            }

            CoreUtils.SetKeyword(material, "_DOUBLESIDED_ON", doubleSidedEnable);

            bool isAnisotropic = (BRDFMode)material.GetFloat("_BRDFMode") == BRDFMode.Anisotropic && material.GetTexture("_TangentMap");
            CoreUtils.SetKeyword(material, "_BRDF_ANISO", isAnisotropic);
            CoreUtils.SetKeyword(material, "_BRDF_STANDARD", !isAnisotropic);

            CoreUtils.SetKeyword(material, "_SPECULAR_SETUP", isSpecularWorkFlow);

            CoreUtils.SetKeyword(material, "_METALLICSPECGLOSSMAP", hasGlossMap);
            CoreUtils.SetKeyword(material, "_SPECGLOSSMAP", hasGlossMap && isSpecularWorkFlow);
            CoreUtils.SetKeyword(material, "_METALLICGLOSSMAP", hasGlossMap && !isSpecularWorkFlow);

            CoreUtils.SetKeyword(material, "_NORMALMAP", material.GetTexture("_BumpMap") || material.GetTexture("_DetailNormalMap"));

            CoreUtils.SetKeyword(material, "_SPECULARHIGHLIGHTS_OFF", material.GetFloat("_SpecularHighlights") == 0.0f);
            CoreUtils.SetKeyword(material, "_GLOSSYREFLECTIONS_OFF", material.GetFloat("_GlossyReflections") == 0.0f);

            CoreUtils.SetKeyword(material, "_OCCLUSIONMAP", material.GetTexture("_OcclusionMap"));
            CoreUtils.SetKeyword(material, "_PARALLAXMAP", material.GetTexture("_ParallaxMap"));
            CoreUtils.SetKeyword(material, "_DETAIL_MASK", material.GetTexture("_DetailMask"));
            CoreUtils.SetKeyword(material, "_DETAIL", material.GetTexture("_DetailAlbedoMap") || material.GetTexture("_DetailNormalMap"));
            CoreUtils.SetKeyword(material, "_DETAIL_MULX2", material.GetTexture("_DetailAlbedoMap") || material.GetTexture("_DetailNormalMap"));

            // A material's GI flag internally keeps track of whether emission is enabled at all, it's enabled but has no effect
            // or is enabled and may be modified at runtime. This state depends on the values of the current flag and emissive color.
            // The fixup routine makes sure that the material is in the correct state if/when changes are made to the mode or color.
            MaterialEditor.FixupEmissiveFlag(material);
            bool shouldEmissionBeEnabled = (material.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) == 0;
            CoreUtils.SetKeyword(material, "_EMISSION", shouldEmissionBeEnabled);

            if (material.HasProperty("_SmoothnessTextureChannel"))
            {
                CoreUtils.SetKeyword(material, "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", GetSmoothnessMapChannel(material) == SmoothnessMapChannel.AlbedoAlpha);
            }
        }
    }
}
