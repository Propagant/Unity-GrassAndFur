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
*/

using System;
using System.Collections;

using UnityEngine;
using UnityEngine.Rendering;

namespace GrassAndFur
{
    using static GrassAndFurDefinitions;
    using static GrassAndFurUtilities;

    /// <summary>
    /// Master Grass And Fur shader controller for renderers.
    /// Written by Matej Vanco, April 2024.
    /// https://matejvanco.com
    /// </summary>
    [ExecuteAlways]
    public sealed class GrassAndFurMaster : MonoBehaviour, IDisposable
    {
        //Serialized

        [Space]
        [Space]
        [SerializeField, Range(1, MAX_DENSITY)] private int density = 32;
        [SerializeField, Range(MIN_OFFSET, 8.0f)] private float offset = 1.0f;
        [SerializeField] private Material targetGrassAndFurMaterial;
        [Space]
        [SerializeField] private GlobalModifierSettings globalModifierSettings;

        [SerializeField, HideInInspector] private Texture2D cachedMaskTexture;
        [SerializeField, HideInInspector] private Texture2D cachedAddColorTexture;
        [SerializeField, HideInInspector] private Texture2D cachedStyleTexture;

        [SerializeField, HideInInspector] private MeshFilter cachedTargetMeshFilter;
        [SerializeField, HideInInspector] private SkinnedMeshRenderer cachedTargetSkinnedMesh;
        [SerializeField, HideInInspector] private ComputeShader srcGnFComputeShader;
        [SerializeField, HideInInspector] private BrushData cachedPaintingBrushData = new BrushData(0.5f, 0.1f, 0f, 0f, Color.white);

        [SerializeField, HideInInspector] private float skinnedMeshScale = 1f;
        [SerializeField, HideInInspector] private bool useSkinLocalMotionVectors = false;
        [SerializeField, HideInInspector] private float motionVectorShellInfluence = 2;
        [SerializeField, HideInInspector] private float motionVectorIntensity = 128.0f;

        [SerializeField, HideInInspector] private GrassAndFurRFDownsampler downsampleRF;
        [SerializeField, HideInInspector] private bool useDownsampleFeature;

        // Properties

        public Material TargetGnFMaterial => targetGrassAndFurMaterial;
        public GrassAndFurRFDownsampler DownsampleRF { get => downsampleRF; set => downsampleRF = value; }
        public bool UseDownsampleFeature => useDownsampleFeature;
		public bool DownsampleFeatureIsValid => UseDownsampleFeature && downsampleRF != null && downsampleRF.isActive;
        public bool IsSkinnedMesh => cachedTargetSkinnedMesh != null;
        public SkinnedMeshRenderer CachedSkinnedMeshRenderer => cachedTargetSkinnedMesh;
        public Mesh CachedTargetMeshSrc 
        {
            get
            {
                if (cachedTargetSkinnedMesh != null)
                    return cachedTargetSkinnedMesh.sharedMesh;
                else if (cachedTargetMeshFilter != null)
                    return cachedTargetMeshFilter.sharedMesh;
                else
                {
                    Debug.LogError("Target Mesh Source is null!");
                    return null;
                }
            }
        }
        public bool HasRenderer => cachedTargetMeshFilter || cachedTargetSkinnedMesh;
        public Texture2D CachedMaskTexture { get => cachedMaskTexture; set => cachedMaskTexture = value; }
        public Texture2D CachedAddColorTexture { get => cachedAddColorTexture; set => cachedAddColorTexture = value; }
        public Texture2D CachedStyleTexture { get => cachedStyleTexture; set => cachedStyleTexture = value; }
        public BrushData CachedPaintingBrushData { get => cachedPaintingBrushData; set => cachedPaintingBrushData = value; }

