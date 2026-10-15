using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace GrassAndFur
{
    using static GrassAndFurDefinitions;

    /// <summary>
    /// Custom renderer feature for Grass And Fur downsampling - compatible with Unity URP 2021 (deprecated) and 2022 or NEWER.
    /// Written by Matej Vanco, May 2024. Updated on 15th of October 2026
    /// </summary>
    public sealed class GrassAndFurRFDownsampler : ScriptableRendererFeature
    {
        [SerializeField] private Settings settings = new Settings();
        private RenderPass renderPass;

        private readonly List<GrassAndFurMaster> indirectDrawingDataElements = new List<GrassAndFurMaster>();
        private readonly List<IndirectDrawingData> indirectDrawingData = new List<IndirectDrawingData>();

        public Settings RFSettings => settings;

        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent renderEvent = RenderPassEvent.BeforeRenderingTransparents;
            [Range(1f, 16f)] public float downsample = 2f;
            public bool renderInPrefabIsolation = false;

            public void SetDownsampleQualityToFull()
                => downsample = 1f;
            public void SetDownsampleQualityToHalf()
                => downsample = 2f;
            public void SetDownsampleQualityToQuarter()
                => downsample = 3f;
        }

        private readonly struct IndirectDrawingData
        {
            public readonly Matrix4x4 matrix;
            public readonly ComputeBuffer indirectArgs;
            public readonly Material gnfMaterial;

            public IndirectDrawingData(Matrix4x4 matrix, ComputeBuffer indirectArgs, Material gnfMaterial)
            {
                this.matrix = matrix;
                this.indirectArgs = indirectArgs;
                this.gnfMaterial = gnfMaterial;
            }
        }

        public override void Create()
        {
            renderPass = new RenderPass(settings);
        }

        public void EnqueueIndirectElement(GrassAndFurMaster element)
        {
            if (!indirectDrawingDataElements.Contains(element))
                indirectDrawingDataElements.Add(element);
        }

        protected override void Dispose(bool disposing)
        {
            renderPass?.Dispose();

            indirectDrawingDataElements.Clear();
            indirectDrawingData.Clear();
        }

#if UNITY_2022_1_OR_NEWER && !UNITY_6000_0_OR_NEWER
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
            => SetupRP(renderer);
#endif

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderPass == null)
                return;

            if (renderingData.cameraData.cameraType == CameraType.Game ||
                renderingData.cameraData.cameraType == CameraType.SceneView)
            {
#if UNITY_EDITOR
                if (!RFSettings.renderInPrefabIsolation &&
                    renderingData.cameraData.cameraType == CameraType.SceneView &&
                    UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage())
                    return;
#endif
#if !UNITY_2022_1_OR_NEWER
                SetupRP(renderer);
#endif

#if UNITY_6000_0_OR_NEWER
                SetupRP(renderer);
#endif
                renderer.EnqueuePass(renderPass);
            }
        }

        private void SetupRP(ScriptableRenderer renderer)
        {
            if (renderPass == null)
                return;

#if !UNITY_6000_0_OR_NEWER
            renderPass.ConfigureInput(ScriptableRenderPassInput.Color);
#endif

#if UNITY_2022_1_OR_NEWER && !UNITY_6000_0_OR_NEWER
            renderPass.SetRTs(renderer.cameraColorTargetHandle);
#endif
            indirectDrawingData.Clear();

            for (int i = indirectDrawingDataElements.Count - 1; i >= 0; i--)
            {
                if (indirectDrawingDataElements[i] == null || !indirectDrawingDataElements[i].UseDownsampleFeature || indirectDrawingDataElements[i].DownsampleRF == null)
                {
                    indirectDrawingDataElements.RemoveAt(i);
                    continue;
                }
                var data = indirectDrawingDataElements[i].CreateIndirectDrawingData;
                if (data.args == null || !data.args.IsValid() || data.gnfMat == null)
                {
                    indirectDrawingDataElements.RemoveAt(i);
                    continue;
                }
                indirectDrawingData.Add(new IndirectDrawingData(data.matrix, data.args, data.gnfMat));
            }

            renderPass.EnqueueDrawingData(indirectDrawingData);
        }

        private sealed class RenderPass : ScriptableRenderPass, System.IDisposable
        {
            private const string PROFILING_NAME = "GnF_RFProfiler";
#if UNITY_2022_1_OR_NEWER
            private const string BLIT_SHADER = "Hidden/Grass And Fur/GrassAndFurDownsampleBlitter";
#else
            private const string BLIT_SHADER = "Hidden/Grass And Fur/GrassAndFurDownsampleBlitterDeprecated";
#endif
            private const string BLIT_SHADER_PROP_TEXB = "_GnFFrame";

            private List<IndirectDrawingData> indirectDrawingData;

            private readonly Settings settings;
#if !UNITY_6000_0_OR_NEWER
            private readonly ProfilingSampler profiler;
#endif
            private Material blittableMaterial;

#if !UNITY_6000_0_OR_NEWER
    #if UNITY_2022_1_OR_NEWER
                    private RTHandle colorRT;
                    private RTHandle tempRT;
    #else
                    private RenderTargetHandle srcTempHandle;
    #endif
#endif

            public RenderPass(Settings cachedSettings)
            {
                settings = cachedSettings;
                renderPassEvent = cachedSettings.renderEvent;

#if !UNITY_6000_0_OR_NEWER
                profiler = new ProfilingSampler(PROFILING_NAME);
#endif

                AllocMat();

#if !UNITY_2022_1_OR_NEWER
                srcTempHandle.Init("TEMP_BLIT");
#endif
            }

            private void AllocMat()
            {
                var shader = Shader.Find(BLIT_SHADER);
                if (shader != null)
                    blittableMaterial = new Material(shader);
                else
                    Debug.LogError($"Shader '{BLIT_SHADER}' couldn't be found! Make sure the shader is included in the 'Always included shaders' in Graphics Settings!");
            }

            public void EnqueueDrawingData(List<IndirectDrawingData> data)
                => indirectDrawingData = data;

#if UNITY_2022_1_OR_NEWER && !UNITY_6000_0_OR_NEWER
            public void SetRTs(RTHandle handle)
                => colorRT = handle;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
                => ConfigureTarget(colorRT);

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                float downsample = Mathf.Clamp(settings.downsample, 1f, 16f);
                int width = Mathf.CeilToInt(cameraTextureDescriptor.width / downsample);
                int height = Mathf.CeilToInt(cameraTextureDescriptor.height / downsample);

                if (tempRT == null || tempRT.rt.width != width || tempRT.rt.height != height)
                {
                    cameraTextureDescriptor.width = width;
                    cameraTextureDescriptor.height = height;
                    cameraTextureDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;

                    RenderingUtils.ReAllocateIfNeeded(
                        ref tempRT,
                        cameraTextureDescriptor,
                        FilterMode.Bilinear,
                        TextureWrapMode.Clamp);

                    tempRT.rt.Release();
                    tempRT.rt.descriptor = cameraTextureDescriptor;
                    tempRT.rt.Create();
                }
            }
