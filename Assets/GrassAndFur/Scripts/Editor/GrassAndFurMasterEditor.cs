using UnityEngine;

using UnityEditor;

namespace GrassAndFur.UEditor
{
    using static GrassAndFurDefinitions;

    [CustomEditor(typeof(GrassAndFurMaster))]
    public sealed class GrassAndFurMasterEditor : Editor
    {
        public static bool preservePaintingEditor = false;

        private GrassAndFurMaster master;

        private bool skinLocalMotionVector;

        private float motionShellInfluence;
        private float motionIntensity;
        private float skinScale;

        private Texture2D tempTex;

        private void OnEnable()
        {
            master = (GrassAndFurMaster)target;

            skinLocalMotionVector = master.IsUsingSkinLocalMotionVectors;

            motionShellInfluence = master.MotionVectorShellInfluence;
            motionIntensity = master.MotionVectorIntensity;
            skinScale = master.SkinnedMeshScale;

            master.MasterInitialize();
        }

        private void OnDisable()
        {
            if(!preservePaintingEditor)
                GrassAndFurPaintingEditor.Disable();
            preservePaintingEditor = false;
        }

        public override void OnInspectorGUI()
        {
            if(!master.HasRenderer)
            {
                EditorGUILayout.HelpBox("The gameObject doesn't have a skinned mesh or mesh filter renderer!", MessageType.Error);
                return;
            }

            if (GUILayout.Button("Manually Initialize"))
                master.MasterInitialize();

            base.OnInspectorGUI();

            GUILayout.Space(10);

            if (master.TargetGnFMaterial)
            {
                GUILayout.Label("Downsample Feature", EditorStyles.boldLabel);
                GUILayout.BeginVertical("Box");
                EditorGUILayout.PropertyField(serializedObject.FindProperty("useDownsampleFeature"), new GUIContent("Use Downsample Feature"));
                if (master.UseDownsampleFeature && !master.TargetGnFMaterial.IsKeywordEnabled(SHADER_MASTER_DOWNSAMPLE_FEATURE))
                    master.TargetGnFMaterial.EnableKeyword(SHADER_MASTER_DOWNSAMPLE_FEATURE);
                else if (!master.UseDownsampleFeature && master.TargetGnFMaterial.IsKeywordEnabled(SHADER_MASTER_DOWNSAMPLE_FEATURE))
                    master.TargetGnFMaterial.DisableKeyword(SHADER_MASTER_DOWNSAMPLE_FEATURE);
                if (master.UseDownsampleFeature)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("downsampleRF"), new GUIContent("Downsample RF"));

                GUILayout.EndVertical();

                GUILayout.Space(10);
            }

            if(master.IsSkinnedMesh)
            {
                GUILayout.Label("Skin Settings", EditorStyles.boldLabel);

                GUILayout.BeginVertical("Box");
                skinScale = EditorGUILayout.FloatField("Skin Scale", skinScale);
                if (skinScale != master.SkinnedMeshScale)
                    master.MasterChangeSkinnedMeshScale(skinScale);

                GUILayout.Space(10);
                
                GUILayout.Label("Skin Local Motion Vector", EditorStyles.boldLabel);
                skinLocalMotionVector = EditorGUILayout.Toggle("Use Skin Local Motion Vector", skinLocalMotionVector);
                if (skinLocalMotionVector != master.IsUsingSkinLocalMotionVectors)
                    master.MasterSetSkinLocalMotionVector(skinLocalMotionVector);
                motionIntensity = EditorGUILayout.Slider("Motion Intensity", motionIntensity, 0f, 512f);
                if (motionIntensity != master.MotionVectorIntensity)
                    master.MasterChangeMotionVectorIntensity(motionIntensity);
                motionShellInfluence = EditorGUILayout.Slider("Motion Shell Influence", motionShellInfluence, 0.01f, 16f);
                if (motionShellInfluence != master.MotionVectorShellInfluence)
                    master.MasterChangeMotionVectorShellInfluence(motionShellInfluence);
                GUILayout.EndVertical();
            }

            GUILayout.Space(10);