        public float SkinnedMeshScale => skinnedMeshScale;
        public bool IsUsingSkinLocalMotionVectors => useSkinLocalMotionVectors;
        public float MotionVectorShellInfluence => motionVectorShellInfluence;
        public float MotionVectorIntensity => motionVectorIntensity;
        public GlobalModifierSettings CurrentGlobalModifierSettings => globalModifierSettings;

        public (Matrix4x4 matrix, ComputeBuffer args, Material gnfMat) CreateIndirectDrawingData
            => (transform.localToWorldMatrix, csArgs, targetGrassAndFurMaterial);

        public bool IsInitialized { get; private set; }
        public bool Is16BitMeshIndexFormat { get; private set; }
        public bool IsUsingTrackingFeature { get; private set; }
        public bool IsUsingExplosionsFeature { get; private set; }
        public bool IgnoreTextureDispatch { get; set; }

        // Consts

        private const int MAX_DENSITY = 128;
        private const float MIN_OFFSET = 1.0e-5f;

        // Privates

        private ComputeBuffer csInTriangles;
        private ComputeBuffer csOutTriangles;
        private ComputeBuffer csArgs;
        private ComputeBuffer csTrackingFeaturePass;

        private GraphicsBuffer csInSkinnedVertexBuffer;
        private ComputeBuffer csInSkinnedPreviousVertexBuffer;
        private GraphicsBuffer csInSkinnedIndiceBuffer;
        private GraphicsBuffer csInSkinnedUVBuffer;

        private LocalKeyword csLocalKeywords;

        private int csVertexBufferNormalsOffset;
        private int csVertexBufferUVOffset;
        private int csVertexBufferUVStreamIndex;

        private int csKernelID;
        private uint csThGroupX;
        private int triangleCount;

        private Bounds currentBounds;
        private Vector3 currentLocalBoundsSize;
        private TrackingData[] trackingDataElements;
        private ExplosionData[] explosionsData;
        private Vector4[] explosionsLocalPackedData_1;

        private RenderTexture trackingFeatureRT;
        private Material maskBlitterMaterial;
        private int maskBlitter_MotionTexClearPass;
        private int maskBlitter_MotionTexDirectSetPass;

        private Coroutine dynamicMotionBlitterCoroutine;
        private Coroutine explosionCoroutine;

        #region Editor Only

#if UNITY_EDITOR
        private void OnValidate()
        {
            if(gameObject.activeSelf && gameObject.activeInHierarchy && UnityEditor.Selection.activeGameObject == gameObject)
                MasterInitialize();
        }

        private void Reset()
        {
            TryGetComponent(out cachedTargetMeshFilter);
            TryGetComponent(out cachedTargetSkinnedMesh);
            srcGnFComputeShader = Resources.Load<ComputeShader>(CS_NAME);
        }
#endif

        #endregion

        #region Public

