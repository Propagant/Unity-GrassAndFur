using UnityEngine;

using UnityEditor;

namespace GrassAndFur.UEditor
{
    using static GrassAndFurDefinitions;

    public sealed class GrassAndFurPaintingEditor : Editor
    {
        public static bool EditorIsRunning => editorRunning;
        public static PaintMode CurrentPaintMode => currentPaintMode;

        public enum PaintMode { Mask, AddColor, Style };

        private static bool editorRunning;

        private static bool mouseDown = false;
        private static bool ctrlDown = false;
        private static bool shiftDown = false;
        private static BrushData brushData;

        private static GrassAndFurMaster targetMaster;
        private static RenderTexture workingMaskRT;
        private static RenderTexture workingAddColorRT;
        private static RenderTexture workingStyleRT;
        private static Material brushMat;

        private static GameObject tempSkinnedMeshCollider;

        private static PaintMode currentPaintMode;

        private static Vector2 prevCoords;
        private static bool prevRegistered;
        private static bool hadMeshCollider;

        private static LayerMask prevLayer;
        private const int EDIT_LAYER = 4;

        public static void PushTextureToMaster(PaintMode paintMode, Texture2D existingTex, GrassAndFurMaster masterSender)
        {
            if (existingTex == null || masterSender == null)
                return;

            switch (paintMode)
            {
                case PaintMode.Mask:
                    masterSender.CachedMaskTexture = existingTex;
                    masterSender.MasterDispatchMaskTexture(existingTex);
                    break;

                case PaintMode.AddColor:
                    masterSender.CachedAddColorTexture = existingTex;
                    masterSender.MasterDispatchAddColorTexture(existingTex);
                    break;

                case PaintMode.Style:
                    masterSender.CachedStyleTexture = existingTex;
                    masterSender.MasterDispatchStyleTexture(existingTex);
                    break;
            }
        }

        public static void Enable(GrassAndFurMaster sender, PaintMode paintMode)
        {
            if (editorRunning)
                return;
            editorRunning = true;

            targetMaster = sender;

            prevLayer = targetMaster.gameObject.layer;
            targetMaster.gameObject.layer = EDIT_LAYER;

            brushData = targetMaster.CachedPaintingBrushData;
            targetMaster.IgnoreTextureDispatch = true;

            currentPaintMode = paintMode;
            brushMat = new Material(Resources.Load<Shader>(SHADER_MASKBLIT_NAME));

            switch(currentPaintMode)
            {
                case PaintMode.Mask:
                    workingMaskRT = new RenderTexture(SHADER_MASTER_TEX_DEFAULT_SIZE, SHADER_MASTER_TEX_DEFAULT_SIZE, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm, 0);
                    BlitExisting(targetMaster.CachedMaskTexture, workingMaskRT);
                    targetMaster.MasterDispatchMaskTexture(workingMaskRT);
                    break;

                case PaintMode.AddColor:
                    workingAddColorRT = new RenderTexture(SHADER_MASTER_TEX_DEFAULT_SIZE, SHADER_MASTER_TEX_DEFAULT_SIZE, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, 0);
                    BlitExisting(targetMaster.CachedAddColorTexture, workingAddColorRT);
                    targetMaster.MasterDispatchAddColorTexture(workingAddColorRT);
                    break;

                case PaintMode.Style:
                    workingStyleRT = new RenderTexture(SHADER_MASTER_TEX_DEFAULT_SIZE, SHADER_MASTER_TEX_DEFAULT_SIZE, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, 0);
                    BlitExisting(targetMaster.CachedStyleTexture, workingStyleRT);
                    if (targetMaster.CachedStyleTexture == null)
                        BlitClear(workingStyleRT, brushMat, SHADER_MASKBLIT_PASS_MotionTex_Clear);
                    targetMaster.MasterDispatchStyleTexture(workingStyleRT);
                    break;
            }

            if (targetMaster.IsSkinnedMesh)
            {
                if (tempSkinnedMeshCollider == null)
                    tempSkinnedMeshCollider = new GameObject(nameof(tempSkinnedMeshCollider));
                tempSkinnedMeshCollider.layer = targetMaster.gameObject.layer;
                tempSkinnedMeshCollider.transform.SetPositionAndRotation(targetMaster.transform.position, targetMaster.transform.rotation);

                Mesh tempMesh = new Mesh();
                targetMaster.CachedSkinnedMeshRenderer.BakeMesh(tempMesh);
                if (!tempSkinnedMeshCollider.TryGetComponent(out MeshCollider meshCollider))
                    meshCollider = tempSkinnedMeshCollider.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = tempMesh;
            }
            else
            {
                if (!targetMaster.TryGetComponent(out MeshCollider meshCollider))
                {
                    hadMeshCollider = false;
                    meshCollider = targetMaster.gameObject.AddComponent<MeshCollider>();
                    GrassAndFurMasterEditor.preservePaintingEditor = true;
                }
                else
                    hadMeshCollider = true;
                meshCollider.sharedMesh = targetMaster.CachedTargetMeshSrc;
            }

            SceneView.duringSceneGui += SceneView_duringSceneGui;

            static void BlitExisting(Texture2D entry, RenderTexture target)
            {
                if (entry == null)
                    return;
                RenderTexture.active = target;
                Graphics.Blit(entry, target);
                RenderTexture.active = null;
            }

            static void BlitClear(RenderTexture target, Material mat, string pass)
            {
                RenderTexture.active = target;
                Graphics.Blit(target, mat, mat.FindPass(pass));
                RenderTexture.active = null;
            }
        }

