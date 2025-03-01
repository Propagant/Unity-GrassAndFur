using UnityEngine;

using UnityEditor;

using GrassAndFur.Extras;

namespace GrassAndFur.UEditor
{
    public sealed class MeshGridGeneratorPaintingEditor : Editor
    {
        public static bool IsActive { get; private set; }

        private static MeshGridGenerator gridMaster;

        private static float currentHeight;
        private static bool shiftDown;
        private static bool mouseDown;
        private static bool showGizmos = true;

        private static readonly Color innerRectColor = new Color(0.2f, 0.6f, 0.9f, 0.1f);
        private static readonly Color heightInnerRectColor = new Color(0.2f, 0.3f, 0.8f, 0.15f);

        private static readonly Vector2 mouseRectSize = new Vector2(10, 10);
        private static readonly Vector2 gridQuadRectSize = new Vector2(100, 100);

        public static void Enable(MeshGridGenerator targetGrid)
        {
            if (IsActive)
                return;

            IsActive = true;
            gridMaster = targetGrid;

            currentHeight = gridMaster.transform.position.y;

            SceneView.duringSceneGui += SceneView_duringSceneGui;
        }

        public static void Disable()
        {
            if (!IsActive)
                return;

            IsActive = false;
            currentHeight = 0;

            gridMaster = null;
            mouseDown = false;
            shiftDown = false;

            SceneView.duringSceneGui -= SceneView_duringSceneGui;
        }


        private static void SceneView_duringSceneGui(SceneView obj)
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                mouseDown = true;
            else if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                mouseDown = false;

            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 250, 220));

            GUILayout.BeginVertical("Box");
            GUILayout.Label("Mesh Grid Quad-Height Painting");
            GUILayout.BeginVertical("Box");
            GUILayout.Label("Current Height (WP): " + currentHeight.ToString("0.00"), EditorStyles.boldLabel);
            GUILayout.Label("Use LSHIFT+SCROLL to change height", EditorStyles.miniBoldLabel);
            GUILayout.Label("Use LCTRL to reset height value", EditorStyles.miniBoldLabel);
            showGizmos = EditorGUILayout.Toggle("Show Gizmos", showGizmos);
            GUILayout.EndVertical();
            GUILayout.EndVertical();

            GUILayout.EndArea();
            Handles.EndGUI();

            SceneWork();
        }

        private static void SceneWork()
        {
            if (Application.isPlaying || !IsActive)
                return;

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            Tools.current = Tool.None;

            if (Event.current.type == EventType.ScrollWheel)
            {
                if (shiftDown)
                {
#if UNITY_2022_1_OR_NEWER
                    currentHeight -= Event.current.delta.x * 0.05f;
#else
                    currentHeight -= Event.current.delta.y * 0.05f;
#endif
                    Event.current.Use();
                }
            }

            shiftDown = Event.current.shift;

            if (Event.current.control)
                currentHeight = gridMaster.transform.position.y;

            var view = SceneView.currentDrawingSceneView;

            Vector3 center;
            Vector3 heightOffset = Vector3.up * gridMaster.transform.position.y;
            Vector3 worldPos = gridMaster.transform.position;
            worldPos.y = 0;
            Vector3 centerOffset = worldPos + new Vector3(-1, 0, -1) * (gridMaster.GridSizePhysical * (gridMaster.GridQuadHalfCount - 1)) + heightOffset;
            float gridSize2x = gridMaster.GridSizePhysical * 2f;
            Rect mouseRect = new Rect(Event.current.mousePosition - mouseRectSize / 2f, mouseRectSize);
            Vector3 localUp = Vector3.up * currentHeight;

            bool casted = false;
            float distToECamera = 1f;
            bool hasViewAndCamera = view != null && view.camera != null;

            // Per each quad
            for (int i = 0; i < gridMaster.GridQuadHalfCount * gridMaster.GridQuadHalfCount; i++)
            {
                int column = i % gridMaster.GridQuadHalfCount;
                int row = Mathf.FloorToInt(i / gridMaster.GridQuadHalfCount);

                Vector3 localQuadHeight = Vector3.up * gridMaster.GeneratedHeightsPerQuad[i];

                center = centerOffset + new Vector3(column, 0, row) * gridSize2x;
                Vector3 quadWorldPosition = center + localQuadHeight;

                if (hasViewAndCamera && view.camera.WorldToViewportPoint(quadWorldPosition).z <= 0)
                    continue;

                if (hasViewAndCamera)
                    distToECamera = Vector3.Distance(center, view.camera.transform.position) / (gridSize2x * 2);
               
                Vector2 calculatedGridRectSize = gridQuadRectSize / distToECamera;
                Rect worldToGuiRect = new Rect(HandleUtility.WorldToGUIPoint(quadWorldPosition) - (calculatedGridRectSize / 2f), calculatedGridRectSize);

                if (showGizmos)
                {
                    Handles.BeginGUI();
                    GUI.Box(worldToGuiRect, "");
                    Handles.EndGUI();
                }

                if (!casted && mouseRect.Overlaps(worldToGuiRect))
                {
                    Handles.DrawSolidRectangleWithOutline(new Vector3[4] // Current height rect
                    {
                        center + localQuadHeight + new Vector3(-0.5f, 0, -0.5f) * gridSize2x,
                        center + localQuadHeight + new Vector3(0.5f, 0, -0.5f) * gridSize2x,
                        center + localQuadHeight + new Vector3(0.5f, 0, 0.5f) * gridSize2x,
                        center + localQuadHeight + new Vector3(-0.5f, 0, 0.5f) * gridSize2x
                    }, innerRectColor, Color.white * 0.1f);

                    Handles.DrawSolidRectangleWithOutline(new Vector3[4] // Editor height rect
                    {
                        center + (new Vector3(-0.5f, 0, -0.5f) * gridSize2x) + localUp - heightOffset,
                        center + (new Vector3(0.5f, 0, -0.5f) * gridSize2x) + localUp - heightOffset,
                        center + (new Vector3(0.5f, 0, 0.5f) * gridSize2x) + localUp - heightOffset,
                        center + (new Vector3(-0.5f, 0, 0.5f) * gridSize2x) + localUp - heightOffset
                    }, heightInnerRectColor, mouseDown ? Color.white : Color.clear);

                    if (mouseDown)
                        gridMaster.SetHeightOnIndex(i, currentHeight);

                    casted = true;
                }
            }
        }
    }
}
