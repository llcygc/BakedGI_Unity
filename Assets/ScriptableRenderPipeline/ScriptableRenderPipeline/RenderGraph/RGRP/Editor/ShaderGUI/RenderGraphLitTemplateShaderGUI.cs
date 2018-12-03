using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace UnityEditor.Experimental.Rendering.ClusterPipeline
{
    class RenderGraphTemplateShaderGUI : ShaderGUI
    {
        public enum WorkflowMode
        {
            Specular = 0,
            Metallic
        }

        public enum SmoothnessMapChannel
        {
            SpecularMetallicAlpha,
            AlbedoAlpha,
        }

        public enum SurfaceType
        {
            Opaque,
            Transparent
        }

        public enum BlendMode
        {
            Alpha,   // Old school alpha-blending mode, fresnel does not affect amount of transparency
            Premultiply, // Physically plausible transparency mode, implemented as alpha pre-multiply
            Additive,
            Multiply
        }

        private static class Styles
        {
            public static GUIContent twoSidedText = new GUIContent("Two Sided", "Render front and back faces");
            public static GUIContent alphaClipText = new GUIContent("Alpha Clip", "Enable Alpha Clip");
            public static GUIContent clipThresholdText = new GUIContent("Clip Threshold", "Threshold for alpha clip");
            public static GUIContent highlightsText = new GUIContent("Specular Highlights", "Specular Highlights");
            public static GUIContent reflectionsText = new GUIContent("Reflections", "Glossy Reflections");
            public static GUIContent emissionText = new GUIContent("Color", "Emission (RGB)");
            public static GUIContent bumpScaleNotSupported = new GUIContent("Bump scale is not supported on mobile platforms");
            public static GUIContent fixNow = new GUIContent("Fix now");

            public static string forwardText = "Forward Rendering Options";
            public static string workflowModeText = "Workflow Mode";
            public static string surfaceType = "Surface Type";
            public static string blendingMode = "Blending Mode";
            public static string advancedText = "Advanced Options";
            public static readonly string[] workflowNames = Enum.GetNames(typeof(WorkflowMode));
            public static readonly string[] surfaceNames = Enum.GetNames(typeof(SurfaceType));
            public static readonly string[] blendNames = Enum.GetNames(typeof(BlendMode));
        }

        private MaterialProperty workflowMode;
        private MaterialProperty surfaceType;
        private MaterialProperty blendMode;
        private MaterialProperty culling;
        private MaterialProperty alphaClip;
        private MaterialProperty alphaThreshold;

        private MaterialProperty highlights;
        private MaterialProperty reflections;

        public void FindProperties(MaterialProperty[] properties)
        {
            workflowMode = FindProperty("_WorkflowMode", properties);
            surfaceType = FindProperty("_Surface", properties);
            blendMode = FindProperty("_Blend", properties);
            culling = FindProperty("_Cull", properties);
            alphaClip = FindProperty("_AlphaClip", properties);
            alphaThreshold = FindProperty("_Cutoff", properties);

            highlights = FindProperty("_SpecularHighlights", properties);
            reflections = FindProperty("_GlossyReflections", properties);
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            FindProperties(properties);
            m_MaterialEditor = materialEditor;
            Material material = materialEditor.target as Material;

            if (material == null)
                return;

            // Make sure that needed setup (ie keywords/renderqueue) are set up if we're switching some existing
            // material to a lightweight shader.
            if (m_FirstTimeApply)
            {
                MaterialChanged(material);
                m_FirstTimeApply = false;
            }

            if (m_MaterialEditor.isVisible)
            {
                ShaderPropertiesGUI(material, properties);
            }
        }

        public void MaterialChanged(Material material)
        {
            SetupMaterialBlendMode(material);
        }

        public void ShaderPropertiesGUI(Material material, MaterialProperty[] properties)
        {
            EditorGUIUtility.labelWidth = 0f;

            EditorGUI.BeginChangeCheck();
            {
                DoPopup(Styles.workflowModeText, workflowMode, Styles.workflowNames);
                DoPopup(Styles.surfaceType, surfaceType, Styles.surfaceNames);
                if ((SurfaceType)material.GetFloat("_Surface") == SurfaceType.Transparent)
                    DoPopup(Styles.blendingMode, blendMode, Styles.blendNames);

                EditorGUI.BeginChangeCheck();
                bool twoSidedEnabled = EditorGUILayout.Toggle(Styles.twoSidedText, culling.floatValue == 0);
                if (EditorGUI.EndChangeCheck())
                    culling.floatValue = twoSidedEnabled ? 0 : 2;

                EditorGUI.BeginChangeCheck();
                bool alphaClipEnabled = EditorGUILayout.Toggle(Styles.alphaClipText, alphaClip.floatValue == 1);
                if (EditorGUI.EndChangeCheck())
                    alphaClip.floatValue = alphaClipEnabled ? 1 : 0;

                if (material.GetFloat("_AlphaClip") == 1)
                {
                    m_MaterialEditor.ShaderProperty(alphaThreshold, Styles.clipThresholdText.text, MaterialEditor.kMiniTextureFieldLabelIndentLevel + 1);
                }

                EditorGUILayout.Space();

                Shader targetShader = material.shader;
                int shaderPorpertyCount = ShaderUtil.GetPropertyCount(targetShader);
                for (int i = 0; i < shaderPorpertyCount; i++)
                {
                    if (!ShaderUtil.IsShaderPropertyHidden(targetShader, i))
                    {
                        string propertyName = ShaderUtil.GetPropertyName(targetShader, i);
                        MaterialProperty property = FindProperty(propertyName, properties);
                        m_MaterialEditor.ShaderProperty(property, propertyName);
                    }
                }

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

        protected void DoPopup(string label, MaterialProperty property, string[] options)
        {
            EditorGUI.showMixedValue = property.hasMixedValue;

            var mode = property.floatValue;
            EditorGUI.BeginChangeCheck();
            mode = EditorGUILayout.Popup(label, (int)mode, options);
            if (EditorGUI.EndChangeCheck())
            {
                m_MaterialEditor.RegisterPropertyChangeUndo(label);
                property.floatValue = (float)mode;
            }

            EditorGUI.showMixedValue = false;
        }

        public static void SetupMaterialBlendMode(Material material)
        {
            bool alphaClip = material.GetFloat("_AlphaClip") == 1;
            if (alphaClip)
                material.EnableKeyword("_ALPHATEST_ON");
            else
                material.DisableKeyword("_ALPHATEST_ON");

            SurfaceType surfaceType = (SurfaceType)material.GetFloat("_Surface");
            if (surfaceType == SurfaceType.Opaque)
            {
                material.SetOverrideTag("RenderType", "Opaque");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = -1;
                material.SetShaderPassEnabled("ShadowCaster", true);
            }
            else
            {
                BlendMode blendMode = (BlendMode)material.GetFloat("_Blend");
                switch (blendMode)
                {
                    case BlendMode.Alpha:
                        material.SetOverrideTag("RenderType", "Transparent");
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        material.SetInt("_ZWrite", 0);
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        material.SetShaderPassEnabled("ShadowCaster", false);
                        break;
                    case BlendMode.Premultiply:
                        material.SetOverrideTag("RenderType", "Transparent");
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        material.SetInt("_ZWrite", 0);
                        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        material.SetShaderPassEnabled("ShadowCaster", false);
                        break;
                    case BlendMode.Additive:
                        material.SetOverrideTag("RenderType", "Transparent");
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        material.SetInt("_ZWrite", 0);
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        material.SetShaderPassEnabled("ShadowCaster", false);
                        break;
                    case BlendMode.Multiply:
                        material.SetOverrideTag("RenderType", "Transparent");
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.DstColor);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        material.SetInt("_ZWrite", 0);
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        material.SetShaderPassEnabled("ShadowCaster", false);
                        break;
                }
            }
        }

        protected MaterialEditor m_MaterialEditor;
        private bool m_FirstTimeApply = true;
    }
}
