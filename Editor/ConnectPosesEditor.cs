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

using System.Linq;
using Readymade.Building.Components;
using UnityEditor;
using UnityEngine;


namespace Readymade.Building.Editor {
    /// <summary>
    /// Custom inspector for <see cref="ConnectPoses"/> components.
    /// </summary>
    [CustomEditor ( typeof ( ConnectPoses ) )]
    [CanEditMultipleObjects]
    public class ConnectPosesEditor : UnityEditor.Editor {
        private SerializedProperty m_Container;
        private ConnectPoses[] m_Components;

        /// <summary>
        /// Event function.
        /// </summary>
        protected void OnEnable () {
            m_Container = serializedObject.FindProperty ( "m_Container" );
            m_Components = targets.Select ( x => x as ConnectPoses ).Where ( y => y != null ).ToArray ();
        }

        /// <summary>
        /// Event function.
        /// </summary>
        public override void OnInspectorGUI () {
            base.OnInspectorGUI ();
            //DrawDefaultInspector ();
            if ( GUILayout.Button ( "Rebuild" ) ) {
                foreach ( ConnectPoses connect in m_Components ) {
                    connect.Rebuild ();
                }
            }
        }
    }
}