            if (master.TargetGnFMaterial)
            {
                GUILayout.Label("Texture Painting", EditorStyles.boldLabel);
                GUILayout.BeginVertical("Box");

                if (!GrassAndFurPaintingEditor.EditorIsRunning)
                {
                    if (GUILayout.Button("Paint Mask Texture"))
                        GrassAndFurPaintingEditor.Enable(master, GrassAndFurPaintingEditor.PaintMode.Mask);
                    GUILayout.BeginHorizontal();
                    tempTex = (Texture2D)EditorGUILayout.ObjectField(tempTex, typeof(Texture2D), false);
                    if (GUILayout.Button("Load Existing Mask Texture"))
                        GrassAndFurPaintingEditor.PushTextureToMaster(GrassAndFurPaintingEditor.PaintMode.Mask, tempTex, master);
                    GUILayout.EndHorizontal();
                    if (master.CachedMaskTexture != null)
                    {
                        GUILayout.BeginHorizontal("Box");
                        if (GUILayout.Button("Remove Mask Texture", GUILayout.Width(180))
                        && EditorUtility.DisplayDialog("Are you sure?", "Are you sure to remove Mask Texture? There is no undo...", "Yes", "No"))
                        {
                            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(master.CachedMaskTexture)))
                                DestroyImmediate(master.CachedMaskTexture);
                            master.CachedMaskTexture = null;
                            master.MasterDispatchMaskTexture();
                            EditorUtility.SetDirty(master);
                        }
                        Color gcolor = GUI.color;
                        GUI.color = Color.white / 1.1f;
                        if (GUILayout.Button("Save Mask To Assets", GUILayout.Width(200))
                        && EditorUtility.DisplayDialog("Are you sure?", "Are you sure to save the mask texture to the assets? There is no undo...", "Yes", "No"))
                        {
                            AssetDatabase.CreateAsset(master.CachedMaskTexture, "Assets/maskTexture.asset");
                            AssetDatabase.Refresh();
                            EditorUtility.SetDirty(master);
                        }
                        GUI.color = gcolor;
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.Space(5);

                    if (GUILayout.Button("Paint Add Color Texture"))
                        GrassAndFurPaintingEditor.Enable(master, GrassAndFurPaintingEditor.PaintMode.AddColor);
                    GUILayout.BeginHorizontal();
                    tempTex = (Texture2D)EditorGUILayout.ObjectField(tempTex, typeof(Texture2D), false);
                    if (GUILayout.Button("Load Existing Color Texture"))
                        GrassAndFurPaintingEditor.PushTextureToMaster(GrassAndFurPaintingEditor.PaintMode.AddColor, tempTex, master);
                    GUILayout.EndHorizontal();
                    if (master.CachedAddColorTexture != null)
                    {
                        GUILayout.BeginHorizontal("Box");
                        if (GUILayout.Button("Remove Add Color Texture", GUILayout.Width(180))
                        && EditorUtility.DisplayDialog("Are you sure?", "Are you sure to remove Add Color Texture? There is no undo...", "Yes", "No"))
                        {
                            if(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(master.CachedAddColorTexture)))
                                DestroyImmediate(master.CachedAddColorTexture);
                            master.CachedAddColorTexture = null;
                            master.MasterDispatchAddColorTexture();
                            EditorUtility.SetDirty(master);
                        }
                        Color gcolor = GUI.color;
                        GUI.color = Color.white / 1.1f;
                        if (GUILayout.Button("Save Add Color To Assets", GUILayout.Width(200))
                        && EditorUtility.DisplayDialog("Are you sure?", "Are you sure to save the add color texture to the assets? There is no undo...", "Yes", "No"))
                        {
                            AssetDatabase.CreateAsset(master.CachedAddColorTexture, "Assets/addColorTexture.asset");
                            AssetDatabase.Refresh();
                            EditorUtility.SetDirty(master);
                        }
                        GUI.color = gcolor;
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.Space(5);

                    if (GUILayout.Button("Paint Style Texture"))
                        GrassAndFurPaintingEditor.Enable(master, GrassAndFurPaintingEditor.PaintMode.Style);
                    GUILayout.BeginHorizontal();
                    tempTex = (Texture2D)EditorGUILayout.ObjectField(tempTex, typeof(Texture2D), false);
                    if (GUILayout.Button("Load Existing Style Texture"))
                        GrassAndFurPaintingEditor.PushTextureToMaster(GrassAndFurPaintingEditor.PaintMode.Style, tempTex, master);
                    GUILayout.EndHorizontal();
                    if (master.CachedStyleTexture != null)
                    {
                        GUILayout.BeginHorizontal("Box");
                        if (GUILayout.Button("Remove Style Texture", GUILayout.Width(180))
                        && EditorUtility.DisplayDialog("Are you sure?", "Are you sure to remove Style Texture? There is no undo...", "Yes", "No"))
                        {
                            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(master.CachedStyleTexture)))
                                DestroyImmediate(master.CachedStyleTexture);
                            master.CachedStyleTexture = null;
                            master.MasterDispatchStyleTexture();
                            EditorUtility.SetDirty(master);
                        }
                        Color gcolor = GUI.color;
                        GUI.color = Color.white / 1.1f;
                        if (GUILayout.Button("Save Style To Assets", GUILayout.Width(200))
                        && EditorUtility.DisplayDialog("Are you sure?", "Are you sure to save the style texture to the assets? There is no undo...", "Yes", "No"))
                        {
                            AssetDatabase.CreateAsset(master.CachedStyleTexture, "Assets/styleTexture.asset");
                            AssetDatabase.Refresh();
                            EditorUtility.SetDirty(master);
                        }
                        GUI.color = gcolor;
                        GUILayout.EndHorizontal();
                    }
                }
                else
                {
                    if (GrassAndFurPaintingEditor.CurrentPaintMode == GrassAndFurPaintingEditor.PaintMode.AddColor)
                    {
                        var cc = master.CachedPaintingBrushData;
                        cc.color = EditorGUILayout.ColorField(master.CachedPaintingBrushData.color);
                        master.CachedPaintingBrushData = cc;
                    }
                    if (GUILayout.Button("Exit Painting"))
                        GrassAndFurPaintingEditor.Disable();
                }
                GUILayout.EndVertical();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}