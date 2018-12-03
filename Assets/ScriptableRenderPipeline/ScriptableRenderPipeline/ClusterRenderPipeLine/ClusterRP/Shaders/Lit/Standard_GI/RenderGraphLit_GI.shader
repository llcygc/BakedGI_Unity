Shader "Viva/RenderGraph/Lit_GI"
{
	Properties
    {
        // Specular vs Metallic workflow
        [HideInInspector] _WorkflowMode("WorkflowMode", Float) = 1.0
        [HideInInspector] _BRDFMode("BrdfMode", Float) = 0.0

        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo", 2D) = "white" {}

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
        _GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        [Gamma] _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
		_MetallicScale("Metallic Scale", Range(0.0, 1.0)) = 1.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _GlossyReflections("Glossy Reflections", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _Parallax("Height Scale", Range(0.005, 0.08)) = 0.02
        _ParallaxMap("Height Map", 2D) = "black" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _TangentMap("Tangent (RG)", 2D) = "bump" {}
        _BaseTangentDir("Base Tangent Direction (XYZ)", Vector) = (0.0,1.0,0.0,0.0)
        _TangentDirStrength("Strength", Range(0,1)) = 1

        _DetailMask("Detail Mask", 2D) = "white" {}

        _DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}
        _DetailNormalMapScale("Scale", Float) = 1.0
        _DetailNormalMap("Normal Map", 2D) = "bump" {}

        [Enum(UV0,0,UV1,1)] _UVSec("UV Set for secondary textures", Float) = 0
            
        [ToggleOff] _DoubleSidedEnable("Double sided enable", Float) = 0.0
        [Enum(None, 0, Mirror, 1, Flip, 2)] _DoubleSidedNormalMode("Double sided normal mode", Float) = 1
        [HideInInspector] _DoubleSidedConstants("_DoubleSidedConstants", Vector) = (1, 1, -1, 0)

		[HideInInspector] _VertexAnimation("Vertex Animation Enabled", Float) = 0.0
		_TrunkStiffness("Trunk Stiffness", Range(0.0, 1.0)) = 0.2
		_BranchStiffness("Branch Stiffness",  Range(0.0, 1.0)) = 0.2
		_BranchFrequency("Branch Frequency", Float) = 1
		_BranchAmplify("Branch Amplify", Float) = 1
		_DetailFrequency("Detial Frequency", Float) = 1
		_DetailAmplify("Detial Amplify", Float) = 1

        // Blending state
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0

        // Stencil state
        [HideInInspector] _StencilRef("_StencilRef", Int) = 2 // StencilLightingUsage.RegularLighting  (fixed at compile time)
        [HideInInspector] _StencilWriteMask("_StencilWriteMask", Int) = 7 // StencilMask.Lighting  (fixed at compile time)
        [HideInInspector] _StencilRefMV("_StencilRefMV", Int) = 128 // StencilLightingUsage.RegularLighting  (fixed at compile time)
        [HideInInspector] _StencilWriteMaskMV("_StencilWriteMaskMV", Int) = 128 // StencilMask.ObjectsVelocity  (fixed at compile time)
	}

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal

    //enable GPU instancing support
	#pragma shader_feature __ _ENABLE_WIND_SINGLE _ENABLE_WIND_HIERARCHY _ENABLE_WIND_PROCEDURAL
    #pragma multi_compile_instancing
	#pragma multi_compile __ LOD_FADE_CROSSFADE

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
    //#define USE_LEGACY_UNITY_MATRIX_VARIABLES

    // All our shaders use same name for entry point
    #pragma vertex Vert
    #pragma fragment Frag

    ENDHLSL

	SubShader
	{
		Tags { "RenderPipeline" = "ClusterRenderPipeline" "RenderType" = "Opaque" }

		Pass
		{
            Name "ClusterForward"
            Tags {"LightMode" = "ClusterForward"}

            Stencil
            {
                //WriteMask[_StencilWriteMask]
                ReadMask 64
                Ref 64//[_StencilRef]
                Comp Equal
                Pass Keep
            }

            ZTest LEqual
            Blend [_SrcBlend] [_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _METALLIC_SETUP _SPECULAR_SETUP
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _ALPHATEST_ON 
            #pragma shader_feature _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _EMISSION
            #pragma shader_feature _METALLICSPECGLOSSMAP
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _OCCLUSIONMAP
            #pragma shader_feature _DOUBLESIDED_ON
            #pragma shader_feature _FORWARD_CLUSTER_FOG

            #pragma shader_feature _BRDF_STANDARD _BRDF_ANISO

            #pragma shader_feature _DETAIL
            #pragma shader_feature _DETAIL_MULX2
			#pragma shader_feature _DETAIL_MASK

            #pragma multi_compile __ SCREEN_SHADOW
			#pragma multi_compile __ VOLUMETRIC_FOG_ON
			#pragma multi_compile __ SINGLE_DIR_LIGHT MULTIPLE_DIR_LIGHTS
			#pragma multi_compile __ LOCAL_LIGHTS_ON
			
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #define SHADERPASS SHADERPASS_FORWARD
			#include "../../../ShaderVariables.hlsl"
			#include "../../WindData/WindData.hlsl"
            #include "../../Material.hlsl"
            #include "ShaderPass/LitSharePass.hlsl"
            #include "../Lighting/LightingUtils.hlsl"
			#include "GI_Module.hlsl"
            #include "ShaderPassForward_GI.hlsl"
            			
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
            #pragma shader_feature _ALPHATEST_ON 
            #pragma shader_feature _ALPHAPREMULTIPLY_ON
			
            #define SHADERPASS SHADERPASS_SHADOWS
            #define USE_LEGACY_UNITY_MATRIX_VARIABLES
			#include "../../../ShaderVariables.hlsl"
			#include "../../WindData/WindData.hlsl"
            #include "../../Material.hlsl"
            #include "ShaderPass/LitDepthPass.hlsl"
            #include "../../ShaderPasses/ShaderPassDepthOnly.hlsl"
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
            #pragma shader_feature _ALPHATEST_ON 
            #pragma shader_feature _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #define SHADERPASS SHADERPASS_DEPTH_ONLY
			#include "../../../ShaderVariables.hlsl"
			#include "../../WindData/WindData.hlsl"
            #include "../../Material.hlsl"
            #include "ShaderPass/LitDepthPass.hlsl"
            #include "../../ShaderPasses/ShaderPassDepthOnly.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{ "LightMode" = "DepthOnly" }
            
            Stencil
            {
                //WriteMask[_StencilWriteMask]
                ReadMask 64
                Ref 64//[_StencilRef]
                Comp Equal
                Pass Keep
            }

            ZWrite On
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON 
            #pragma shader_feature _ALPHAPREMULTIPLY_ON

			#pragma multi_compile_fragment __ LOD_FADE_CROSSFADE
            #define SHADERPASS SHADERPASS_DEPTH_ONLY
			#include "../../../ShaderVariables.hlsl"
			#include "../../WindData/WindData.hlsl"
            #include "../../Material.hlsl"
            #include "ShaderPass/LitDepthPass.hlsl"
            #include "../../ShaderPasses/ShaderPassDepthOnly.hlsl"
            ENDHLSL
	    }

            
        Pass
        {
            Name "DepthColor"
            Tags{ "LightMode" = "DepthColor" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON 
            #pragma shader_feature _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #define SHADERPASS SHADERPASS_DEPTH_ONLY
			#include "../../../ShaderVariables.hlsl"
			#include "../../WindData/WindData.hlsl"
            #include "../../Material.hlsl"
            #include "ShaderPass/LitDepthPass.hlsl"
            #include "../../ShaderPasses/ShaderPassDepthColor.hlsl"
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
			#pragma shader_feature _ALPHATEST_ON 
			#pragma shader_feature _ALPHAPREMULTIPLY_ON
			#pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #define SHADERPASS SHADERPASS_VELOCITY
			#include "../../../ShaderVariables.hlsl"
			#include "../../WindData/WindData.hlsl"
            #include "../../Material.hlsl"
            #include "ShaderPass/LitVelocityPass.hlsl"
            #include "../../ShaderPasses/ShaderPassVelocity.hlsl"

            ENDHLSL

        }
            

        Pass
        {
            Name "META"
            Tags{ "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            
            #pragma shader_feature _EMISSION
            #define SHADERPASS SHADERPASS_LIGHT_TRANSPORT
			#include "../../../ShaderVariables.hlsl"
			#include "../../WindData/WindData.hlsl"
            #include "../../Material.hlsl"
            #include "ShaderPass/LitSharePass.hlsl"
            #include "../Lighting/LightingUtils.hlsl"
            #include "../../ShaderPasses/ShaderPassLightTransport.hlsl"

            ENDHLSL
        }
    }

    //FallBack "Hidden/InternalErrorShader"
    CustomEditor "Experimental.Rendering.ClusterPipeline.RenderGraphLitShaderGUI"
}
