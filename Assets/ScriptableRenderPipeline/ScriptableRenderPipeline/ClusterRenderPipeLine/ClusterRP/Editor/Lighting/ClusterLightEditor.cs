using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Viva.Rendering.RenderGraph;
using Viva.Rendering.RenderGraph.ClusterPipeline;
using Unity.Collections;

namespace UnityEditor.Experimental.Rendering.ClusterPipeline
{
    [CanEditMultipleObjects]
    [CustomEditorForRenderPipeline(typeof(Light), typeof(ClusterRenderPipeLineAsset))]
    sealed partial class ClusterLightEditor : LightEditor
    {
        sealed class SerializedBaseData
        {
            public SerializedProperty type;
            public SerializedProperty range;
            public SerializedProperty spotAngle;
            public SerializedProperty cookie;
            public SerializedProperty cookieSize;
            public SerializedProperty color;
            public SerializedProperty intensity;
            public SerializedProperty bounceIntensity;
            public SerializedProperty colorTemperature;
            public SerializedProperty useColorTemperature;
            public SerializedProperty shadowsType;
            public SerializedProperty shadowsBias;
            public SerializedProperty shadowsNormalBias;
            public SerializedProperty shadowsNearPlane;
            public SerializedProperty lightmapping;
            public SerializedProperty areaSizeX;
            public SerializedProperty areaSizeY;
            public SerializedProperty bakedShadowRadius;
            public SerializedProperty bakedShadowAngle;
        }

        sealed class SerializedLightData
        {
            public SerializedProperty spotInnerPercent;
            public SerializedProperty lightDimmer;
            public SerializedProperty fadeDistance;
            public SerializedProperty affectDiffuse;
            public SerializedProperty affectSpecular;
            public SerializedProperty lightTypeExtent;
            public SerializedProperty spotLightShape;
            public SerializedProperty shapeLength;
            public SerializedProperty shapeWidth;
            public SerializedProperty aspectRatio;
            public SerializedProperty shapeRadius;
            public SerializedProperty maxSmoothness;
            public SerializedProperty applyRangeAttenuation;

            // Editor stuff
            public SerializedProperty useOldInspector;
            public SerializedProperty showFeatures;
            public SerializedProperty showAdditionalSettings;
        }

        sealed class SerializedShadowData
        {
            public SerializedProperty dimmer;
            public SerializedProperty fadeDistance;
            public SerializedProperty cascadeCount;
            public SerializedProperty cascadeRatios;
            public SerializedProperty cascadeBorders;
            public SerializedProperty resolution;
        }

        sealed class SerializedCachedShadowData
        {
            public SerializedProperty useStaticShadowmapForLastCascade;
            public SerializedProperty needUpdateStaticShadowmapForLastCascade;
            public SerializedProperty shadowUpdateType;
            public SerializedProperty staticShadowmapResolution;
            public SerializedProperty depthFallOffPercent;
            public SerializedProperty shadowNearMultiplier;
            public SerializedProperty shadowFarMulitplier;
            public SerializedProperty staticShadowmapInfo;
            public SerializedProperty affectVolumetric;
        }

        SerializedObject m_SerializedAdditionalLightData;
        SerializedObject m_SerializedAdditionalShadowData;
        SerializedObject m_SerializedCachedShadowData;

        SerializedBaseData m_BaseData;
        SerializedLightData m_AdditionalLightData;
        SerializedShadowData m_ShadowData;
        SerializedCachedShadowData m_CachedShadowData;

