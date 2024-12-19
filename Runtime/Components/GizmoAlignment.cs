/* Copyright 2023 Gerold Schneider
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

using UnityEngine;

namespace Readymade.Building.Components {
    /// <summary>
    /// Facilitates the display of an alignment gizmo.
    /// </summary>
    [RequireComponent ( typeof ( LineRenderer ) )]
    [ExecuteAlways]
    public class GizmoAlignment : MonoBehaviour {
        [Tooltip ( "Scale factor to compensate for perspective foreshortening." )]
        [SerializeField]
        private float distanceScale;

        private LineRenderer[] _lineRenderers;
        private Camera _camera;

        /// <summary>
        /// Sets the gizmo's alignment line between two given world-space points.
        /// </summary>
        /// <param name="start">The start point.</param>
        /// <param name="end">The end point.</param>
        public void SetLine ( Vector3 start, Vector3 end ) {
            /*
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.positionCount = 2;
            _lineRenderer.SetPositions ( new[] {start, end} );
            */
            foreach ( LineRenderer lineRenderer in _lineRenderers ) {
                lineRenderer.enabled = true;
            }

            transform.position = start;
            transform.forward = end - start;
        }

        /// <summary>
        /// Clears the current line (hides the gizmo). 
        /// </summary>
        public void ClearLine () {
            foreach ( LineRenderer lineRenderer in _lineRenderers ) {
                lineRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void Awake () {
            _lineRenderers = GetComponentsInChildren<LineRenderer> ();
            _camera = Camera.main;
            ClearLine ();
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void Update () {
            if ( _camera ) {
                transform.localScale = Vector3.one *
                                       ( Vector3.Distance ( transform.position, _camera.transform.position ) * distanceScale );
            }
        }
    }
}