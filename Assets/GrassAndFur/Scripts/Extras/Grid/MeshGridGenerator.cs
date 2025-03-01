using UnityEngine;

namespace GrassAndFur.Extras
{
#if UNITY_EDITOR
    [ExecuteAlways]
#endif
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class MeshGridGenerator : MonoBehaviour
    {
        // Serialized

        [SerializeField, HideInInspector] private int gridQuadHalfCount = 1;
        [SerializeField, HideInInspector] private float gridSize = 1f;

        [SerializeField, HideInInspector] private MeshFilter meshFilter;
        [SerializeField, HideInInspector] private float[] generatedHeightsPerQuad;

        // Properties

        public int GridQuadHalfCount => gridQuadHalfCount;
        public int GridQuadCountTotal => GridQuadHalfCount * GridQuadHalfCount;
        public float GridSizePhysical => gridSize;
        public float[] GeneratedHeightsPerQuad => generatedHeightsPerQuad;

#if UNITY_EDITOR
        private void Reset()
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        private void Update()
        {
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;
        }
#endif

        public void RegenerateMeshQuadGrid()
        {
            int quadCount = gridQuadHalfCount * gridQuadHalfCount;

            generatedHeightsPerQuad = new float[quadCount];

            Vector3[] vertices = new Vector3[quadCount * 4];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] indices = new int[quadCount * 6];

            Vector3 center;
            Vector3 centerOffset = new Vector3(-1, 0, -1) * (gridSize * (gridQuadHalfCount - 1));

            Vector3 forward = Vector3.forward * gridSize;
            Vector3 right = Vector3.right * gridSize;
            float size2x = gridSize * 2f;

            float uvScale = 1f / gridQuadHalfCount;
            Vector2 uvScaleX = new Vector2(uvScale, 0);
            Vector2 uvScaleY = new Vector2(0, uvScale);

            for (int i = 0; i < quadCount; i++)
            {
                int column = i % gridQuadHalfCount;
                int row = Mathf.FloorToInt(i / gridQuadHalfCount);

                center = centerOffset + new Vector3(column, 0, row) * size2x;

                int vIndex = i * 4;
                vertices[vIndex] = center - right - forward;
                vertices[vIndex + 1] = center + right - forward;
                vertices[vIndex + 2] = center + right + forward;
                vertices[vIndex + 3] = center - right + forward;

                normals[vIndex] = Vector3.up;
                normals[vIndex + 1] = Vector3.up;
                normals[vIndex + 2] = Vector3.up;
                normals[vIndex + 3] = Vector3.up;

                Vector2 uv = new Vector2((float)column / gridQuadHalfCount, (float)row / gridQuadHalfCount);
                uvs[vIndex] = uv;
                uvs[vIndex + 1] = uv + uvScaleX;
                uvs[vIndex + 2] = uv + uvScaleX + uvScaleY;
                uvs[vIndex + 3] = uv + uvScaleY;

                int tIndex = i * 6;
                indices[tIndex] = vIndex + 2;
                indices[tIndex + 1] = vIndex + 1;
                indices[tIndex + 2] = vIndex + 0;
                indices[tIndex + 3] = vIndex + 0;
                indices[tIndex + 4] = vIndex + 3;
                indices[tIndex + 5] = vIndex + 2;
            }

            Mesh generatedMesh = new Mesh();
            generatedMesh.vertices = vertices;
            generatedMesh.triangles = indices;
            generatedMesh.normals = normals;
            generatedMesh.uv = uvs;
            generatedMesh.RecalculateBounds();
            generatedMesh.MarkDynamic();

            meshFilter.sharedMesh = generatedMesh;
            
            if (TryGetComponent(out GrassAndFurMaster grassAndFurMaster))
                grassAndFurMaster.MasterInitialize();
        }

        public void RegenerateMeshQuadHeights()
        {
            if(meshFilter.sharedMesh == null)
            {
                Debug.LogError("Shared mesh in mesh filter on MeshGridGenerator is null");
                return;
            }
            if (meshFilter.sharedMesh.vertices == null)
                return;

            int quadCount = gridQuadHalfCount * gridQuadHalfCount;

            if (generatedHeightsPerQuad == null || generatedHeightsPerQuad.Length != quadCount)
                return;

            Vector3[] vertices = meshFilter.sharedMesh.vertices;

            for (int i = 0; i < quadCount; i++)
            {
                int vIndex = i * 4;
                vertices[vIndex] = SetHeight(vIndex, i);
                vertices[vIndex + 1] = SetHeight(vIndex + 1, i);
                vertices[vIndex + 2] = SetHeight(vIndex + 2, i);
                vertices[vIndex + 3] = SetHeight(vIndex + 3, i);
            }

            meshFilter.sharedMesh.vertices = vertices;
            meshFilter.sharedMesh.RecalculateBounds();

            if (TryGetComponent(out GrassAndFurMaster grassAndFurMaster))
                grassAndFurMaster.MasterInitialize();

            Vector3 SetHeight(int vIndex, int quadIndex)
            {
                Vector3 v = vertices[vIndex];
                v.y = generatedHeightsPerQuad[quadIndex];
                return v;
            }
        }

        public void SnapVerticesOnNearestCollider()
        {
            if (meshFilter.sharedMesh == null)
            {
                Debug.LogError("Shared mesh in mesh filter on MeshGridGenerator is null");
                return;
            }
            if (meshFilter.sharedMesh.vertices == null)
                return;

            bool mColliderEnabled = false;
            if (TryGetComponent(out MeshCollider meshCollider))
            {
                mColliderEnabled = meshCollider.enabled;
                meshCollider.enabled = false;
            }

            Vector3[] newVerts = meshFilter.sharedMesh.vertices;
            for (int i = 0; i < newVerts.Length; i++)
            {
                if (Physics.Raycast(transform.TransformPoint(newVerts[i]), Vector3.down, out RaycastHit hit) && hit.collider)
                    newVerts[i] = transform.InverseTransformPoint(hit.point);
            }
            meshFilter.sharedMesh.vertices = newVerts;
            meshFilter.sharedMesh.RecalculateBounds();

            if (meshCollider)
                meshCollider.enabled = mColliderEnabled;

            if (TryGetComponent(out GrassAndFurMaster grassAndFurMaster))
                grassAndFurMaster.MasterInitialize();
        }

        public void ChangeGridHalfCount(int newHalfCount)
        {
            if (gridQuadHalfCount == newHalfCount)
                return;
            gridQuadHalfCount = Mathf.Clamp(newHalfCount, 1, 64);
            RegenerateMeshQuadGrid();
        }

        public void ChangeGridSize(float newSize)
        {
            if (gridSize == newSize)
                return;
            gridSize = Mathf.Max(newSize, 0);
            RegenerateMeshQuadGrid();
        }

        public void SetHeightOnIndex(int index, float worldSpaceHeightValue)
        {
            if (index < 0 || index >= gridQuadHalfCount * gridQuadHalfCount)
                return;
            generatedHeightsPerQuad[index] = worldSpaceHeightValue - transform.position.y;
            RegenerateMeshQuadHeights();
        }
    }
}