        public static void Disable()
        {
            if (!editorRunning)
                return;
            editorRunning = false;

            switch (currentPaintMode)
            {
                case PaintMode.Mask:
                    targetMaster.CachedMaskTexture = BlitToTex2D(workingMaskRT);
                    targetMaster.MasterDispatchMaskTexture();
                    break;

                case PaintMode.AddColor:
                    targetMaster.CachedAddColorTexture = BlitToTex2D(workingAddColorRT);
                    targetMaster.MasterDispatchAddColorTexture();
                    break;

                case PaintMode.Style:
                    targetMaster.CachedStyleTexture = BlitToTex2D(workingStyleRT);
                    targetMaster.MasterDispatchStyleTexture();
                    break;
            }

            if (tempSkinnedMeshCollider)
                DestroyImmediate(tempSkinnedMeshCollider);
            tempSkinnedMeshCollider = null;

            targetMaster.CachedPaintingBrushData = brushData;
            targetMaster.IgnoreTextureDispatch = false;
            targetMaster.gameObject.layer = prevLayer;

            targetMaster.TargetGnFMaterial.SetVector(SHADER_MASTER_BRUSH_DATA0, Vector4.zero);
            targetMaster.TargetGnFMaterial.SetVector(SHADER_MASTER_BRUSH_DATA1, Vector4.zero);

            workingMaskRT = null;
            workingAddColorRT = null;
            workingStyleRT = null;

            DestroyImmediate(brushMat);

            if (!hadMeshCollider && targetMaster.TryGetComponent(out MeshCollider meshCollider))
                DestroyImmediate(meshCollider);

            brushMat = null;
            MouseDown(false);

            EditorUtility.SetDirty(targetMaster);

            targetMaster = null;

            SceneView.duringSceneGui -= SceneView_duringSceneGui;

            static Texture2D BlitToTex2D(RenderTexture entry)
            {
                RenderTexture.active = entry;

                Texture2D newTex = new Texture2D(entry.width, entry.height,
                    entry.graphicsFormat,
                    0, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
                newTex.wrapMode = TextureWrapMode.Clamp;
                newTex.ReadPixels(new Rect(0, 0, entry.width, entry.height), 0, 0);
                newTex.Apply();

                RenderTexture.active = null;

                entry.Release();

                return newTex;
            }
        }

        private static void SceneView_duringSceneGui(SceneView obj)
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                MouseDown(true);
            else if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                MouseDown(false);

            bool inGUIArea = Event.current.mousePosition.x <= 260 && Event.current.mousePosition.y <= 230;

            if (!inGUIArea)
                SceneWork();

            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 250, 220));
            PS();
            PS();
            GUILayout.Label(currentPaintMode.ToString() + " Painting", EditorStyles.boldLabel);
            PS();
            DP("Radius", brushData.radius.ToString("0.00"), "CTRL");
            brushData.radius = GUILayout.HorizontalSlider(brushData.radius, 0, 1);
            GUILayout.Space(15);
            DP("Smoothness", brushData.smoothness.ToString("0.00"), "CTRL+ALT");
            brushData.smoothness = GUILayout.HorizontalSlider(brushData.smoothness, 0, 1);
            GUILayout.Space(15);
            PE();