        public void MasterInitialize()
        {
            IsInitialized = false;
            MasterRender(true);

            if (!Application.isPlaying)
                return;
            if (!gameObject.activeSelf)
                return;

            if (!IsInitialized)
                return;

            if (IsSkinnedMesh && useSkinLocalMotionVectors)
                MasterSetSkinLocalMotionVector(useSkinLocalMotionVectors);

            IsUsingTrackingFeature = TargetGnFMaterial.IsKeywordEnabled(SHADER_MASTER_TRACKING_FEATURE);
            IsUsingExplosionsFeature = TargetGnFMaterial.IsKeywordEnabled(SHADER_MASTER_EXPLOSIONS_FEATURE);

            if (IsUsingExplosionsFeature)
            {
                explosionsData = new ExplosionData[SHADER_MASTER_MAX_EXPLOSIONS];
                explosionsLocalPackedData_1 = new Vector4[SHADER_MASTER_MAX_EXPLOSIONS];

                if (explosionCoroutine != null)
                    StopCoroutine(explosionCoroutine);
                explosionCoroutine = StartCoroutine(IEExplosionProcess());
            }

            if (IsUsingTrackingFeature)
            {
                if (maskBlitterMaterial == null)
                {
                    var shader = Resources.Load<Shader>(SHADER_MASKBLIT_NAME);
                    if (shader == null)
                    {
                        Debug.LogError($"Couldn't initialize tracking feature at runtime. Painting brush shader '{SHADER_MASKBLIT_NAME}' couldn't be found!");
                        return;
                    }
                    maskBlitterMaterial = new Material(shader);
                }

                maskBlitter_MotionTexClearPass = maskBlitterMaterial.FindPass(SHADER_MASKBLIT_PASS_MotionTex_Clear);
                maskBlitter_MotionTexDirectSetPass = maskBlitterMaterial.FindPass(SHADER_MASKBLIT_PASS_TrackingFeature_DirectSet);

                trackingFeatureRT = new RenderTexture(SHADER_MASTER_TEX_DEFAULT_SIZE, SHADER_MASTER_TEX_DEFAULT_SIZE, 0, 
                    UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, 0);

                RenderTexture temp = RenderTexture.GetTemporary(trackingFeatureRT.descriptor);
                RenderTexture.active = trackingFeatureRT;
                Graphics.Blit(trackingFeatureRT, temp, maskBlitterMaterial, maskBlitter_MotionTexClearPass);
                Graphics.Blit(temp, trackingFeatureRT);
                RenderTexture.ReleaseTemporary(temp);
                RenderTexture.active = null;

                if (dynamicMotionBlitterCoroutine != null)
                    StopCoroutine(dynamicMotionBlitterCoroutine);

                csTrackingFeaturePass = new ComputeBuffer(SHADER_MASKBLIT_TRACKING_DATA_MAX_ELEMENTS, SHADER_MASKBLIT_TRACKING_DATA_STRIDE);
                trackingDataElements = new TrackingData[SHADER_MASKBLIT_TRACKING_DATA_MAX_ELEMENTS];
                dynamicMotionBlitterCoroutine = StartCoroutine(IETrackingFeatureBlitter());
            }
        }


        public void MasterChangeDensity(int newDensity)
        {
            newDensity = Mathf.Clamp(newDensity, 1, MAX_DENSITY);
            density = newDensity;
            MasterInitialize();
        }

        public void MasterChangeOffset(float newOffset)
        {
            offset = Mathf.Max(MIN_OFFSET, newOffset);
            SetData();
        }

        public void MasterChangeMotionVectorShellInfluence(float newShellInfluence)
        {
            newShellInfluence = Mathf.Clamp(newShellInfluence, 0.01f, 16.0f);
            motionVectorShellInfluence = newShellInfluence;
            SetData();
        }

        public void MasterChangeMotionVectorIntensity(float newIntensity)
        {
            motionVectorIntensity = Mathf.Abs(newIntensity);
            SetData();
        }

        public void MasterChangeSkinnedMeshScale(float scale)
        {
            skinnedMeshScale = Mathf.Abs(scale);
            SetData();
        }

        public void MasterSetSkinLocalMotionVector(bool enable)
        {
            if (srcGnFComputeShader == null)
                return;
            useSkinLocalMotionVectors = enable;
            csLocalKeywords = new LocalKeyword(srcGnFComputeShader, CS_PROP_SKINFEATURE_LOCALMOTION);

            if (enable)
                srcGnFComputeShader.EnableKeyword(csLocalKeywords);
            else
                srcGnFComputeShader.DisableKeyword(csLocalKeywords);
        }


        public void MasterDispatchMaskTexture(Texture newMaskTex = null)
        {
            if (newMaskTex != null)
                TargetGnFMaterial.SetTexture(SHADER_MASTER_TEX_MAIN, newMaskTex);
            else
                TargetGnFMaterial.SetTexture(SHADER_MASTER_TEX_MAIN, CachedMaskTexture);
        }