        bool m_TypeIsSame { get { return !m_BaseData.type.hasMultipleDifferentValues; } }
        bool m_LightmappingTypeIsName { get { return !m_BaseData.lightmapping.hasMultipleDifferentValues; } }
        bool m_IsCompletelyBaked { get { return m_BaseData.lightmapping.intValue == 2; } }
        bool m_IsRealtime { get { return m_BaseData.lightmapping.intValue == 4; } }
        Light light { get { return serializedObject.targetObject as Light; } }
        Texture m_Cookie { get { return m_BaseData.cookie.objectReferenceValue as Texture; } }
        bool m_BakingWarningValue { get { return !UnityEditor.Lightmapping.bakedGI && m_LightmappingTypeIsName && !m_IsRealtime; } }
        bool m_BounceWarningValue
        {
            get
            {
                return m_TypeIsSame && (light.type == LightType.Point || light.type == LightType.Spot) &&
                    m_LightmappingTypeIsName && m_IsRealtime && !m_BaseData.bounceIntensity.hasMultipleDifferentValues
                    && m_BaseData.bounceIntensity.floatValue > 0.0f;
            }
        }

        public bool cookieWarningValue
        {
            get
            {
                return m_TypeIsSame && light.type == LightType.Spot &&
                    !m_BaseData.cookie.hasMultipleDifferentValues && m_Cookie && m_Cookie.wrapMode != TextureWrapMode.Clamp;
            }
        }

        // LightType + LightTypeExtent combined
        enum LightShape
        {
            Spot,
            Directional,
            Point,
            //Area, <= offline base type not displayed in our case but used for GI of our area light
            Rectangle,
            Line,
            //Sphere,
            //Disc,
        }

        // Used for UI only; the processing code must use LightTypeExtent and LightType
        LightShape m_LightShape;

        void OnEnable()
        {
            // Get & automatically add additional HD data if not present
            var lightData = GetAdditionalData<ClusterAdditionalLightData>();
            var shadowData = GetAdditionalData<AdditionalShadowData>();
            var cachedShadowData = GetAdditionalData<CachedShadowData>();
            m_SerializedAdditionalLightData = new SerializedObject(lightData);
            m_SerializedAdditionalShadowData = new SerializedObject(shadowData);
            m_SerializedCachedShadowData = new SerializedObject(cachedShadowData);

            m_BaseData = new SerializedBaseData
            {
                type = serializedObject.FindProperty("m_Type"),
                range = serializedObject.FindProperty("m_Range"),
                spotAngle = serializedObject.FindProperty("m_SpotAngle"),
                cookie = serializedObject.FindProperty("m_Cookie"),
                cookieSize = serializedObject.FindProperty("m_CookieSize"),
                color = serializedObject.FindProperty("m_Color"),
                intensity = serializedObject.FindProperty("m_Intensity"),
                bounceIntensity = serializedObject.FindProperty("m_BounceIntensity"),
                colorTemperature = serializedObject.FindProperty("m_ColorTemperature"),
                useColorTemperature = serializedObject.FindProperty("m_UseColorTemperature"),
                shadowsType = serializedObject.FindProperty("m_Shadows.m_Type"),
                shadowsBias = serializedObject.FindProperty("m_Shadows.m_Bias"),
                shadowsNormalBias = serializedObject.FindProperty("m_Shadows.m_NormalBias"),
                shadowsNearPlane = serializedObject.FindProperty("m_Shadows.m_NearPlane"),
                lightmapping = serializedObject.FindProperty("m_Lightmapping"),
                areaSizeX = serializedObject.FindProperty("m_AreaSize.x"),
                areaSizeY = serializedObject.FindProperty("m_AreaSize.y"),
                bakedShadowRadius = serializedObject.FindProperty("m_ShadowRadius"),
                bakedShadowAngle = serializedObject.FindProperty("m_ShadowAngle")
            };

            using (var o = new PropertyFetcher<ClusterAdditionalLightData>(m_SerializedAdditionalLightData))
                m_AdditionalLightData = new SerializedLightData
                {
                    spotInnerPercent = o.Find(x => x.m_InnerSpotPercent),
                    lightDimmer = o.Find(x => x.lightDimmer),
                    fadeDistance = o.Find(x => x.fadeDistance),
                    affectDiffuse = o.Find(x => x.affectDiffuse),
                    affectSpecular = o.Find(x => x.affectSpecular),
                    lightTypeExtent = o.Find(x => x.lightTypeExtent),
                    spotLightShape = o.Find(x => x.spotLightShape),
                    shapeLength = o.Find(x => x.shapeLength),
                    shapeWidth = o.Find(x => x.shapeWidth),
                    aspectRatio = o.Find(x => x.aspectRatio),
                    shapeRadius = o.Find(x => x.shapeRadius),
                    maxSmoothness = o.Find(x => x.maxSmoothness),
                    applyRangeAttenuation = o.Find(x => x.applyRangeAttenuation),

                    // Editor stuff
                    useOldInspector = o.Find(x => x.useOldInspector),
                    showFeatures = o.Find(x => x.featuresFoldout),
                    showAdditionalSettings = o.Find(x => x.showAdditionalSettings)
                };

            // TODO: Review this once AdditionalShadowData is refactored
            using (var o = new PropertyFetcher<AdditionalShadowData>(m_SerializedAdditionalShadowData))
                m_ShadowData = new SerializedShadowData
                {
                    dimmer = o.Find(x => x.shadowDimmer),
                    fadeDistance = o.Find(x => x.shadowFadeDistance),
                    cascadeCount = o.Find("shadowCascadeCount"),
                    cascadeRatios = o.Find("shadowCascadeRatios"),
                    cascadeBorders = o.Find("shadowCascadeBorders"),
                    resolution = o.Find(x => x.shadowResolution)
                };

            using (var o = new PropertyFetcher<CachedShadowData>(m_SerializedCachedShadowData))
                m_CachedShadowData = new SerializedCachedShadowData
                {
                    shadowUpdateType = o.Find(x => x.shadowUpdateType),
                    useStaticShadowmapForLastCascade = o.Find(x => x.UseStaticShadowmapForLastCascade),
                    needUpdateStaticShadowmapForLastCascade = o.Find(x => x.NeedUpdateStaticShadowmapForLastCascade),
                    staticShadowmapResolution = o.Find(x => x.StaticShadowResolution),
                    depthFallOffPercent = o.Find(x => x.DepthFallOffPercent),
                    shadowNearMultiplier = o.Find(x => x.ShadowNearMultiplier),
                    shadowFarMulitplier = o.Find(x => x.ShadowFarMultiplier),
                    staticShadowmapInfo = o.Find(x => x.cachedShadowInfo),
                    affectVolumetric = o.Find(x => x.AffectVolumectricFog)
                };
        }