            if (currentPaintMode == PaintMode.Mask)
            {
                PS();
                if (!shiftDown)
                {
                    DP("Intensity", brushData.intensity.ToString("0.00"), "ALT");
                    brushData.intensity = GUILayout.HorizontalSlider(brushData.intensity, 0.001f, 1);
                    GUILayout.Space(15);
                }
                else
                {
                    DP("Height", brushData.height.ToString("0.00"), "SHIFT");
                    brushData.height = GUILayout.HorizontalSlider(brushData.height, 0, 1);
                    GUILayout.Space(15);
                }
                PE();

                GUILayout.Space(5);
                PS();
                GUILayout.Label("Hold LSHIFT to set height", EditorStyles.miniBoldLabel);
                GUILayout.Label("Hold LCTRL to negate intensity", EditorStyles.miniBoldLabel);
                PE();
            }
            else
            {
                PS();
                DP("Intensity", brushData.intensity.ToString("0.00"), "ALT");
                brushData.intensity = GUILayout.HorizontalSlider(brushData.intensity, 0.001f, 1);
                GUILayout.Space(15);
                PE();

                if (currentPaintMode == PaintMode.Style)
                {
                    GUILayout.Space(5);
                    PS();
                    GUILayout.Label("Hold LSHIFT to revert", EditorStyles.miniBoldLabel);
                    PE();
                }
            }

            PE();
            PE();
            GUILayout.EndArea();
            Handles.EndGUI();

            static void PS()
                => GUILayout.BeginVertical("Box");
            static void PE()
                => GUILayout.EndVertical();
            static void DP(string pre, string val, string sh)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(pre + ": ");
                GUILayout.Label(val, EditorStyles.boldLabel);
                GUILayout.Label($"[{sh}]", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }

        private static void SceneWork()
        {
            if (Application.isPlaying || !editorRunning)
                return;

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            Tools.current = Tool.None;

            if (Event.current.type == EventType.ScrollWheel)
            {
                if (shiftDown)
                {
                    // This is somehow switched in different versions lol
#if UNITY_2022_3_OR_NEWER
                    brushData.height -= Event.current.delta.x * 0.01f;
#else
                    brushData.height -= Event.current.delta.y * 0.01f;
#endif
                    brushData.height = Mathf.Clamp(brushData.height, 0.0f, 1f);
                    Event.current.Use();
                }
                else if (Event.current.control && Event.current.alt)
                {
                    brushData.smoothness -= Event.current.delta.y * 0.01f;
                    brushData.smoothness = Mathf.Clamp(brushData.smoothness, 1.0e-5f, 1f);
                    Event.current.Use();
                }
                else if (Event.current.control)
                {
                    brushData.radius -= Event.current.delta.y * 0.01f;
                    brushData.radius = Mathf.Clamp(brushData.radius, 1.0e-4f, 1f);
                    Event.current.Use();
                }
                else if (Event.current.alt)
                {
                    brushData.intensity -= Event.current.delta.y * 0.01f;
                    brushData.intensity = Mathf.Clamp(brushData.intensity, 1.0e-4f, 1f);
                    Event.current.Use();
                }
            }

            RaycastWork();

            ctrlDown = Event.current.control;
            shiftDown = Event.current.shift;
        }

