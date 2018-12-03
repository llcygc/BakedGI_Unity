Shader "Viva/RenderGraph/Vegetation"
{
	Properties
    {
		_Color("Main Color", Color) = (1,1,1,1)
		_HueVariation("Hue Variation", Color) = (1.0,0.5,0.0,0.1)
		_MainTex("Base (RGB) Trans (A)", 2D) = "white" {}
		_DetailTex("Detail", 2D) = "black" {}
		_BumpMap("Normal Map", 2D) = "bump" {}
		_Cutoff("Alpha Cutoff", Range(0,1)) = 0.333
		_Specular("Specular Power", Range(0,1)) = 0.333
		_Translucency("Trans Settings", Vector) = (0, 1.0, 0, 0.05)

		[MaterialEnum(Off,0,Front,1,Back,2)] _Cull("Cull", Int) = 2
		[MaterialEnum(None,0,Fastest,1,Fast,2,Better,3,Best,4,Palm,5)] _WindQuality("Wind Quality", Range(0,5)) = 0

        _DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}

        //// Blending state
        //[HideInInspector] _Surface("__surface", Float) = 0.0
        //[HideInInspector] _Blend("__blend", Float) = 0.0
        //[HideInInspector] _AlphaClip("__clip", Float) = 0.0
        //[HideInInspector] _SrcBlend("__src", Float) = 1.0
        //[HideInInspector] _DstBlend("__dst", Float) = 0.0
        //[HideInInspector] _ZWrite("__zw", Float) = 1.0
	}

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal
            
    //enable GPU instancing support
    #pragma multi_compile_instancing

    //-------------------------------------------------------------------------------------
    // Include
    //-------------------------------------------------------------------------------------

    #include "CoreRP/ShaderLibrary/Common.hlsl"
    #include "CoreRP/ShaderLibrary/Wind.hlsl"
    #include "../../ShaderPasses/FragInputs.hlsl"
    #include "../../ShaderPasses/ShaderPass.cs.hlsl"


    //-------------------------------------------------------------------------------------
    // variable declaration
    //-------------------------------------------------------------------------------------

    #include "../LitProperties.hlsl"
    // All our shaders use same name for entry point
    #pragma vertex Vert
    #pragma fragment Frag

    ENDHLSL

	SubShader
	{
		Tags { "RenderPipeline" = "ClusterRenderPipeline" "DisableBatching" = "LODFading" }

        LOD 400
		Pass
		{
            Name "ClusterForward"
            Tags {"LightMode" = "ClusterForward"}

            //Stencil
            //{
            //    //WriteMask[_StencilWriteMask]
            //    ReadMask 64
            //    Ref 64//[_StencilRef]
            //    Comp Equal
            //    Pass Keep
            //}
            
            /*Blend [_SrcBlend] [_DstBlend]
            ZWrite[_ZWrite]*/
            Cull[_Cull]

            HLSLPROGRAM

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _ALPHATEST_ON 
            #pragma shader_feature _ALPHAPREMULTIPLY_ON

            #define ENABLE_WIND
        
            #pragma shader_feature EFFECT_BUMP
            #pragma shader_feature SPEEDTREE_ALPHATEST
            #pragma shader_feature EFFECT_HUE_VARIATION
            
            #pragma multi_compile __ SCREEN_SHADOW
            #pragma multi_compile __ LOD_FADE_PERCENTAGE LOD_FADE_CROSSFADE
            #pragma instancing_options assumeuniformscaling maxcount:50

            #define SHADERPASS SHADERPASS_FORWARD_VEGETATION
            #include "../../../ShaderVariables.hlsl"
            #include "../../Material.hlsl"
            #include "ShaderPass/VegetationSharePass.hlsl"
            #include "../../ShaderPasses/ShaderPassVegetationForward.hlsl"
            
			ENDHLSL
		}

        Pass
        {
            Name "ShadowCaster"
            Tags{ "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
            #pragma shader_feature _ALPHATEST_ON 
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #define ENABLE_WIND

            #pragma shader_feature SPEEDTREE_ALPHATEST
            #pragma multi_compile_vertex __ LOD_FADE_PERCENTAGE LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment __ LOD_FADE_CROSSFADE

            #define SHADERPASS SHADERPASS_SHADOWS
            #define USE_LEGACY_UNITY_MATRIX_VARIABLES
            #include "../../../ShaderVariables.hlsl"
            #include "../../Material.hlsl"
            #include "ShaderPass/VegetationDepthPass.hlsl"
            #include "../../ShaderPasses/ShaderPassVegetationDepthOnly.hlsl"
            ENDHLSL
	    }

        Pass
        {
            Name "TransparentPrePass"
            Tags{ "LightMode" = "TransparentPrePass" }
            ZWrite On
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _ALPHATEST_ON 
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        
            #define ENABLE_WIND
            
            #pragma shader_feature SPEEDTREE_ALPHATEST
            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex __ LOD_FADE_PERCENTAGE LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment __ LOD_FADE_CROSSFADE

            #define SHADERPASS SHADERPASS_DEPTH_ONLY
            #include "../../../ShaderVariables.hlsl"
            #include "../../Material.hlsl"
            #include "ShaderPass/VegetationDepthPass.hlsl"
            #include "../../ShaderPasses/ShaderPassVegetationDepthOnly.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{ "LightMode" = "DepthOnly" }

            ZWrite On
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
            #pragma shader_feature _ALPHATEST_ON 
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            
            #define ENABLE_WIND
            
            #pragma shader_feature SPEEDTREE_ALPHATEST
            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex __ LOD_FADE_PERCENTAGE LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment __ LOD_FADE_CROSSFADE

            #define SHADERPASS SHADERPASS_DEPTH_ONLY
            #include "../../../ShaderVariables.hlsl"
            #include "../../Material.hlsl"
            #include "ShaderPass/VegetationDepthPass.hlsl"
            #include "../../ShaderPasses/ShaderPassVegetationDepthOnly.hlsl"
            ENDHLSL

        }
            
        Pass
        {
            Name "DepthColor"
            Tags{ "LightMode" = "DepthColor" }

            ZWrite On
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
            #pragma shader_feature _ALPHATEST_ON 
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            
            #define ENABLE_WIND
            
            #pragma shader_feature SPEEDTREE_ALPHATEST
            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex __ LOD_FADE_PERCENTAGE LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment __ LOD_FADE_CROSSFADE

            #define SHADERPASS SHADERPASS_DEPTH_ONLY
            #include "../../../ShaderVariables.hlsl"
            #include "../../Material.hlsl"
            #include "ShaderPass/VegetationDepthPass.hlsl"
            #include "../../ShaderPasses/ShaderPassVegetationDepth.hlsl"
            ENDHLSL

        }

        Pass
        {
            Name "MotionVectors"
            Tags{"LightMode" = "MotionVectors"}

            // If velocity pass (motion vectors) is enabled we tag the stencil so it don't perform CameraMotionVelocity
            Stencil
            {
                WriteMask 128//[_StencilWriteMaskMV]
                ReadMask 64
                Ref 192//[_StencilRefMV]
                Comp Equal
                Pass Replace
            }

            Cull[_Cull]
            ZWrite On

            HLSLPROGRAM

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH
            #pragma shader_feature _ALPHATEST_ON 
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            
            #define ENABLE_WIND
            
            #pragma shader_feature SPEEDTREE_ALPHATEST
            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex __ LOD_FADE_PERCENTAGE LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment __ LOD_FADE_CROSSFADE

            #define SHADERPASS SHADERPASS_VELOCITY
            #include "../../../ShaderVariables.hlsl"
            #include "../../Material.hlsl"
            #include "ShaderPass/LitVelocityPass.hlsl"

            ENDHLSL

        }    

        Pass
        {
            Name "META"
            Tags{ "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            
            #pragma shader_feature GEOM_TYPE_BRANCH GEOM_TYPE_BRANCH_DETAIL GEOM_TYPE_FROND GEOM_TYPE_LEAF GEOM_TYPE_MESH

            #define SHADERPASS SHADERPASS_LIGHT_TRANSPORT
            #include "../../../ShaderVariables.hlsl"
            #include "../../Material.hlsl"
            #include "ShaderPass/VegetationSharePass.hlsl"
            #include "../Lighting/LightingUtils.hlsl"
            #include "../../ShaderPasses/ShaderPassLightTransport.hlsl"

            ENDHLSL
        }
    }

    CustomEditor "SpeedTreeMaterialInspector"
}