        public override void OnInspectorGUI()
        {
            m_SerializedAdditionalLightData.Update();
            m_SerializedAdditionalShadowData.Update();
            m_SerializedCachedShadowData.Update();

            bool useOldInspector = m_AdditionalLightData.useOldInspector.boolValue;

            if (GUILayout.Button("Toggle default light editor"))
                useOldInspector = !useOldInspector;

            m_AdditionalLightData.useOldInspector.boolValue = useOldInspector;

            if (useOldInspector)
            {
                DrawDefaultInspector();
                ApplyAdditionalComponentVisibility(false);
                m_SerializedAdditionalShadowData.ApplyModifiedProperties();
                m_SerializedAdditionalLightData.ApplyModifiedProperties();
                m_SerializedCachedShadowData.ApplyModifiedProperties();
                return;
            }

            //New editor
            ApplyAdditionalComponentVisibility(true);
            CheckStyles();

            serializedObject.Update();

            ResolveLightShape();

            DrawFoldout(m_AdditionalLightData.showFeatures, "Features", DrawFeatures);
            DrawFoldout(m_BaseData.type, "Shape", DrawShape);
            DrawFoldout(m_BaseData.intensity, "Light", DrawLightSettings);

            if (m_BaseData.shadowsType.enumValueIndex != (int)LightShadows.None)
                DrawFoldout(m_BaseData.shadowsType, "Shadows", DrawShadows);

            CoreEditorUtils.DrawSplitter();
            EditorGUILayout.Space();

            m_SerializedAdditionalShadowData.ApplyModifiedProperties();
            m_SerializedAdditionalLightData.ApplyModifiedProperties();
            m_SerializedCachedShadowData.ApplyModifiedProperties();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawFoldout(SerializedProperty foldoutProperty, string title, Action func)
        {
            CoreEditorUtils.DrawSplitter();

            bool state = foldoutProperty.isExpanded;
            state = CoreEditorUtils.DrawHeaderFoldout(title, state);

            if(state)
            {
                EditorGUI.indentLevel++;
                func();
                EditorGUI.indentLevel--;
                GUILayout.Space(2f);
            }

            foldoutProperty.isExpanded = state;
        }

        void DrawFeatures()
        {
            EditorGUILayout.PropertyField(m_AdditionalLightData.showAdditionalSettings);
            EditorGUILayout.PropertyField(m_CachedShadowData.affectVolumetric);

            bool disableScope = m_IsCompletelyBaked
                || m_LightShape == LightShape.Line
                || m_LightShape == LightShape.Rectangle;

            using (new EditorGUI.DisabledScope(disableScope))
            {
                bool shadowEnabled = EditorGUILayout.Toggle(new GUIContent("Enable Shadows"), m_BaseData.shadowsType.enumValueIndex != 0);
                m_BaseData.shadowsType.enumValueIndex = shadowEnabled ? (int)LightShadows.Hard : (int)LightShadows.None;
            }
        }
        
        void DrawShape()
        {
            m_LightShape = (LightShape)EditorGUILayout.Popup(s_Styles.shape, (int)m_LightShape, s_Styles.shapeNames);

            //LightShape ie Cluster specific, it need to drive LightType from the original LightType
            //When it make sense, so the GI is still in sync with the light shape
            switch(m_LightShape)
            {
                case LightShape.Directional:
                    m_BaseData.type.enumValueIndex = (int)LightType.Directional;
                    m_AdditionalLightData.lightTypeExtent.enumValueIndex = (int)LightTypeExtent.Punctual;
                    break;

                case LightShape.Point:
                    m_BaseData.type.enumValueIndex = (int)LightType.Point;
                    m_AdditionalLightData.lightTypeExtent.enumValueIndex = (int)LightTypeExtent.Punctual;
                    EditorGUILayout.PropertyField(m_AdditionalLightData.maxSmoothness, s_Styles.maxSmoothness);
                    break;

                case LightShape.Spot:
                    m_BaseData.type.enumValueIndex = (int)LightType.Spot;
                    m_AdditionalLightData.lightTypeExtent.enumValueIndex = (int)LightTypeExtent.Punctual;
                    EditorGUILayout.PropertyField(m_AdditionalLightData.spotLightShape, s_Styles.spotLightShape);
                    var spotLightShape = (SpotLightShape)m_AdditionalLightData.spotLightShape.enumValueIndex;
                    if(spotLightShape == SpotLightShape.Cone)
                    {
                        EditorGUILayout.Slider(m_BaseData.spotAngle, 0f, 179.9f, s_Styles.spotAngle);
                        EditorGUILayout.Slider(m_AdditionalLightData.spotInnerPercent, 0f, 100f, s_Styles.spotInnerPercent);
                    }
                    else if(spotLightShape == SpotLightShape.Pyramid)
                    {
                        EditorGUILayout.Slider(m_BaseData.spotAngle, 0f, 179.9f, s_Styles.spotAngle);
                        EditorGUILayout.Slider(m_AdditionalLightData.aspectRatio, 0.05f, 20.0f, s_Styles.aspectRatioPyramid);
                    }
                    else if (spotLightShape == SpotLightShape.Box)
                    {
                        EditorGUILayout.PropertyField(m_AdditionalLightData.shapeLength, s_Styles.shapeLengthBox);
                        EditorGUILayout.PropertyField(m_AdditionalLightData.shapeWidth, s_Styles.shapeWidthBox);
                    }
                    EditorGUILayout.PropertyField(m_AdditionalLightData.maxSmoothness, s_Styles.maxSmoothness);
                    break;

                case LightShape.Rectangle:
                    m_BaseData.type.enumValueIndex = (int)LightType.Point;
                    m_AdditionalLightData.lightTypeExtent.enumValueIndex = (int)LightTypeExtent.Rectangle;
                    EditorGUILayout.PropertyField(m_AdditionalLightData.shapeLength, s_Styles.shapeLengthRect);
                    EditorGUILayout.PropertyField(m_AdditionalLightData.shapeWidth, s_Styles.shapeWidthRect);
                    m_BaseData.areaSizeX.floatValue = m_AdditionalLightData.shapeLength.floatValue;
                    m_BaseData.areaSizeY.floatValue = m_AdditionalLightData.shapeWidth.floatValue;
                    m_BaseData.shadowsType.enumValueIndex = (int)LightShadows.None;
                    break;

                case LightShape.Line:
                    // TODO: Currently if we use Area type as it is offline light in legacy, the light will not exist at runtime
                    //m_BaseData.type.enumValueIndex = (int)LightType.Area;
                    m_BaseData.type.enumValueIndex = (int)LightType.Point;
                    m_AdditionalLightData.lightTypeExtent.enumValueIndex = (int)LightTypeExtent.Line;
                    EditorGUILayout.PropertyField(m_AdditionalLightData.shapeLength, s_Styles.shapeLengthLine);
                    // Fake line with a small rectangle in vanilla unity for GI
                    m_BaseData.areaSizeX.floatValue = m_AdditionalLightData.shapeLength.floatValue;
                    m_BaseData.areaSizeY.floatValue = 0.01f;
                    m_BaseData.shadowsType.enumValueIndex = (int)LightShadows.None;
                    break;

                case (LightShape)(-1):
                    // don't do anything, this is just to handle multi selection
                    break;

                default:
                    Debug.Assert(false, "Not implemented light type");
                    break;
            }
        }

        void DrawLightSettings()
        {
            if (GraphicsSettings.lightsUseLinearIntensity && GraphicsSettings.lightsUseColorTemperature)
            {
                EditorGUILayout.PropertyField(m_BaseData.useColorTemperature, s_Styles.useColorTemperature);
                if (m_BaseData.useColorTemperature.boolValue)
                {
                    const float kMinKelvin = 1000f;
                    const float kMaxKelvin = 20000f;

                    EditorGUILayout.LabelField(s_Styles.color);
                    EditorGUI.indentLevel += 1;
                    EditorGUILayout.PropertyField(m_BaseData.color, s_Styles.colorFilter);
                    EditorGUILayout.Slider(m_BaseData.colorTemperature, kMinKelvin, kMaxKelvin, s_Styles.colorTemperature);
                    EditorGUI.indentLevel -= 1;
                }
                else EditorGUILayout.PropertyField(m_BaseData.color, s_Styles.colorFilter);
            }
            else EditorGUILayout.PropertyField(m_BaseData.color, s_Styles.color);

            EditorGUILayout.PropertyField(m_BaseData.intensity, s_Styles.intensity);
            EditorGUILayout.PropertyField(m_BaseData.bounceIntensity, s_Styles.lightBounceIntensity);

            //Indirect shadows warning (Should be removed when we support realtime indirect shadows)
            if(m_BounceWarningValue)
                EditorGUILayout.HelpBox(s_Styles.indirectBounceShadowWarning.text, MessageType.Info);

            EditorGUILayout.PropertyField(m_BaseData.range, s_Styles.range);
            EditorGUILayout.PropertyField(m_BaseData.lightmapping, s_Styles.lightmappingMode);

            // Warning if GI Baking disabled and m_Lightmapping isn't realtime
            if (m_BakingWarningValue)
                EditorGUILayout.HelpBox(s_Styles.bakingWarning.text, MessageType.Info);

            //No cookie with area light (maybe in future textured area light?)
            if(m_LightShape != LightShape.Rectangle && m_LightShape != LightShape.Line)
            {
                EditorGUILayout.PropertyField(m_BaseData.cookie, s_Styles.cookie);

                //Warning on spotlights if the cookie is set to repeat
                if(cookieWarningValue)
                    EditorGUILayout.HelpBox(s_Styles.cookieWarning.text, MessageType.Warning);

                //When directional light use a cookie it can control the size
                if(m_Cookie != null && m_LightShape == LightShape.Directional)
                {
                    EditorGUILayout.Slider(m_AdditionalLightData.shapeLength, 0.01f, 10f, s_Styles.cookieSizeX);
                    EditorGUILayout.Slider(m_AdditionalLightData.shapeWidth, 0.01f, 10f, s_Styles.cookieSizeY);
                }
            }

            if(m_AdditionalLightData.showAdditionalSettings.boolValue)
            {
                EditorGUILayout.LabelField("Additional Settings", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_AdditionalLightData.affectDiffuse, s_Styles.affectDiffuse);
                EditorGUILayout.PropertyField(m_AdditionalLightData.affectSpecular, s_Styles.affectSpecular);
                EditorGUILayout.PropertyField(m_AdditionalLightData.fadeDistance, s_Styles.fadeDistance);
                EditorGUILayout.PropertyField(m_AdditionalLightData.lightDimmer, s_Styles.lightDimmer);
                EditorGUILayout.PropertyField(m_AdditionalLightData.applyRangeAttenuation, s_Styles.applyRangeAttenuation);
                EditorGUI.indentLevel--;
            }
        }

        void DrawShadows()
        {
            if (m_IsCompletelyBaked)
            {
                switch ((LightType)m_BaseData.type.enumValueIndex)
                {
                    case LightType.Directional:
                        EditorGUILayout.Slider(m_BaseData.bakedShadowAngle, 0f, 90f, s_Styles.bakedShadowAngle);
                        break;
                    case LightType.Spot:
                    case LightType.Point:
                        EditorGUILayout.PropertyField(m_BaseData.bakedShadowRadius, s_Styles.bakedShadowRadius);
                        break;
                }

                return;
            }

            EditorGUILayout.PropertyField(m_ShadowData.resolution, s_Styles.shadowResolution);
            EditorGUILayout.Slider(m_BaseData.shadowsBias, 0.001f, 1f, s_Styles.shadowBias);
            EditorGUILayout.Slider(m_BaseData.shadowsNormalBias, 0.001f, 1f, s_Styles.shadowNormalBias);
            EditorGUILayout.Slider(m_BaseData.shadowsNearPlane, 0.01f, 10f, s_Styles.shadowNearPlane);

            if (m_BaseData.type.enumValueIndex != (int)LightType.Directional)
            {
                EditorGUILayout.PropertyField(m_CachedShadowData.shadowUpdateType, s_Styles.shadowUpdateType);
                return;
            }

            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.IntSlider(m_ShadowData.cascadeCount, 1, 4, s_Styles.shadowCascadeCount);

                if (scope.changed)
                {
                    int len = m_ShadowData.cascadeCount.intValue;
                    m_ShadowData.cascadeRatios.arraySize = len - 1;
                    m_ShadowData.cascadeBorders.arraySize = len;
                }
            }

            if (m_ShadowData.cascadeCount.intValue > 1)
            {
                EditorGUILayout.PropertyField(m_CachedShadowData.useStaticShadowmapForLastCascade, s_Styles.useStaticShadowmapForLastCascade);
            }            
            else
                m_CachedShadowData.useStaticShadowmapForLastCascade.boolValue = false;

            EditorGUI.indentLevel++;
            if (m_CachedShadowData.useStaticShadowmapForLastCascade.boolValue)
            {
                EditorGUILayout.PropertyField(m_CachedShadowData.staticShadowmapResolution);
            }


            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                // Draw each field first...
                int arraySize = m_ShadowData.cascadeRatios.arraySize;
                for (int i = 0; i < arraySize; i++)
                    EditorGUILayout.Slider(m_ShadowData.cascadeRatios.GetArrayElementAtIndex(i), 0f, 1f, s_Styles.shadowCascadeRatios[i]);

                if (scope.changed)
                {
                    // ...then clamp values to avoid out of bounds cascade ratios
                    for (int i = 0; i < arraySize; i++)
                    {
                        var ratios = m_ShadowData.cascadeRatios;
                        var ratioProp = ratios.GetArrayElementAtIndex(i);
                        float val = ratioProp.floatValue;

                        if (i > 0)
                        {
                            var prevRatioProp = ratios.GetArrayElementAtIndex(i - 1);
                            float prevVal = prevRatioProp.floatValue;
                            val = Mathf.Max(val, prevVal);
                        }

                        if (i < arraySize - 1)
                        {
                            var nextRatioProp = ratios.GetArrayElementAtIndex(i + 1);
                            float nextVal = nextRatioProp.floatValue;
                            val = Mathf.Min(val, nextVal);
                        }

                        ratioProp.floatValue = val;
                    }
                }

            }