        private static void RaycastWork()
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance:Mathf.Infinity, layerMask: 1 << targetMaster.gameObject.layer) && hit.collider)
            {
                Vector2 coords = hit.textureCoord;
                targetMaster.TargetGnFMaterial.SetVector(SHADER_MASTER_BRUSH_DATA0, new Vector4(coords.x, coords.y, brushData.radius, brushData.smoothness));
                if(currentPaintMode == PaintMode.Mask)
                    targetMaster.TargetGnFMaterial.SetVector(SHADER_MASTER_BRUSH_DATA1, new Vector4(1,1,1,
                        !shiftDown ? brushData.intensity : brushData.height));
                else if(currentPaintMode == PaintMode.AddColor)
                    targetMaster.TargetGnFMaterial.SetVector(SHADER_MASTER_BRUSH_DATA1, 
                        new Vector4(targetMaster.CachedPaintingBrushData.color.r, targetMaster.CachedPaintingBrushData.color.g, targetMaster.CachedPaintingBrushData.color.b,
                        brushData.intensity));
                else
                    targetMaster.TargetGnFMaterial.SetVector(SHADER_MASTER_BRUSH_DATA1, Vector4.one);

                if (!mouseDown)
                    return;

                if (!prevRegistered)
                {
                    prevCoords = hit.textureCoord;
                    prevRegistered = true;
                }

                string pass = "";
                RenderTexture targetRT = null;
                float intensity = brushData.intensity;

                switch (currentPaintMode)
                {
                    case PaintMode.Mask:
                        pass = shiftDown ? SHADER_MASKBLIT_PASS_MaskTex_DirectSet : SHADER_MASKBLIT_PASS_MaskTex_Additive;
                        targetMaster.MasterDispatchMaskTexture(workingMaskRT);
                        targetRT = workingMaskRT;
                        intensity *= 0.1f;
                        break;

                    case PaintMode.AddColor:
                        pass = SHADER_MASKBLIT_PASS_ColorTex_DirectSet;
                        targetMaster.MasterDispatchAddColorTexture(workingAddColorRT);
                        targetRT = workingAddColorRT;
                        intensity *= 0.1f;
                        break;

                    case PaintMode.Style:
                        pass = shiftDown ? SHADER_MASKBLIT_PASS_MotionTex_DirectSet : SHADER_MASKBLIT_PASS_MotionTex_Additive;
                        targetMaster.MasterDispatchStyleTexture(workingStyleRT);
                        targetRT = workingStyleRT;
                        break;
                }

                brushMat.SetVector("_Point", hit.textureCoord);
                brushMat.SetVector("_Dir", prevCoords - hit.textureCoord);
                brushMat.SetFloat("_Radius", brushData.radius);
                brushMat.SetFloat("_Intensity", intensity * (ctrlDown ? -1 : 1));
                brushMat.SetFloat("_Smoothness", brushData.smoothness);
                brushMat.SetFloat("_Height", brushData.height);
                brushMat.SetColor("_Color", targetMaster.CachedPaintingBrushData.color);

                RenderTexture temp = RenderTexture.GetTemporary(targetRT.descriptor);
                RenderTexture.active = targetRT;
                Graphics.Blit(targetRT, temp, brushMat, brushMat.FindPass(pass));
                Graphics.Blit(temp, targetRT);
                RenderTexture.ReleaseTemporary(temp);
                RenderTexture.active = null;

                prevCoords = hit.textureCoord;
            }
            else
                targetMaster.TargetGnFMaterial.SetVector(SHADER_MASTER_BRUSH_DATA0, Vector4.zero);
        }

        private static void MouseDown(bool state)
        {
            if(state)
                prevRegistered = false;
            mouseDown = state;
        }
    }
}
