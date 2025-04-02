using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace OceanRendering
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WaterGeometryRenderer : MonoBehaviour
    {
        [SerializeField] private Vector2 _size = new Vector2(1,1);
        [SerializeField] private int2 _resolution = new int2(2,2); // number of subdivisions

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _planeMesh;

        private List<Vector3> _vertices;
        private List<Vector2> _UVs;
        private List<Vector3> _normals;
        private List<int> _triangles;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        private void Start()
        {
            GenerateMeshData();

            _planeMesh = new Mesh();
            SetMeshData();
            _planeMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            _meshFilter.mesh = _planeMesh;
        }

        private void GenerateMeshData()
        {
            // Vertices, normals and UVs
            _vertices = new List<Vector3>();
            _UVs = new List<Vector2>();
            _normals = new List<Vector3>();
            
            float xVertexSep = _size.x / _resolution.x;
            float yVertexSep = _size.y / _resolution.y;

            for (int y = 0; y < _resolution.y + 1; y++)
            {
                for (int x = 0; x < _resolution.x + 1; x++)
                {
                    _vertices.Add(new Vector3(x * xVertexSep - _size.x / 2f, 0f, y * yVertexSep - _size.y / 2f));
                    _UVs.Add(new Vector2(x / (float)_resolution.x, y / (float)_resolution.y));
                    _normals.Add(Vector3.up);
                }
            }

            // Tris
            _triangles = new List<int>();
            for (int y = 0; y < _resolution.y; y++)
            {
                for (int x = 0; x < _resolution.x; x++)
                {
                    // vertex list index of the bottom left corner of current quad
                    int currentIndex = x + y * (_resolution.x + 1);

                    // top-left tri
                    _triangles.Add(currentIndex);
                    _triangles.Add(currentIndex + _resolution.x + 1);
                    _triangles.Add(currentIndex + _resolution.x + 2);
                    
                    // bottom-right tri
                    _triangles.Add(currentIndex);
                    _triangles.Add(currentIndex + _resolution.x + 2);
                    _triangles.Add(currentIndex + 1);
                }
            }
        }

        private void SetMeshData()
        {
            _planeMesh.Clear();
            _planeMesh.SetVertices(_vertices.ToArray());
            _planeMesh.SetTriangles(_triangles.ToArray(), 0);
            _planeMesh.SetUVs(0, _UVs);
            _planeMesh.SetNormals(_normals.ToArray());
        }
    }
}
