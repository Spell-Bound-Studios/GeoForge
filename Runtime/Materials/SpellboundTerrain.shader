Shader "Spellbound/SpellboundTerrain"
{
    Properties
    {
        _Blend("Blend", Float) = 8
        _Tiling("Tiling", Float) = 0.05
        [NoScaleOffset]_TerrainAlbedoArray("TerrainAlbedoArray", 2DArray) = "" {}
        [NoScaleOffset]_TerrainMetalSmoothArray("TerrainMetalSmoothArray", 2DArray) = "" {}
        [NoScaleOffset]_TerrainNormalArray("TerrainNormalArray", 2DArray) = "" {}
        _Normal_Power("Normal Power", Float) = 0
        [NoScaleOffset]_MappingTable("MappingTable", 2DArray) = "" {}
        [NoScaleOffset]_Fallbacks("Fallbacks", 2DArray) = "" {}
        [NoScaleOffset]_AltAlbedoArray("AltAlbedoArray", 2DArray) = "" {}
        [NoScaleOffset]_AltMASArray("AltMASArray", 2DArray) = "" {}
        [NoScaleOffset]_AltNormalArray("AltNormalArray", 2DArray) = "" {}
        _StepLowEdge("StepLowEdge", Float) = 0.8
        _StepHighEdge("StepHighEdge", Float) = 0.95
        [HideInInspector]_QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "UniversalMaterialType" = "Lit"
            "Queue"="Geometry"
            "DisableBatching"="False"
            "ShaderGraphShader"="true"
            "ShaderGraphTargetId"="UniversalLitSubTarget"
        }
        Pass
        {
            Name "Universal Forward"
            Tags
            {
                "LightMode" = "UniversalForward"
            }
        
        // Render State
        Cull Back
        Blend One Zero
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma multi_compile_instancing
        #pragma multi_compile_fog
        #pragma instancing_options renderinglayer
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
        #pragma multi_compile _ LIGHTMAP_ON
        #pragma multi_compile _ DYNAMICLIGHTMAP_ON
        #pragma multi_compile _ DIRLIGHTMAP_COMBINED
        #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
        #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
        #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
        #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
        #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
        #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
        #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
        #pragma multi_compile _ SHADOWS_SHADOWMASK
        #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
        #pragma multi_compile_fragment _ _LIGHT_LAYERS
        #pragma multi_compile_fragment _ DEBUG_DISPLAY
        #pragma multi_compile_fragment _ _LIGHT_COOKIES
        #pragma multi_compile _ _FORWARD_PLUS
        #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define ATTRIBUTES_NEED_TEXCOORD2
        #define ATTRIBUTES_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TANGENT_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_COLOR
        #define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        #define VARYINGS_NEED_SHADOW_COORD
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_FORWARD
        #define _FOG_FRAGMENT 1
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
             float4 uv2 : TEXCOORD2;
             float4 color : COLOR;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 tangentWS;
             float4 texCoord0;
             nointerpolation float4 color;
            #if defined(LIGHTMAP_ON)
             float2 staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
             float2 dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh;
            #endif
            #if defined(USE_APV_PROBE_OCCLUSION)
             float4 probeOcclusion;
            #endif
             float4 fogFactorAndVertexLight;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
             float4 shadowCoord;
            #endif
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpaceNormal;
             float3 TangentSpaceNormal;
             float3 WorldSpacePosition;
             float4 uv0;
             float4 VertexColor;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if defined(LIGHTMAP_ON)
             float2 staticLightmapUV : INTERP0;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
             float2 dynamicLightmapUV : INTERP1;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh : INTERP2;
            #endif
            #if defined(USE_APV_PROBE_OCCLUSION)
             float4 probeOcclusion : INTERP3;
            #endif
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
             float4 shadowCoord : INTERP4;
            #endif
             float4 tangentWS : INTERP5;
             float4 texCoord0 : INTERP6;
             nointerpolation float4 color : INTERP7;
             float4 fogFactorAndVertexLight : INTERP8;
             float3 positionWS : INTERP9;
             float3 normalWS : INTERP10;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.staticLightmapUV = input.staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.dynamicLightmapUV = input.dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            #if defined(USE_APV_PROBE_OCCLUSION)
            output.probeOcclusion = input.probeOcclusion;
            #endif
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            output.shadowCoord = input.shadowCoord;
            #endif
            output.tangentWS.xyzw = input.tangentWS;
            output.texCoord0.xyzw = input.texCoord0;
            output.color.xyzw = input.color;
            output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.staticLightmapUV = input.staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.dynamicLightmapUV = input.dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            #if defined(USE_APV_PROBE_OCCLUSION)
            output.probeOcclusion = input.probeOcclusion;
            #endif
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            output.shadowCoord = input.shadowCoord;
            #endif
            output.tangentWS = input.tangentWS.xyzw;
            output.texCoord0 = input.texCoord0.xyzw;
            output.color = input.color.xyzw;
            output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float _Blend;
        float _Tiling;
        float4x4 _WorldToLocal;
        float _Normal_Power;
        float _StepLowEdge;
        float _StepHighEdge;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainNormalArray);
        SAMPLER(sampler_TerrainNormalArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        TEXTURE2D_ARRAY(_MappingTable);
        SAMPLER(sampler_MappingTable);
        TEXTURE2D_ARRAY(_Fallbacks);
        SAMPLER(sampler_Fallbacks);
        TEXTURE2D_ARRAY(_AltAlbedoArray);
        SAMPLER(sampler_AltAlbedoArray);
        TEXTURE2D_ARRAY(_AltMASArray);
        SAMPLER(sampler_AltMASArray);
        TEXTURE2D_ARRAY(_AltNormalArray);
        SAMPLER(sampler_AltNormalArray);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
        {
            Out = A >= B ? 1 : 0;
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        void Unity_Absolute_float(float In, out float Out)
        {
            Out = abs(In);
        }
        
        void Unity_Comparison_LessOrEqual_float(float A, float B, out float Out)
        {
            Out = A <= B ? 1 : 0;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Round_float(float In, out float Out)
        {
            Out = round(In);
        }
        
        void Unity_Branch_float(float Predicate, float True, float False, out float Out)
        {
            Out = Predicate ? True : False;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
        }
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Comparison_Equal_float(float A, float B, out float Out)
        {
            Out = A == B ? 1 : 0;
        }
        
        // unity-custom-func-begin
        void TransformPositionToVolumeSpace_float(float3 worldPos, float4x4 worldToLocal, out float3 volumeLocalPos){
            volumeLocalPos = mul(worldToLocal, float4(worldPos, 1.0)).xyz;
        }
        // unity-custom-func-end
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        // unity-custom-func-begin
        void TransformNormal_float(float3 worldNormal, float4x4 worldToLocal, out float3 volumeLocalNormal){
            volumeLocalNormal = mul((float3x3)worldToLocal, worldNormal);
            volumeLocalNormal = normalize(volumeLocalNormal);
        }
        // unity-custom-func-end
        
        void Unity_Absolute_float3(float3 In, out float3 Out)
        {
            Out = abs(In);
        }
        
        void Unity_Power_float3(float3 A, float3 B, out float3 Out)
        {
            Out = pow(A, B);
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
        {
            Out = dot(A, B);
        }
        
        void Unity_Divide_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A / B;
        }
        
        void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
        {
            Out = lerp(A, B, T);
        }
        
        void Unity_Branch_float4(float Predicate, float4 True, float4 False, out float4 Out)
        {
            Out = Predicate ? True : False;
        }
        
        void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
        {
            Out = smoothstep(Edge1, Edge2, In);
        }
        
        void Unity_Subtract_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A - B;
        }
        
        void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A + B;
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float3 NormalTS;
            float3 Emission;
            float Metallic;
            float Smoothness;
            float Occlusion;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float = IN.VertexColor[0];
            float _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float = IN.VertexColor[1];
            float _Split_a8d1957c8fd4453686400eb31d654258_B_3_Float = IN.VertexColor[2];
            float _Split_a8d1957c8fd4453686400eb31d654258_A_4_Float = IN.VertexColor[3];
            float _Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean;
            Unity_Comparison_GreaterOrEqual_float(_Split_a8d1957c8fd4453686400eb31d654258_B_3_Float, float(1), _Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean);
            UnityTexture2DArray _Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_MappingTable);
            float4 _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4 = IN.uv0;
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[0];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_G_2_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[1];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_B_3_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[2];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_A_4_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[3];
            float _Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float;
            Unity_Subtract_float(_Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float, _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float, _Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float);
            float _Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float;
            Unity_Absolute_float(_Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float, _Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float);
            float _Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float;
            Unity_Subtract_float(_Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float, _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float, _Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float);
            float _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float;
            Unity_Absolute_float(_Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float, _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float);
            float _Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean;
            Unity_Comparison_LessOrEqual_float(_Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float, _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float, _Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean);
            float _Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float;
            Unity_Multiply_float_float(_Split_a8d1957c8fd4453686400eb31d654258_R_1_Float, 255, _Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float);
            float _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float;
            Unity_Round_float(_Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float, _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float);
            float _Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float;
            Unity_Multiply_float_float(_Split_a8d1957c8fd4453686400eb31d654258_G_2_Float, 255, _Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float);
            float _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float;
            Unity_Round_float(_Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float, _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float);
            float _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float;
            Unity_Branch_float(_Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean, _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float, _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float);
            float _Add_cb7536069f014983b789b899b046cdd1_Out_2_Float;
            Unity_Add_float(_Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float, float(0.5), _Add_cb7536069f014983b789b899b046cdd1_Out_2_Float);
            float _Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float;
            Unity_Divide_float(_Add_cb7536069f014983b789b899b046cdd1_Out_2_Float, float(256), _Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float);
            float4 _Combine_151f632a12c04805a28fcc5e175b3cbc_RGBA_4_Vector4;
            float3 _Combine_151f632a12c04805a28fcc5e175b3cbc_RGB_5_Vector3;
            float2 _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2;
            Unity_Combine_float(_Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float, float(0), float(0), float(0), _Combine_151f632a12c04805a28fcc5e175b3cbc_RGBA_4_Vector4, _Combine_151f632a12c04805a28fcc5e175b3cbc_RGB_5_Vector3, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2);
            float4 _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray.tex, _Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(0) );
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_R_4_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_G_5_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_B_6_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_A_7_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.a;
            float _Multiply_c3c15e8bfe914b07b98b2c48b02e5770_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_R_4_Float, 255, _Multiply_c3c15e8bfe914b07b98b2c48b02e5770_Out_2_Float);
            float _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float;
            Unity_Round_float(_Multiply_c3c15e8bfe914b07b98b2c48b02e5770_Out_2_Float, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float);
            float _Comparison_1fdd40ec77c94b85b238b762d9ed7cdf_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float, float(255), _Comparison_1fdd40ec77c94b85b238b762d9ed7cdf_Out_2_Boolean);
            UnityTexture2DArray _Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_Fallbacks);
            float4 _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray.tex, _Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(0) );
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_R_4_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_G_5_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_B_6_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_A_7_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.a;
            UnityTexture2DArray _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainAlbedoArray);
            float4x4 _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4 = _WorldToLocal;
            float3 _TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3;
            TransformPositionToVolumeSpace_float(IN.WorldSpacePosition, _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4, _TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3);
            float _Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float = _Tiling;
            float3 _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3;
            Unity_Multiply_float3_float3(_TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3, (_Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float.xxx), _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3);
            float2 _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xz;
            float4 _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_R_4_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_G_5_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_B_6_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_A_7_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.a;
            float2 _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.yz;
            float4 _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_R_4_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_G_5_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_B_6_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_A_7_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.a;
            float3 _TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3;
            TransformNormal_float(IN.WorldSpaceNormal, _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4, _TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3);
            float3 _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3;
            Unity_Absolute_float3(_TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3, _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3);
            float _Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float = _Blend;
            float3 _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3;
            Unity_Power_float3(_Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3, (_Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float.xxx), _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3);
            float3 _Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3;
            Unity_Add_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(0.001, 0.001, 0.001), _Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3);
            float _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float;
            Unity_DotProduct_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(1, 1, 1), _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float);
            float3 _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3;
            Unity_Divide_float3(_Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3, (_DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float.xxx), _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3);
            float _Split_3690e7172951494d811295287d62f6a9_R_1_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[0];
            float _Split_3690e7172951494d811295287d62f6a9_G_2_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[1];
            float _Split_3690e7172951494d811295287d62f6a9_B_3_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[2];
            float _Split_3690e7172951494d811295287d62f6a9_A_4_Float = 0;
            float4 _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4, _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4);
            float2 _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xy;
            float4 _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_R_4_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_G_5_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_B_6_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_A_7_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.a;
            float4 _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4, _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4);
            float4 _Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_1fdd40ec77c94b85b238b762d9ed7cdf_Out_2_Boolean, _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4, _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4, _Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4);
            UnityTexture2DArray _Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_MappingTable);
            float4 _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray.tex, _Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(1) );
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_R_4_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_G_5_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_B_6_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_A_7_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.a;
            float _Multiply_aed0d73afb104603b9218f3e986c52b1_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_R_4_Float, 255, _Multiply_aed0d73afb104603b9218f3e986c52b1_Out_2_Float);
            float _Round_7bf7643be96e45e7bc63501c211b5484_Out_1_Float;
            Unity_Round_float(_Multiply_aed0d73afb104603b9218f3e986c52b1_Out_2_Float, _Round_7bf7643be96e45e7bc63501c211b5484_Out_1_Float);
            float _Comparison_2dadcda8a25d4983b878c03ec3cfcc74_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_7bf7643be96e45e7bc63501c211b5484_Out_1_Float, float(255), _Comparison_2dadcda8a25d4983b878c03ec3cfcc74_Out_2_Boolean);
            UnityTexture2DArray _Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_Fallbacks);
            float4 _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray.tex, _Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(2) );
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_R_4_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_G_5_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_B_6_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_A_7_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.a;
            UnityTexture2DArray _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_AltAlbedoArray);
            float4 _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.tex, _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_R_4_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_G_5_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_B_6_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_A_7_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.tex, _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_R_4_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_G_5_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_B_6_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_A_7_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.a;
            float4 _Lerp_0e74d01d58954dd3a62d1766879888ef_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4, _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_0e74d01d58954dd3a62d1766879888ef_Out_3_Vector4);
            float4 _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.tex, _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_R_4_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_G_5_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_B_6_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_A_7_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.a;
            float4 _Lerp_d8444c08f4c24be981674285302358d0_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_0e74d01d58954dd3a62d1766879888ef_Out_3_Vector4, _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_d8444c08f4c24be981674285302358d0_Out_3_Vector4);
            float4 _Branch_ffd13beb31a34cdba80d0601903f0c4b_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_2dadcda8a25d4983b878c03ec3cfcc74_Out_2_Boolean, _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4, _Lerp_d8444c08f4c24be981674285302358d0_Out_3_Vector4, _Branch_ffd13beb31a34cdba80d0601903f0c4b_Out_3_Vector4);
            float _Property_75980a93ffd2444fb44695ea95d01dd1_Out_0_Float = _StepLowEdge;
            float _Property_0f28291fbab94789b01ad35d1f7e6da3_Out_0_Float = _StepHighEdge;
            float _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float;
            Unity_DotProduct_float3(IN.WorldSpaceNormal, float3(0, 1, 0), _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float);
            float _Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float;
            Unity_Smoothstep_float(_Property_75980a93ffd2444fb44695ea95d01dd1_Out_0_Float, _Property_0f28291fbab94789b01ad35d1f7e6da3_Out_0_Float, _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float, _Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float);
            float4 _Lerp_ebe93a7ee03a42998c196045b35050a6_Out_3_Vector4;
            Unity_Lerp_float4(_Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4, _Branch_ffd13beb31a34cdba80d0601903f0c4b_Out_3_Vector4, (_Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float.xxxx), _Lerp_ebe93a7ee03a42998c196045b35050a6_Out_3_Vector4);
            float4 _Branch_946f2ac7a40f4a4cacf6ba6bded44189_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean, _Lerp_ebe93a7ee03a42998c196045b35050a6_Out_3_Vector4, _Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4, _Branch_946f2ac7a40f4a4cacf6ba6bded44189_Out_3_Vector4);
            float _Multiply_29fcd482b27a499db64a6140d829c02c_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_B_6_Float, 255, _Multiply_29fcd482b27a499db64a6140d829c02c_Out_2_Float);
            float _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float;
            Unity_Round_float(_Multiply_29fcd482b27a499db64a6140d829c02c_Out_2_Float, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float);
            float _Comparison_595e2dca3b0d46b3b5d3264c115e1139_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float, float(255), _Comparison_595e2dca3b0d46b3b5d3264c115e1139_Out_2_Boolean);
            UnityTexture2DArray _Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainNormalArray);
            float4 _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.tex, _Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_R_4_Float = _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_G_5_Float = _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_B_6_Float = _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_A_7_Float = _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.tex, _Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_R_4_Float = _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_G_5_Float = _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_B_6_Float = _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_A_7_Float = _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4.a;
            float4 _Lerp_2f0293325fd2459ab54488be09edd1b1_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4, _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_2f0293325fd2459ab54488be09edd1b1_Out_3_Vector4);
            float4 _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.tex, _Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_R_4_Float = _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_G_5_Float = _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_B_6_Float = _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_A_7_Float = _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4.a;
            float4 _Lerp_6a7cbbf9d3de4a1fbb98d582330d8efa_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_2f0293325fd2459ab54488be09edd1b1_Out_3_Vector4, _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_6a7cbbf9d3de4a1fbb98d582330d8efa_Out_3_Vector4);
            float4 _Subtract_4fd39b463ead4d7fac0468b13e81ddd8_Out_2_Vector4;
            Unity_Subtract_float4(_Lerp_6a7cbbf9d3de4a1fbb98d582330d8efa_Out_3_Vector4, float4(0.5, 0.5, 0.5, 0.5), _Subtract_4fd39b463ead4d7fac0468b13e81ddd8_Out_2_Vector4);
            float _Property_b17539c59ddd4a87afbe6c608633be29_Out_0_Float = _Normal_Power;
            float4 _Multiply_1515bdb9bc774abfb0456db98a0243d2_Out_2_Vector4;
            Unity_Multiply_float4_float4(_Subtract_4fd39b463ead4d7fac0468b13e81ddd8_Out_2_Vector4, (_Property_b17539c59ddd4a87afbe6c608633be29_Out_0_Float.xxxx), _Multiply_1515bdb9bc774abfb0456db98a0243d2_Out_2_Vector4);
            float4 _Add_03cb5a02416e4166a52ae531c503c743_Out_2_Vector4;
            Unity_Add_float4(_Multiply_1515bdb9bc774abfb0456db98a0243d2_Out_2_Vector4, float4(0.5, 0.5, 0.5, 0.5), _Add_03cb5a02416e4166a52ae531c503c743_Out_2_Vector4);
            float4 _Branch_1e0b2c2cf59348308c46fdad1a9fec27_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_595e2dca3b0d46b3b5d3264c115e1139_Out_2_Boolean, float4(0.5, 0.5, 1, 1), _Add_03cb5a02416e4166a52ae531c503c743_Out_2_Vector4, _Branch_1e0b2c2cf59348308c46fdad1a9fec27_Out_3_Vector4);
            float _Multiply_f3c8028f13af41e0b44e52c24c65a76f_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_B_6_Float, 255, _Multiply_f3c8028f13af41e0b44e52c24c65a76f_Out_2_Float);
            float _Round_a1f5cd81fab64904bbc59f02b1c27564_Out_1_Float;
            Unity_Round_float(_Multiply_f3c8028f13af41e0b44e52c24c65a76f_Out_2_Float, _Round_a1f5cd81fab64904bbc59f02b1c27564_Out_1_Float);
            float _Comparison_1b9e8a0d006644bba678fe0a1896b29c_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_a1f5cd81fab64904bbc59f02b1c27564_Out_1_Float, float(255), _Comparison_1b9e8a0d006644bba678fe0a1896b29c_Out_2_Boolean);
            UnityTexture2DArray _Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_AltNormalArray);
            float4 _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.tex, _Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_R_4_Float = _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_G_5_Float = _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_B_6_Float = _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_A_7_Float = _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.tex, _Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_R_4_Float = _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_G_5_Float = _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_B_6_Float = _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_A_7_Float = _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4.a;
            float4 _Lerp_7ff9104dbca24fa2b3a06984365a777e_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4, _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_7ff9104dbca24fa2b3a06984365a777e_Out_3_Vector4);
            float4 _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.tex, _Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_R_4_Float = _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_G_5_Float = _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_B_6_Float = _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_A_7_Float = _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4.a;
            float4 _Lerp_9391453b43c747319ee2578915fea73f_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_7ff9104dbca24fa2b3a06984365a777e_Out_3_Vector4, _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_9391453b43c747319ee2578915fea73f_Out_3_Vector4);
            float4 _Subtract_af00415070f64bd9bdd37e3dd7c94f72_Out_2_Vector4;
            Unity_Subtract_float4(_Lerp_9391453b43c747319ee2578915fea73f_Out_3_Vector4, float4(0.5, 0.5, 0.5, 0.5), _Subtract_af00415070f64bd9bdd37e3dd7c94f72_Out_2_Vector4);
            float _Property_a3c454331c734e23ad7372ed8035b4b3_Out_0_Float = _Normal_Power;
            float4 _Multiply_bbf575daae1d4ce19d6e1148ce34dd34_Out_2_Vector4;
            Unity_Multiply_float4_float4(_Subtract_af00415070f64bd9bdd37e3dd7c94f72_Out_2_Vector4, (_Property_a3c454331c734e23ad7372ed8035b4b3_Out_0_Float.xxxx), _Multiply_bbf575daae1d4ce19d6e1148ce34dd34_Out_2_Vector4);
            float4 _Add_63a7222bed214f5dab24dbeb940bef0e_Out_2_Vector4;
            Unity_Add_float4(_Multiply_bbf575daae1d4ce19d6e1148ce34dd34_Out_2_Vector4, float4(0.5, 0.5, 0.5, 0.5), _Add_63a7222bed214f5dab24dbeb940bef0e_Out_2_Vector4);
            float4 _Branch_bfe9bed6f9824470826084ee3e1bd76d_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_1b9e8a0d006644bba678fe0a1896b29c_Out_2_Boolean, float4(0.5, 0.5, 1, 1), _Add_63a7222bed214f5dab24dbeb940bef0e_Out_2_Vector4, _Branch_bfe9bed6f9824470826084ee3e1bd76d_Out_3_Vector4);
            float4 _Lerp_6adbf4fb510241f5b91e897d9f410dfc_Out_3_Vector4;
            Unity_Lerp_float4(_Branch_1e0b2c2cf59348308c46fdad1a9fec27_Out_3_Vector4, _Branch_bfe9bed6f9824470826084ee3e1bd76d_Out_3_Vector4, (_Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float.xxxx), _Lerp_6adbf4fb510241f5b91e897d9f410dfc_Out_3_Vector4);
            float4 _Branch_6383d865716d463a812e65ad2e73b7b6_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean, _Lerp_6adbf4fb510241f5b91e897d9f410dfc_Out_3_Vector4, _Branch_1e0b2c2cf59348308c46fdad1a9fec27_Out_3_Vector4, _Branch_6383d865716d463a812e65ad2e73b7b6_Out_3_Vector4);
            float _Multiply_acd3aaaaa9d94cc48ba0d8420648b053_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_G_5_Float, 255, _Multiply_acd3aaaaa9d94cc48ba0d8420648b053_Out_2_Float);
            float _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float;
            Unity_Round_float(_Multiply_acd3aaaaa9d94cc48ba0d8420648b053_Out_2_Float, _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float);
            float _Comparison_5248daeeba384db3a15e6e407d595575_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float, float(255), _Comparison_5248daeeba384db3a15e6e407d595575_Out_2_Boolean);
            float4 _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray.tex, _Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(1) );
            float _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_R_4_Float = _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_G_5_Float = _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_B_6_Float = _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_A_7_Float = _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_RGBA_0_Vector4.a;
            UnityTexture2DArray _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainMetalSmoothArray);
            float4 _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.tex, _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float );
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_R_4_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_G_5_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_B_6_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_A_7_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.tex, _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float );
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_R_4_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_G_5_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_B_6_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_A_7_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.a;
            float4 _Lerp_5e7471adc1004f0fbe7ba4b877c88637_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4, _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_5e7471adc1004f0fbe7ba4b877c88637_Out_3_Vector4);
            float4 _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.tex, _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float );
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_R_4_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_G_5_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_B_6_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_A_7_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.a;
            float4 _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_5e7471adc1004f0fbe7ba4b877c88637_Out_3_Vector4, _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4);
            float4 _Branch_ae82b004b98b4535a974be19633d7905_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_5248daeeba384db3a15e6e407d595575_Out_2_Boolean, _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_RGBA_0_Vector4, _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4, _Branch_ae82b004b98b4535a974be19633d7905_Out_3_Vector4);
            float _Multiply_48487faad46d49ed8d947ffe6ad6d691_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_G_5_Float, 255, _Multiply_48487faad46d49ed8d947ffe6ad6d691_Out_2_Float);
            float _Round_2c9d810d4f38426882c13718e40314ed_Out_1_Float;
            Unity_Round_float(_Multiply_48487faad46d49ed8d947ffe6ad6d691_Out_2_Float, _Round_2c9d810d4f38426882c13718e40314ed_Out_1_Float);
            float _Comparison_1f5ef5913e65410191e2147185fb9b13_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_2c9d810d4f38426882c13718e40314ed_Out_1_Float, float(255), _Comparison_1f5ef5913e65410191e2147185fb9b13_Out_2_Boolean);
            float4 _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray.tex, _Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(3) );
            float _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_R_4_Float = _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_G_5_Float = _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_B_6_Float = _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_A_7_Float = _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_RGBA_0_Vector4.a;
            UnityTexture2DArray _Property_8e7bdf8cec4a434a80265fbd13054132_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_AltMASArray);
            float4 _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8e7bdf8cec4a434a80265fbd13054132_Out_0_Texture2DArray.tex, _Property_8e7bdf8cec4a434a80265fbd13054132_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float );
            float _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_R_4_Float = _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_G_5_Float = _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_B_6_Float = _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_A_7_Float = _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8e7bdf8cec4a434a80265fbd13054132_Out_0_Texture2DArray.tex, _Property_8e7bdf8cec4a434a80265fbd13054132_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float );
            float _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_R_4_Float = _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_G_5_Float = _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_B_6_Float = _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_A_7_Float = _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_RGBA_0_Vector4.a;
            float4 _Lerp_7d472004cb904276a104bfc56ef29b21_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_RGBA_0_Vector4, _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_7d472004cb904276a104bfc56ef29b21_Out_3_Vector4);
            float4 _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8e7bdf8cec4a434a80265fbd13054132_Out_0_Texture2DArray.tex, _Property_8e7bdf8cec4a434a80265fbd13054132_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float );
            float _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_R_4_Float = _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_G_5_Float = _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_B_6_Float = _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_A_7_Float = _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_RGBA_0_Vector4.a;
            float4 _Lerp_caf034aa50fb476d995ee09759d34f02_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_7d472004cb904276a104bfc56ef29b21_Out_3_Vector4, _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_caf034aa50fb476d995ee09759d34f02_Out_3_Vector4);
            float4 _Branch_6956f9f8f46e49afac7bb6aa9923e0d0_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_1f5ef5913e65410191e2147185fb9b13_Out_2_Boolean, _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_RGBA_0_Vector4, _Lerp_caf034aa50fb476d995ee09759d34f02_Out_3_Vector4, _Branch_6956f9f8f46e49afac7bb6aa9923e0d0_Out_3_Vector4);
            float4 _Lerp_c8c2a9ce74d24754ae42eeb2a040fae2_Out_3_Vector4;
            Unity_Lerp_float4(_Branch_ae82b004b98b4535a974be19633d7905_Out_3_Vector4, _Branch_6956f9f8f46e49afac7bb6aa9923e0d0_Out_3_Vector4, (_Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float.xxxx), _Lerp_c8c2a9ce74d24754ae42eeb2a040fae2_Out_3_Vector4);
            float4 _Branch_df861d6373f64bbdba520d8214fc61ff_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean, _Lerp_c8c2a9ce74d24754ae42eeb2a040fae2_Out_3_Vector4, _Branch_ae82b004b98b4535a974be19633d7905_Out_3_Vector4, _Branch_df861d6373f64bbdba520d8214fc61ff_Out_3_Vector4);
            float _Split_3264dfc20eb84c93971370a089767e2c_R_1_Float = _Branch_df861d6373f64bbdba520d8214fc61ff_Out_3_Vector4[0];
            float _Split_3264dfc20eb84c93971370a089767e2c_G_2_Float = _Branch_df861d6373f64bbdba520d8214fc61ff_Out_3_Vector4[1];
            float _Split_3264dfc20eb84c93971370a089767e2c_B_3_Float = _Branch_df861d6373f64bbdba520d8214fc61ff_Out_3_Vector4[2];
            float _Split_3264dfc20eb84c93971370a089767e2c_A_4_Float = _Branch_df861d6373f64bbdba520d8214fc61ff_Out_3_Vector4[3];
            surface.BaseColor = (_Branch_946f2ac7a40f4a4cacf6ba6bded44189_Out_3_Vector4.xyz);
            surface.NormalTS = (_Branch_6383d865716d463a812e65ad2e73b7b6_Out_3_Vector4.xyz);
            surface.Emission = float3(0, 0, 0);
            surface.Metallic = _Split_3264dfc20eb84c93971370a089767e2c_R_1_Float;
            surface.Smoothness = _Split_3264dfc20eb84c93971370a089767e2c_B_3_Float;
            surface.Occlusion = _Split_3264dfc20eb84c93971370a089767e2c_G_2_Float;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
            // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
            float3 unnormalizedNormalWS = input.normalWS;
            const float renormFactor = 1.0 / length(unnormalizedNormalWS);
        
        
            output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
            output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
        
        
            output.WorldSpacePosition = input.positionWS;
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
            output.uv0 = input.texCoord0;
            output.VertexColor = input.color;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBRForwardPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "GBuffer"
            Tags
            {
                "LightMode" = "UniversalGBuffer"
            }
        
        // Render State
        Cull Back
        Blend One Zero
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 4.5
        #pragma exclude_renderers gles3 glcore
        #pragma multi_compile_instancing
        #pragma multi_compile_fog
        #pragma instancing_options renderinglayer
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma multi_compile _ LIGHTMAP_ON
        #pragma multi_compile _ DYNAMICLIGHTMAP_ON
        #pragma multi_compile _ DIRLIGHTMAP_COMBINED
        #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
        #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
        #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
        #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
        #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
        #pragma multi_compile _ SHADOWS_SHADOWMASK
        #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
        #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
        #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
        #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
        #pragma multi_compile_fragment _ DEBUG_DISPLAY
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define ATTRIBUTES_NEED_TEXCOORD2
        #define ATTRIBUTES_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TANGENT_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_COLOR
        #define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        #define VARYINGS_NEED_SHADOW_COORD
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_GBUFFER
        #define _FOG_FRAGMENT 1
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
             float4 uv2 : TEXCOORD2;
             float4 color : COLOR;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 tangentWS;
             float4 texCoord0;
             float4 color;
            #if defined(LIGHTMAP_ON)
             float2 staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
             float2 dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh;
            #endif
            #if defined(USE_APV_PROBE_OCCLUSION)
             float4 probeOcclusion;
            #endif
             float4 fogFactorAndVertexLight;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
             float4 shadowCoord;
            #endif
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpaceNormal;
             float3 TangentSpaceNormal;
             float3 WorldSpacePosition;
             float4 uv0;
             float4 VertexColor;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if defined(LIGHTMAP_ON)
             float2 staticLightmapUV : INTERP0;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
             float2 dynamicLightmapUV : INTERP1;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh : INTERP2;
            #endif
            #if defined(USE_APV_PROBE_OCCLUSION)
             float4 probeOcclusion : INTERP3;
            #endif
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
             float4 shadowCoord : INTERP4;
            #endif
             float4 tangentWS : INTERP5;
             float4 texCoord0 : INTERP6;
             float4 color : INTERP7;
             float4 fogFactorAndVertexLight : INTERP8;
             float3 positionWS : INTERP9;
             float3 normalWS : INTERP10;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.staticLightmapUV = input.staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.dynamicLightmapUV = input.dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            #if defined(USE_APV_PROBE_OCCLUSION)
            output.probeOcclusion = input.probeOcclusion;
            #endif
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            output.shadowCoord = input.shadowCoord;
            #endif
            output.tangentWS.xyzw = input.tangentWS;
            output.texCoord0.xyzw = input.texCoord0;
            output.color.xyzw = input.color;
            output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.staticLightmapUV = input.staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.dynamicLightmapUV = input.dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            #if defined(USE_APV_PROBE_OCCLUSION)
            output.probeOcclusion = input.probeOcclusion;
            #endif
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            output.shadowCoord = input.shadowCoord;
            #endif
            output.tangentWS = input.tangentWS.xyzw;
            output.texCoord0 = input.texCoord0.xyzw;
            output.color = input.color.xyzw;
            output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float _Blend;
        float _Tiling;
        float4x4 _WorldToLocal;
        float _Normal_Power;
        float _StepLowEdge;
        float _StepHighEdge;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainNormalArray);
        SAMPLER(sampler_TerrainNormalArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        TEXTURE2D_ARRAY(_MappingTable);
        SAMPLER(sampler_MappingTable);
        TEXTURE2D_ARRAY(_Fallbacks);
        SAMPLER(sampler_Fallbacks);
        TEXTURE2D_ARRAY(_AltAlbedoArray);
        SAMPLER(sampler_AltAlbedoArray);
        TEXTURE2D_ARRAY(_AltMASArray);
        SAMPLER(sampler_AltMASArray);
        TEXTURE2D_ARRAY(_AltNormalArray);
        SAMPLER(sampler_AltNormalArray);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
        {
            Out = A >= B ? 1 : 0;
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        void Unity_Absolute_float(float In, out float Out)
        {
            Out = abs(In);
        }
        
        void Unity_Comparison_LessOrEqual_float(float A, float B, out float Out)
        {
            Out = A <= B ? 1 : 0;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Round_float(float In, out float Out)
        {
            Out = round(In);
        }
        
        void Unity_Branch_float(float Predicate, float True, float False, out float Out)
        {
            Out = Predicate ? True : False;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
        }
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Comparison_Equal_float(float A, float B, out float Out)
        {
            Out = A == B ? 1 : 0;
        }
        
        // unity-custom-func-begin
        void TransformPositionToVolumeSpace_float(float3 worldPos, float4x4 worldToLocal, out float3 volumeLocalPos){
            volumeLocalPos = mul(worldToLocal, float4(worldPos, 1.0)).xyz;
        }
        // unity-custom-func-end
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        // unity-custom-func-begin
        void TransformNormal_float(float3 worldNormal, float4x4 worldToLocal, out float3 volumeLocalNormal){
            volumeLocalNormal = mul((float3x3)worldToLocal, worldNormal);
            volumeLocalNormal = normalize(volumeLocalNormal);
        }
        // unity-custom-func-end
        
        void Unity_Absolute_float3(float3 In, out float3 Out)
        {
            Out = abs(In);
        }
        
        void Unity_Power_float3(float3 A, float3 B, out float3 Out)
        {
            Out = pow(A, B);
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
        {
            Out = dot(A, B);
        }
        
        void Unity_Divide_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A / B;
        }
        
        void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
        {
            Out = lerp(A, B, T);
        }
        
        void Unity_Branch_float4(float Predicate, float4 True, float4 False, out float4 Out)
        {
            Out = Predicate ? True : False;
        }
        
        void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
        {
            Out = smoothstep(Edge1, Edge2, In);
        }
        
        void Unity_Subtract_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A - B;
        }
        
        void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A + B;
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float3 NormalTS;
            float3 Emission;
            float Metallic;
            float Smoothness;
            float Occlusion;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float = IN.VertexColor[0];
            float _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float = IN.VertexColor[1];
            float _Split_a8d1957c8fd4453686400eb31d654258_B_3_Float = IN.VertexColor[2];
            float _Split_a8d1957c8fd4453686400eb31d654258_A_4_Float = IN.VertexColor[3];
            float _Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean;
            Unity_Comparison_GreaterOrEqual_float(_Split_a8d1957c8fd4453686400eb31d654258_B_3_Float, float(1), _Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean);
            UnityTexture2DArray _Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_MappingTable);
            float4 _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4 = IN.uv0;
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[0];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_G_2_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[1];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_B_3_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[2];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_A_4_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[3];
            float _Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float;
            Unity_Subtract_float(_Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float, _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float, _Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float);
            float _Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float;
            Unity_Absolute_float(_Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float, _Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float);
            float _Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float;
            Unity_Subtract_float(_Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float, _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float, _Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float);
            float _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float;
            Unity_Absolute_float(_Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float, _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float);
            float _Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean;
            Unity_Comparison_LessOrEqual_float(_Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float, _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float, _Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean);
            float _Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float;
            Unity_Multiply_float_float(_Split_a8d1957c8fd4453686400eb31d654258_R_1_Float, 255, _Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float);
            float _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float;
            Unity_Round_float(_Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float, _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float);
            float _Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float;
            Unity_Multiply_float_float(_Split_a8d1957c8fd4453686400eb31d654258_G_2_Float, 255, _Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float);
            float _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float;
            Unity_Round_float(_Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float, _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float);
            float _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float;
            Unity_Branch_float(_Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean, _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float, _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float);
            float _Add_cb7536069f014983b789b899b046cdd1_Out_2_Float;
            Unity_Add_float(_Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float, float(0.5), _Add_cb7536069f014983b789b899b046cdd1_Out_2_Float);
            float _Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float;
            Unity_Divide_float(_Add_cb7536069f014983b789b899b046cdd1_Out_2_Float, float(256), _Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float);
            float4 _Combine_151f632a12c04805a28fcc5e175b3cbc_RGBA_4_Vector4;
            float3 _Combine_151f632a12c04805a28fcc5e175b3cbc_RGB_5_Vector3;
            float2 _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2;
            Unity_Combine_float(_Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float, float(0), float(0), float(0), _Combine_151f632a12c04805a28fcc5e175b3cbc_RGBA_4_Vector4, _Combine_151f632a12c04805a28fcc5e175b3cbc_RGB_5_Vector3, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2);
            float4 _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray.tex, _Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(0) );
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_R_4_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_G_5_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_B_6_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_A_7_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.a;
            float _Multiply_c3c15e8bfe914b07b98b2c48b02e5770_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_R_4_Float, 255, _Multiply_c3c15e8bfe914b07b98b2c48b02e5770_Out_2_Float);
            float _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float;
            Unity_Round_float(_Multiply_c3c15e8bfe914b07b98b2c48b02e5770_Out_2_Float, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float);
            float _Comparison_1fdd40ec77c94b85b238b762d9ed7cdf_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float, float(255), _Comparison_1fdd40ec77c94b85b238b762d9ed7cdf_Out_2_Boolean);
            UnityTexture2DArray _Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_Fallbacks);
            float4 _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray.tex, _Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(0) );
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_R_4_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_G_5_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_B_6_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_A_7_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.a;
            UnityTexture2DArray _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainAlbedoArray);
            float4x4 _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4 = _WorldToLocal;
            float3 _TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3;
            TransformPositionToVolumeSpace_float(IN.WorldSpacePosition, _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4, _TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3);
            float _Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float = _Tiling;
            float3 _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3;
            Unity_Multiply_float3_float3(_TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3, (_Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float.xxx), _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3);
            float2 _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xz;
            float4 _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_R_4_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_G_5_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_B_6_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_A_7_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.a;
            float2 _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.yz;
            float4 _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_R_4_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_G_5_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_B_6_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_A_7_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.a;
            float3 _TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3;
            TransformNormal_float(IN.WorldSpaceNormal, _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4, _TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3);
            float3 _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3;
            Unity_Absolute_float3(_TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3, _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3);
            float _Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float = _Blend;
            float3 _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3;
            Unity_Power_float3(_Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3, (_Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float.xxx), _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3);
            float3 _Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3;
            Unity_Add_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(0.001, 0.001, 0.001), _Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3);
            float _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float;
            Unity_DotProduct_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(1, 1, 1), _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float);
            float3 _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3;
            Unity_Divide_float3(_Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3, (_DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float.xxx), _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3);
            float _Split_3690e7172951494d811295287d62f6a9_R_1_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[0];
            float _Split_3690e7172951494d811295287d62f6a9_G_2_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[1];
            float _Split_3690e7172951494d811295287d62f6a9_B_3_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[2];
            float _Split_3690e7172951494d811295287d62f6a9_A_4_Float = 0;
            float4 _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4, _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4);
            float2 _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xy;
            float4 _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_R_4_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_G_5_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_B_6_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_A_7_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.a;
            float4 _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4, _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4);
            float4 _Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_1fdd40ec77c94b85b238b762d9ed7cdf_Out_2_Boolean, _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4, _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4, _Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4);
            UnityTexture2DArray _Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_MappingTable);
            float4 _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray.tex, _Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(1) );
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_R_4_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_G_5_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_B_6_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_A_7_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.a;
            float _Multiply_aed0d73afb104603b9218f3e986c52b1_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_R_4_Float, 255, _Multiply_aed0d73afb104603b9218f3e986c52b1_Out_2_Float);
            float _Round_7bf7643be96e45e7bc63501c211b5484_Out_1_Float;
            Unity_Round_float(_Multiply_aed0d73afb104603b9218f3e986c52b1_Out_2_Float, _Round_7bf7643be96e45e7bc63501c211b5484_Out_1_Float);
            float _Comparison_2dadcda8a25d4983b878c03ec3cfcc74_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_7bf7643be96e45e7bc63501c211b5484_Out_1_Float, float(255), _Comparison_2dadcda8a25d4983b878c03ec3cfcc74_Out_2_Boolean);
            UnityTexture2DArray _Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_Fallbacks);
            float4 _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray.tex, _Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(2) );
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_R_4_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_G_5_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_B_6_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_A_7_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.a;
            UnityTexture2DArray _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_AltAlbedoArray);
            float4 _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.tex, _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_R_4_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_G_5_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_B_6_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_A_7_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.tex, _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_R_4_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_G_5_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_B_6_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_A_7_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.a;
            float4 _Lerp_0e74d01d58954dd3a62d1766879888ef_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4, _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_0e74d01d58954dd3a62d1766879888ef_Out_3_Vector4);
            float4 _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.tex, _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_R_4_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_G_5_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_B_6_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_A_7_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.a;
            float4 _Lerp_d8444c08f4c24be981674285302358d0_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_0e74d01d58954dd3a62d1766879888ef_Out_3_Vector4, _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_d8444c08f4c24be981674285302358d0_Out_3_Vector4);
            float4 _Branch_ffd13beb31a34cdba80d0601903f0c4b_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_2dadcda8a25d4983b878c03ec3cfcc74_Out_2_Boolean, _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4, _Lerp_d8444c08f4c24be981674285302358d0_Out_3_Vector4, _Branch_ffd13beb31a34cdba80d0601903f0c4b_Out_3_Vector4);
            float _Property_75980a93ffd2444fb44695ea95d01dd1_Out_0_Float = _StepLowEdge;
            float _Property_0f28291fbab94789b01ad35d1f7e6da3_Out_0_Float = _StepHighEdge;
            float _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float;
            Unity_DotProduct_float3(IN.WorldSpaceNormal, float3(0, 1, 0), _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float);
            float _Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float;
            Unity_Smoothstep_float(_Property_75980a93ffd2444fb44695ea95d01dd1_Out_0_Float, _Property_0f28291fbab94789b01ad35d1f7e6da3_Out_0_Float, _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float, _Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float);
            float4 _Lerp_ebe93a7ee03a42998c196045b35050a6_Out_3_Vector4;
            Unity_Lerp_float4(_Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4, _Branch_ffd13beb31a34cdba80d0601903f0c4b_Out_3_Vector4, (_Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float.xxxx), _Lerp_ebe93a7ee03a42998c196045b35050a6_Out_3_Vector4);
            float4 _Branch_946f2ac7a40f4a4cacf6ba6bded44189_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean, _Lerp_ebe93a7ee03a42998c196045b35050a6_Out_3_Vector4, _Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4, _Branch_946f2ac7a40f4a4cacf6ba6bded44189_Out_3_Vector4);
            float _Multiply_29fcd482b27a499db64a6140d829c02c_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_B_6_Float, 255, _Multiply_29fcd482b27a499db64a6140d829c02c_Out_2_Float);
            float _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float;
            Unity_Round_float(_Multiply_29fcd482b27a499db64a6140d829c02c_Out_2_Float, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float);
            float _Comparison_595e2dca3b0d46b3b5d3264c115e1139_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float, float(255), _Comparison_595e2dca3b0d46b3b5d3264c115e1139_Out_2_Boolean);
            UnityTexture2DArray _Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainNormalArray);
            float4 _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.tex, _Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_R_4_Float = _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_G_5_Float = _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_B_6_Float = _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_A_7_Float = _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.tex, _Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_R_4_Float = _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_G_5_Float = _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_B_6_Float = _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_A_7_Float = _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4.a;
            float4 _Lerp_2f0293325fd2459ab54488be09edd1b1_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4, _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_2f0293325fd2459ab54488be09edd1b1_Out_3_Vector4);
            float4 _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.tex, _Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_R_4_Float = _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_G_5_Float = _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_B_6_Float = _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_A_7_Float = _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4.a;
            float4 _Lerp_6a7cbbf9d3de4a1fbb98d582330d8efa_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_2f0293325fd2459ab54488be09edd1b1_Out_3_Vector4, _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_6a7cbbf9d3de4a1fbb98d582330d8efa_Out_3_Vector4);
            float4 _Subtract_4fd39b463ead4d7fac0468b13e81ddd8_Out_2_Vector4;
            Unity_Subtract_float4(_Lerp_6a7cbbf9d3de4a1fbb98d582330d8efa_Out_3_Vector4, float4(0.5, 0.5, 0.5, 0.5), _Subtract_4fd39b463ead4d7fac0468b13e81ddd8_Out_2_Vector4);
            float _Property_b17539c59ddd4a87afbe6c608633be29_Out_0_Float = _Normal_Power;
            float4 _Multiply_1515bdb9bc774abfb0456db98a0243d2_Out_2_Vector4;
            Unity_Multiply_float4_float4(_Subtract_4fd39b463ead4d7fac0468b13e81ddd8_Out_2_Vector4, (_Property_b17539c59ddd4a87afbe6c608633be29_Out_0_Float.xxxx), _Multiply_1515bdb9bc774abfb0456db98a0243d2_Out_2_Vector4);
            float4 _Add_03cb5a02416e4166a52ae531c503c743_Out_2_Vector4;
            Unity_Add_float4(_Multiply_1515bdb9bc774abfb0456db98a0243d2_Out_2_Vector4, float4(0.5, 0.5, 0.5, 0.5), _Add_03cb5a02416e4166a52ae531c503c743_Out_2_Vector4);
            float4 _Branch_1e0b2c2cf59348308c46fdad1a9fec27_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_595e2dca3b0d46b3b5d3264c115e1139_Out_2_Boolean, float4(0.5, 0.5, 1, 1), _Add_03cb5a02416e4166a52ae531c503c743_Out_2_Vector4, _Branch_1e0b2c2cf59348308c46fdad1a9fec27_Out_3_Vector4);
            float _Multiply_f3c8028f13af41e0b44e52c24c65a76f_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_B_6_Float, 255, _Multiply_f3c8028f13af41e0b44e52c24c65a76f_Out_2_Float);
            float _Round_a1f5cd81fab64904bbc59f02b1c27564_Out_1_Float;
            Unity_Round_float(_Multiply_f3c8028f13af41e0b44e52c24c65a76f_Out_2_Float, _Round_a1f5cd81fab64904bbc59f02b1c27564_Out_1_Float);
            float _Comparison_1b9e8a0d006644bba678fe0a1896b29c_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_a1f5cd81fab64904bbc59f02b1c27564_Out_1_Float, float(255), _Comparison_1b9e8a0d006644bba678fe0a1896b29c_Out_2_Boolean);
            UnityTexture2DArray _Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_AltNormalArray);
            float4 _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.tex, _Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_R_4_Float = _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_G_5_Float = _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_B_6_Float = _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_A_7_Float = _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.tex, _Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_R_4_Float = _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_G_5_Float = _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_B_6_Float = _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_A_7_Float = _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4.a;
            float4 _Lerp_7ff9104dbca24fa2b3a06984365a777e_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4, _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_7ff9104dbca24fa2b3a06984365a777e_Out_3_Vector4);
            float4 _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.tex, _Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_R_4_Float = _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_G_5_Float = _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_B_6_Float = _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_A_7_Float = _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4.a;
            float4 _Lerp_9391453b43c747319ee2578915fea73f_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_7ff9104dbca24fa2b3a06984365a777e_Out_3_Vector4, _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_9391453b43c747319ee2578915fea73f_Out_3_Vector4);
            float4 _Subtract_af00415070f64bd9bdd37e3dd7c94f72_Out_2_Vector4;
            Unity_Subtract_float4(_Lerp_9391453b43c747319ee2578915fea73f_Out_3_Vector4, float4(0.5, 0.5, 0.5, 0.5), _Subtract_af00415070f64bd9bdd37e3dd7c94f72_Out_2_Vector4);
            float _Property_a3c454331c734e23ad7372ed8035b4b3_Out_0_Float = _Normal_Power;
            float4 _Multiply_bbf575daae1d4ce19d6e1148ce34dd34_Out_2_Vector4;
            Unity_Multiply_float4_float4(_Subtract_af00415070f64bd9bdd37e3dd7c94f72_Out_2_Vector4, (_Property_a3c454331c734e23ad7372ed8035b4b3_Out_0_Float.xxxx), _Multiply_bbf575daae1d4ce19d6e1148ce34dd34_Out_2_Vector4);
            float4 _Add_63a7222bed214f5dab24dbeb940bef0e_Out_2_Vector4;
            Unity_Add_float4(_Multiply_bbf575daae1d4ce19d6e1148ce34dd34_Out_2_Vector4, float4(0.5, 0.5, 0.5, 0.5), _Add_63a7222bed214f5dab24dbeb940bef0e_Out_2_Vector4);
            float4 _Branch_bfe9bed6f9824470826084ee3e1bd76d_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_1b9e8a0d006644bba678fe0a1896b29c_Out_2_Boolean, float4(0.5, 0.5, 1, 1), _Add_63a7222bed214f5dab24dbeb940bef0e_Out_2_Vector4, _Branch_bfe9bed6f9824470826084ee3e1bd76d_Out_3_Vector4);
            float4 _Lerp_6adbf4fb510241f5b91e897d9f410dfc_Out_3_Vector4;
            Unity_Lerp_float4(_Branch_1e0b2c2cf59348308c46fdad1a9fec27_Out_3_Vector4, _Branch_bfe9bed6f9824470826084ee3e1bd76d_Out_3_Vector4, (_Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float.xxxx), _Lerp_6adbf4fb510241f5b91e897d9f410dfc_Out_3_Vector4);
            float4 _Branch_6383d865716d463a812e65ad2e73b7b6_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean, _Lerp_6adbf4fb510241f5b91e897d9f410dfc_Out_3_Vector4, _Branch_1e0b2c2cf59348308c46fdad1a9fec27_Out_3_Vector4, _Branch_6383d865716d463a812e65ad2e73b7b6_Out_3_Vector4);
            float _Multiply_acd3aaaaa9d94cc48ba0d8420648b053_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_G_5_Float, 255, _Multiply_acd3aaaaa9d94cc48ba0d8420648b053_Out_2_Float);
            float _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float;
            Unity_Round_float(_Multiply_acd3aaaaa9d94cc48ba0d8420648b053_Out_2_Float, _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float);
            float _Comparison_5248daeeba384db3a15e6e407d595575_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float, float(255), _Comparison_5248daeeba384db3a15e6e407d595575_Out_2_Boolean);
            float4 _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray.tex, _Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(1) );
            float _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_R_4_Float = _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_G_5_Float = _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_B_6_Float = _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_A_7_Float = _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_RGBA_0_Vector4.a;
            UnityTexture2DArray _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainMetalSmoothArray);
            float4 _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.tex, _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float );
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_R_4_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_G_5_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_B_6_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_A_7_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.tex, _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float );
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_R_4_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_G_5_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_B_6_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_A_7_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.a;
            float4 _Lerp_5e7471adc1004f0fbe7ba4b877c88637_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4, _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_5e7471adc1004f0fbe7ba4b877c88637_Out_3_Vector4);
            float4 _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.tex, _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float );
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_R_4_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_G_5_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_B_6_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_A_7_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.a;
            float4 _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_5e7471adc1004f0fbe7ba4b877c88637_Out_3_Vector4, _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4);
            float4 _Branch_ae82b004b98b4535a974be19633d7905_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_5248daeeba384db3a15e6e407d595575_Out_2_Boolean, _SampleTexture2DArray_16a81302407346ab8343da6eb6eb107c_RGBA_0_Vector4, _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4, _Branch_ae82b004b98b4535a974be19633d7905_Out_3_Vector4);
            float _Multiply_48487faad46d49ed8d947ffe6ad6d691_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_G_5_Float, 255, _Multiply_48487faad46d49ed8d947ffe6ad6d691_Out_2_Float);
            float _Round_2c9d810d4f38426882c13718e40314ed_Out_1_Float;
            Unity_Round_float(_Multiply_48487faad46d49ed8d947ffe6ad6d691_Out_2_Float, _Round_2c9d810d4f38426882c13718e40314ed_Out_1_Float);
            float _Comparison_1f5ef5913e65410191e2147185fb9b13_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_2c9d810d4f38426882c13718e40314ed_Out_1_Float, float(255), _Comparison_1f5ef5913e65410191e2147185fb9b13_Out_2_Boolean);
            float4 _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray.tex, _Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(3) );
            float _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_R_4_Float = _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_G_5_Float = _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_B_6_Float = _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_A_7_Float = _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_RGBA_0_Vector4.a;
            UnityTexture2DArray _Property_8e7bdf8cec4a434a80265fbd13054132_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_AltMASArray);
            float4 _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8e7bdf8cec4a434a80265fbd13054132_Out_0_Texture2DArray.tex, _Property_8e7bdf8cec4a434a80265fbd13054132_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float );
            float _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_R_4_Float = _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_G_5_Float = _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_B_6_Float = _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_A_7_Float = _SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8e7bdf8cec4a434a80265fbd13054132_Out_0_Texture2DArray.tex, _Property_8e7bdf8cec4a434a80265fbd13054132_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float );
            float _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_R_4_Float = _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_G_5_Float = _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_B_6_Float = _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_A_7_Float = _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_RGBA_0_Vector4.a;
            float4 _Lerp_7d472004cb904276a104bfc56ef29b21_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_e358014dfc9b4ceba92ff13ab592463b_RGBA_0_Vector4, _SampleTexture2DArray_c43c8a415c864fcc896799a01f223d41_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_7d472004cb904276a104bfc56ef29b21_Out_3_Vector4);
            float4 _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8e7bdf8cec4a434a80265fbd13054132_Out_0_Texture2DArray.tex, _Property_8e7bdf8cec4a434a80265fbd13054132_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_8809fae94ac748cf81d085e1184690ae_Out_1_Float );
            float _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_R_4_Float = _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_G_5_Float = _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_B_6_Float = _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_A_7_Float = _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_RGBA_0_Vector4.a;
            float4 _Lerp_caf034aa50fb476d995ee09759d34f02_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_7d472004cb904276a104bfc56ef29b21_Out_3_Vector4, _SampleTexture2DArray_684c8ac089d24023b2e3ea79799fd5ad_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_caf034aa50fb476d995ee09759d34f02_Out_3_Vector4);
            float4 _Branch_6956f9f8f46e49afac7bb6aa9923e0d0_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_1f5ef5913e65410191e2147185fb9b13_Out_2_Boolean, _SampleTexture2DArray_86d3af4d77ac4a278de3743ca61b97d3_RGBA_0_Vector4, _Lerp_caf034aa50fb476d995ee09759d34f02_Out_3_Vector4, _Branch_6956f9f8f46e49afac7bb6aa9923e0d0_Out_3_Vector4);
            float4 _Lerp_c8c2a9ce74d24754ae42eeb2a040fae2_Out_3_Vector4;
            Unity_Lerp_float4(_Branch_ae82b004b98b4535a974be19633d7905_Out_3_Vector4, _Branch_6956f9f8f46e49afac7bb6aa9923e0d0_Out_3_Vector4, (_Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float.xxxx), _Lerp_c8c2a9ce74d24754ae42eeb2a040fae2_Out_3_Vector4);
            float4 _Branch_df861d6373f64bbdba520d8214fc61ff_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean, _Lerp_c8c2a9ce74d24754ae42eeb2a040fae2_Out_3_Vector4, _Branch_ae82b004b98b4535a974be19633d7905_Out_3_Vector4, _Branch_df861d6373f64bbdba520d8214fc61ff_Out_3_Vector4);
            float _Split_3264dfc20eb84c93971370a089767e2c_R_1_Float = _Branch_df861d6373f64bbdba520d8214fc61ff_Out_3_Vector4[0];
            float _Split_3264dfc20eb84c93971370a089767e2c_G_2_Float = _Branch_df861d6373f64bbdba520d8214fc61ff_Out_3_Vector4[1];
            float _Split_3264dfc20eb84c93971370a089767e2c_B_3_Float = _Branch_df861d6373f64bbdba520d8214fc61ff_Out_3_Vector4[2];
            float _Split_3264dfc20eb84c93971370a089767e2c_A_4_Float = _Branch_df861d6373f64bbdba520d8214fc61ff_Out_3_Vector4[3];
            surface.BaseColor = (_Branch_946f2ac7a40f4a4cacf6ba6bded44189_Out_3_Vector4.xyz);
            surface.NormalTS = (_Branch_6383d865716d463a812e65ad2e73b7b6_Out_3_Vector4.xyz);
            surface.Emission = float3(0, 0, 0);
            surface.Metallic = _Split_3264dfc20eb84c93971370a089767e2c_R_1_Float;
            surface.Smoothness = _Split_3264dfc20eb84c93971370a089767e2c_B_3_Float;
            surface.Occlusion = _Split_3264dfc20eb84c93971370a089767e2c_G_2_Float;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
            // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
            float3 unnormalizedNormalWS = input.normalWS;
            const float renormFactor = 1.0 / length(unnormalizedNormalWS);
        
        
            output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
            output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
        
        
            output.WorldSpacePosition = input.positionWS;
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
            output.uv0 = input.texCoord0;
            output.VertexColor = input.color;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBRGBufferPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }
        
        // Render State
        Cull Back
        ZTest LEqual
        ZWrite On
        ColorMask 0
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_NORMAL_WS
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_SHADOWCASTER
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float3 normalWS : INTERP0;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float _Blend;
        float _Tiling;
        float4x4 _WorldToLocal;
        float _Normal_Power;
        float _StepLowEdge;
        float _StepHighEdge;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainNormalArray);
        SAMPLER(sampler_TerrainNormalArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        TEXTURE2D_ARRAY(_MappingTable);
        SAMPLER(sampler_MappingTable);
        TEXTURE2D_ARRAY(_Fallbacks);
        SAMPLER(sampler_Fallbacks);
        TEXTURE2D_ARRAY(_AltAlbedoArray);
        SAMPLER(sampler_AltAlbedoArray);
        TEXTURE2D_ARRAY(_AltMASArray);
        SAMPLER(sampler_AltMASArray);
        TEXTURE2D_ARRAY(_AltNormalArray);
        SAMPLER(sampler_AltNormalArray);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        // GraphFunctions: <None>
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShadowCasterPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "MotionVectors"
            Tags
            {
                "LightMode" = "MotionVectors"
            }
        
        // Render State
        Cull Back
        ZTest LEqual
        ZWrite On
        ColorMask RG
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 3.5
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_MOTION_VECTORS
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float _Blend;
        float _Tiling;
        float4x4 _WorldToLocal;
        float _Normal_Power;
        float _StepLowEdge;
        float _StepHighEdge;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainNormalArray);
        SAMPLER(sampler_TerrainNormalArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        TEXTURE2D_ARRAY(_MappingTable);
        SAMPLER(sampler_MappingTable);
        TEXTURE2D_ARRAY(_Fallbacks);
        SAMPLER(sampler_Fallbacks);
        TEXTURE2D_ARRAY(_AltAlbedoArray);
        SAMPLER(sampler_AltAlbedoArray);
        TEXTURE2D_ARRAY(_AltMASArray);
        SAMPLER(sampler_AltMASArray);
        TEXTURE2D_ARRAY(_AltNormalArray);
        SAMPLER(sampler_AltNormalArray);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        // GraphFunctions: <None>
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpacePosition =                        input.positionOS;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/MotionVectorPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }
        
        // Render State
        Cull Back
        ZTest LEqual
        ZWrite On
        ColorMask R
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float _Blend;
        float _Tiling;
        float4x4 _WorldToLocal;
        float _Normal_Power;
        float _StepLowEdge;
        float _StepHighEdge;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainNormalArray);
        SAMPLER(sampler_TerrainNormalArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        TEXTURE2D_ARRAY(_MappingTable);
        SAMPLER(sampler_MappingTable);
        TEXTURE2D_ARRAY(_Fallbacks);
        SAMPLER(sampler_Fallbacks);
        TEXTURE2D_ARRAY(_AltAlbedoArray);
        SAMPLER(sampler_AltAlbedoArray);
        TEXTURE2D_ARRAY(_AltMASArray);
        SAMPLER(sampler_AltMASArray);
        TEXTURE2D_ARRAY(_AltNormalArray);
        SAMPLER(sampler_AltNormalArray);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        // GraphFunctions: <None>
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }
        
        // Render State
        Cull Back
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define ATTRIBUTES_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TANGENT_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHNORMALS
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
             float4 color : COLOR;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 tangentWS;
             float4 texCoord0;
             float4 color;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpaceNormal;
             float3 TangentSpaceNormal;
             float3 WorldSpacePosition;
             float4 uv0;
             float4 VertexColor;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 tangentWS : INTERP0;
             float4 texCoord0 : INTERP1;
             float4 color : INTERP2;
             float3 positionWS : INTERP3;
             float3 normalWS : INTERP4;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.tangentWS.xyzw = input.tangentWS;
            output.texCoord0.xyzw = input.texCoord0;
            output.color.xyzw = input.color;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.tangentWS = input.tangentWS.xyzw;
            output.texCoord0 = input.texCoord0.xyzw;
            output.color = input.color.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float _Blend;
        float _Tiling;
        float4x4 _WorldToLocal;
        float _Normal_Power;
        float _StepLowEdge;
        float _StepHighEdge;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainNormalArray);
        SAMPLER(sampler_TerrainNormalArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        TEXTURE2D_ARRAY(_MappingTable);
        SAMPLER(sampler_MappingTable);
        TEXTURE2D_ARRAY(_Fallbacks);
        SAMPLER(sampler_Fallbacks);
        TEXTURE2D_ARRAY(_AltAlbedoArray);
        SAMPLER(sampler_AltAlbedoArray);
        TEXTURE2D_ARRAY(_AltMASArray);
        SAMPLER(sampler_AltMASArray);
        TEXTURE2D_ARRAY(_AltNormalArray);
        SAMPLER(sampler_AltNormalArray);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
        {
            Out = A >= B ? 1 : 0;
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        void Unity_Absolute_float(float In, out float Out)
        {
            Out = abs(In);
        }
        
        void Unity_Comparison_LessOrEqual_float(float A, float B, out float Out)
        {
            Out = A <= B ? 1 : 0;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Round_float(float In, out float Out)
        {
            Out = round(In);
        }
        
        void Unity_Branch_float(float Predicate, float True, float False, out float Out)
        {
            Out = Predicate ? True : False;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
        }
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Comparison_Equal_float(float A, float B, out float Out)
        {
            Out = A == B ? 1 : 0;
        }
        
        // unity-custom-func-begin
        void TransformPositionToVolumeSpace_float(float3 worldPos, float4x4 worldToLocal, out float3 volumeLocalPos){
            volumeLocalPos = mul(worldToLocal, float4(worldPos, 1.0)).xyz;
        }
        // unity-custom-func-end
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        // unity-custom-func-begin
        void TransformNormal_float(float3 worldNormal, float4x4 worldToLocal, out float3 volumeLocalNormal){
            volumeLocalNormal = mul((float3x3)worldToLocal, worldNormal);
            volumeLocalNormal = normalize(volumeLocalNormal);
        }
        // unity-custom-func-end
        
        void Unity_Absolute_float3(float3 In, out float3 Out)
        {
            Out = abs(In);
        }
        
        void Unity_Power_float3(float3 A, float3 B, out float3 Out)
        {
            Out = pow(A, B);
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
        {
            Out = dot(A, B);
        }
        
        void Unity_Divide_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A / B;
        }
        
        void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
        {
            Out = lerp(A, B, T);
        }
        
        void Unity_Subtract_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A - B;
        }
        
        void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A + B;
        }
        
        void Unity_Branch_float4(float Predicate, float4 True, float4 False, out float4 Out)
        {
            Out = Predicate ? True : False;
        }
        
        void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
        {
            Out = smoothstep(Edge1, Edge2, In);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 NormalTS;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float = IN.VertexColor[0];
            float _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float = IN.VertexColor[1];
            float _Split_a8d1957c8fd4453686400eb31d654258_B_3_Float = IN.VertexColor[2];
            float _Split_a8d1957c8fd4453686400eb31d654258_A_4_Float = IN.VertexColor[3];
            float _Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean;
            Unity_Comparison_GreaterOrEqual_float(_Split_a8d1957c8fd4453686400eb31d654258_B_3_Float, float(1), _Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean);
            UnityTexture2DArray _Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_MappingTable);
            float4 _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4 = IN.uv0;
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[0];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_G_2_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[1];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_B_3_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[2];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_A_4_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[3];
            float _Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float;
            Unity_Subtract_float(_Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float, _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float, _Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float);
            float _Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float;
            Unity_Absolute_float(_Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float, _Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float);
            float _Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float;
            Unity_Subtract_float(_Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float, _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float, _Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float);
            float _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float;
            Unity_Absolute_float(_Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float, _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float);
            float _Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean;
            Unity_Comparison_LessOrEqual_float(_Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float, _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float, _Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean);
            float _Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float;
            Unity_Multiply_float_float(_Split_a8d1957c8fd4453686400eb31d654258_R_1_Float, 255, _Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float);
            float _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float;
            Unity_Round_float(_Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float, _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float);
            float _Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float;
            Unity_Multiply_float_float(_Split_a8d1957c8fd4453686400eb31d654258_G_2_Float, 255, _Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float);
            float _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float;
            Unity_Round_float(_Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float, _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float);
            float _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float;
            Unity_Branch_float(_Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean, _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float, _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float);
            float _Add_cb7536069f014983b789b899b046cdd1_Out_2_Float;
            Unity_Add_float(_Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float, float(0.5), _Add_cb7536069f014983b789b899b046cdd1_Out_2_Float);
            float _Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float;
            Unity_Divide_float(_Add_cb7536069f014983b789b899b046cdd1_Out_2_Float, float(256), _Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float);
            float4 _Combine_151f632a12c04805a28fcc5e175b3cbc_RGBA_4_Vector4;
            float3 _Combine_151f632a12c04805a28fcc5e175b3cbc_RGB_5_Vector3;
            float2 _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2;
            Unity_Combine_float(_Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float, float(0), float(0), float(0), _Combine_151f632a12c04805a28fcc5e175b3cbc_RGBA_4_Vector4, _Combine_151f632a12c04805a28fcc5e175b3cbc_RGB_5_Vector3, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2);
            float4 _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray.tex, _Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(0) );
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_R_4_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_G_5_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_B_6_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_A_7_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.a;
            float _Multiply_29fcd482b27a499db64a6140d829c02c_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_B_6_Float, 255, _Multiply_29fcd482b27a499db64a6140d829c02c_Out_2_Float);
            float _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float;
            Unity_Round_float(_Multiply_29fcd482b27a499db64a6140d829c02c_Out_2_Float, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float);
            float _Comparison_595e2dca3b0d46b3b5d3264c115e1139_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float, float(255), _Comparison_595e2dca3b0d46b3b5d3264c115e1139_Out_2_Boolean);
            UnityTexture2DArray _Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainNormalArray);
            float4x4 _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4 = _WorldToLocal;
            float3 _TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3;
            TransformPositionToVolumeSpace_float(IN.WorldSpacePosition, _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4, _TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3);
            float _Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float = _Tiling;
            float3 _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3;
            Unity_Multiply_float3_float3(_TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3, (_Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float.xxx), _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3);
            float2 _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xz;
            float4 _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.tex, _Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_R_4_Float = _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_G_5_Float = _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_B_6_Float = _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_A_7_Float = _SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4.a;
            float2 _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.yz;
            float4 _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.tex, _Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_R_4_Float = _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_G_5_Float = _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_B_6_Float = _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_A_7_Float = _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4.a;
            float3 _TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3;
            TransformNormal_float(IN.WorldSpaceNormal, _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4, _TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3);
            float3 _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3;
            Unity_Absolute_float3(_TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3, _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3);
            float _Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float = _Blend;
            float3 _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3;
            Unity_Power_float3(_Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3, (_Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float.xxx), _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3);
            float3 _Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3;
            Unity_Add_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(0.001, 0.001, 0.001), _Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3);
            float _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float;
            Unity_DotProduct_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(1, 1, 1), _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float);
            float3 _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3;
            Unity_Divide_float3(_Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3, (_DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float.xxx), _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3);
            float _Split_3690e7172951494d811295287d62f6a9_R_1_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[0];
            float _Split_3690e7172951494d811295287d62f6a9_G_2_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[1];
            float _Split_3690e7172951494d811295287d62f6a9_B_3_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[2];
            float _Split_3690e7172951494d811295287d62f6a9_A_4_Float = 0;
            float4 _Lerp_2f0293325fd2459ab54488be09edd1b1_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_d97d121eaef7432aa43b627f22725a89_RGBA_0_Vector4, _SampleTexture2DArray_f5e49bcc545c490abc51dea18fdbf92e_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_2f0293325fd2459ab54488be09edd1b1_Out_3_Vector4);
            float2 _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xy;
            float4 _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.tex, _Property_33d3996171e343349b69919f1c8accf5_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_R_4_Float = _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_G_5_Float = _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_B_6_Float = _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_A_7_Float = _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4.a;
            float4 _Lerp_6a7cbbf9d3de4a1fbb98d582330d8efa_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_2f0293325fd2459ab54488be09edd1b1_Out_3_Vector4, _SampleTexture2DArray_58868a65f81642049e0d81cf7d509960_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_6a7cbbf9d3de4a1fbb98d582330d8efa_Out_3_Vector4);
            float4 _Subtract_4fd39b463ead4d7fac0468b13e81ddd8_Out_2_Vector4;
            Unity_Subtract_float4(_Lerp_6a7cbbf9d3de4a1fbb98d582330d8efa_Out_3_Vector4, float4(0.5, 0.5, 0.5, 0.5), _Subtract_4fd39b463ead4d7fac0468b13e81ddd8_Out_2_Vector4);
            float _Property_b17539c59ddd4a87afbe6c608633be29_Out_0_Float = _Normal_Power;
            float4 _Multiply_1515bdb9bc774abfb0456db98a0243d2_Out_2_Vector4;
            Unity_Multiply_float4_float4(_Subtract_4fd39b463ead4d7fac0468b13e81ddd8_Out_2_Vector4, (_Property_b17539c59ddd4a87afbe6c608633be29_Out_0_Float.xxxx), _Multiply_1515bdb9bc774abfb0456db98a0243d2_Out_2_Vector4);
            float4 _Add_03cb5a02416e4166a52ae531c503c743_Out_2_Vector4;
            Unity_Add_float4(_Multiply_1515bdb9bc774abfb0456db98a0243d2_Out_2_Vector4, float4(0.5, 0.5, 0.5, 0.5), _Add_03cb5a02416e4166a52ae531c503c743_Out_2_Vector4);
            float4 _Branch_1e0b2c2cf59348308c46fdad1a9fec27_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_595e2dca3b0d46b3b5d3264c115e1139_Out_2_Boolean, float4(0.5, 0.5, 1, 1), _Add_03cb5a02416e4166a52ae531c503c743_Out_2_Vector4, _Branch_1e0b2c2cf59348308c46fdad1a9fec27_Out_3_Vector4);
            UnityTexture2DArray _Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_MappingTable);
            float4 _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray.tex, _Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(1) );
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_R_4_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_G_5_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_B_6_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_A_7_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.a;
            float _Multiply_f3c8028f13af41e0b44e52c24c65a76f_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_B_6_Float, 255, _Multiply_f3c8028f13af41e0b44e52c24c65a76f_Out_2_Float);
            float _Round_a1f5cd81fab64904bbc59f02b1c27564_Out_1_Float;
            Unity_Round_float(_Multiply_f3c8028f13af41e0b44e52c24c65a76f_Out_2_Float, _Round_a1f5cd81fab64904bbc59f02b1c27564_Out_1_Float);
            float _Comparison_1b9e8a0d006644bba678fe0a1896b29c_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_a1f5cd81fab64904bbc59f02b1c27564_Out_1_Float, float(255), _Comparison_1b9e8a0d006644bba678fe0a1896b29c_Out_2_Boolean);
            UnityTexture2DArray _Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_AltNormalArray);
            float4 _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.tex, _Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_R_4_Float = _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_G_5_Float = _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_B_6_Float = _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_A_7_Float = _SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.tex, _Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_R_4_Float = _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_G_5_Float = _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_B_6_Float = _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_A_7_Float = _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4.a;
            float4 _Lerp_7ff9104dbca24fa2b3a06984365a777e_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_02b542c7ed214d18b91f921242fd3e3f_RGBA_0_Vector4, _SampleTexture2DArray_94e3429a36484c60993f5af722757bba_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_7ff9104dbca24fa2b3a06984365a777e_Out_3_Vector4);
            float4 _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.tex, _Property_56cfa073474e43b0a227a6d1ea940018_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_b2ee406a94d64521a324f5df9e73c853_Out_1_Float );
            float _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_R_4_Float = _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_G_5_Float = _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_B_6_Float = _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_A_7_Float = _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4.a;
            float4 _Lerp_9391453b43c747319ee2578915fea73f_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_7ff9104dbca24fa2b3a06984365a777e_Out_3_Vector4, _SampleTexture2DArray_a2f6be3e495345c69a8ab52f1cdb6861_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_9391453b43c747319ee2578915fea73f_Out_3_Vector4);
            float4 _Subtract_af00415070f64bd9bdd37e3dd7c94f72_Out_2_Vector4;
            Unity_Subtract_float4(_Lerp_9391453b43c747319ee2578915fea73f_Out_3_Vector4, float4(0.5, 0.5, 0.5, 0.5), _Subtract_af00415070f64bd9bdd37e3dd7c94f72_Out_2_Vector4);
            float _Property_a3c454331c734e23ad7372ed8035b4b3_Out_0_Float = _Normal_Power;
            float4 _Multiply_bbf575daae1d4ce19d6e1148ce34dd34_Out_2_Vector4;
            Unity_Multiply_float4_float4(_Subtract_af00415070f64bd9bdd37e3dd7c94f72_Out_2_Vector4, (_Property_a3c454331c734e23ad7372ed8035b4b3_Out_0_Float.xxxx), _Multiply_bbf575daae1d4ce19d6e1148ce34dd34_Out_2_Vector4);
            float4 _Add_63a7222bed214f5dab24dbeb940bef0e_Out_2_Vector4;
            Unity_Add_float4(_Multiply_bbf575daae1d4ce19d6e1148ce34dd34_Out_2_Vector4, float4(0.5, 0.5, 0.5, 0.5), _Add_63a7222bed214f5dab24dbeb940bef0e_Out_2_Vector4);
            float4 _Branch_bfe9bed6f9824470826084ee3e1bd76d_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_1b9e8a0d006644bba678fe0a1896b29c_Out_2_Boolean, float4(0.5, 0.5, 1, 1), _Add_63a7222bed214f5dab24dbeb940bef0e_Out_2_Vector4, _Branch_bfe9bed6f9824470826084ee3e1bd76d_Out_3_Vector4);
            float _Property_75980a93ffd2444fb44695ea95d01dd1_Out_0_Float = _StepLowEdge;
            float _Property_0f28291fbab94789b01ad35d1f7e6da3_Out_0_Float = _StepHighEdge;
            float _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float;
            Unity_DotProduct_float3(IN.WorldSpaceNormal, float3(0, 1, 0), _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float);
            float _Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float;
            Unity_Smoothstep_float(_Property_75980a93ffd2444fb44695ea95d01dd1_Out_0_Float, _Property_0f28291fbab94789b01ad35d1f7e6da3_Out_0_Float, _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float, _Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float);
            float4 _Lerp_6adbf4fb510241f5b91e897d9f410dfc_Out_3_Vector4;
            Unity_Lerp_float4(_Branch_1e0b2c2cf59348308c46fdad1a9fec27_Out_3_Vector4, _Branch_bfe9bed6f9824470826084ee3e1bd76d_Out_3_Vector4, (_Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float.xxxx), _Lerp_6adbf4fb510241f5b91e897d9f410dfc_Out_3_Vector4);
            float4 _Branch_6383d865716d463a812e65ad2e73b7b6_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean, _Lerp_6adbf4fb510241f5b91e897d9f410dfc_Out_3_Vector4, _Branch_1e0b2c2cf59348308c46fdad1a9fec27_Out_3_Vector4, _Branch_6383d865716d463a812e65ad2e73b7b6_Out_3_Vector4);
            surface.NormalTS = (_Branch_6383d865716d463a812e65ad2e73b7b6_Out_3_Vector4.xyz);
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
            // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
            float3 unnormalizedNormalWS = input.normalWS;
            const float renormFactor = 1.0 / length(unnormalizedNormalWS);
        
        
            output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
            output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
        
        
            output.WorldSpacePosition = input.positionWS;
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
            output.uv0 = input.texCoord0;
            output.VertexColor = input.color;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthNormalsOnlyPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }
        
        // Render State
        Cull Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma shader_feature _ EDITOR_VISUALIZATION
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define ATTRIBUTES_NEED_TEXCOORD2
        #define ATTRIBUTES_NEED_COLOR
        #define ATTRIBUTES_NEED_INSTANCEID
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_TEXCOORD1
        #define VARYINGS_NEED_TEXCOORD2
        #define VARYINGS_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_META
        #define _FOG_FRAGMENT 1
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
             float4 uv2 : TEXCOORD2;
             float4 color : COLOR;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 texCoord0;
             float4 texCoord1;
             float4 texCoord2;
             float4 color;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpaceNormal;
             float3 WorldSpacePosition;
             float4 uv0;
             float4 VertexColor;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0 : INTERP0;
             float4 texCoord1 : INTERP1;
             float4 texCoord2 : INTERP2;
             float4 color : INTERP3;
             float3 positionWS : INTERP4;
             float3 normalWS : INTERP5;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.texCoord0.xyzw = input.texCoord0;
            output.texCoord1.xyzw = input.texCoord1;
            output.texCoord2.xyzw = input.texCoord2;
            output.color.xyzw = input.color;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.texCoord0 = input.texCoord0.xyzw;
            output.texCoord1 = input.texCoord1.xyzw;
            output.texCoord2 = input.texCoord2.xyzw;
            output.color = input.color.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float _Blend;
        float _Tiling;
        float4x4 _WorldToLocal;
        float _Normal_Power;
        float _StepLowEdge;
        float _StepHighEdge;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainNormalArray);
        SAMPLER(sampler_TerrainNormalArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        TEXTURE2D_ARRAY(_MappingTable);
        SAMPLER(sampler_MappingTable);
        TEXTURE2D_ARRAY(_Fallbacks);
        SAMPLER(sampler_Fallbacks);
        TEXTURE2D_ARRAY(_AltAlbedoArray);
        SAMPLER(sampler_AltAlbedoArray);
        TEXTURE2D_ARRAY(_AltMASArray);
        SAMPLER(sampler_AltMASArray);
        TEXTURE2D_ARRAY(_AltNormalArray);
        SAMPLER(sampler_AltNormalArray);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
        {
            Out = A >= B ? 1 : 0;
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        void Unity_Absolute_float(float In, out float Out)
        {
            Out = abs(In);
        }
        
        void Unity_Comparison_LessOrEqual_float(float A, float B, out float Out)
        {
            Out = A <= B ? 1 : 0;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Round_float(float In, out float Out)
        {
            Out = round(In);
        }
        
        void Unity_Branch_float(float Predicate, float True, float False, out float Out)
        {
            Out = Predicate ? True : False;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
        }
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Comparison_Equal_float(float A, float B, out float Out)
        {
            Out = A == B ? 1 : 0;
        }
        
        // unity-custom-func-begin
        void TransformPositionToVolumeSpace_float(float3 worldPos, float4x4 worldToLocal, out float3 volumeLocalPos){
            volumeLocalPos = mul(worldToLocal, float4(worldPos, 1.0)).xyz;
        }
        // unity-custom-func-end
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        // unity-custom-func-begin
        void TransformNormal_float(float3 worldNormal, float4x4 worldToLocal, out float3 volumeLocalNormal){
            volumeLocalNormal = mul((float3x3)worldToLocal, worldNormal);
            volumeLocalNormal = normalize(volumeLocalNormal);
        }
        // unity-custom-func-end
        
        void Unity_Absolute_float3(float3 In, out float3 Out)
        {
            Out = abs(In);
        }
        
        void Unity_Power_float3(float3 A, float3 B, out float3 Out)
        {
            Out = pow(A, B);
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
        {
            Out = dot(A, B);
        }
        
        void Unity_Divide_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A / B;
        }
        
        void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
        {
            Out = lerp(A, B, T);
        }
        
        void Unity_Branch_float4(float Predicate, float4 True, float4 False, out float4 Out)
        {
            Out = Predicate ? True : False;
        }
        
        void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
        {
            Out = smoothstep(Edge1, Edge2, In);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float3 Emission;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float = IN.VertexColor[0];
            float _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float = IN.VertexColor[1];
            float _Split_a8d1957c8fd4453686400eb31d654258_B_3_Float = IN.VertexColor[2];
            float _Split_a8d1957c8fd4453686400eb31d654258_A_4_Float = IN.VertexColor[3];
            float _Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean;
            Unity_Comparison_GreaterOrEqual_float(_Split_a8d1957c8fd4453686400eb31d654258_B_3_Float, float(1), _Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean);
            UnityTexture2DArray _Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_MappingTable);
            float4 _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4 = IN.uv0;
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[0];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_G_2_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[1];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_B_3_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[2];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_A_4_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[3];
            float _Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float;
            Unity_Subtract_float(_Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float, _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float, _Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float);
            float _Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float;
            Unity_Absolute_float(_Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float, _Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float);
            float _Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float;
            Unity_Subtract_float(_Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float, _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float, _Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float);
            float _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float;
            Unity_Absolute_float(_Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float, _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float);
            float _Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean;
            Unity_Comparison_LessOrEqual_float(_Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float, _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float, _Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean);
            float _Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float;
            Unity_Multiply_float_float(_Split_a8d1957c8fd4453686400eb31d654258_R_1_Float, 255, _Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float);
            float _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float;
            Unity_Round_float(_Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float, _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float);
            float _Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float;
            Unity_Multiply_float_float(_Split_a8d1957c8fd4453686400eb31d654258_G_2_Float, 255, _Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float);
            float _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float;
            Unity_Round_float(_Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float, _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float);
            float _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float;
            Unity_Branch_float(_Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean, _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float, _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float);
            float _Add_cb7536069f014983b789b899b046cdd1_Out_2_Float;
            Unity_Add_float(_Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float, float(0.5), _Add_cb7536069f014983b789b899b046cdd1_Out_2_Float);
            float _Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float;
            Unity_Divide_float(_Add_cb7536069f014983b789b899b046cdd1_Out_2_Float, float(256), _Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float);
            float4 _Combine_151f632a12c04805a28fcc5e175b3cbc_RGBA_4_Vector4;
            float3 _Combine_151f632a12c04805a28fcc5e175b3cbc_RGB_5_Vector3;
            float2 _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2;
            Unity_Combine_float(_Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float, float(0), float(0), float(0), _Combine_151f632a12c04805a28fcc5e175b3cbc_RGBA_4_Vector4, _Combine_151f632a12c04805a28fcc5e175b3cbc_RGB_5_Vector3, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2);
            float4 _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray.tex, _Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(0) );
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_R_4_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_G_5_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_B_6_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_A_7_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.a;
            float _Multiply_c3c15e8bfe914b07b98b2c48b02e5770_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_R_4_Float, 255, _Multiply_c3c15e8bfe914b07b98b2c48b02e5770_Out_2_Float);
            float _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float;
            Unity_Round_float(_Multiply_c3c15e8bfe914b07b98b2c48b02e5770_Out_2_Float, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float);
            float _Comparison_1fdd40ec77c94b85b238b762d9ed7cdf_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float, float(255), _Comparison_1fdd40ec77c94b85b238b762d9ed7cdf_Out_2_Boolean);
            UnityTexture2DArray _Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_Fallbacks);
            float4 _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray.tex, _Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(0) );
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_R_4_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_G_5_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_B_6_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_A_7_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.a;
            UnityTexture2DArray _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainAlbedoArray);
            float4x4 _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4 = _WorldToLocal;
            float3 _TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3;
            TransformPositionToVolumeSpace_float(IN.WorldSpacePosition, _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4, _TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3);
            float _Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float = _Tiling;
            float3 _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3;
            Unity_Multiply_float3_float3(_TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3, (_Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float.xxx), _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3);
            float2 _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xz;
            float4 _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_R_4_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_G_5_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_B_6_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_A_7_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.a;
            float2 _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.yz;
            float4 _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_R_4_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_G_5_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_B_6_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_A_7_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.a;
            float3 _TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3;
            TransformNormal_float(IN.WorldSpaceNormal, _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4, _TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3);
            float3 _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3;
            Unity_Absolute_float3(_TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3, _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3);
            float _Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float = _Blend;
            float3 _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3;
            Unity_Power_float3(_Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3, (_Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float.xxx), _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3);
            float3 _Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3;
            Unity_Add_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(0.001, 0.001, 0.001), _Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3);
            float _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float;
            Unity_DotProduct_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(1, 1, 1), _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float);
            float3 _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3;
            Unity_Divide_float3(_Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3, (_DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float.xxx), _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3);
            float _Split_3690e7172951494d811295287d62f6a9_R_1_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[0];
            float _Split_3690e7172951494d811295287d62f6a9_G_2_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[1];
            float _Split_3690e7172951494d811295287d62f6a9_B_3_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[2];
            float _Split_3690e7172951494d811295287d62f6a9_A_4_Float = 0;
            float4 _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4, _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4);
            float2 _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xy;
            float4 _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_R_4_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_G_5_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_B_6_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_A_7_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.a;
            float4 _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4, _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4);
            float4 _Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_1fdd40ec77c94b85b238b762d9ed7cdf_Out_2_Boolean, _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4, _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4, _Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4);
            UnityTexture2DArray _Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_MappingTable);
            float4 _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray.tex, _Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(1) );
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_R_4_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_G_5_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_B_6_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_A_7_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.a;
            float _Multiply_aed0d73afb104603b9218f3e986c52b1_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_R_4_Float, 255, _Multiply_aed0d73afb104603b9218f3e986c52b1_Out_2_Float);
            float _Round_7bf7643be96e45e7bc63501c211b5484_Out_1_Float;
            Unity_Round_float(_Multiply_aed0d73afb104603b9218f3e986c52b1_Out_2_Float, _Round_7bf7643be96e45e7bc63501c211b5484_Out_1_Float);
            float _Comparison_2dadcda8a25d4983b878c03ec3cfcc74_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_7bf7643be96e45e7bc63501c211b5484_Out_1_Float, float(255), _Comparison_2dadcda8a25d4983b878c03ec3cfcc74_Out_2_Boolean);
            UnityTexture2DArray _Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_Fallbacks);
            float4 _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray.tex, _Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(2) );
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_R_4_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_G_5_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_B_6_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_A_7_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.a;
            UnityTexture2DArray _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_AltAlbedoArray);
            float4 _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.tex, _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_R_4_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_G_5_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_B_6_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_A_7_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.tex, _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_R_4_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_G_5_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_B_6_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_A_7_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.a;
            float4 _Lerp_0e74d01d58954dd3a62d1766879888ef_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4, _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_0e74d01d58954dd3a62d1766879888ef_Out_3_Vector4);
            float4 _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.tex, _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_R_4_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_G_5_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_B_6_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_A_7_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.a;
            float4 _Lerp_d8444c08f4c24be981674285302358d0_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_0e74d01d58954dd3a62d1766879888ef_Out_3_Vector4, _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_d8444c08f4c24be981674285302358d0_Out_3_Vector4);
            float4 _Branch_ffd13beb31a34cdba80d0601903f0c4b_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_2dadcda8a25d4983b878c03ec3cfcc74_Out_2_Boolean, _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4, _Lerp_d8444c08f4c24be981674285302358d0_Out_3_Vector4, _Branch_ffd13beb31a34cdba80d0601903f0c4b_Out_3_Vector4);
            float _Property_75980a93ffd2444fb44695ea95d01dd1_Out_0_Float = _StepLowEdge;
            float _Property_0f28291fbab94789b01ad35d1f7e6da3_Out_0_Float = _StepHighEdge;
            float _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float;
            Unity_DotProduct_float3(IN.WorldSpaceNormal, float3(0, 1, 0), _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float);
            float _Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float;
            Unity_Smoothstep_float(_Property_75980a93ffd2444fb44695ea95d01dd1_Out_0_Float, _Property_0f28291fbab94789b01ad35d1f7e6da3_Out_0_Float, _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float, _Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float);
            float4 _Lerp_ebe93a7ee03a42998c196045b35050a6_Out_3_Vector4;
            Unity_Lerp_float4(_Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4, _Branch_ffd13beb31a34cdba80d0601903f0c4b_Out_3_Vector4, (_Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float.xxxx), _Lerp_ebe93a7ee03a42998c196045b35050a6_Out_3_Vector4);
            float4 _Branch_946f2ac7a40f4a4cacf6ba6bded44189_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean, _Lerp_ebe93a7ee03a42998c196045b35050a6_Out_3_Vector4, _Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4, _Branch_946f2ac7a40f4a4cacf6ba6bded44189_Out_3_Vector4);
            surface.BaseColor = (_Branch_946f2ac7a40f4a4cacf6ba6bded44189_Out_3_Vector4.xyz);
            surface.Emission = float3(0, 0, 0);
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
            // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
            float3 unnormalizedNormalWS = input.normalWS;
            const float renormFactor = 1.0 / length(unnormalizedNormalWS);
        
        
            output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
        
        
            output.WorldSpacePosition = input.positionWS;
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
            output.uv0 = input.texCoord0;
            output.VertexColor = input.color;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/LightingMetaPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "SceneSelectionPass"
            Tags
            {
                "LightMode" = "SceneSelectionPass"
            }
        
        // Render State
        Cull Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        #define SCENESELECTIONPASS 1
        #define ALPHA_CLIP_THRESHOLD 1
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float _Blend;
        float _Tiling;
        float4x4 _WorldToLocal;
        float _Normal_Power;
        float _StepLowEdge;
        float _StepHighEdge;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainNormalArray);
        SAMPLER(sampler_TerrainNormalArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        TEXTURE2D_ARRAY(_MappingTable);
        SAMPLER(sampler_MappingTable);
        TEXTURE2D_ARRAY(_Fallbacks);
        SAMPLER(sampler_Fallbacks);
        TEXTURE2D_ARRAY(_AltAlbedoArray);
        SAMPLER(sampler_AltAlbedoArray);
        TEXTURE2D_ARRAY(_AltMASArray);
        SAMPLER(sampler_AltMASArray);
        TEXTURE2D_ARRAY(_AltNormalArray);
        SAMPLER(sampler_AltNormalArray);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        // GraphFunctions: <None>
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "ScenePickingPass"
            Tags
            {
                "LightMode" = "Picking"
            }
        
        // Render State
        Cull Back
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        #define SCENEPICKINGPASS 1
        #define ALPHA_CLIP_THRESHOLD 1
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 color : COLOR;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 texCoord0;
             float4 color;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpaceNormal;
             float3 WorldSpacePosition;
             float4 uv0;
             float4 VertexColor;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0 : INTERP0;
             float4 color : INTERP1;
             float3 positionWS : INTERP2;
             float3 normalWS : INTERP3;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.texCoord0.xyzw = input.texCoord0;
            output.color.xyzw = input.color;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.texCoord0 = input.texCoord0.xyzw;
            output.color = input.color.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float _Blend;
        float _Tiling;
        float4x4 _WorldToLocal;
        float _Normal_Power;
        float _StepLowEdge;
        float _StepHighEdge;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainNormalArray);
        SAMPLER(sampler_TerrainNormalArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        TEXTURE2D_ARRAY(_MappingTable);
        SAMPLER(sampler_MappingTable);
        TEXTURE2D_ARRAY(_Fallbacks);
        SAMPLER(sampler_Fallbacks);
        TEXTURE2D_ARRAY(_AltAlbedoArray);
        SAMPLER(sampler_AltAlbedoArray);
        TEXTURE2D_ARRAY(_AltMASArray);
        SAMPLER(sampler_AltMASArray);
        TEXTURE2D_ARRAY(_AltNormalArray);
        SAMPLER(sampler_AltNormalArray);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
        {
            Out = A >= B ? 1 : 0;
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        void Unity_Absolute_float(float In, out float Out)
        {
            Out = abs(In);
        }
        
        void Unity_Comparison_LessOrEqual_float(float A, float B, out float Out)
        {
            Out = A <= B ? 1 : 0;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Round_float(float In, out float Out)
        {
            Out = round(In);
        }
        
        void Unity_Branch_float(float Predicate, float True, float False, out float Out)
        {
            Out = Predicate ? True : False;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
        }
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Comparison_Equal_float(float A, float B, out float Out)
        {
            Out = A == B ? 1 : 0;
        }
        
        // unity-custom-func-begin
        void TransformPositionToVolumeSpace_float(float3 worldPos, float4x4 worldToLocal, out float3 volumeLocalPos){
            volumeLocalPos = mul(worldToLocal, float4(worldPos, 1.0)).xyz;
        }
        // unity-custom-func-end
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        // unity-custom-func-begin
        void TransformNormal_float(float3 worldNormal, float4x4 worldToLocal, out float3 volumeLocalNormal){
            volumeLocalNormal = mul((float3x3)worldToLocal, worldNormal);
            volumeLocalNormal = normalize(volumeLocalNormal);
        }
        // unity-custom-func-end
        
        void Unity_Absolute_float3(float3 In, out float3 Out)
        {
            Out = abs(In);
        }
        
        void Unity_Power_float3(float3 A, float3 B, out float3 Out)
        {
            Out = pow(A, B);
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
        {
            Out = dot(A, B);
        }
        
        void Unity_Divide_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A / B;
        }
        
        void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
        {
            Out = lerp(A, B, T);
        }
        
        void Unity_Branch_float4(float Predicate, float4 True, float4 False, out float4 Out)
        {
            Out = Predicate ? True : False;
        }
        
        void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
        {
            Out = smoothstep(Edge1, Edge2, In);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float = IN.VertexColor[0];
            float _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float = IN.VertexColor[1];
            float _Split_a8d1957c8fd4453686400eb31d654258_B_3_Float = IN.VertexColor[2];
            float _Split_a8d1957c8fd4453686400eb31d654258_A_4_Float = IN.VertexColor[3];
            float _Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean;
            Unity_Comparison_GreaterOrEqual_float(_Split_a8d1957c8fd4453686400eb31d654258_B_3_Float, float(1), _Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean);
            UnityTexture2DArray _Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_MappingTable);
            float4 _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4 = IN.uv0;
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[0];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_G_2_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[1];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_B_3_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[2];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_A_4_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[3];
            float _Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float;
            Unity_Subtract_float(_Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float, _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float, _Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float);
            float _Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float;
            Unity_Absolute_float(_Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float, _Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float);
            float _Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float;
            Unity_Subtract_float(_Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float, _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float, _Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float);
            float _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float;
            Unity_Absolute_float(_Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float, _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float);
            float _Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean;
            Unity_Comparison_LessOrEqual_float(_Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float, _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float, _Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean);
            float _Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float;
            Unity_Multiply_float_float(_Split_a8d1957c8fd4453686400eb31d654258_R_1_Float, 255, _Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float);
            float _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float;
            Unity_Round_float(_Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float, _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float);
            float _Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float;
            Unity_Multiply_float_float(_Split_a8d1957c8fd4453686400eb31d654258_G_2_Float, 255, _Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float);
            float _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float;
            Unity_Round_float(_Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float, _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float);
            float _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float;
            Unity_Branch_float(_Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean, _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float, _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float);
            float _Add_cb7536069f014983b789b899b046cdd1_Out_2_Float;
            Unity_Add_float(_Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float, float(0.5), _Add_cb7536069f014983b789b899b046cdd1_Out_2_Float);
            float _Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float;
            Unity_Divide_float(_Add_cb7536069f014983b789b899b046cdd1_Out_2_Float, float(256), _Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float);
            float4 _Combine_151f632a12c04805a28fcc5e175b3cbc_RGBA_4_Vector4;
            float3 _Combine_151f632a12c04805a28fcc5e175b3cbc_RGB_5_Vector3;
            float2 _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2;
            Unity_Combine_float(_Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float, float(0), float(0), float(0), _Combine_151f632a12c04805a28fcc5e175b3cbc_RGBA_4_Vector4, _Combine_151f632a12c04805a28fcc5e175b3cbc_RGB_5_Vector3, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2);
            float4 _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray.tex, _Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(0) );
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_R_4_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_G_5_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_B_6_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_A_7_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.a;
            float _Multiply_c3c15e8bfe914b07b98b2c48b02e5770_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_R_4_Float, 255, _Multiply_c3c15e8bfe914b07b98b2c48b02e5770_Out_2_Float);
            float _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float;
            Unity_Round_float(_Multiply_c3c15e8bfe914b07b98b2c48b02e5770_Out_2_Float, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float);
            float _Comparison_1fdd40ec77c94b85b238b762d9ed7cdf_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float, float(255), _Comparison_1fdd40ec77c94b85b238b762d9ed7cdf_Out_2_Boolean);
            UnityTexture2DArray _Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_Fallbacks);
            float4 _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray.tex, _Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(0) );
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_R_4_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_G_5_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_B_6_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_A_7_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.a;
            UnityTexture2DArray _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainAlbedoArray);
            float4x4 _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4 = _WorldToLocal;
            float3 _TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3;
            TransformPositionToVolumeSpace_float(IN.WorldSpacePosition, _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4, _TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3);
            float _Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float = _Tiling;
            float3 _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3;
            Unity_Multiply_float3_float3(_TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3, (_Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float.xxx), _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3);
            float2 _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xz;
            float4 _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_R_4_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_G_5_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_B_6_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_A_7_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.a;
            float2 _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.yz;
            float4 _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_R_4_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_G_5_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_B_6_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_A_7_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.a;
            float3 _TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3;
            TransformNormal_float(IN.WorldSpaceNormal, _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4, _TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3);
            float3 _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3;
            Unity_Absolute_float3(_TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3, _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3);
            float _Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float = _Blend;
            float3 _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3;
            Unity_Power_float3(_Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3, (_Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float.xxx), _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3);
            float3 _Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3;
            Unity_Add_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(0.001, 0.001, 0.001), _Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3);
            float _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float;
            Unity_DotProduct_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(1, 1, 1), _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float);
            float3 _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3;
            Unity_Divide_float3(_Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3, (_DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float.xxx), _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3);
            float _Split_3690e7172951494d811295287d62f6a9_R_1_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[0];
            float _Split_3690e7172951494d811295287d62f6a9_G_2_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[1];
            float _Split_3690e7172951494d811295287d62f6a9_B_3_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[2];
            float _Split_3690e7172951494d811295287d62f6a9_A_4_Float = 0;
            float4 _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4, _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4);
            float2 _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xy;
            float4 _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_R_4_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_G_5_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_B_6_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_A_7_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.a;
            float4 _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4, _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4);
            float4 _Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_1fdd40ec77c94b85b238b762d9ed7cdf_Out_2_Boolean, _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4, _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4, _Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4);
            UnityTexture2DArray _Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_MappingTable);
            float4 _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray.tex, _Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(1) );
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_R_4_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_G_5_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_B_6_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_A_7_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.a;
            float _Multiply_aed0d73afb104603b9218f3e986c52b1_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_R_4_Float, 255, _Multiply_aed0d73afb104603b9218f3e986c52b1_Out_2_Float);
            float _Round_7bf7643be96e45e7bc63501c211b5484_Out_1_Float;
            Unity_Round_float(_Multiply_aed0d73afb104603b9218f3e986c52b1_Out_2_Float, _Round_7bf7643be96e45e7bc63501c211b5484_Out_1_Float);
            float _Comparison_2dadcda8a25d4983b878c03ec3cfcc74_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_7bf7643be96e45e7bc63501c211b5484_Out_1_Float, float(255), _Comparison_2dadcda8a25d4983b878c03ec3cfcc74_Out_2_Boolean);
            UnityTexture2DArray _Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_Fallbacks);
            float4 _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray.tex, _Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(2) );
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_R_4_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_G_5_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_B_6_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_A_7_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.a;
            UnityTexture2DArray _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_AltAlbedoArray);
            float4 _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.tex, _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_R_4_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_G_5_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_B_6_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_A_7_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.tex, _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_R_4_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_G_5_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_B_6_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_A_7_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.a;
            float4 _Lerp_0e74d01d58954dd3a62d1766879888ef_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4, _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_0e74d01d58954dd3a62d1766879888ef_Out_3_Vector4);
            float4 _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.tex, _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_R_4_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_G_5_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_B_6_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_A_7_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.a;
            float4 _Lerp_d8444c08f4c24be981674285302358d0_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_0e74d01d58954dd3a62d1766879888ef_Out_3_Vector4, _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_d8444c08f4c24be981674285302358d0_Out_3_Vector4);
            float4 _Branch_ffd13beb31a34cdba80d0601903f0c4b_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_2dadcda8a25d4983b878c03ec3cfcc74_Out_2_Boolean, _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4, _Lerp_d8444c08f4c24be981674285302358d0_Out_3_Vector4, _Branch_ffd13beb31a34cdba80d0601903f0c4b_Out_3_Vector4);
            float _Property_75980a93ffd2444fb44695ea95d01dd1_Out_0_Float = _StepLowEdge;
            float _Property_0f28291fbab94789b01ad35d1f7e6da3_Out_0_Float = _StepHighEdge;
            float _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float;
            Unity_DotProduct_float3(IN.WorldSpaceNormal, float3(0, 1, 0), _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float);
            float _Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float;
            Unity_Smoothstep_float(_Property_75980a93ffd2444fb44695ea95d01dd1_Out_0_Float, _Property_0f28291fbab94789b01ad35d1f7e6da3_Out_0_Float, _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float, _Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float);
            float4 _Lerp_ebe93a7ee03a42998c196045b35050a6_Out_3_Vector4;
            Unity_Lerp_float4(_Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4, _Branch_ffd13beb31a34cdba80d0601903f0c4b_Out_3_Vector4, (_Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float.xxxx), _Lerp_ebe93a7ee03a42998c196045b35050a6_Out_3_Vector4);
            float4 _Branch_946f2ac7a40f4a4cacf6ba6bded44189_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean, _Lerp_ebe93a7ee03a42998c196045b35050a6_Out_3_Vector4, _Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4, _Branch_946f2ac7a40f4a4cacf6ba6bded44189_Out_3_Vector4);
            surface.BaseColor = (_Branch_946f2ac7a40f4a4cacf6ba6bded44189_Out_3_Vector4.xyz);
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
            // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
            float3 unnormalizedNormalWS = input.normalWS;
            const float renormFactor = 1.0 / length(unnormalizedNormalWS);
        
        
            output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
        
        
            output.WorldSpacePosition = input.positionWS;
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
            output.uv0 = input.texCoord0;
            output.VertexColor = input.color;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "Universal 2D"
            Tags
            {
                "LightMode" = "Universal2D"
            }
        
        // Render State
        Cull Back
        Blend One Zero
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_2D
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 color : COLOR;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 texCoord0;
             float4 color;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpaceNormal;
             float3 WorldSpacePosition;
             float4 uv0;
             float4 VertexColor;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0 : INTERP0;
             float4 color : INTERP1;
             float3 positionWS : INTERP2;
             float3 normalWS : INTERP3;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.texCoord0.xyzw = input.texCoord0;
            output.color.xyzw = input.color;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.texCoord0 = input.texCoord0.xyzw;
            output.color = input.color.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float _Blend;
        float _Tiling;
        float4x4 _WorldToLocal;
        float _Normal_Power;
        float _StepLowEdge;
        float _StepHighEdge;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainNormalArray);
        SAMPLER(sampler_TerrainNormalArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        TEXTURE2D_ARRAY(_MappingTable);
        SAMPLER(sampler_MappingTable);
        TEXTURE2D_ARRAY(_Fallbacks);
        SAMPLER(sampler_Fallbacks);
        TEXTURE2D_ARRAY(_AltAlbedoArray);
        SAMPLER(sampler_AltAlbedoArray);
        TEXTURE2D_ARRAY(_AltMASArray);
        SAMPLER(sampler_AltMASArray);
        TEXTURE2D_ARRAY(_AltNormalArray);
        SAMPLER(sampler_AltNormalArray);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
        {
            Out = A >= B ? 1 : 0;
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        void Unity_Absolute_float(float In, out float Out)
        {
            Out = abs(In);
        }
        
        void Unity_Comparison_LessOrEqual_float(float A, float B, out float Out)
        {
            Out = A <= B ? 1 : 0;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Round_float(float In, out float Out)
        {
            Out = round(In);
        }
        
        void Unity_Branch_float(float Predicate, float True, float False, out float Out)
        {
            Out = Predicate ? True : False;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
        }
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Comparison_Equal_float(float A, float B, out float Out)
        {
            Out = A == B ? 1 : 0;
        }
        
        // unity-custom-func-begin
        void TransformPositionToVolumeSpace_float(float3 worldPos, float4x4 worldToLocal, out float3 volumeLocalPos){
            volumeLocalPos = mul(worldToLocal, float4(worldPos, 1.0)).xyz;
        }
        // unity-custom-func-end
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        // unity-custom-func-begin
        void TransformNormal_float(float3 worldNormal, float4x4 worldToLocal, out float3 volumeLocalNormal){
            volumeLocalNormal = mul((float3x3)worldToLocal, worldNormal);
            volumeLocalNormal = normalize(volumeLocalNormal);
        }
        // unity-custom-func-end
        
        void Unity_Absolute_float3(float3 In, out float3 Out)
        {
            Out = abs(In);
        }
        
        void Unity_Power_float3(float3 A, float3 B, out float3 Out)
        {
            Out = pow(A, B);
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
        {
            Out = dot(A, B);
        }
        
        void Unity_Divide_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A / B;
        }
        
        void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
        {
            Out = lerp(A, B, T);
        }
        
        void Unity_Branch_float4(float Predicate, float4 True, float4 False, out float4 Out)
        {
            Out = Predicate ? True : False;
        }
        
        void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
        {
            Out = smoothstep(Edge1, Edge2, In);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float = IN.VertexColor[0];
            float _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float = IN.VertexColor[1];
            float _Split_a8d1957c8fd4453686400eb31d654258_B_3_Float = IN.VertexColor[2];
            float _Split_a8d1957c8fd4453686400eb31d654258_A_4_Float = IN.VertexColor[3];
            float _Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean;
            Unity_Comparison_GreaterOrEqual_float(_Split_a8d1957c8fd4453686400eb31d654258_B_3_Float, float(1), _Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean);
            UnityTexture2DArray _Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_MappingTable);
            float4 _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4 = IN.uv0;
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[0];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_G_2_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[1];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_B_3_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[2];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_A_4_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[3];
            float _Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float;
            Unity_Subtract_float(_Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float, _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float, _Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float);
            float _Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float;
            Unity_Absolute_float(_Subtract_2d11b71bee934370b68fedc83062af7e_Out_2_Float, _Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float);
            float _Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float;
            Unity_Subtract_float(_Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float, _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float, _Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float);
            float _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float;
            Unity_Absolute_float(_Subtract_1317ae32cdf64a53ac6acfb00bf394c4_Out_2_Float, _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float);
            float _Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean;
            Unity_Comparison_LessOrEqual_float(_Absolute_201e9339a486444b849796a03e1085f3_Out_1_Float, _Absolute_dd7198872cab446885ba9ea2a0b1eefa_Out_1_Float, _Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean);
            float _Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float;
            Unity_Multiply_float_float(_Split_a8d1957c8fd4453686400eb31d654258_R_1_Float, 255, _Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float);
            float _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float;
            Unity_Round_float(_Multiply_0dbcd82b874041a4bed71fffb0423120_Out_2_Float, _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float);
            float _Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float;
            Unity_Multiply_float_float(_Split_a8d1957c8fd4453686400eb31d654258_G_2_Float, 255, _Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float);
            float _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float;
            Unity_Round_float(_Multiply_7d0c35813a68494b88bbf756e8a19f42_Out_2_Float, _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float);
            float _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float;
            Unity_Branch_float(_Comparison_cedabaa9c3fa472296e82dafee96b6a2_Out_2_Boolean, _Round_0accd3a9f0504274b58d72d2bf205c87_Out_1_Float, _Round_66945dc35e3f42388503a0ba244e34ea_Out_1_Float, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float);
            float _Add_cb7536069f014983b789b899b046cdd1_Out_2_Float;
            Unity_Add_float(_Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float, float(0.5), _Add_cb7536069f014983b789b899b046cdd1_Out_2_Float);
            float _Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float;
            Unity_Divide_float(_Add_cb7536069f014983b789b899b046cdd1_Out_2_Float, float(256), _Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float);
            float4 _Combine_151f632a12c04805a28fcc5e175b3cbc_RGBA_4_Vector4;
            float3 _Combine_151f632a12c04805a28fcc5e175b3cbc_RGB_5_Vector3;
            float2 _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2;
            Unity_Combine_float(_Divide_5f61b3723011437b9e1f298f669a4d21_Out_2_Float, float(0), float(0), float(0), _Combine_151f632a12c04805a28fcc5e175b3cbc_RGBA_4_Vector4, _Combine_151f632a12c04805a28fcc5e175b3cbc_RGB_5_Vector3, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2);
            float4 _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray.tex, _Property_673b0661b57c4ddd9c29a930022241b7_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(0) );
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_R_4_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_G_5_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_B_6_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_A_7_Float = _SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_RGBA_0_Vector4.a;
            float _Multiply_c3c15e8bfe914b07b98b2c48b02e5770_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_d7750213375a42bf9268d87d236cecc7_R_4_Float, 255, _Multiply_c3c15e8bfe914b07b98b2c48b02e5770_Out_2_Float);
            float _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float;
            Unity_Round_float(_Multiply_c3c15e8bfe914b07b98b2c48b02e5770_Out_2_Float, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float);
            float _Comparison_1fdd40ec77c94b85b238b762d9ed7cdf_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float, float(255), _Comparison_1fdd40ec77c94b85b238b762d9ed7cdf_Out_2_Boolean);
            UnityTexture2DArray _Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_Fallbacks);
            float4 _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray.tex, _Property_3c75e953ff764640bdc45733627d9418_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(0) );
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_R_4_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_G_5_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_B_6_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_A_7_Float = _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4.a;
            UnityTexture2DArray _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainAlbedoArray);
            float4x4 _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4 = _WorldToLocal;
            float3 _TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3;
            TransformPositionToVolumeSpace_float(IN.WorldSpacePosition, _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4, _TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3);
            float _Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float = _Tiling;
            float3 _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3;
            Unity_Multiply_float3_float3(_TransformPositionToVolumeSpaceCustomFunction_00d16ccb3994440289608bddd4d489b7_volumeLocalPos_2_Vector3, (_Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float.xxx), _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3);
            float2 _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xz;
            float4 _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_R_4_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_G_5_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_B_6_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_A_7_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.a;
            float2 _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.yz;
            float4 _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_R_4_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_G_5_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_B_6_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_A_7_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.a;
            float3 _TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3;
            TransformNormal_float(IN.WorldSpaceNormal, _Property_afc06409f24c43289f65687b68236683_Out_0_Matrix4, _TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3);
            float3 _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3;
            Unity_Absolute_float3(_TransformNormalCustomFunction_9d661e2462a641f5b517a83e82bd22cf_volumeLocalNormal_2_Vector3, _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3);
            float _Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float = _Blend;
            float3 _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3;
            Unity_Power_float3(_Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3, (_Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float.xxx), _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3);
            float3 _Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3;
            Unity_Add_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(0.001, 0.001, 0.001), _Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3);
            float _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float;
            Unity_DotProduct_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(1, 1, 1), _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float);
            float3 _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3;
            Unity_Divide_float3(_Add_9568d3ef0e354b9e8d488098645f602b_Out_2_Vector3, (_DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float.xxx), _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3);
            float _Split_3690e7172951494d811295287d62f6a9_R_1_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[0];
            float _Split_3690e7172951494d811295287d62f6a9_G_2_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[1];
            float _Split_3690e7172951494d811295287d62f6a9_B_3_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[2];
            float _Split_3690e7172951494d811295287d62f6a9_A_4_Float = 0;
            float4 _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4, _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4);
            float2 _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xy;
            float4 _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_R_4_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_G_5_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_B_6_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_A_7_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.a;
            float4 _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4, _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4);
            float4 _Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_1fdd40ec77c94b85b238b762d9ed7cdf_Out_2_Boolean, _SampleTexture2DArray_6d39bebb975b4569aaf7a54e6d0f3069_RGBA_0_Vector4, _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4, _Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4);
            UnityTexture2DArray _Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_MappingTable);
            float4 _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray.tex, _Property_22653d56e21a40b39033633512f18f2d_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(1) );
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_R_4_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_G_5_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_B_6_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_A_7_Float = _SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_RGBA_0_Vector4.a;
            float _Multiply_aed0d73afb104603b9218f3e986c52b1_Out_2_Float;
            Unity_Multiply_float_float(_SampleTexture2DArray_cf7882f6e1b949719b5dd3d2ed4898ed_R_4_Float, 255, _Multiply_aed0d73afb104603b9218f3e986c52b1_Out_2_Float);
            float _Round_7bf7643be96e45e7bc63501c211b5484_Out_1_Float;
            Unity_Round_float(_Multiply_aed0d73afb104603b9218f3e986c52b1_Out_2_Float, _Round_7bf7643be96e45e7bc63501c211b5484_Out_1_Float);
            float _Comparison_2dadcda8a25d4983b878c03ec3cfcc74_Out_2_Boolean;
            Unity_Comparison_Equal_float(_Round_7bf7643be96e45e7bc63501c211b5484_Out_1_Float, float(255), _Comparison_2dadcda8a25d4983b878c03ec3cfcc74_Out_2_Boolean);
            UnityTexture2DArray _Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_Fallbacks);
            float4 _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray.tex, _Property_c362c96ce17446e990fb02f1c1ee9ee0_Out_0_Texture2DArray.samplerstate, _Combine_151f632a12c04805a28fcc5e175b3cbc_RG_6_Vector2, float(2) );
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_R_4_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_G_5_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_B_6_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_4f8d39186b53448b829826b297910839_A_7_Float = _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4.a;
            UnityTexture2DArray _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_AltAlbedoArray);
            float4 _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.tex, _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_R_4_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_G_5_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_B_6_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_A_7_Float = _SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.tex, _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_R_4_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_G_5_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_B_6_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_A_7_Float = _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4.a;
            float4 _Lerp_0e74d01d58954dd3a62d1766879888ef_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_8d6f589071a74444816dd1ebe0c3a7c7_RGBA_0_Vector4, _SampleTexture2DArray_5b8bbc46400846f48f47ecabc3e01228_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_0e74d01d58954dd3a62d1766879888ef_Out_3_Vector4);
            float4 _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.tex, _Property_f7c871211b2e4d7394c73681910edabb_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Round_c6a9293928cb4c5488b6dbcb9c66085e_Out_1_Float );
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_R_4_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_G_5_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_B_6_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_A_7_Float = _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4.a;
            float4 _Lerp_d8444c08f4c24be981674285302358d0_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_0e74d01d58954dd3a62d1766879888ef_Out_3_Vector4, _SampleTexture2DArray_357d97f0143e4bb686dde134f878bdc8_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_d8444c08f4c24be981674285302358d0_Out_3_Vector4);
            float4 _Branch_ffd13beb31a34cdba80d0601903f0c4b_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_2dadcda8a25d4983b878c03ec3cfcc74_Out_2_Boolean, _SampleTexture2DArray_4f8d39186b53448b829826b297910839_RGBA_0_Vector4, _Lerp_d8444c08f4c24be981674285302358d0_Out_3_Vector4, _Branch_ffd13beb31a34cdba80d0601903f0c4b_Out_3_Vector4);
            float _Property_75980a93ffd2444fb44695ea95d01dd1_Out_0_Float = _StepLowEdge;
            float _Property_0f28291fbab94789b01ad35d1f7e6da3_Out_0_Float = _StepHighEdge;
            float _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float;
            Unity_DotProduct_float3(IN.WorldSpaceNormal, float3(0, 1, 0), _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float);
            float _Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float;
            Unity_Smoothstep_float(_Property_75980a93ffd2444fb44695ea95d01dd1_Out_0_Float, _Property_0f28291fbab94789b01ad35d1f7e6da3_Out_0_Float, _DotProduct_4dd7036845a4488091f8acbab509a002_Out_2_Float, _Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float);
            float4 _Lerp_ebe93a7ee03a42998c196045b35050a6_Out_3_Vector4;
            Unity_Lerp_float4(_Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4, _Branch_ffd13beb31a34cdba80d0601903f0c4b_Out_3_Vector4, (_Smoothstep_43d23f86665d4f9284b1571a89834c83_Out_3_Float.xxxx), _Lerp_ebe93a7ee03a42998c196045b35050a6_Out_3_Vector4);
            float4 _Branch_946f2ac7a40f4a4cacf6ba6bded44189_Out_3_Vector4;
            Unity_Branch_float4(_Comparison_19d0bcc0c0a84e34be73fed896de3175_Out_2_Boolean, _Lerp_ebe93a7ee03a42998c196045b35050a6_Out_3_Vector4, _Branch_fed0a9a4ad4d4da88aaa870003ae1605_Out_3_Vector4, _Branch_946f2ac7a40f4a4cacf6ba6bded44189_Out_3_Vector4);
            surface.BaseColor = (_Branch_946f2ac7a40f4a4cacf6ba6bded44189_Out_3_Vector4.xyz);
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
            // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
            float3 unnormalizedNormalWS = input.normalWS;
            const float renormFactor = 1.0 / length(unnormalizedNormalWS);
        
        
            output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
        
        
            output.WorldSpacePosition = input.positionWS;
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
            output.uv0 = input.texCoord0;
            output.VertexColor = input.color;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBR2DPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}