        public void MasterDispatchAddColorTexture(Texture newColorTex = null)
        {
            if(newColorTex != null)
                TargetGnFMaterial.SetTexture(SHADER_MASTER_TEX_ADDCOL, newColorTex);
            else
                TargetGnFMaterial.SetTexture(SHADER_MASTER_TEX_ADDCOL, CachedAddColorTexture);
        }

        public void MasterDispatchStyleTexture(Texture newStyleTex = null)
        {
            if (newStyleTex != null)
                TargetGnFMaterial.SetTexture(SHADER_MASTER_TEX_STYLE, newStyleTex);
            else
                TargetGnFMaterial.SetTexture(SHADER_MASTER_TEX_STYLE, CachedStyleTexture);
        }


        public void MasterRender(bool setAndDispatchData = false)
        {
            if (srcGnFComputeShader == null)
            {
                Debug.LogError("Grass And Fur Compute Shader is null!");
                return;
            }
            if (targetGrassAndFurMaterial == null)
                return;
            if (CachedTargetMeshSrc == null)
                return;

            if (!IsInitialized)
                InitData();

            if (setAndDispatchData || (Application.isPlaying && IsSkinnedMesh))
                SetData();

            if (useDownsampleFeature && downsampleRF != null && downsampleRF.isActive)
            {
                downsampleRF.EnqueueIndirectElement(this);
                return;
            }

            Graphics.DrawProceduralIndirect(
             targetGrassAndFurMaterial,
             currentBounds,
             MeshTopology.Triangles,
             csArgs,
             castShadows: ShadowCastingMode.Off,
             layer: gameObject.layer);
        }

        public void Dispose()
        {
            csInTriangles?.Dispose();
            csInTriangles = null;

            csOutTriangles?.Dispose();
            csOutTriangles = null;

            csArgs?.Dispose();
            csArgs = null;

            csTrackingFeaturePass?.Dispose();
            csTrackingFeaturePass = null;

            csInSkinnedIndiceBuffer?.Dispose();
            csInSkinnedIndiceBuffer = null;

            csInSkinnedVertexBuffer?.Dispose();
            csInSkinnedVertexBuffer = null;

            csInSkinnedPreviousVertexBuffer?.Dispose();
            csInSkinnedPreviousVertexBuffer = null;

            csInSkinnedUVBuffer?.Dispose();
            csInSkinnedUVBuffer = null;

            if (trackingFeatureRT != null)
                trackingFeatureRT.Release();
            trackingFeatureRT = null;

            if (explosionCoroutine != null)
                StopCoroutine(explosionCoroutine);
            explosionCoroutine = null;

            if (dynamicMotionBlitterCoroutine != null)
                StopCoroutine(dynamicMotionBlitterCoroutine);
            dynamicMotionBlitterCoroutine = null;

            trackingDataElements = null;

            IsInitialized = false;
        }

        #region Modifiers

        public void MasterCreatePlanarTrack(Vector3 worldSpacePosition,
            float worldSpaceRadius,
            float shellCutWorldSpaceRadius,
            float worldSpaceRadiusSmoothness,
            float shellCutHeight = 0.1f)
            => MasterCreatePlanarTrack(new TrackingData(
                    worldSpacePosition, worldSpaceRadius, shellCutWorldSpaceRadius, worldSpaceRadiusSmoothness, shellCutHeight));
        