#endif


#if UNITY_6000_0_OR_NEWER

            private sealed class GraphPassData
            {
                internal TextureHandle sceneColorSrc;
                internal TextureHandle sceneColorTarget;
                internal TextureHandle sceneDepthTarget;
                internal Material blittableMaterial;
                internal List<IndirectDrawingData> indirectDrawingData;
                internal int lightCount;
                internal float downsampleValue;
            }

            private sealed class BlitPassData
            {
                internal TextureHandle source;
                internal Material blittableMaterial;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (blittableMaterial == null)
                    AllocMat();
                if (blittableMaterial == null || indirectDrawingData == null || indirectDrawingData.Count == 0)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();

                RenderTextureDescriptor mainSceneColorDesc = cameraData.cameraTargetDescriptor;
                mainSceneColorDesc.depthBufferBits = 0;

                TextureHandle gnfTex;

                using (var builder = renderGraph.AddRasterRenderPass<GraphPassData>(PROFILING_NAME, out var data))
                {
                    data.sceneColorSrc = resourceData.activeColorTexture;
                    builder.UseTexture(data.sceneColorSrc);

                    float downsample = Mathf.Clamp(settings.downsample, 1f, 16f);

                    int width = Mathf.FloorToInt(mainSceneColorDesc.width / downsample);
                    int height = Mathf.FloorToInt(mainSceneColorDesc.height / downsample);

                    var desc = mainSceneColorDesc;
                    desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm;
                    desc.width = width;
                    desc.height = height;

                    gnfTex = data.sceneColorTarget = UniversalRenderer.CreateRenderGraphTexture(
                        renderGraph,
                        desc,
                        BLIT_SHADER_PROP_TEXB,
                        true,
                        FilterMode.Bilinear,
                        TextureWrapMode.Clamp);

                    var depthDesc = desc;
                    depthDesc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
                    depthDesc.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.D32_SFloat;
                    depthDesc.depthBufferBits = 32;
                    data.sceneDepthTarget = UniversalRenderer.CreateRenderGraphTexture(
                        renderGraph,
                        depthDesc,
                        BLIT_SHADER_PROP_TEXB + "Depth",
                        true,
                        FilterMode.Point,
                        TextureWrapMode.Clamp);

                    data.blittableMaterial = blittableMaterial;
                    data.indirectDrawingData = indirectDrawingData;
                    data.lightCount = lightData.additionalLightsCount;
                    data.downsampleValue = downsample;

                    builder.SetRenderAttachment(data.sceneColorTarget, 0);
                    builder.SetRenderAttachmentDepth(data.sceneDepthTarget, AccessFlags.ReadWrite);

                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((GraphPassData graphData, RasterGraphContext context) => ExecuteGraphPass(graphData, context));
                }

                var source = gnfTex;
                var destination = resourceData.activeColorTexture;

                using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(PROFILING_NAME + "_Composite", out var data))
                {
                    data.source = source;
                    data.blittableMaterial = blittableMaterial;

                    builder.UseTexture(data.source);
                    builder.SetRenderAttachment(destination, 0);

                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) => ExecuteBlitGraphPass(data, context));
                }
            }

            private static void ExecuteGraphPass(GraphPassData data, RasterGraphContext context)
            {
                foreach (var dat in data.indirectDrawingData)
                {
                    if (dat.gnfMaterial == null)
                        continue;

                    dat.gnfMaterial.SetFloat(SHADER_MASTER_VISIBLE_ADDLIGHTS_COUNT, data.lightCount);
                    dat.gnfMaterial.SetFloat(SHADER_MASTER_DOWNSAMPLE_VALUE, data.downsampleValue);

                    context.cmd.DrawProceduralIndirect(
                        dat.matrix,
                        dat.gnfMaterial,
                        0,
                        MeshTopology.Triangles,
                        dat.indirectArgs);
                }

            }

            private static void ExecuteBlitGraphPass(BlitPassData data, RasterGraphContext context)
            {
                data.blittableMaterial.SetTexture(BLIT_SHADER_PROP_TEXB, data.source);
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.blittableMaterial, 0);
            }