            EditorGUI.indentLevel--;

            EditorGUILayout.LabelField("Light Leak Fix Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(m_CachedShadowData.depthFallOffPercent, 0f, 1f, "Depth Falloff Percent");
            EditorGUILayout.PropertyField(m_CachedShadowData.shadowNearMultiplier, new GUIContent("Shadow Near Mulitplier"));
            EditorGUILayout.PropertyField(m_CachedShadowData.shadowFarMulitplier, new GUIContent("Shadow Far Mulitplier"));
            EditorGUI.indentLevel--;

            if (m_AdditionalLightData.showAdditionalSettings.boolValue)
            {
                EditorGUILayout.LabelField("Additional Settings", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_ShadowData.fadeDistance, s_Styles.shadowFadeDistance);
                EditorGUILayout.PropertyField(m_ShadowData.dimmer, s_Styles.shadowDimmer);
                EditorGUI.indentLevel--;
            }
        }

        void ApplyAdditionalComponentVisibility(bool hide)
        {
            var flags = hide ? HideFlags.HideInInspector : HideFlags.None;

            foreach (var t in m_SerializedAdditionalLightData.targetObjects)
                ((ClusterAdditionalLightData)t).hideFlags = flags;

            foreach (var t in m_SerializedAdditionalShadowData.targetObjects)
                ((AdditionalShadowData)t).hideFlags = flags;

            foreach (var t in m_SerializedCachedShadowData.targetObjects)
                ((CachedShadowData)t).hideFlags = flags;
        }