        public void MasterCreatePlanarTrack(TrackingData trackingData)
        {
            if (!IsInitialized)
                return;
            if (!IsUsingTrackingFeature)
                return;
            if (maskBlitterMaterial == null || trackingFeatureRT == null)
            {
                Debug.LogError("Couldn't modify dynamic motion texture! Some resources are null...");
                return;
            }

            for (int i = 0; i < SHADER_MASKBLIT_TRACKING_DATA_MAX_ELEMENTS; i++)
            {
                if (trackingDataElements[i].radius == 0 && trackingDataElements[i].shellCutRadius == 0)
                {
                    trackingDataElements[i] = trackingData;

                    trackingDataElements[i].uvCoords = 
                        ConvertWorldSpaceToUVSpace(trackingData.uvCoords, CachedTargetMeshSrc.bounds.max, transform, globalModifierSettings.invertUVCoords);

                    trackingDataElements[i].radius =
                        ConvertWorldSpaceToUVSpace(trackingDataElements[i].radius, currentLocalBoundsSize) * globalModifierSettings.MultiplyTrackingRadius;
                    trackingDataElements[i].shellCutRadius =
                        ConvertWorldSpaceToUVSpace(trackingDataElements[i].shellCutRadius, currentLocalBoundsSize) * globalModifierSettings.MultiplyTrackingRadius;
                    trackingDataElements[i].radiusSmoothness =
                        ConvertWorldSpaceToUVSpace(trackingDataElements[i].radiusSmoothness, currentLocalBoundsSize);
                    break;
                }
            }
        }

        public void MasterSimulatePlanarExplosion(Vector3 worldPosition,
            float explosionWorldRadius, 
            float explosionWorldRadiusBlendSmoothness, 
            float explosionIntensity01,
            float shockWaveDuration = 0.4f,
            float shellWiggleDuration = 1f,
            float explosionImpactCutIntensity = 0.1f,
            float shockWavePulsingSpeed = 5f,
            AnimationCurve explosionEasing = null,
            AnimationCurve wigglerEasing = null)
        {
            if (!IsInitialized)
            {
                Debug.LogError("Couldn't simulate an explosion! The component is not initialized...");
                return;
            }

            if (IsUsingExplosionsFeature)
                MasterSimulatePlanarExplosion(new ExplosionData(
                ConvertWorldSpaceToUVSpace(worldPosition, CachedTargetMeshSrc.bounds.max, transform, globalModifierSettings.invertUVCoords),
                ConvertWorldSpaceToUVSpace(explosionWorldRadius, currentLocalBoundsSize),
                ConvertWorldSpaceToUVSpace(explosionWorldRadiusBlendSmoothness, currentLocalBoundsSize),
                explosionIntensity01,
                shockWaveDuration,
                shellWiggleDuration,
                explosionImpactCutIntensity,
                shockWavePulsingSpeed,
                explosionEasing, wigglerEasing));
        }

        public void MasterSimulatePlanarExplosion(ExplosionData exploData)
        {
            if (!IsInitialized)
            {
                Debug.LogError("Couldn't simulate an explosion! The component is not initialized...");
                return;
            }

            if (!IsUsingExplosionsFeature)
                return;

            for (int i = 0; i < explosionsData.Length; i++)
            {
                if (explosionsData[i].time > 0)
                    continue;
                explosionsLocalPackedData_1[i] = new Vector4(
                    Time.time,
                    exploData.explosionUVSpaceRadiusBlendSmoothness,
                    exploData.explosionImpactCutIntensity,
                    exploData.shockWavePulsingSpeed);
                explosionsData[i] = exploData;
                break;
            }
        }

        #endregion

        #endregion

        #region Data Handling

