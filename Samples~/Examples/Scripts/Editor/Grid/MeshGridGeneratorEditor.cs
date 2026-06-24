using UnityEngine;
using UnityEditor;

using GrassAndFur.Extras;

namespace GrassAndFur.UEditor
{
    [CustomEditor(typeof(MeshGridGenerator))]
    public sealed class MeshGridGeneratorEditor : Editor
    {
        private MeshGridGenerator grid;

        private bool editGridQuadCount = false;
        private int gridQuadHalfCount;
        private float gridSize;

        private void OnEnable()
        {
            grid = (MeshGridGenerator)target;
            gridQuadHalfCount = grid.GridQuadHalfCount;
            gridSize = grid.GridSizePhysical;
        }

        private void OnDisable()
        {
            MeshGridGeneratorPaintingEditor.Disable();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUILayout.Space(20);

            if (MeshGridGeneratorPaintingEditor.IsActive)
            {
                if (GUILayout.Button("Exit Grid Height Editor"))
                    MeshGridGeneratorPaintingEditor.Disable();
                return;
            }

            if (editGridQuadCount)
            {
                GUILayout.Label("Grid Half Quad Count");
                gridQuadHalfCount = (int)EditorGUILayout.Slider(gridQuadHalfCount, 1, 32);
                GUILayout.Label("Grid Size");
                gridSize = EditorGUILayout.Slider(gridSize, 0f, 32f);
                grid.ChangeGridHalfCount(gridQuadHalfCount);
                grid.ChangeGridSize(gridSize);
                GUILayout.Space(5);
                int quads = gridQuadHalfCount * gridQuadHalfCount;
                GUILayout.Label("Vertex Count: " + quads * 4);
                GUILayout.Label("Triangle Count: " + quads * 2);
                GUILayout.Label("Quad Count Total: " + quads);
                if (GUILayout.Button("Exit Grid Quads Editor"))
                    editGridQuadCount = false;
                GUILayout.Space(10);
            }
            else
            {
                if (!editGridQuadCount && GUILayout.Button("Edit Grid Quads"))
                {
                    if (EditorUtility.DisplayDialog("Double Check",
                "Are you sure to edit base grid quads? This will reset the grid heights", "Yes", "No"))
                    {
                        editGridQuadCount = true;
                        if (MeshGridGeneratorPaintingEditor.IsActive)
                            MeshGridGeneratorPaintingEditor.Disable();
                    }
                }

                if (GUILayout.Button("Edit Grid Quad Heights"))
                    MeshGridGeneratorPaintingEditor.Enable(grid);

                GUILayout.Space(5);

                if (!editGridQuadCount && GUILayout.Button("Snap Grid Quads On Nearest Collider"))
                {
                    if (EditorUtility.DisplayDialog("Double Check",
                "Are you sure to modify vertices of the grid quads? This will modify every vertice on the current grid", "Yes", "No"))
                        grid.SnapVerticesOnNearestCollider();
                }

                GUILayout.Space(5);
                if (!editGridQuadCount && GUILayout.Button("Reset Grid"))
                {
                    if (EditorUtility.DisplayDialog("Double Check",
                "Are you sure to reset the current grid? Only heights will be restored", "Yes", "No"))
                        grid.RegenerateMeshQuadGrid();
                }

                int totalQuads = gridQuadHalfCount * gridQuadHalfCount;
                GUILayout.Label("Grid Quad Count: " + totalQuads);
                GUILayout.Label("Grid Vertex Count: " + totalQuads * 4);
                GUILayout.Label("Grid Triangle Count: " + totalQuads * 2);
                GUILayout.Label("Grid Size: " + gridSize);
            }
        }
    }
}