        void ResolveLightShape()
        {
            var type = m_BaseData.type;

            if(type.hasMultipleDifferentValues)
            {
                m_LightShape = (LightShape)(-1);
                return;
            }

            var lightTypeExtent = (LightTypeExtent)m_AdditionalLightData.lightTypeExtent.enumValueIndex;

            if(lightTypeExtent == LightTypeExtent.Punctual)
            {
                switch((LightType)type.enumValueIndex)
                {
                    case LightType.Directional:
                        m_LightShape = LightShape.Directional;
                        break;
                    case LightType.Point:
                        m_LightShape = LightShape.Point;
                        break;
                    case LightType.Spot:
                        m_LightShape = LightShape.Spot;
                        break;
                }
            }
            else
            {
                switch(lightTypeExtent)
                {
                    case LightTypeExtent.Rectangle:
                        m_LightShape = LightShape.Rectangle;
                        break;
                    case LightTypeExtent.Line:
                        m_LightShape = LightShape.Line;
                        break;
                }
            }
        }

        // TODO: Move this to a generic EditorUtilities class
        T[] GetAdditionalData<T>()
            where T : Component
        {
            // Handles multi-selection
            var data = targets.Cast<Component>()
                .Select(t => t.GetComponent<T>())
                .ToArray();

            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == null)
                    data[i] = Undo.AddComponent<T>(((Component)targets[i]).gameObject);
            }

            return data;
        }
    }
}