        private void InitData()
        {
            Dispose();

            Mesh targetMesh = CachedTargetMeshSrc;
            if (targetMesh == null)
                return;

            Is16BitMeshIndexFormat = targetMesh.indexFormat == IndexFormat.UInt16;

            int[] mTriIndices = targetMesh.triangles;

            triangleCount = mTriIndices.Length / 3;

            if (IsSkinnedMesh)
            {
                if(!CachedTargetMeshSrc.isReadable)
                {
                    Debug.LogError($"Mesh '{CachedTargetMeshSrc.name}' on the current skinned mesh renderer is not set to Read/Write! Please enable 'Read/Write' on the asset!");
                    return;
                }

                csKernelID = srcGnFComputeShader.FindKernel(Is16BitMeshIndexFormat
                    ? CS_KERNELNAME_SKIN16B
                    : CS_KERNELNAME_SKIN32B);

                cachedTargetSkinnedMesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
                targetMesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
                targetMesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

                csVertexBufferNormalsOffset = targetMesh.GetVertexAttributeOffset(VertexAttribute.Normal);
                csVertexBufferUVOffset = targetMesh.GetVertexAttributeOffset(VertexAttribute.TexCoord0);
                csVertexBufferUVStreamIndex = targetMesh.GetVertexAttributeStream(VertexAttribute.TexCoord0);

                csInSkinnedVertexBuffer = cachedTargetSkinnedMesh.GetVertexBuffer();
                if(useSkinLocalMotionVectors)
                    csInSkinnedPreviousVertexBuffer = new ComputeBuffer(targetMesh.vertexCount, sizeof(float) * 3);
                csInSkinnedIndiceBuffer = targetMesh.GetIndexBuffer();
                csInSkinnedUVBuffer = targetMesh.GetVertexBuffer(csVertexBufferUVStreamIndex);
            }
            else
            {
                csKernelID = srcGnFComputeShader.FindKernel(CS_KERNELNAME_DEFAULT);

                csInTriangles = new ComputeBuffer(triangleCount, CS_STRIDE_TRIANGLEDATA, ComputeBufferType.Structured);

                Vector3[] mVerts = targetMesh.vertices;
                Vector3[] mNormals = targetMesh.normals;
                Vector2[] mUvs = targetMesh.uv;

                InTriangleData[] refTriangles = new InTriangleData[triangleCount];
                for (int i = 0; i < mTriIndices.Length; i += 3)
                {
                    refTriangles[i / 3] = new InTriangleData(
                        new VertexData(mVerts[mTriIndices[i]], mNormals[mTriIndices[i]], mUvs[mTriIndices[i]]),
                        new VertexData(mVerts[mTriIndices[i + 1]], mNormals[mTriIndices[i + 1]], mUvs[mTriIndices[i + 1]]),
                        new VertexData(mVerts[mTriIndices[i + 2]], mNormals[mTriIndices[i + 2]], mUvs[mTriIndices[i + 2]]));
                }

                csInTriangles.SetData(refTriangles);
            }

            csOutTriangles = new ComputeBuffer(triangleCount * density, CS_STRIDE_TRIANGLEDATA, ComputeBufferType.Structured);

            csArgs = new ComputeBuffer(1, sizeof(uint) * 4, ComputeBufferType.IndirectArguments);
            csArgs.SetData(new uint[]
            {
                (uint)(mTriIndices.Length * density),  // Vert Count Per Instance 
                1,  // Instance Count
                0,  // Start Vertex Index
                0   // Start Instance Index
            });

            srcGnFComputeShader.GetKernelThreadGroupSizes(csKernelID, out csThGroupX, out _, out _);

            IsInitialized = true;
        }

