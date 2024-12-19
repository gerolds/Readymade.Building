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

using NaughtyAttributes.Editor;
using System;
using System.Linq;
using Readymade.Building.Components;
using Readymade.Utils;
using UnityEditor;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

namespace Readymade.Building.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="SplineExtrudeShape"/> components.
    /// </summary>
    /// <remarks>Derived from <see cref="SplineExtrudeEditor"/>.</remarks>
    [CustomEditor(typeof(SplineExtrudeShape))]
    [CanEditMultipleObjects]
    class SplineExtrudeShapeEditor : 
#if ODIN_INSPECTOR
        Sirenix.OdinInspector.Editor.OdinEditor
#else
        NaughtyInspector
#endif
    {
        private static readonly string s_Spline = "Spline";
        private static readonly string s_Geometry = L10n.Tr("Geometry");
        private static readonly string s_ProfileEdges = "Profile Edges";
        private static readonly string s_CapEnds = "Cap Ends";
        private static readonly string s_AutoRegenGeo = "Auto-Regen Geometry";

        private SerializedProperty m_Container;
        private SerializedProperty m_RebuildOnSplineChange;
        private SerializedProperty m_RebuildFrequency;
        private SerializedProperty m_UpdateColliders;
        private SerializedProperty m_Shape;
        private SerializedProperty m_CreateMeshInstance;
        private SerializedProperty m_RotationOffset;
        private SerializedProperty m_ScaleOffset;
        private SerializedProperty m_PositionOffset;
        private SplineExtrudeShape[] m_Components;
        private bool m_AnyMissingMesh;

        /// <summary>
        /// Event function.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            m_Container = serializedObject.FindProperty(nameof(m_Container));
            m_RebuildOnSplineChange = serializedObject.FindProperty(nameof(m_RebuildOnSplineChange));
            m_RebuildFrequency = serializedObject.FindProperty(nameof(m_RebuildFrequency));
            m_UpdateColliders = serializedObject.FindProperty(nameof(m_UpdateColliders));
            m_Shape = serializedObject.FindProperty(nameof(m_Shape));
            m_CreateMeshInstance = serializedObject.FindProperty(nameof(m_CreateMeshInstance));
            m_RotationOffset = serializedObject.FindProperty(nameof(m_RotationOffset));
            m_ScaleOffset = serializedObject.FindProperty(nameof(m_ScaleOffset));
            m_PositionOffset = serializedObject.FindProperty(nameof(m_PositionOffset));

            m_Components = targets.Select(x => x as SplineExtrudeShape).Where(y => y != null).ToArray();
            m_AnyMissingMesh = false;

            Spline.Changed += OnSplineChanged;
            EditorSplineUtility.AfterSplineWasModified += OnSplineModified;
            SplineContainer.SplineAdded += OnContainerSplineSetModified;
            SplineContainer.SplineRemoved += OnContainerSplineSetModified;
        }

        /// <summary>
        /// Event function.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();
            Spline.Changed -= OnSplineChanged;
            EditorSplineUtility.AfterSplineWasModified -= OnSplineModified;
            SplineContainer.SplineAdded -= OnContainerSplineSetModified;
            SplineContainer.SplineRemoved -= OnContainerSplineSetModified;
        }

        private void OnSplineModified(Spline spline)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            foreach (SplineExtrudeShape extrude in m_Components)
            {
                if (extrude.Container != null && extrude.Splines.Contains(spline))
                    extrude.Rebuild();
            }
        }

        private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modificationType)
        {
            OnSplineModified(spline);
        }

        private void OnContainerSplineSetModified(SplineContainer container, int spline)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            foreach (SplineExtrudeShape extrude in m_Components)
            {
                if (extrude.Container == container)
                    extrude.Rebuild();
            }
        }

        /// <summary>
        /// Event function.
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            m_AnyMissingMesh =
                m_Components.Any(x => x.TryGetComponent(out MeshFilter filter) && filter.sharedMesh == null);

            NaughtyEditorGUI.HelpBox_Layout($"Use this component to loft a mesh along a spline", MessageType.None);
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(m_Container, new GUIContent(s_Spline, m_Container.tooltip));
            EditorGUILayout.LabelField(s_Geometry, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_Shape);
            EditorGUILayout.PropertyField(m_CreateMeshInstance);


            if (m_AnyMissingMesh)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(" ");
                if (GUILayout.Button("Create Mesh Asset"))
                {
                    CreateMeshAssets(m_Components);
                }

                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(" ");
                if (GUILayout.Button("Clear Mesh"))
                {
                    m_Components.ForEach(x =>
                    {
                        x.TryGetComponent(out MeshFilter filter);
                        filter.sharedMesh = default;
                    });
                }

                if (GUILayout.Button("Update Mesh"))
                {
                    UpdateMeshAssets(m_Components);
                }

                GUILayout.EndHorizontal();
            }


            EditorGUILayout.PropertyField(m_RebuildOnSplineChange,
                new GUIContent(s_AutoRegenGeo, m_RebuildOnSplineChange.tooltip));
            if (m_RebuildOnSplineChange.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginDisabledGroup(!m_RebuildOnSplineChange.boolValue);
                EditorGUILayout.PropertyField(m_RebuildFrequency);
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(m_UpdateColliders);
            EditorGUILayout.PropertyField(m_RotationOffset);
            EditorGUILayout.PropertyField(m_ScaleOffset);
            EditorGUILayout.PropertyField(m_PositionOffset);


            serializedObject.ApplyModifiedProperties();

            if (EditorGUI.EndChangeCheck())
                foreach (SplineExtrudeShape extrude in m_Components)
                    extrude.Rebuild();
        }

        private void CreateMeshAssets(SplineExtrudeShape[] components)
        {
            foreach (SplineExtrudeShape extrude in components)
            {
                if (!extrude.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                    filter.sharedMesh = extrude.CreateMeshAsset();
            }

            m_AnyMissingMesh = false;
        }

        static void UpdateMeshAssets(SplineExtrudeShape[] components)
        {
            foreach (SplineExtrudeShape extrude in components)
            {
                if (!extrude.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                    extrude.UpdateMeshAsset(filter.sharedMesh);
            }
        }

        internal struct LabelWidthScope : IDisposable
        {
            float previousWidth;

            public LabelWidthScope(float width)
            {
                previousWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = width;
            }

            public void Dispose()
            {
                EditorGUIUtility.labelWidth = previousWidth;
            }
        }
    }
}