#else
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (blittableMaterial == null)
                    AllocMat();
                if (blittableMaterial == null || indirectDrawingData == null || indirectDrawingData.Count == 0)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get(PROFILING_NAME);

                using (new ProfilingScope(cmd, profiler))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

#if UNITY_2022_1_OR_NEWER
                    cmd.SetRenderTarget(tempRT);
                    cmd.ClearRenderTarget(true, true, Color.clear);
#else
                    var desc = renderingData.cameraData.cameraTargetDescriptor;
                    float downsample = Mathf.Clamp(settings.downsample, 1f, 16f);

                    cmd.GetTemporaryRT(srcTempHandle.id,
                        Mathf.CeilToInt(desc.width / downsample), Mathf.CeilToInt(desc.height / downsample),
                        32, FilterMode.Bilinear, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm);

                    cmd.SetRenderTarget(srcTempHandle.id);
                    cmd.ClearRenderTarget(true, true, Color.clear);
#endif
                    foreach (var data in indirectDrawingData)
                    {
                        if (data.gnfMaterial == null)
                            continue;

                        data.gnfMaterial.SetFloat(SHADER_MASTER_VISIBLE_ADDLIGHTS_COUNT, renderingData.lightData.visibleLights.Length);
                        cmd.DrawProceduralIndirect(
                            data.matrix,
                            data.gnfMaterial,
                            0,
                            MeshTopology.Triangles,
                            data.indirectArgs);
                    }

#if UNITY_2022_1_OR_NEWER
                    blittableMaterial.SetTexture(BLIT_SHADER_PROP_TEXB, tempRT.rt);
                    Blitter.BlitCameraTexture(cmd, tempRT, colorRT, blittableMaterial, 0);
#else
                    cmd.SetGlobalTexture(BLIT_SHADER_PROP_TEXB, srcTempHandle.Identifier());
                    cmd.Blit(srcTempHandle.Identifier(), renderingData.cameraData.renderer.cameraColorTarget, blittableMaterial, 0);
#endif
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#endif

#if !UNITY_2022_1_OR_NEWER
            public override void FrameCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(srcTempHandle.id);
            }
#endif

            public void Dispose()
            {
                indirectDrawingData?.Clear();

#if UNITY_2022_1_OR_NEWER && !UNITY_6000_0_OR_NEWER
                tempRT?.Release();
#endif

                if (!Application.isPlaying)
                    DestroyImmediate(blittableMaterial);
                else
                    Destroy(blittableMaterial);
                blittableMaterial = null;
            }
        }
    }
}