        private void SetData()
        {
            if (!IsInitialized)
                return;

            srcGnFComputeShader.SetFloat(CS_PROP_OFF, offset);
            srcGnFComputeShader.SetInt(CS_PROP_DENS, density);
            srcGnFComputeShader.SetInt(CS_PROP_TRISCOUNT, triangleCount);
            srcGnFComputeShader.SetFloat(CS_PROP_MOTION_INTENSITY, motionVectorIntensity);
            srcGnFComputeShader.SetFloat(CS_PROP_MOTION_SHELLINFLUENCE, motionVectorShellInfluence);

            if (IsSkinnedMesh)
            {
                if (csInSkinnedVertexBuffer == null || csInSkinnedIndiceBuffer == null || csInSkinnedUVBuffer == null)
                    return;
                if (useSkinLocalMotionVectors && csInSkinnedPreviousVertexBuffer == null)
                    return;

                srcGnFComputeShader.SetBuffer(csKernelID, CS_PROP_SKINVERTEXBUFFER, csInSkinnedVertexBuffer);
                srcGnFComputeShader.SetBuffer(csKernelID, CS_PROP_SKININDICEBUFFER, csInSkinnedIndiceBuffer);
                srcGnFComputeShader.SetBuffer(csKernelID, CS_PROP_SKINUVBUFFER, csInSkinnedUVBuffer);
                if (useSkinLocalMotionVectors && csInSkinnedPreviousVertexBuffer != null)
                    srcGnFComputeShader.SetBuffer(csKernelID, CS_PROP_SKINPREVIOUSVERTEXBUFFER, csInSkinnedPreviousVertexBuffer);

                srcGnFComputeShader.SetInt(CS_PROP_SKINVERTBUFFER_STRIDE, csInSkinnedVertexBuffer.stride);
                srcGnFComputeShader.SetInt(CS_PROP_SKINVERTBUFFER_UV_STRIDE, csInSkinnedUVBuffer.stride);
                srcGnFComputeShader.SetInt(CS_PROP_SKINVERTBUFFER_NORM_OFFSET, csVertexBufferNormalsOffset);
                srcGnFComputeShader.SetInt(CS_PROP_SKINVERTBUFFER_UV_OFFSET, csVertexBufferUVOffset);
                srcGnFComputeShader.SetFloat(CS_PROP_SKIN_SCALE, SkinnedMeshScale);

                srcGnFComputeShader.SetMatrix(CS_PROP_MATRIX, CachedSkinnedMeshRenderer.rootBone.localToWorldMatrix);
            }
            else
            {
                srcGnFComputeShader.SetBuffer(csKernelID, CS_PROP_INTRIS, csInTriangles);
                srcGnFComputeShader.SetMatrix(CS_PROP_MATRIX, transform.localToWorldMatrix);
            }

            srcGnFComputeShader.SetBuffer(csKernelID, CS_PROP_OUTTRIS, csOutTriangles);
            targetGrassAndFurMaterial.SetBuffer(CS_PROP_OUTTRIS, csOutTriangles);

            if (!IgnoreTextureDispatch)
            {
                MasterDispatchMaskTexture();
                MasterDispatchAddColorTexture();
                MasterDispatchStyleTexture();
            }

            if (!IsSkinnedMesh)
            {
                currentBounds = new Bounds(transform.position, CachedTargetMeshSrc.bounds.size + (Vector3.one * density));
                currentLocalBoundsSize = Vector3.Scale(transform.lossyScale, CachedTargetMeshSrc.bounds.size * 2f);
            }
            else
            {
                currentBounds = cachedTargetSkinnedMesh.bounds;
                currentLocalBoundsSize = Vector3.Scale(transform.lossyScale, CachedTargetMeshSrc.bounds.size * 2f);
            }

            DispatchData();
        }

        private void DispatchData()
        {
            if (IsInitialized && srcGnFComputeShader != null)
                srcGnFComputeShader.Dispatch(csKernelID, Mathf.CeilToInt((float)triangleCount / csThGroupX), 1, 1);
        }

        #endregion

        #region IEnumerators

