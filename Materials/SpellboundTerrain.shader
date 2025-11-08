Shader "Spellbound/Spellbound Terrain"
{
    Properties
    {
        _Blend("Blend", Float) = 8
        _Tiling("Tiling", Float) = 0.05
        [NoScaleOffset]_TerrainAlbedoArray("TerrainAlbedoArray", 2DArray) = "" {}
        [NoScaleOffset]_TerrainMetalSmoothArray("TerrainMetalSmoothArray", 2DArray) = "" {}
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
             nointerpolation float4 color : COLOR;
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
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        
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
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        void Unity_Absolute_float3(float3 In, out float3 Out)
        {
            Out = abs(In);
        }
        
        void Unity_Power_float3(float3 A, float3 B, out float3 Out)
        {
            Out = pow(A, B);
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
            UnityTexture2DArray _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainAlbedoArray);
            float4 _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4 = IN.uv0;
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[0];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_G_2_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[1];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_B_3_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[2];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_A_4_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[3];
            float _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float = IN.VertexColor[0];
            float _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float = IN.VertexColor[1];
            float _Split_a8d1957c8fd4453686400eb31d654258_B_3_Float = IN.VertexColor[2];
            float _Split_a8d1957c8fd4453686400eb31d654258_A_4_Float = IN.VertexColor[3];
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
            float _Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float = _Tiling;
            float3 _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.WorldSpacePosition, (_Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float.xxx), _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3);
            float2 _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xz;
            float4 _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_R_4_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_G_5_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_B_6_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_A_7_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.a;
            float2 _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.yz;
            float4 _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_R_4_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_G_5_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_B_6_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_A_7_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.a;
            float3 _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3;
            Unity_Absolute_float3(IN.WorldSpaceNormal, _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3);
            float _Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float = _Blend;
            float3 _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3;
            Unity_Power_float3(_Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3, (_Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float.xxx), _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3);
            float _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float;
            Unity_DotProduct_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(1, 1, 1), _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float);
            float3 _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3;
            Unity_Divide_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, (_DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float.xxx), _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3);
            float _Split_3690e7172951494d811295287d62f6a9_R_1_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[0];
            float _Split_3690e7172951494d811295287d62f6a9_G_2_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[1];
            float _Split_3690e7172951494d811295287d62f6a9_B_3_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[2];
            float _Split_3690e7172951494d811295287d62f6a9_A_4_Float = 0;
            float4 _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4, _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4);
            float2 _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xy;
            float4 _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_R_4_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_G_5_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_B_6_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_A_7_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.a;
            float4 _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4, _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4);
            UnityTexture2DArray _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainMetalSmoothArray);
            float4 _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.tex, _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_R_4_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_G_5_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_B_6_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_A_7_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.tex, _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_R_4_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_G_5_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_B_6_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_A_7_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.a;
            float4 _Lerp_5e7471adc1004f0fbe7ba4b877c88637_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4, _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_5e7471adc1004f0fbe7ba4b877c88637_Out_3_Vector4);
            float4 _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.tex, _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_R_4_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_G_5_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_B_6_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_A_7_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.a;
            float4 _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_5e7471adc1004f0fbe7ba4b877c88637_Out_3_Vector4, _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4);
            float _Split_3264dfc20eb84c93971370a089767e2c_R_1_Float = _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4[0];
            float _Split_3264dfc20eb84c93971370a089767e2c_G_2_Float = _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4[1];
            float _Split_3264dfc20eb84c93971370a089767e2c_B_3_Float = _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4[2];
            float _Split_3264dfc20eb84c93971370a089767e2c_A_4_Float = _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4[3];
            surface.BaseColor = (_Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4.xyz);
            surface.NormalTS = IN.TangentSpaceNormal;
            surface.Emission = float3(0, 0, 0);
            surface.Metallic = _Split_3264dfc20eb84c93971370a089767e2c_R_1_Float;
            surface.Smoothness = _Split_3264dfc20eb84c93971370a089767e2c_A_4_Float;
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
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        
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
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        void Unity_Absolute_float3(float3 In, out float3 Out)
        {
            Out = abs(In);
        }
        
        void Unity_Power_float3(float3 A, float3 B, out float3 Out)
        {
            Out = pow(A, B);
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
            UnityTexture2DArray _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainAlbedoArray);
            float4 _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4 = IN.uv0;
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[0];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_G_2_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[1];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_B_3_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[2];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_A_4_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[3];
            float _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float = IN.VertexColor[0];
            float _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float = IN.VertexColor[1];
            float _Split_a8d1957c8fd4453686400eb31d654258_B_3_Float = IN.VertexColor[2];
            float _Split_a8d1957c8fd4453686400eb31d654258_A_4_Float = IN.VertexColor[3];
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
            float _Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float = _Tiling;
            float3 _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.WorldSpacePosition, (_Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float.xxx), _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3);
            float2 _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xz;
            float4 _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_R_4_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_G_5_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_B_6_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_A_7_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.a;
            float2 _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.yz;
            float4 _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_R_4_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_G_5_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_B_6_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_A_7_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.a;
            float3 _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3;
            Unity_Absolute_float3(IN.WorldSpaceNormal, _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3);
            float _Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float = _Blend;
            float3 _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3;
            Unity_Power_float3(_Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3, (_Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float.xxx), _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3);
            float _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float;
            Unity_DotProduct_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(1, 1, 1), _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float);
            float3 _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3;
            Unity_Divide_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, (_DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float.xxx), _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3);
            float _Split_3690e7172951494d811295287d62f6a9_R_1_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[0];
            float _Split_3690e7172951494d811295287d62f6a9_G_2_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[1];
            float _Split_3690e7172951494d811295287d62f6a9_B_3_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[2];
            float _Split_3690e7172951494d811295287d62f6a9_A_4_Float = 0;
            float4 _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4, _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4);
            float2 _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xy;
            float4 _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_R_4_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_G_5_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_B_6_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_A_7_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.a;
            float4 _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4, _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4);
            UnityTexture2DArray _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainMetalSmoothArray);
            float4 _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.tex, _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_R_4_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_G_5_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_B_6_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_A_7_Float = _SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4.a;
            float4 _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.tex, _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_R_4_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_G_5_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_B_6_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_A_7_Float = _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4.a;
            float4 _Lerp_5e7471adc1004f0fbe7ba4b877c88637_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_c0ccb36506234fbb9e3d602f86282808_RGBA_0_Vector4, _SampleTexture2DArray_8563222af2cc4f7cbeccf614f6cd8307_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_5e7471adc1004f0fbe7ba4b877c88637_Out_3_Vector4);
            float4 _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.tex, _Property_8d97dfe317724bcaa11d2f1e85ae95dc_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_R_4_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_G_5_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_B_6_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_A_7_Float = _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4.a;
            float4 _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_5e7471adc1004f0fbe7ba4b877c88637_Out_3_Vector4, _SampleTexture2DArray_36ebdea543024a01b4c694ef5c161a2f_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4);
            float _Split_3264dfc20eb84c93971370a089767e2c_R_1_Float = _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4[0];
            float _Split_3264dfc20eb84c93971370a089767e2c_G_2_Float = _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4[1];
            float _Split_3264dfc20eb84c93971370a089767e2c_B_3_Float = _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4[2];
            float _Split_3264dfc20eb84c93971370a089767e2c_A_4_Float = _Lerp_4c419025b66a4f3eac25f921069a9fac_Out_3_Vector4[3];
            surface.BaseColor = (_Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4.xyz);
            surface.NormalTS = IN.TangentSpaceNormal;
            surface.Emission = float3(0, 0, 0);
            surface.Metallic = _Split_3264dfc20eb84c93971370a089767e2c_R_1_Float;
            surface.Smoothness = _Split_3264dfc20eb84c93971370a089767e2c_A_4_Float;
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
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        
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
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        
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
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        
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
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TANGENT_WS
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
             float4 uv1 : TEXCOORD1;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 normalWS;
             float4 tangentWS;
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
             float3 TangentSpaceNormal;
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
             float3 normalWS : INTERP1;
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
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        
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
            float3 NormalTS;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            surface.NormalTS = IN.TangentSpaceNormal;
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
        
            
        
        
        
            output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
        
        
        
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
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        
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
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        void Unity_Absolute_float3(float3 In, out float3 Out)
        {
            Out = abs(In);
        }
        
        void Unity_Power_float3(float3 A, float3 B, out float3 Out)
        {
            Out = pow(A, B);
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
            UnityTexture2DArray _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainAlbedoArray);
            float4 _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4 = IN.uv0;
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[0];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_G_2_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[1];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_B_3_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[2];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_A_4_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[3];
            float _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float = IN.VertexColor[0];
            float _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float = IN.VertexColor[1];
            float _Split_a8d1957c8fd4453686400eb31d654258_B_3_Float = IN.VertexColor[2];
            float _Split_a8d1957c8fd4453686400eb31d654258_A_4_Float = IN.VertexColor[3];
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
            float _Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float = _Tiling;
            float3 _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.WorldSpacePosition, (_Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float.xxx), _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3);
            float2 _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xz;
            float4 _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_R_4_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_G_5_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_B_6_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_A_7_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.a;
            float2 _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.yz;
            float4 _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_R_4_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_G_5_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_B_6_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_A_7_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.a;
            float3 _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3;
            Unity_Absolute_float3(IN.WorldSpaceNormal, _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3);
            float _Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float = _Blend;
            float3 _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3;
            Unity_Power_float3(_Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3, (_Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float.xxx), _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3);
            float _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float;
            Unity_DotProduct_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(1, 1, 1), _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float);
            float3 _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3;
            Unity_Divide_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, (_DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float.xxx), _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3);
            float _Split_3690e7172951494d811295287d62f6a9_R_1_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[0];
            float _Split_3690e7172951494d811295287d62f6a9_G_2_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[1];
            float _Split_3690e7172951494d811295287d62f6a9_B_3_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[2];
            float _Split_3690e7172951494d811295287d62f6a9_A_4_Float = 0;
            float4 _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4, _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4);
            float2 _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xy;
            float4 _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_R_4_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_G_5_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_B_6_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_A_7_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.a;
            float4 _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4, _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4);
            surface.BaseColor = (_Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4.xyz);
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
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        
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
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        
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
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        void Unity_Absolute_float3(float3 In, out float3 Out)
        {
            Out = abs(In);
        }
        
        void Unity_Power_float3(float3 A, float3 B, out float3 Out)
        {
            Out = pow(A, B);
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
            UnityTexture2DArray _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainAlbedoArray);
            float4 _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4 = IN.uv0;
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[0];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_G_2_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[1];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_B_3_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[2];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_A_4_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[3];
            float _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float = IN.VertexColor[0];
            float _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float = IN.VertexColor[1];
            float _Split_a8d1957c8fd4453686400eb31d654258_B_3_Float = IN.VertexColor[2];
            float _Split_a8d1957c8fd4453686400eb31d654258_A_4_Float = IN.VertexColor[3];
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
            float _Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float = _Tiling;
            float3 _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.WorldSpacePosition, (_Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float.xxx), _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3);
            float2 _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xz;
            float4 _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_R_4_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_G_5_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_B_6_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_A_7_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.a;
            float2 _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.yz;
            float4 _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_R_4_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_G_5_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_B_6_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_A_7_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.a;
            float3 _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3;
            Unity_Absolute_float3(IN.WorldSpaceNormal, _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3);
            float _Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float = _Blend;
            float3 _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3;
            Unity_Power_float3(_Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3, (_Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float.xxx), _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3);
            float _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float;
            Unity_DotProduct_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(1, 1, 1), _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float);
            float3 _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3;
            Unity_Divide_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, (_DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float.xxx), _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3);
            float _Split_3690e7172951494d811295287d62f6a9_R_1_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[0];
            float _Split_3690e7172951494d811295287d62f6a9_G_2_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[1];
            float _Split_3690e7172951494d811295287d62f6a9_B_3_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[2];
            float _Split_3690e7172951494d811295287d62f6a9_A_4_Float = 0;
            float4 _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4, _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4);
            float2 _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xy;
            float4 _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_R_4_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_G_5_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_B_6_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_A_7_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.a;
            float4 _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4, _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4);
            surface.BaseColor = (_Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4.xyz);
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
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D_ARRAY(_TerrainMetalSmoothArray);
        SAMPLER(sampler_TerrainMetalSmoothArray);
        TEXTURE2D_ARRAY(_TerrainAlbedoArray);
        SAMPLER(sampler_TerrainAlbedoArray);
        
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
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        void Unity_Absolute_float3(float3 In, out float3 Out)
        {
            Out = abs(In);
        }
        
        void Unity_Power_float3(float3 A, float3 B, out float3 Out)
        {
            Out = pow(A, B);
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
            UnityTexture2DArray _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray = UnityBuildTexture2DArrayStruct(_TerrainAlbedoArray);
            float4 _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4 = IN.uv0;
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_R_1_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[0];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_G_2_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[1];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_B_3_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[2];
            float _Split_44d5bfe0ca154fe3b46e89dbc335a256_A_4_Float = _UV_d9d0b1f921d04d9792208331091bd732_Out_0_Vector4[3];
            float _Split_a8d1957c8fd4453686400eb31d654258_R_1_Float = IN.VertexColor[0];
            float _Split_a8d1957c8fd4453686400eb31d654258_G_2_Float = IN.VertexColor[1];
            float _Split_a8d1957c8fd4453686400eb31d654258_B_3_Float = IN.VertexColor[2];
            float _Split_a8d1957c8fd4453686400eb31d654258_A_4_Float = IN.VertexColor[3];
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
            float _Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float = _Tiling;
            float3 _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3;
            Unity_Multiply_float3_float3(IN.WorldSpacePosition, (_Property_f87d2573603e417eaf85659e4ec6023a_Out_0_Float.xxx), _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3);
            float2 _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xz;
            float4 _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_d64cbb936d3746ca99a954b6a7d1d565_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_R_4_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_G_5_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_B_6_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_A_7_Float = _SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4.a;
            float2 _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.yz;
            float4 _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_dd007626d1d740eeaa2a29d9fda70a8c_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_R_4_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_G_5_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_B_6_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_A_7_Float = _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4.a;
            float3 _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3;
            Unity_Absolute_float3(IN.WorldSpaceNormal, _Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3);
            float _Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float = _Blend;
            float3 _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3;
            Unity_Power_float3(_Absolute_094194bc00394f35809dbcc8b16b67aa_Out_1_Vector3, (_Property_00ee875f04c647c5b8b41e0fad8dc487_Out_0_Float.xxx), _Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3);
            float _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float;
            Unity_DotProduct_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, float3(1, 1, 1), _DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float);
            float3 _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3;
            Unity_Divide_float3(_Power_90252c4dd15645f9b7bb39152532570a_Out_2_Vector3, (_DotProduct_a845186bc324466090213cee57784f1b_Out_2_Float.xxx), _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3);
            float _Split_3690e7172951494d811295287d62f6a9_R_1_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[0];
            float _Split_3690e7172951494d811295287d62f6a9_G_2_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[1];
            float _Split_3690e7172951494d811295287d62f6a9_B_3_Float = _Divide_fe4d854d8eea41a78aa2d52fb159164a_Out_2_Vector3[2];
            float _Split_3690e7172951494d811295287d62f6a9_A_4_Float = 0;
            float4 _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4;
            Unity_Lerp_float4(_SampleTexture2DArray_fa8f76eed3fd4a46bb9272808806f4bb_RGBA_0_Vector4, _SampleTexture2DArray_b8bf9de695ad4e4eb696cc4b285c26f6_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_R_1_Float.xxxx), _Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4);
            float2 _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2 = _Multiply_78100c177a1d48e1976fc70c31b63407_Out_2_Vector3.xy;
            float4 _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4 = PLATFORM_SAMPLE_TEXTURE2D_ARRAY(_Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.tex, _Property_5dd74e5921fb4cc4a047052d4a37861e_Out_0_Texture2DArray.samplerstate, _Swizzle_ee38ded8142641ec85a87b825241a2a1_Out_1_Vector2, _Branch_61526934108c4936984ce0f31f1f2e14_Out_3_Float );
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_R_4_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.r;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_G_5_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.g;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_B_6_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.b;
            float _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_A_7_Float = _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4.a;
            float4 _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4;
            Unity_Lerp_float4(_Lerp_290c1d9c4cb64b02b474dd6224995ae1_Out_3_Vector4, _SampleTexture2DArray_b02235227cd34d979a73900a505eddf9_RGBA_0_Vector4, (_Split_3690e7172951494d811295287d62f6a9_B_3_Float.xxxx), _Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4);
            surface.BaseColor = (_Lerp_6d5efb7386c24058b77436625a1eae4d_Out_3_Vector4.xyz);
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