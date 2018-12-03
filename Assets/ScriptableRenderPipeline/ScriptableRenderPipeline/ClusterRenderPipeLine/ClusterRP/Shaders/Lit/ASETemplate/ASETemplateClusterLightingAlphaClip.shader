Shader /*ase_name*/ "ASETemplateShaders/ClusterLightingAlphaClip" /*end*/
{
	Properties
	{
        /*ase_props*/

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [HideInInspector][ToggleOff] _DoubleSidedEnable("Double sided enable", Float) = 0.0
        [HideInInspector][Enum(None, 0, Mirror, 1, Flip, 2)] _DoubleSidedNormalMode("Double sided normal mode", Float) = 1
        [HideInInspector] _DoubleSidedConstants("_DoubleSidedConstants", Vector) = (1, 1, -1, 0)

        // Blending state
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
	}

    HLSLINCLUDE

    #pragma target 3.0
    //#pragma only_renderers d3d11 ps4 xboxone vulkan metal
    //enable GPU instancing support
    #pragma multi_compile_instancing

    //-------------------------------------------------------------------------------------
    // Include
    //-------------------------------------------------------------------------------------

    #include "CoreRP/ShaderLibrary/Common.hlsl"
    #include "CoreRP/ShaderLibrary/Wind.hlsl"
    #include "ClusterRP/Shaders/ShaderPasses/FragInputs.hlsl"
    #include "ClusterRP/Shaders/ShaderPasses/ShaderPass.cs.hlsl"

    //-------------------------------------------------------------------------------------
    // variable declaration
    //-------------------------------------------------------------------------------------

    #define USE_LEGACY_UNITY_MATRIX_VARIABLES
    #define USE_LEGACY_UNITY_FUNCTIONS
    #define UNITY_MATRIX_I_M   unity_WorldToObject
    float4 _ScreenToTargetScale;
    float4 _ScreenSize;
    uniform float _Cutoff = 0.5;

    //#include "ClusterRP/ShaderVariables.hlsl"
    //#include "ClusterRP/Shaders/Material.hlsl"

    ENDHLSL

        SubShader
    {
        Tags{ "RenderPipeline" = "ClusterRenderPipeline" "RenderType" = "AlphaTest" }
        LOD 100

        /*ase_pass*/
        Pass
        {
            Name "ClusterForward"
            Tags{ "LightMode" = "ClusterForward" }        

            ZTest LEqual
            Blend Off
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityInstancing.cginc"
            #include "ClusterRP/ShaderVariablesFunctions.hlsl"
            #include "ClusterRP/Shaders/ShaderPasses/FragData.hlsl"
            #include "ClusterRP/Shaders/Lit/Lighting/ClusterUtils.cginc"
            #include "ClusterRP/Shaders/Lit/ASETemplate/LightingASETemplate.hlsl"
            #include "ClusterRP/Shaders/Lit/Lighting/ClusterLighting.hlsl"
			/*ase_pragma*/
            #pragma shader_feature _DOUBLESIDED_ON
            #pragma multi_compile __ _VOLUME_FOG_ON
            #pragma multi_compile __ SCREEN_SHADOW
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #define _EMISSION

			struct appdata
			{
				float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                half4 color : COLOR;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
                float2 lightmapUV : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
				/*ase_vdata:p=p;uv0=tc0.xy;uv1=tc1.xy;n=n;t=t;c=c*/
			};
			
			struct v2f
			{
				float4 vertex : SV_POSITION;
                half4 color : COLOR;
				float4 texcoord : TEXCOORD0;
                float4 lightmapUVOrVertexSH : TEXCOORD1; // holds either lightmapUV or vertex SH. depending on LIGHTMAP_ON
                float3 posWS : TEXCOORD2;
                half3 normal : TEXCOORD3;
                half3 tangent                   : TEXCOORD4;
                half3 binormal                  : TEXCOORD5;
                half3 viewDir : TEXCOORD6;
                #ifdef SCREEN_SHADOW
                float4 screenPos : TEXCOORD7;
                #endif
                float4 clusterPos : TEXCOORD8;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
				/*ase_interp(9,12):sp=sp.xyzw;uv0=tc0;wp=tc2;wn=tc3;wt=tc4;wvd=tc6;c=c*/
			};


			/*ase_globals*/
			
			v2f vert ( appdata v /*ase_vert_input*/)
			{
				v2f o = (v2f)0;
				o.texcoord.xy = v.texcoord.xy;
				o.texcoord.zw = v.texcoord1.xy;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				// ase common template code
				/*ase_vert_code:v=appdata;o=v2f*/
				
				v.vertex.xyz += /*ase_vert_out:Local Vertex;Float3*/ float3(0,0,0) /*end*/;
				//o.vertex = UnityObjectToClipPos(v.vertex);

                o.posWS.xyz = TransformObjectToWorld(v.vertex.xyz);
                o.vertex = TransformWorldToHClip(o.posWS.xyz);
                OutputTangentToWorld(v.tangent, v.normal, o.tangent.xyz, o.binormal.xyz, o.normal.xyz);
                OUTPUT_LIGHTMAP_UV(v.lightmapUV, unity_LightmapST, o.lightmapUVOrVertexSH);
                OUTPUT_SH(o.normal.xyz, o.lightmapUVOrVertexSH);
                o.viewDir = _WorldSpaceCameraPos - o.posWS.xyz;
                o.color = v.color;

                o.clusterPos = ComputeClusterPos(float4(o.posWS.xyz, 1.0f), Cluster_Matrix_LinearZ); //ComputeNonStereoScreenPos(o.pos);
#ifdef SCREEN_SHADOW
                o.screenPos = o.vertex * 0.5f;
                o.screenPos.xy = float2(o.screenPos.x, o.screenPos.y * _ProjectionParams.x) + o.screenPos.w;
                o.screenPos.zw = o.vertex.zw;
#endif
				return o;
			}
			
			half4 frag (v2f i /*ase_frag_input*/) : SV_Target
			{

                BRDFData_Direct brdfDataDirect;
                BRDFData_Indirect brdfDataIndirect;

                SurfaceData surfData = InitializedSurfaceData();
				i.viewDir = normalize(i.viewDir);
				// ase common template code
				/*ase_frag_code:i=v2f*/

                surfData.albedo = /*ase_frag_out:Albedo;Float3*/half3(1, 0, 0)/*end*/;
                surfData.metallic = /*ase_frag_out:Metallic;Float*/0/*end*/;
                surfData.smoothness = /*ase_frag_out:Smoothness;Float*/0/*end*/;
                surfData.normalTS = /*ase_frag_out:Normal;Float3*/half3(0, 0, 1)/*end*/;
                surfData.emission = /*ase_frag_out:Emission;Float3*/half3(0, 0, 0)/*end*/;
                surfData.occlusion = /*ase_frag_out:Occlusion;Float*/1/*end*/;
                surfData.alpha =  /*ase_frag_out:Alpha;Float*/1/*end*/;
                surfData.specular = lerp(unity_ColorSpaceDielectricSpec.rgb, surfData.albedo, surfData.metallic);

                clip(surfData.alpha - _Cutoff);

                GetBRDFDataFromSurfaceData(surfData, brdfDataDirect, brdfDataIndirect);

                half4 color = half4(0, 0, 0, brdfDataDirect.alpha);
#ifdef _EMISSION
                color.rgb += brdfDataIndirect.emission;
#endif

                InputDataSimple inputData;
                INIT_INPUT_DATA(i, surfData.normalTS, inputData);

                brdfDataIndirect.bakedGI = SampleGI(i.lightmapUVOrVertexSH, inputData.normalWS);
                color.rgb += GlobalIllumination(brdfDataIndirect, brdfDataDirect, inputData.normalWS, inputData.viewDirectionWS);

                int sliceIndex = 0;
#ifdef UNITY_STEREO_INSTANCING_ENABLED
                sliceIndex = i.stereoTargetEyeIndex;
#endif

#ifdef SCREEN_SHADOW
                int2 screenCoord = (i.screenPos.xy / i.screenPos.w) * _ScreenSize;
                color.rgb += SURFACE_LIGHTING_CLUSTER_DIRECT_SCREENSHADOW(brdfDataDirect, int3(screenCoord.xy, sliceIndex), inputData.normalWS, inputData.viewDirectionWS, inputData.positionWS, inputData.clusterId);
#else
                color.rgb += SURFACE_LIGHTING_CLUSTER_DIRECT(brdfDataDirect, inputData.normalWS, inputData.viewDirectionWS, inputData.positionWS, inputData.clusterId);
#endif
                return color;
			}
			ENDHLSL
		}

		Pass
		{
            Name "DepthOnly"
            Tags{ "LightMode" = "DepthOnly" }

            ZTest LEqual
            Blend Off
            ZWrite On
            Cull Back

            HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
            
            #include "UnityCG.cginc"
            #include "UnityInstancing.cginc"
            #include "ClusterRP/ShaderVariablesFunctions.hlsl"
            #include "ClusterRP/Shaders/ShaderPasses/FragData.hlsl"
			/*ase_pragma*/   

			struct appdata
			{
				float4 vertex : POSITION;
				float4 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
				/*ase_vdata:p=p;uv0=tc0;*/
			};
			
			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
				/*ase_interp(9,12):sp=sp.xyzw;uv0=tc0;*/
			};


			/*ase_globals*/
			
			v2f vert ( appdata v /*ase_vert_input*/)
			{
				v2f o = (v2f)0;
				o.texcoord.xy = v.texcoord.xy;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				// ase common template code
				/*ase_vert_code:v=appdata;o=v2f*/
				
				v.vertex.xyz += /*ase_vert_out:Local Vertex;Float3*/ float3(0,0,0) /*end*/;
				//o.vertex = UnityObjectToClipPos(v.vertex);

                float3 posWS = TransformObjectToWorld(v.vertex.xyz);
                o.vertex = TransformWorldToHClip(posWS);


				return o;
			}
			
			half4 frag (v2f i /*ase_frag_input*/) : SV_Target
			{
				// ase common template code
				/*ase_frag_code:i=v2f*/

                UNITY_SETUP_INSTANCE_ID(i);

                half alpha = /*ase_frag_out:Alpha;Float*/1/*end*/;

                clip(alpha - _Cutoff);
                // TODO: handle cubemap shadow
                return half4(0.0, 0.0, 0.0, 0.0);
			}
			ENDHLSL
		}

		Pass
		{
            Name "ShadowCaster"
            Tags{ "LightMode" = "ShadowCaster" }                

            ZTest LEqual
            Blend Off
            ZWrite On
            Cull Back

            HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
            
            #include "UnityCG.cginc"
            #include "UnityInstancing.cginc"
            #include "ClusterRP/ShaderVariablesFunctions.hlsl"
            #include "ClusterRP/Shaders/ShaderPasses/FragData.hlsl"
			/*ase_pragma*/   

			struct appdata
			{
				float4 vertex : POSITION;
                float3 normal : NORMAL;
				float4 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
				/*ase_vdata:p=p;uv0=tc0;n=n;*/
			};
			
			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 texcoord : TEXCOORD0;
                half3 normal : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
				/*ase_interp(9,12):sp=sp.xyzw;uv0=tc0;*/
			};


			/*ase_globals*/
            float4 _ShadowBias;
            float3 _LightDirection;
			
			v2f vert ( appdata v /*ase_vert_input*/)
			{
				v2f o = (v2f)0;
				o.texcoord.xy = v.texcoord.xy;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				// ase common template code
				/*ase_vert_code:v=appdata;o=v2f*/
				
				v.vertex.xyz += /*ase_vert_out:Local Vertex;Float3*/ float3(0,0,0) /*end*/;
				//o.vertex = UnityObjectToClipPos(v.vertex);

                float3 posWS = TransformObjectToWorld(v.vertex.xyz);
                o.normal = TransformObjectToWorldNormal(v.normal);
                float invNdotL = 1.0 - saturate(dot(_LightDirection, o.normal));
                float scale = invNdotL * _ShadowBias.y;
                // normal bias is negative since we want to apply an inset normal offset
                posWS += o.normal * scale.xxx;

                o.vertex = TransformWorldToHClip(posWS);
                // _ShadowBias.x sign depens on if platform has reversed z buffer
                o.vertex.z += _ShadowBias.x;

#if UNITY_REVERSED_Z
                o.vertex.z = min(o.vertex.z, o.vertex.w * UNITY_NEAR_CLIP_VALUE);
#else
                o.vertex.z = max(o.vertex.z, o.vertex.w * UNITY_NEAR_CLIP_VALUE);
#endif

				return o;
			}
			
			half4 frag (v2f i /*ase_frag_input*/) : SV_Target
			{
				// ase common template code
				/*ase_frag_code:i=v2f*/

                UNITY_SETUP_INSTANCE_ID(i);

                half alpha = /*ase_frag_out:Alpha;Float*/1/*end*/;

                clip(alpha - _Cutoff);
                // TODO: handle cubemap shadow
                return half4(0.0, 0.0, 0.0, 0.0);
			}
			ENDHLSL
		}
	}

    CustomEditor "ASEMaterialInspectorClusterRP"
}
