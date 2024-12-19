/* MIT License
 * Copyright 2023 Gerold Schneider
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the “Software”), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace Readymade.Building.Components
{
    /// <summary>
    /// The results of a spline evaluation at a specific position.
    /// </summary>
    public struct SplineSample
    {
        /// <summary>
        /// Whether this struct was assigned non-default values (in case the default values are to be considered non-default).
        /// </summary>
        public bool IsSet;

        /// <summary>
        /// The sampled position;
        /// </summary>
        public float3 Position;

        /// <summary>
        /// The sampled/calculated tangent vector;
        /// </summary>
        public float3 Tangent;

        /// <summary>
        /// The sampled/calculated bi-tangent vector;
        /// </summary>
        public float3 BiTangent;

        /// <summary>
        /// The sampled up direction;
        /// </summary>
        public float3 Up;
    }

    /// <summary>
    /// A component for creating an extruded mesh from prototype mesh and a Spline at runtime. This is an experimental
    /// implementation and should not be considered overly robust.
    /// </summary>
    /// <remarks>
    ///<para>
    /// For performance reasons this class will sample the source splines at a fixed resolution (i.e. <see cref="SampleCount"/> samples) which
    /// serve as buckets into which all vertices of the resulting mesh will be sorted. This results in varying degrees of
    /// inaccuracy on the result mesh. However the start and end position will always be correct and not based on the discrete
    /// buckets in order to avoid gaps and glitches with adjacent meshes.</para>
    /// <para>Extrusions should ideally be short enough and shapes simple enough that the <see cref="SampleCount"/> provides sufficient detail.</para>
    /// <para>The array used for sampling is statically allocated and shared by all extruders to avoid unnecessary memory
    /// pressure. Therefore the same sample count cannot be changes per instance.</para>
    /// <para>
    /// The implementation is inspired by and partially derived from <see cref="SplineExtrude"/>.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Splines/Spline Extrude Shape")]
    public class SplineExtrudeShape : MonoBehaviour
    {
        private static readonly VertexAttributeDescriptor[] k_PipeVertexAttribs =
        {
            new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new(VertexAttribute.TexCoord0, dimension: 2)
        };

        public const int SampleCount = 256;

        private static SplineSample[] s_samples = new SplineSample[SampleCount];

        [SerializeField, Tooltip("The Spline to extrude.")]
        private SplineContainer m_Container;

        [SerializeField, Tooltip("Enable to regenerate the extruded mesh when the target Spline is modified. Disable " +
             "this option if the Spline will not be modified at runtime.")]
        private bool m_RebuildOnSplineChange;

        [SerializeField, Tooltip("The maximum number of times per-second that the mesh will be rebuilt.")]
        private int m_RebuildFrequency = 30;

        [SerializeField,
         Tooltip("Automatically update any Mesh, Box, or Sphere collider components when the mesh is extruded.")]
#pragma warning disable 414
        private bool m_UpdateColliders = true;
#pragma warning restore 414

        [SerializeField, Tooltip("The shape to extrude along the spline.")]
        private Mesh m_Shape;

        [SerializeField, Tooltip("The rotation offset to apply to the shape.")]
        private Quaternion m_RotationOffset;
        
        [SerializeField, Tooltip("The position offset to apply to the shape.")]
        private Vector3 m_PositionOffset;

        [SerializeField, Tooltip("The scale offset to apply to the shape.")]
        private Vector3 m_ScaleOffset;

        [SerializeField]
        [Tooltip("Whether to create a new Mesh instance or update the existing one. If disabled, the Mesh will be " +
            "created as an asset in the project.")]
        private bool m_CreateMeshInstance;

        /* TODO: implement partial extrusion
        [SerializeField, Tooltip ( "The section of the Spline to extrude." )]
        Vector2 m_Range = new Vector2 ( 0f, 1f );
        */

        private Mesh m_Mesh;
        private bool m_RebuildRequested;
        private float m_NextScheduledRebuild;

        /// <summary>The SplineContainer of the <see cref="Spline"/> to extrude.</summary>
        public SplineContainer Container
        {
            get => m_Container;
            set => m_Container = value;
        }

        /// <summary>
        /// Enable to regenerate the extruded mesh when the target Spline is modified. Disable this option if the Spline
        /// will not be modified at runtime.
        /// </summary>
        public bool RebuildOnSplineChange
        {
            get => m_RebuildOnSplineChange;
            set => m_RebuildOnSplineChange = value;
        }

        /// <summary>The maximum number of times per-second that the mesh will be rebuilt.</summary>
        [Obsolete("Use RebuildFrequency instead.", false)]
        public int rebuildFrequency => RebuildFrequency;

        /// <summary>The maximum number of times per-second that the mesh will be rebuilt.</summary>
        public int RebuildFrequency
        {
            get => m_RebuildFrequency;
            set => m_RebuildFrequency = Mathf.Max(value, 1);
        }

        /* TODO: implement ranges
        /// <summary>
        /// The section of the Spline to extrude.
        /// </summary>
        public Vector2 Range {
            get => m_Range;
            set => m_Range = new Vector2 ( Mathf.Min ( value.x, value.y ), Mathf.Max ( value.x, value.y ) );
        }
        */

        /// <summary>The main Spline to extrude.</summary>
        public Spline Spline => m_Container.Spline;

        /// <summary>The Splines to extrude.</summary>
        public IReadOnlyList<Spline> Splines => m_Container.Splines;

        private void Reset()
        {
            TryGetComponent(out m_Container);

            if (TryGetComponent<MeshFilter>(out MeshFilter filter))
            {
                if (m_CreateMeshInstance)
                {
                    filter.sharedMesh = m_Mesh = CreateMeshInstance();
                }
                else
                {
                    filter.sharedMesh = m_Mesh = CreateMeshAsset();
                }
            }

            if (TryGetComponent<MeshRenderer>(out MeshRenderer renderer) && renderer.sharedMaterial == null)
            {
                // todo Make Material.GetDefaultMaterial() public
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Material mat = cube.GetComponent<MeshRenderer>().sharedMaterial;
                DestroyImmediate(cube);
                renderer.sharedMaterial = mat;
            }

            Rebuild();
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void Start()
        {
            if (m_Container == null || m_Container.Spline == null)
            {
                Debug.LogError("Spline Extrude does not have a valid SplineContainer set.");
                return;
            }

            if ((m_Mesh = GetComponent<MeshFilter>().sharedMesh) == null && !m_CreateMeshInstance)
            {
                Debug.LogError("SplineExtrude.createMeshInstance is disabled, but there is no valid mesh assigned. " +
                    "Please create or assign a writable mesh asset.");
            }

            if (Application.isPlaying)
            {
                if (m_CreateMeshInstance)
                {
                    m_Mesh = CreateMeshInstance();
                    GetComponent<MeshFilter>().sharedMesh = m_Mesh;
                }
            }

            Rebuild();
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                if (m_CreateMeshInstance)
                {
                    m_Mesh = CreateMeshInstance();
                    GetComponent<MeshFilter>().sharedMesh = m_Mesh;
                }
            }

            Spline.Changed += OnSplineChanged;
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
        }


        /// <summary>
        /// Event function.
        /// </summary>
        private void Update()
        {
            if (m_RebuildRequested && Time.time >= m_NextScheduledRebuild)
                Rebuild();
        }

        /// <summary>
        /// Called whenever a spline has changed. This is a global/static event callback.
        /// </summary>
        /// <param name="spline"></param>
        /// <param name="knotIndex"></param>
        /// <param name="modificationType"></param>
        private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modificationType)
        {
            if (m_Container != null && Splines.Contains(spline) && m_RebuildOnSplineChange)
                m_RebuildRequested = true;
        }

        /// <summary>
        /// Triggers the rebuild of a Spline's extrusion mesh and collider.
        /// </summary>
        public void Rebuild()
        {
            if ((m_Mesh = GetComponent<MeshFilter>().sharedMesh) == null)
            {
                return;
            }


            m_RebuildRequested = false;
            m_NextScheduledRebuild = Time.time + 1f / m_RebuildFrequency;
            Mesh rotatedShape = new();
            rotatedShape.name = "copy";
            Vector3[] vxs = m_Shape.vertices;
            for (int i = 0; i < vxs.Length; i++)
            {
                var pos = vxs[i];
                pos.Scale(Vector3.one + m_ScaleOffset);
                vxs[i] = m_RotationOffset * (pos + m_PositionOffset);
            }

            rotatedShape.SetVertices(vxs);
            rotatedShape.SetNormals(m_Shape.normals);
            rotatedShape.SetTriangles(m_Shape.GetTriangles(0), 0);
            rotatedShape.SetUVs(0, m_Shape.uv);
            rotatedShape.UploadMeshData(false);
            Extrude(Splines, m_Mesh, rotatedShape, transform.up);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(m_Mesh);
#endif

            if (m_UpdateColliders)
            {
                if (TryGetComponent<MeshCollider>(out var meshCollider))
                    meshCollider.sharedMesh = m_Mesh;

                if (TryGetComponent<BoxCollider>(out var boxCollider))
                {
                    boxCollider.center = m_Mesh.bounds.center;
                    boxCollider.size = m_Mesh.bounds.size;
                }

                if (TryGetComponent<SphereCollider>(out var sphereCollider))
                {
                    sphereCollider.center = m_Mesh.bounds.center;
                    Vector3 ext = m_Mesh.bounds.extents;
                    sphereCollider.radius = Mathf.Max(ext.x, ext.y, ext.z);
                }
            }
        }

        /// <summary>
        /// Optimized data structure for storing spline-vertex data.
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct VertexData
        {
            public float3 position;
            public float3 normal;
            public float2 texture;
        }

        /// <summary>
        /// Optimized data structure for storing shape-vertex data.
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct ShapeVertexData
        {
            public float3 position;
            public float3 normal;
            public float2 uv;
        }

        /// <summary>
        /// Extrudes a shape along a spline and writes the results into a mesh. This primarily sets up the performance optimized implementation of the actual extrusion in <see cref="ExtrudeWorker{TSpline}"/>.
        /// </summary>
        /// <param name="splines">The spline(s) to extrude along.</param>
        /// <param name="mesh">The mesh to receive the extruded shape.</param>
        /// <param name="shape">The extruded shape.</param>
        /// <param name="up">The up vector to be used in performance optimizations of bi-tangent calculations.</param>
        private void Extrude(IReadOnlyList<Spline> splines, Mesh mesh, Mesh shape, float3 up)
        {
            mesh.Clear();
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData data = meshDataArray[0];
            data.subMeshCount = 1;

            //Mesh.MeshDataArray shapeDataArray = Mesh.AcquireReadOnlyMeshData ( mesh );
            //Mesh.MeshData shapeData = shapeDataArray[ 0 ];
            int[] shapeIndices = shape.GetIndices(0);
            Vector3[] shapePositions = shape.vertices;
            Vector3[] shapeNormals = shape.normals;
            Vector2[] shapeUVs = shape.uv;
            //Vector4[] shapeTangents = mesh.tangents;
            //Color[] shapeColors = mesh.colors;
            ShapeVertexData[] shapeVertices = new ShapeVertexData[shape.vertexCount];
            for (int i = 0; i < shape.vertexCount; i++)
            {
                Vector3 pos = shapePositions[i];
                pos += m_PositionOffset;
                pos.Scale(Vector3.one + m_ScaleOffset);
                shapeVertices[i].position = m_RotationOffset * pos;
                shapeVertices[i].normal = shapeNormals[i];
                shapeVertices[i].uv = shapeUVs[i];
            }

            int totalVertexCount = 0;
            int totalIndexCount = 0;
            (int indexStart, int vertexStart)[] splineMeshOffsets =
                new (int indexStart, int vertexStart)[splines.Count];
            for (int i = 0; i < splines.Count; ++i)
            {
                splineMeshOffsets[i] = (totalIndexCount, totalVertexCount);

                float z = shape.bounds.size.z * (m_ScaleOffset.z + 1f);
                float l = splines[i].GetLength();
                int segmentCount = (int)math.round((double)(l / z)); // round to int
                float segmentSize = l / segmentCount;
                int vertexCount = shapeVertices.Length * segmentCount;
                int indexCount = shapeIndices.Length * segmentCount;

                totalVertexCount += vertexCount;
                totalIndexCount += indexCount;
            }

            IndexFormat indexFormat = IndexFormat.UInt16;

            data.SetIndexBufferParams(totalIndexCount, indexFormat);
            data.SetVertexBufferParams(totalVertexCount, k_PipeVertexAttribs);

            NativeArray<VertexData> vertices = data.GetVertexData<VertexData>();
            NativeArray<ushort> indices = data.GetIndexData<UInt16>();
            for (int i = 0; i < splines.Count; ++i)
            {
                ExtrudeWorker(
                    splines[i],
                    vertices,
                    indices,
                    shape,
                    shapeVertices,
                    shapeIndices,
                    up,
                    splineMeshOffsets[i].vertexStart,
                    splineMeshOffsets[i].indexStart,
                    m_ScaleOffset,
                    m_PositionOffset
                );
            }


            data.SetSubMesh(0, new SubMeshDescriptor(0, totalIndexCount));

            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }

        /// <summary>
        /// Thread-safe, optimized extrusion worker.
        /// </summary>
        /// <param name="spline">The source spline to operate on.</param>
        /// <param name="vertices">Sampled spline vertex data.</param>
        /// <param name="vertexIndices">indices into the vertex data.</param>
        /// <param name="shape">The mesh to be extruded.</param>
        /// <param name="shapeVertices">The shape's vertex data.</param>
        /// <param name="shapeIndices">The shape's vertex indices.</param>
        /// <param name="up">The up vector to be used in performance optimizations of bi-tangent calculations.</param>
        /// <param name="vertexArrayOffset"></param>
        /// <param name="indicesArrayOffset"></param>
        /// <param name="scaleOffset"></param>
        /// <param name="positionOffset"></param>
        /// <typeparam name="TSpline"></typeparam>
        /// <remarks>This does some performance gymnastics by operating on the native mesh data.
        /// Apologies for the lack of explanations.</remarks>
        private static void ExtrudeWorker<TSpline>(TSpline spline,
            NativeArray<VertexData> vertices,
            NativeArray<ushort> vertexIndices,
            Mesh shape,
            ShapeVertexData[] shapeVertices,
            int[] shapeIndices,
            float3 up,
            int vertexArrayOffset = 0,
            int indicesArrayOffset = 0,
            Vector3 scaleOffset = default, 
            Vector3 positionOffset = default)
            where TSpline : ISpline
        {
            /*
            for ( int i = 0; i < s_samples.Length; i++ ) {
                s_samples = default;
            }*/

            float z = shape.bounds.size.z * (scaleOffset.z + 1f);
            float l = spline.GetLength();
            int segmentCount = (int)math.round((double)(l / z)); // round to int
            float segmentSize = l / segmentCount;
            float segmentSizePct = segmentSize / l;

            for (int segmentID = 0; segmentID < segmentCount; segmentID++)
            {
                float segmentStartPct = segmentID * segmentSizePct;
                float segmentEndPct = (segmentID + 1) * segmentSizePct;
                float3 segmentStartInSpline = spline.EvaluatePosition(segmentStartPct);
                float3 segmentEndInSpline = spline.EvaluatePosition(segmentEndPct);
                for (int shapeVID = 0; shapeVID < shapeVertices.Length; shapeVID++)
                {
                    float vZ = (shapeVertices[shapeVID].position.z - (shape.bounds.min.z * (scaleOffset.z + 1f)));
                    float vPctInSegment = vZ / (shape.bounds.size.z * (scaleOffset.z + 1f));
                    float vPctInSpline = math.lerp(math.max(0, segmentStartPct), math.min(segmentEndPct, 1f),
                        vPctInSegment);
                    int vPctIndex = (int)math.round(vPctInSpline * 128);
                    float vPctDiscrete = vPctIndex / 128f;

                    if (!s_samples[vPctIndex].IsSet)
                    {
                        s_samples[vPctIndex].Position = spline.EvaluatePosition(vPctDiscrete);
                        s_samples[vPctIndex].Tangent = spline.EvaluateTangent(vPctDiscrete);

                        //bool isValidSpline = spline.Evaluate ( vPctInSpline, out float3 vOnSpline, out float3 tangent, out float3 up );
                        float tangentLength = math.lengthsq(s_samples[vPctIndex].Tangent);
                        if (tangentLength == 0f || float.IsNaN(tangentLength))
                        {
                            float adjustedT = math.clamp(vPctDiscrete + (0.0001f * (vPctDiscrete < 1f ? 1f : -1f)), 0f,
                                1f);
                            //spline.Evaluate ( adjustedT, out _, out s_samples[ vPctIndex ].Tangent, out s_samples[ vPctIndex ].Up );
                            s_samples[vPctIndex].Tangent = spline.EvaluateTangent(adjustedT);
                        }

                        s_samples[vPctIndex].BiTangent = math.cross(s_samples[vPctIndex].Tangent, up);
                        s_samples[vPctIndex].Up =
                            math.normalize(math.cross(s_samples[vPctIndex].BiTangent, s_samples[vPctIndex].Tangent));
                    }

                    SplineSample sample = s_samples[vPctIndex];
                    float3 biTangentNormal = math.normalize(sample.BiTangent);
                    float3 p = s_samples[vPctIndex].Position +
                        biTangentNormal * shapeVertices[shapeVID].position.x +
                        s_samples[vPctIndex].Up * shapeVertices[shapeVID].position.y;


                    vertices[vertexArrayOffset++] = new VertexData
                    {
                        position = p,
                        normal = shapeVertices[shapeVID].normal,
                        texture = shapeVertices[shapeVID].uv
                    };
                }

                for (int shapeIID = shapeIndices.Length - 1; shapeIID >= 0; shapeIID--)
                {
                    vertexIndices[indicesArrayOffset] =
                        (ushort)(shapeIndices[shapeIID] + segmentID * shapeVertices.Length);
                    indicesArrayOffset++;
                }
            }

            // reset samples
            for (int i = 0; i < s_samples.Length; i++)
            {
                s_samples[i] = default;
            }
        }

        /// <summary>
        /// Creates a new mesh instance.
        /// </summary>
        /// <returns></returns>
        public Mesh CreateMeshInstance()
        {
            Mesh mesh = new Mesh();
            mesh.name = $"{name} {mesh.GetInstanceID()}";
            return mesh;
        }


        /// <summary>
        /// Updates the mesh asset with new mesh data.
        /// </summary>
        /// <param name="mesh">The mesh data to write to the asset.</param>
        /// <remarks>This can only be used in the Unity Editor.</remarks>
        public void UpdateMeshAsset(Mesh mesh)
        {
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GetAssetPath(mesh);
            UnityEditor.AssetDatabase.CreateAsset(mesh, path);
            mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
            UnityEditor.EditorGUIUtility.PingObject(mesh);
#else
            Debug.LogWarning ( $"{nameof ( UpdateMeshAsset )} can only be used in the Editor" );
#endif
        }

        /// <summary>
        /// Creates a new mesh asset. Will override any existing one with he same name.
        /// </summary>
        /// <param name="overrideName">The name of the new asset.</param>
        /// <returns>The created asset.</returns>
        /// <remarks>This can only be used in the Unity Editor.</remarks>
        public Mesh CreateMeshAsset(string overrideName = default)
        {
            Mesh mesh = new()
            {
                name = overrideName ?? name
            };

#if UNITY_EDITOR
            Scene scene = SceneManager.GetActiveScene();
            string sceneDataDir = "Assets";

            if (!string.IsNullOrEmpty(scene.path))
            {
                string dir = System.IO.Path.GetDirectoryName(scene.path);
                sceneDataDir = $"{dir}/{System.IO.Path.GetFileNameWithoutExtension(scene.path)}";
                if (!System.IO.Directory.Exists(sceneDataDir))
                    System.IO.Directory.CreateDirectory(sceneDataDir);
            }

            string path =
                UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"{sceneDataDir}/SplineExtrude_{mesh.name}.asset");
            UnityEditor.AssetDatabase.CreateAsset(mesh, path);
            mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
            UnityEditor.EditorGUIUtility.PingObject(mesh);
#else
        Debug.LogWarning ( $"{nameof ( UpdateMeshAsset )} can only be used in the Editor" );
#endif

            return mesh;
        }
    }
}