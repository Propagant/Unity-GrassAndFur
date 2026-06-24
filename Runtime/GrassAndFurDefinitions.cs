using System;
using System.Runtime.InteropServices;

using UnityEngine;

namespace GrassAndFur
{
    public static class GrassAndFurDefinitions
    {
        // Data

        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct VertexData
        {
            public Vector3 posWS;
            public Vector3 normalOS;
            public Vector2 uv;
            public float offset;

            public VertexData(Vector3 posWS, Vector3 normalOS, Vector3 uv)
            {
                this.posWS = posWS;
                this.normalOS = normalOS;
                this.uv = uv;
                offset = 0;
            }
        }

        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct InTriangleData
        {
            public VertexData v0;
            public VertexData v1;
            public VertexData v2;

            public InTriangleData(VertexData v0, VertexData v1, VertexData v2)
            {
                this.v0 = v0;
                this.v1 = v1;
                this.v2 = v2;
            }
        }

        [Serializable]
        public struct BrushData
        {
            public float radius;
            public float intensity;
            public float smoothness;
            public float height;
            public Color color;

            public BrushData(float radius, float intensity, float smoothness, float height, Color color)
            {
                this.radius = radius;
                this.intensity = intensity;
                this.smoothness = smoothness;
                this.height = height;
                this.color = color;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TrackingData
        {
            public Vector2 uvCoords;
            public float radius;
            public float shellCutRadius;
            public float radiusSmoothness;
            public float shellCutHeight;

            public readonly Vector2 dummy;

            public TrackingData(Vector3 worldSpacePlanarPosition, float radius, float shellCutRadius, float radiusSmoothness, float shellCutHeight)
            {
                uvCoords = new Vector2(worldSpacePlanarPosition.x, worldSpacePlanarPosition.z);
                this.radius = radius;
                this.shellCutRadius = shellCutRadius;
                this.radiusSmoothness = radiusSmoothness;
                this.shellCutHeight = shellCutHeight;

                dummy = Vector2.zero;
            }
        }

        [Serializable]
        public sealed class GlobalModifierSettings
        {
            private const float MIN_VAL = 0.1f;
            private const float MAX_VAL = 16.0f;

            [Tooltip("If enabled, the transformations from world space to uv space will be inverted (this is related to tracking feature and explosions feature)")]
            public bool invertUVCoords = false;
            [SerializeField, Range(MIN_VAL, MAX_VAL)] private float multiplyTrackingRadius = 1f;
            [SerializeField, Range(MIN_VAL, MAX_VAL)] private float multiplyExplosionIntensity = 1f;

            public float MultiplyTrackingRadius
            {
                get => multiplyTrackingRadius;
                set => multiplyTrackingRadius = Mathf.Clamp(value, MIN_VAL, MAX_VAL);
            }

            public float MultiplyExplosionIntensity
            {
                get => multiplyExplosionIntensity;
                set => multiplyExplosionIntensity = Mathf.Clamp(value, MIN_VAL, MAX_VAL);
            }
        }

        public struct ExplosionData
        {
            public Vector2 uvSpaceCoords;
            public float explosionUVSpaceRadius;
            public float explosionUVSpaceRadiusBlendSmoothness;
            public float explosionIntensity01;
            public float explosionImpactCutIntensity;
            public float shockWaveDuration;
            public float shockWavePulsingSpeed;
            public float shellWiggleDuration;
            public readonly AnimationCurve explosionEasing;
            public readonly AnimationCurve wigglerEasing;

            public float time;
            public float maxTime;

            public ExplosionData(Vector2 uvCoords, 
                float explosionUVSpaceRadius, float explosionUVSpaceRadiusBlendSmoothness, float explosionIntensity01,
                float shockWaveDuration = 0.4f, float shellWiggleDuration = 1f,
                float explosionImpactCutIntensity = 0.1f, float shockWavePulsingSpeed = 5f,
                AnimationCurve explosionEasing = null, AnimationCurve wigglerEasing = null)
            {
                uvSpaceCoords = uvCoords;
                this.explosionUVSpaceRadius = explosionUVSpaceRadius;
                this.explosionUVSpaceRadiusBlendSmoothness = explosionUVSpaceRadiusBlendSmoothness;
                this.explosionImpactCutIntensity = explosionImpactCutIntensity;
                this.explosionIntensity01 = explosionIntensity01;
                this.shockWaveDuration = shockWaveDuration;
                this.shockWavePulsingSpeed = shockWavePulsingSpeed;
                this.shellWiggleDuration = shellWiggleDuration;
                this.explosionEasing = explosionEasing ?? AnimationCurve.Linear(0, 0, 1, 1);
                this.wigglerEasing = wigglerEasing ?? AnimationCurve.Linear(0, 0, 1, 1);
                maxTime = time = Mathf.Max(shockWaveDuration, shellWiggleDuration);
            }
        }

        // Consts

        //Compute Shader (Mesh Builder)
        public const string CS_NAME = "GrassAndFurCompute";
        public const string CS_KERNELNAME_DEFAULT = "DefaultMeshBuilder";
        public const string CS_KERNELNAME_SKIN16B = "SkinnedMeshBuilder16Bits";
        public const string CS_KERNELNAME_SKIN32B = "SkinnedMeshBuilder32Bits";

        public const string CS_PROP_INTRIS = "_InTriangles";
        public const string CS_PROP_OUTTRIS = "_OutTriangles";
        public const string CS_PROP_DENS = "_Density";
        public const string CS_PROP_OFF = "_Offset";
        public const string CS_PROP_TRISCOUNT = "_TriangleCount";
        public const string CS_PROP_MATRIX = "_Matrix";

        public const string CS_PROP_SKINVERTEXBUFFER = "_InSkinnedVertexBuffer";
        public const string CS_PROP_SKINPREVIOUSVERTEXBUFFER = "_InSkinnedPreviousVertexPositions";
        public const string CS_PROP_SKININDICEBUFFER = "_InSkinnedIndiceBuffer";
        public const string CS_PROP_SKINUVBUFFER = "_InSkinnedUVBuffer";
        public const string CS_PROP_SKINVERTBUFFER_STRIDE = "_VertexBufferStride";
        public const string CS_PROP_SKINVERTBUFFER_NORM_OFFSET = "_VertexBufferNormalsOffset";
        public const string CS_PROP_SKINVERTBUFFER_UV_STRIDE = "_VertexBufferUVStride";
        public const string CS_PROP_SKINVERTBUFFER_UV_OFFSET = "_VertexBufferUVOffset";

        public const string CS_PROP_SKINFEATURE_LOCALMOTION = "SKIN_LOCAL_MOTION_VECTOR";
        public const string CS_PROP_MOTION_SHELLINFLUENCE = "_MotionShellInfluence";
        public const string CS_PROP_MOTION_INTENSITY = "_MotionIntensity";
        public const string CS_PROP_SKIN_SCALE = "_SkinScale";

        public const int CS_STRIDE_TRIANGLEDATA = sizeof(float) * 9 * 3;

        //Master Shader
        public const string SHADER_MASTER_TEX_MAIN = "_MaskTex";
        public const string SHADER_MASTER_TEX_ADDCOL = "_AddColorTex";
        public const string SHADER_MASTER_TEX_STYLE = "_StyleTex";
        public const string SHADER_MASTER_TEX_TRACKING = "_TrackingTex";
        public const int SHADER_MASTER_TEX_DEFAULT_SIZE = 256;
        public const int SHADER_MASTER_MAX_EXPLOSIONS = 8;
        public const string SHADER_MASTER_EXPLOSIONS_FEATURE = "USE_EXPLOSION";
        public const string SHADER_MASTER_EXPLOSIONS_DATA0_PROP = "_ExplosionsData0";
        public const string SHADER_MASTER_EXPLOSIONS_DATA1_PROP = "_ExplosionsData1";
        public const string SHADER_MASTER_VISIBLE_ADDLIGHTS_COUNT = "_VisibleAddLightCount";

        public const string SHADER_MASTER_DOWNSAMPLE_FEATURE = "DOWNSAMPLE";
        public const string SHADER_MASTER_TRACKING_FEATURE = "USE_TRACKING_FEATURE";

        public const string SHADER_MASTER_BRUSH_DATA0 = "_BlitterBrushData0";
        public const string SHADER_MASTER_BRUSH_DATA1 = "_BlitterBrushData1";

        //MaskBlitter Shader
        public const string SHADER_MASKBLIT_NAME = "GrassAndFurMaskBlitter";
        public const string SHADER_MASKBLIT_PASS_MaskTex_Additive = "MaskTex_Additive";
        public const string SHADER_MASKBLIT_PASS_MaskTex_DirectSet = "MaskTex_DirectSet";
        public const string SHADER_MASKBLIT_PASS_ColorTex_DirectSet = "ColorTex_DirectSet";
        public const string SHADER_MASKBLIT_PASS_MotionTex_Additive = "MotionTex_Additive";
        public const string SHADER_MASKBLIT_PASS_MotionTex_DirectSet = "MotionTex_DirectSet";
        public const string SHADER_MASKBLIT_PASS_MotionTex_Clear = "MotionTex_Clear";
        public const string SHADER_MASKBLIT_PASS_TrackingFeature_DirectSet = "TrackingFeature_DirectSet";
        public const int SHADER_MASKBLIT_TRACKING_DATA_MAX_ELEMENTS = 16;
        public const int SHADER_MASKBLIT_TRACKING_DATA_STRIDE = sizeof(float) * 8;
        public const string SHADER_MASKBLIT_TRACKING_DATA = "_TrackingData";
    }
}