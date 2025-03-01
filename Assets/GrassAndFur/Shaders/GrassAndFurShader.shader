Shader "Matej Vanco/Grass And Fur Shader"
{
    /*
    Copyright 2024/2025 Matej Vanco (https://matejvanco.com)

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////

    Opaque Alpha-Cutout "Grass and Fur" shell-based shader

    Feature set:
    - Custom color, mask and style textures
    - Wind texture
    - Dynamic tracking feature (blittable from extern source)
    - Advanced shell settings (curvature, gravity, local motion vectors)
    - Traditional Lambertian shading + cascaded shadow map sampling
    - Realtime point/spot/directional lights support (per-pixel + per-object)
    - Async explosions feature (up to 8)
    - Skinned meshes support
    - Downsampling feature support through renderer features
    - URP Batcher compatible
    - URP Fog compatible
    - Forward/Deferred rendering path compatible

    Tech limitations:
    - No WebGL support
    - No GPU Instancing
    - No per-vertex lighting calculation
    - No depth & shadow writing
    - No specular highlights/reflections/SSS
    - No HDRP/BuiltIn support
    - Not compatible with Forward+ rendering path

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////
    */

    Properties
    {
        [Header(Base Settings)][Space]
        [KeywordEnum(Off,Front,Back)]_Culling("Culling", Float) = 0
        _ColorTint("Color Tint", Color) = (1., 1., 1., 1.)
        _ColorAmbient("Ambient Color", Color) = (0., 0., 0., 1.)
        [Space]
        _TopColor("Top Color", Color) = (0.02, 0.9, 0.1, 1.0)
        _BotColor("Bottom Color", Color) = (0., 0., 0., 0.)
        _ColTrans("Color Transition", Range(0., 1.)) = 0.5

        [Header(Texture Settings)][Space]
        [SHOWSCALEOFFSET]_AlbedoTex("Albedo Color Texture", 2D) = "white"{}
        [SHOWSCALEOFFSET]_MainTex("Main Shell Texture", 2D) = "white"{}
        [Space]
        [SHOWSCALEOFFSET]_WindTex("Wind Texture", 2D) = "white"{}
        _WindIntens("Wind Intensity", Range(0.0, 1.0)) = 0.01
        _WindSpeed("Wind Speed", Range(0.0, 2.0)) = 0.2
        [Space]
        [NoScaleOffset]_MaskTex("Mask Texture", 2D) = "black"{}
        [Space]
        [NoScaleOffset]_AddColorTex("Add Albedo Texture", 2D) = "black"{}
        _AddAlbTrans("Add Albedo Transition", Range(0., 1.)) = 0.5
        _AddAlbOpac("Add Albedo Opacity", Range(0., 1.)) = 0.0
        [HDR]_AddAlbedoTint("Add Albedo Tint", Color) = (1,1,1,1)
        [Toggle(ADD_ALBEDO_INVERT)]_InvertAddAlbedo("Invert Shell Offset Add Albedo", Float) = 0
        [Toggle(ADD_ALBEDO_MULTI)]_MultipAddAlbedo("Multiply Add Albedo", Float) = 0

        [Header(Styling And Realtime Tracking Feature)][Space]
        [Toggle(USE_STYLE_TEX)]_UseStyleTexParam("Use Style Texture", Float) = 1
        [SHOWIF_1_UseStyleTexParam]_StyleTex("Style Texture", 2D) = "bump" {}
        [Toggle(USE_TRACKING_FEATURE)]_UseTrackingFeature("Use Realtime Tracking Feature", Float) = 0
        [HideInInspector]_TrackingTex("", 2D) = "bump" {}

        [Header(Shell Settings)][Space]
        _MainTexCut("Main Shell Cut", Range(0., 10.)) = 1.
        [Toggle(CUT_EXPO)]_ExpoShellCut("Exponential Shell Cut", Float) = 1
        _ShellOffsetInfluence("Shell Offset Influence", Range(0.0, 6.0)) = 2.2
        _ShellMotionIntensity("Shell Motion & Style Intensity", Range(0.0, 1.0)) = 0.5
        [Toggle(SHELL_ADVANCED)]_ShellAdvancedSettings("Shell Advanced Settings", Float) = 0
        [SHOWIF_1_ShellAdvancedSettings]_ShellGravity("Shell Gravity", Float) = 0
        [SHOWIF_1_ShellAdvancedSettings]_ShellCurvatureIntens("Shell Curvature Intensity", Float) = 0
        [SHOWIF_1_ShellAdvancedSettings]_ShellCurvatureFreq("Shell Curvature Frequency", Float) = 0
        [Toggle(DISTANCE_FADE)]_UseDistanceFade("Use Distance Fadeout", Float) = 0
        [SHOWIF_1_UseDistanceFade]_FadeOutDist("Fadeout Distance", Float) = 10
        [SHOWIF_1_UseDistanceFade]_FadeOutDistSmh("Fadeout Blend", Float) = 2

        [Header(Shading Settings)][Space]
        _ShdCoverage("Shading Coverage", Range(-1., 1.)) = 0.5
        _ShdSmh("Shading Smoothness", Range(0., 1.)) = 1.0
        _ShdTint("Shading Tint", Color) = (0.5, 0.5, 0.5, 1.0)
        _ShadowOpac("Received Shadow Opacity", Range(0., 1.)) = 0.8

        [Header(Planar Explosion Feature)][Space]
        [Toggle(USE_EXPLOSION)]_UseExplo("Use Explosions Feature", Float) = 0
        [SHOWIF_1_UseExplo]_ExploWaveCount("Explosion Wave Count", Float) = 8.0
        [SHOWIF_1_UseExplo]_ExploWaveOffset("Explosion Wave Time-Offset", Float) = 4.0

        [HideInInspector]_BlitterBrushData0("", Vector) = (0,0,0,0) // Brush UV coords = xy, uv-space radius = z, uv-space radius smoothness = w
        [HideInInspector]_BlitterBrushData1("", Vector) = (0,0,0,0) // Brush color = rgb, brush intensity = w
        [HideInInspector]_VisibleAddLightCount("", Float) = 0       // Downsample-required
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType"="Plane"
        }

        Cull [_Culling]

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM

            #pragma target 3.0

            #pragma vertex VertexStage
            #pragma fragment FragStage

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fog

            #pragma shader_feature_local ADD_ALBEDO_INVERT
            #pragma shader_feature_local ADD_ALBEDO_MULTI
            #pragma shader_feature_local CUT_EXPO
            #pragma shader_feature_local USE_STYLE_TEX
            #pragma shader_feature_local USE_TRACKING_FEATURE
            #pragma shader_feature_local USE_EXPLOSION
            #pragma shader_feature_local SHELL_ADVANCED
            #pragma shader_feature_local DISTANCE_FADE
            #pragma shader_feature_local DOWNSAMPLE

            // URP only for now:)
            #define UNITY_URP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#ifdef DOWNSAMPLE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#endif

            #include "GrassAndFurInput.hlsl"
            #include "GrassAndFurCommon.hlsl"
            #include "GrassAndFurVertFrag.hlsl"

            ENDHLSL
        }
    }
    CustomEditor "GrassAndFur.UEditor.CustomMaterialEditor"
}