        private IEnumerator IETrackingFeatureBlitter()
        {
            bool hasElements = false;

            while (true)
            {
#if UNITY_EDITOR
               while(UnityEditor.EditorApplication.isPaused)
                    yield return null;
#endif

                if (!IsInitialized)
                {
                    Debug.LogError("Couldn't modify tracking feature texture! The component is not initialized...");
                    yield break;
                }
                if (!IsUsingTrackingFeature)
                {
                    Debug.LogError("Couldn't modify tracking feature texture! The tracking feature texture must be initialized before playing...");
                    yield break;
                }
                if (maskBlitterMaterial == null || trackingFeatureRT == null)
                {
                    Debug.LogError("Couldn't modify tracking feature texture! Some resources are null...");
                    yield break;
                }

                for (int i = 0; i < SHADER_MASKBLIT_TRACKING_DATA_MAX_ELEMENTS; i++)
                {
                    if (trackingDataElements[i].radius > 0 && trackingDataElements[i].shellCutRadius > 0)
                        hasElements = true;
                }

                yield return null;

                if (hasElements)
                {
                    csTrackingFeaturePass.SetData(trackingDataElements);
                    maskBlitterMaterial.SetBuffer(SHADER_MASKBLIT_TRACKING_DATA, csTrackingFeaturePass);

                    RenderTexture temp = RenderTexture.GetTemporary(trackingFeatureRT.descriptor);
                    RenderTexture.active = trackingFeatureRT;
                    Graphics.Blit(trackingFeatureRT, temp, maskBlitterMaterial, maskBlitter_MotionTexDirectSetPass);
                    Graphics.Blit(temp, trackingFeatureRT);
                    RenderTexture.ReleaseTemporary(temp);
                    RenderTexture.active = null;

                    TargetGnFMaterial.SetTexture(SHADER_MASTER_TEX_TRACKING, trackingFeatureRT);

                    for (int i = 0; i < SHADER_MASKBLIT_TRACKING_DATA_MAX_ELEMENTS; i++)
                        trackingDataElements[i] = default;
                }

                hasElements = false;
            }
        }

        private IEnumerator IEExplosionProcess()
        {
            Vector4[] explosionsLocalPackedData_0 = new Vector4[SHADER_MASTER_MAX_EXPLOSIONS];

            while (true)
            {
                for (int i = 0; i < explosionsData.Length; i++)
                {
                    var explosion = explosionsData[i];
                    if (explosion.time <= 0)
                        continue;

                    float t = explosion.maxTime - explosion.time;

                    float expt = explosion.explosionEasing.Evaluate(Mathf.Clamp01(t / explosion.shockWaveDuration));
                    float wigt = explosion.wigglerEasing.Evaluate(Mathf.Clamp01(t / explosion.shellWiggleDuration));

                    Vector4 pData = new Vector4(explosion.uvSpaceCoords.x, explosion.uvSpaceCoords.y);
                    pData.z = explosion.explosionUVSpaceRadius * expt;
                    pData.w = explosion.explosionIntensity01 * globalModifierSettings.MultiplyExplosionIntensity * (1 - wigt);
                    explosionsLocalPackedData_0[i] = pData;

                    explosion.time -= Time.deltaTime;
                    if (explosion.time <= 0)
                        explosion.time = 0;

                    explosionsData[i] = explosion;
                }

                TargetGnFMaterial.SetVectorArray(SHADER_MASTER_EXPLOSIONS_DATA0_PROP, explosionsLocalPackedData_0);
                TargetGnFMaterial.SetVectorArray(SHADER_MASTER_EXPLOSIONS_DATA1_PROP, explosionsLocalPackedData_1);

                yield return null;
            }
        }

        #endregion

        #region Private UReflection

        private void LateUpdate()
        {
            if (IsSkinnedMesh && Application.isPlaying)
                return;

#if UNITY_EDITOR
            MasterRender(!Application.isPlaying);
#else
            MasterRender();
#endif
        }

        private void OnWillRenderObject()
        {
            if (IsSkinnedMesh && DownsampleFeatureIsValid && Application.isPlaying)
                MasterRender();
        }

        private void OnRenderObject()
        {
            if (IsSkinnedMesh && !DownsampleFeatureIsValid && Application.isPlaying)
                MasterRender();
        }

        private void OnEnable()
        {
            if (srcGnFComputeShader == null)
                return;

            if (!IsSkinnedMesh)
                MasterInitialize();
            else if(!Application.isPlaying)
                MasterInitialize();
        }

        private IEnumerator Start()
        {
            if (!IsSkinnedMesh || !Application.isPlaying)
                yield break;
            yield return null;
            MasterInitialize();
        }

        private void OnDisable()
        {
            Dispose();
        }

        #endregion
    }
}