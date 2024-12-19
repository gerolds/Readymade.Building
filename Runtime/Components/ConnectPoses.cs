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
using Cysharp.Threading.Tasks;
using Readymade.Building.Dubins;
using Readymade.Building;
using Readymade.Utils;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using Vertx.Debugging;

namespace Readymade.Building.Components
{
    /// <summary>
    /// Connects two poses with a spline based on a configurable interpolation/path-tracing mode.
    /// </summary>
    /// <remarks>This is an experimental component that will not yet produce satisfactory results in all cases.</remarks>
    public class ConnectPoses : MonoBehaviour
    {
        /// <summary>
        /// The path tracing/interpolation modes that are available.
        /// </summary>
        private enum Mode
        {
            /// <summary>
            /// generate dubins curves to connect poses. Use this for conveyor belts and similar connections.
            /// Only works in 2D. Any elevation changes will simply use a b-spline in the straight segments.
            /// </summary>
            Dubins,

            /// <summary>
            /// Interpolate between the two poses with a linear spline.
            /// </summary>
            Linear,

            /// <summary>
            /// Interpolate between the two poses with a b-spline.
            /// </summary>
            BSpline,

            /// <summary>
            /// Use this for creating pipes. A variation of the dubins curve that works in 3D.
            /// </summary>
            Pipe_NOT_YET_IMPLEMENTED,

            /// <summary>
            /// Use this for connecting wires and chains that follow a catenary curve.
            /// </summary>
            Catenary_NOT_YET_IMPLEMENTED,
        }

        [Tooltip("The spline container that will be used to store the generated spline.")]
        [SerializeField]
        private SplineContainer m_Container;

        [Tooltip("The transform that marks the start of the spline.")]
        [SerializeField]
        private Transform start;

        [Tooltip("The transform that marks the end of the spline.")]
        [SerializeField]
        private Transform end;

        [Tooltip("The tension of the b-spline segments of any interpolation. Default is 1.")]
        [SerializeField]
        [Min(0)]
        private float tension = 1f;

        [Tooltip("Controls the curvature of the dubins curve. Default is 1.")]
        [SerializeField]
        private float curvePower = 1f;

        [Tooltip(
            "Controls the curvature of the dubins curve. This is a magic number used to approximate a circle segment via" +
            " two bezier control points. Default is 1.33.")]
        [SerializeField]
        private float tangentAtPi = 1.33f;

        [Tooltip("THe mode that will be used to interpolate between the two poses.")]
        [SerializeField]
        private Mode mode = Mode.Linear;

        [SerializeField, Tooltip("The maximum number of times per-second that the mesh will be rebuilt.")]
        private int m_RebuildFrequency = 30;

        private bool m_RebuildRequested;

        private float m_NextScheduledRebuild;

        [Tooltip("If true, the mesh will be rebuilt automatically whenever a property changes.")]
        [SerializeField]
        private bool m_RebuildAutomatically;

        /// <summary>
        /// The spline container that will be used to store the generated spline.
        /// </summary>
        public SplineContainer Container => m_Container;

        /// <summary>
        /// The splines that are stored in the container.
        /// </summary>
        public IReadOnlyList<Spline> Splines => m_Container.Splines;

        // we use this value to ignore events this component has triggered due to knot insertion.
        public static bool IsInternalChange { get; private set; }

        private ISet<IDisposable> _disposeOnDisable = new HashSet<IDisposable>();
        private BezierKnot[] _knots;

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnEnable()
        {
            Rebuild();
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnDisable()
        {
            _disposeOnDisable.DisposeAll();
        }


        private void Update()
        {
            /*
            if ( ( m_RebuildRequested ) && Time.time >= m_NextScheduledRebuild ) {
                Rebuild ();
            }
            */
        }

        [Obsolete]
        private void ScheduleRebuildImmediately(AsyncUnit nothing)
        {
            Rebuild();
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (CheckStraight())
            {
                D.raw(new Shape.Line(start.position, end.position), Color.yellow);
                return;
            }

            // draw circles

            switch (mode)
            {
                case Mode.Dubins:
                {
                    float r = tension;
                    Vector3 LStartCenter = start.position - start.right * r;
                    Vector3 RStartCenter = start.position + start.right * r;
                    Vector3 LEndCenter = end.position - end.right * r;
                    Vector3 REndCenter = end.position + end.right * r;
                    D.raw(new Shape.Circle(LStartCenter, start.up, r));
                    D.raw(new Shape.Circle(RStartCenter, start.up, r));
                    D.raw(new Shape.Circle(LEndCenter, end.up, r));
                    D.raw(new Shape.Circle(REndCenter, end.up, r));
                    D.raw(LStartCenter);
                    D.raw(RStartCenter);
                    D.raw(LEndCenter);

                    D.raw(REndCenter);
                    break;
                }
                case Mode.Linear:
                    break;
                case Mode.BSpline:
                    break;
                case Mode.Pipe_NOT_YET_IMPLEMENTED:
                {
                    D.raw(new Shape.Line(start.position, end.position), Color.green);
                    Vector3 av = start.forward;
                    Vector3 bv = end.forward;
                    Vector3 ap = start.position;
                    Vector3 bp = end.position;
                    D.raw(new Shape.Axis(start));
                    D.raw(new Shape.Axis(end));
                    D.raw(new Shape.Line(ap, bp), Color.black);
                    float aDot = (Vector3.Dot(av.normalized, (ap - bp).normalized) + 1f) / 2f;
                    float bDot = (Vector3.Dot(bv.normalized, (bp - ap).normalized) + 1f) / 2f;
                    D.raw(new Shape.Arc(start.position, start.rotation, .2f , Shape.Angle.FromTurns(aDot / 2f)));
                    D.raw(new Shape.Arc(end.position, end.rotation, .2f , Shape.Angle.FromTurns(bDot / 2f)));
                    for (int i = 0; i < 10; i++)
                    {
                        D.raw(
                            new Shape.Ray(Vector3.Lerp(start.position, end.position, i / 10f),
                                Vector3.Slerp(start.forward, end.forward, i / 10f).normalized), Color.red);
                    }
                }
                    break;
                case Mode.Catenary_NOT_YET_IMPLEMENTED:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (CheckStraight())
            {
                return;
            }

            switch (mode)
            {
                case Mode.Dubins:
                {
                    DubinsPath dub = new(
                        new double[]
                            { start.position.x, start.position.z, Mathf.Atan2(start.forward.z, start.forward.x) },
                        new double[] { end.position.x, end.position.z, Mathf.Atan2(end.forward.z, end.forward.x) },
                        tension
                    );

                    double segmentLength = dub.GetLength() / 24;
                    double[] sample0 = dub.Sample(0);
                    double[] sample1 = dub.Sample(0);
                    for (int i = 1; i < 24; i++)
                    {
                        sample1 = dub.Sample(segmentLength * i);
                        D.raw(
                            new Shape.Line(
                                new Vector3((float)sample0[0], start.position.y, (float)sample0[1]),
                                new Vector3((float)sample1[0], start.position.y, (float)sample1[1])
                            ),
                            Color.red
                        );
                        sample0 = sample1;
                    }

                    double[] segmentLengths = dub.SegmentLengths;
                    double[] st = dub.Sample(0);
                    double[] a = dub.Sample((segmentLengths[0]) * dub.Rho);
                    double[] b = dub.Sample((segmentLengths[0] + segmentLengths[1]) * dub.Rho);
                    double[] c = dub.Sample((segmentLengths[0] + segmentLengths[1] + segmentLengths[2]) * dub.Rho);


                    D.raw(
                        new Shape.Line(
                            new Vector3((float)st[0], start.position.y, (float)st[1]),
                            new Vector3((float)a[0], start.position.y, (float)a[1])
                        ),
                        Color.cyan
                    );
                    D.raw(
                        new Shape.Line(
                            new Vector3((float)a[0], start.position.y, (float)a[1]),
                            new Vector3((float)b[0], start.position.y, (float)b[1])
                        ),
                        Color.green
                    );
                    D.raw(
                        new Shape.Line(
                            new Vector3((float)b[0], start.position.y, (float)b[1]),
                            new Vector3((float)c[0], start.position.y, (float)c[1])
                        ),
                        Color.cyan
                    );
                    break;
                }
                case Mode.Linear:
                    break;
                case Mode.BSpline:
                    break;
                case Mode.Pipe_NOT_YET_IMPLEMENTED:

                    break;
                case Mode.Catenary_NOT_YET_IMPLEMENTED:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Rebuild the spline based on the current configuration.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"> if the mode is not supported.</exception>
        public void Rebuild()
        {
            m_NextScheduledRebuild = Time.time + 1f / m_RebuildFrequency;

            if (CheckStraight())
            {
                // straight path
                CalculateLinearKnots();
            }
            else
            {
                switch (mode)
                {
                    case Mode.Dubins:
                        CalculateDubinsKnots();
                        break;
                    case Mode.Linear:
                        CalculateLinearKnots();
                        break;
                    case Mode.BSpline:
                        CalculateBSplineKnots();
                        break;
                    case Mode.Catenary_NOT_YET_IMPLEMENTED:
                        break;
                    case Mode.Pipe_NOT_YET_IMPLEMENTED:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            /*
            if ( m_Container.Spline.Count != 4 ) {
                for ( int i = 0; i < _knots.Length; i++ ) {
                    BezierKnot bezierKnot = _knots[ i ];
                    m_Container.Spline.Add ( bezierKnot );
                }
            } else {
                for ( int i = 0; i < _knots.Length; i++ ) {
                    m_Container.Spline.SetKnot ( i, _knots[ i ], BezierTangent.Out );
                }
            }


            if ( segmentLengths[ 0 ] < DubinsCurves.Epsilon ) {
                m_Container.Spline.RemoveAt ( 0 );
            }
            */
        }

        /// <summary>
        /// Calculate the b-spline knots required to interpolate the configured poses.
        /// </summary>
        private void CalculateBSplineKnots()
        {
            Vector3 localStartForward = transform.InverseTransformDirection(start.forward);
            Vector3 localStartUp = transform.InverseTransformDirection(start.up);
            Vector3 localEndForward = transform.InverseTransformDirection(end.forward);
            Vector3 localEndUp = transform.InverseTransformDirection(end.up);

            float distance = Vector3.Distance(start.localPosition, end.localPosition);
            Vector3 p1 = start.localPosition + localStartForward * (distance * tension);
            Vector3 p2 = end.localPosition - localEndForward * (distance * tension);
            Vector3 p1p2 = p2 - p1;
            Vector3 p1p2Middle = p1 + p1p2 * 0.5f;
            Vector3 p1p2Normal = p1p2.normalized;
            Vector3 p1p2Tension = p1p2Normal * (distance * tension);
            Vector3 avgDirection = (localStartForward + localEndForward) * 0.5f;
            Vector3 middleTangent = (avgDirection + p1p2Normal) * 0.5f;

            EnsureFourKnots();

            _knots[0] = new(
                position: start.localPosition,
                tangentIn: -localStartForward * (distance * tension),
                tangentOut: localStartForward * (distance * tension)
            );
            _knots[1] = new(
                position: p1p2Middle,
                tangentIn: -p1p2Normal * (distance * tension),
                tangentOut: p1p2Normal * (distance * tension)
            );
            _knots[2] = new(
                position: end.localPosition,
                tangentIn: -localEndForward * (distance * tension),
                tangentOut: localEndForward * (distance * tension)
            );
            _knots[3] = new(
                position: end.localPosition,
                tangentIn: 0,
                tangentOut: 0
            );

            for (int i = 0; i < m_Container.Spline.Count; i++)
            {
                m_Container.Spline.SetKnot(i, _knots[i]);
                m_Container.Spline.SetTangentMode(i, TangentMode.Continuous);
            }
        }

        /// <summary>
        /// Calculate the linear knots required to interpolate the configured poses.
        /// </summary>
        private void CalculateLinearKnots()
        {
            Vector3 loaclStartForward = transform.InverseTransformDirection(start.forward);
            Vector3 localStartUp = transform.InverseTransformDirection(start.up);
            Vector3 localEndForward = transform.InverseTransformDirection(end.forward);
            Vector3 localEndUp = transform.InverseTransformDirection(end.up);

            Vector3 p1 = start.localPosition + loaclStartForward * tension;
            Vector3 p2 = end.localPosition - localEndForward * tension;
            Vector3 p1p2 = p2 - p1;
            Vector3 p1p2Normal = p1p2.normalized;
            Vector3 p1p2Tension = p1p2Normal * tension;

            EnsureTwoKnots();

            _knots[0] = new(position: start.localPosition);
            /*
            _knots[ 1 ] = new (
                position: p1 + p1p2Tension,
                tangentIn: -p1p2Tension * 0.67f,
                tangentOut: p1p2Tension * 0.67f
            );
            _knots[ 2 ] = new (
                position: p2 - p1p2Tension,
                tangentIn: -p1p2Tension * 0.67f,
                tangentOut: p1p2Tension * 0.67f
            );
            */
            _knots[1] = new(position: end.localPosition);

            for (int i = 0; i < m_Container.Spline.Count; i++)
            {
                m_Container.Spline.SetKnot(i, _knots[i]);
                m_Container.Spline.SetTangentMode(i, TangentMode.Linear);
            }
        }

        /// <summary>
        /// Calculate the dubins knots required to interpolate the configured poses.
        /// </summary>
        private void CalculateDubinsKnots()
        {
            // non-straight -> dubins curves
            Vector3 planeStartForward = transform.InverseTransformDirection(start.forward);
            Vector3 planeStartUp = transform.InverseTransformDirection(start.up);
            Vector3 planeEndForward = transform.InverseTransformDirection(end.forward);
            Vector3 planeEndUp = transform.InverseTransformDirection(end.up);

            DubinsPath dub = new(
                new double[]
                {
                    start.localPosition.x, start.localPosition.z, Mathf.Atan2(planeStartForward.z, planeStartForward.x)
                },
                new double[]
                    { end.localPosition.x, end.localPosition.z, Mathf.Atan2(planeEndForward.z, planeEndForward.x) },
                tension
            );

            double[] segmentLengths = dub.SegmentLengths;
            double[] a = dub.Sample((segmentLengths[0]) * dub.Rho);
            double[] b = dub.Sample((segmentLengths[0] + segmentLengths[1]) * dub.Rho);
            Vector3 posA = new Vector3((float)a[0], start.localPosition.y, (float)a[1]);
            Quaternion rotA = Quaternion.AngleAxis(-(float)a[2] * Mathf.Rad2Deg, planeStartUp);
            Vector3 tanA = rotA * Vector3.right;
            Vector3 posB = new Vector3((float)b[0], end.localPosition.y, (float)b[1]);
            Quaternion rotB = Quaternion.AngleAxis(-(float)b[2] * Mathf.Rad2Deg, planeStartUp);
            Vector3 tanB = rotB * Vector3.right;

            float t0 = math.pow((float)dub.SegmentLengths[0], curvePower) / Mathf.PI;
            float t0Rho = (float)dub.Rho * math.lerp(0f, tangentAtPi, t0);
            float t2 = math.pow((float)dub.SegmentLengths[2], curvePower) / Mathf.PI;
            float t2Rho = (float)dub.Rho * math.lerp(0f, tangentAtPi, t2);

            EnsureFourKnots();

            _knots[0] = new(
                position: start.localPosition,
                rotation: quaternion.identity,
                tangentIn: -planeStartForward * t0Rho,
                tangentOut: planeStartForward * t0Rho
            );
            _knots[1] = new(
                position: posA,
                rotation: quaternion.identity,
                tangentIn: -tanA * t0Rho,
                tangentOut: (tanA * (float)math.min(dub.Rho, dub.Rho * dub.SegmentLengths[1] / math.PI))
            );
            _knots[2] = new(
                position: posB,
                rotation: quaternion.identity,
                tangentIn: (-tanB * (float)math.min(dub.Rho, dub.Rho * dub.SegmentLengths[1] / math.PI)),
                tangentOut: tanB * t2Rho
            );
            _knots[3] = new(
                position: end.localPosition,
                rotation: quaternion.identity,
                tangentIn: -planeEndForward * t2Rho,
                tangentOut: planeEndForward * t2Rho
            );

            for (int i = 0; i < m_Container.Spline.Count; i++)
            {
                m_Container.Spline.SetKnot(i, _knots[i]);
                m_Container.Spline.SetTangentMode(i, TangentMode.Continuous);
            }
        }


        /// <summary>
        /// Ensures that the spline container has exactly two knots.
        /// </summary>
        private void EnsureTwoKnots()
        {
            if (_knots?.Length != 2)
            {
                _knots = new BezierKnot[2];
                m_Container.Spline.Knots = _knots;
            }
        }

        /// <summary>
        /// Ensures that the spline container has exactly four knots.
        /// </summary>
        private void EnsureFourKnots()
        {
            if (_knots?.Length != 4)
            {
                _knots = new BezierKnot[4];
                m_Container.Spline.Knots = _knots;
            }
        }

        /// <summary>
        /// Checks whether the two poses are collinear.
        /// </summary>
        /// <returns></returns>
        private bool CheckStraight()
        {
            return Mathf.Abs(Vector3.Dot(start.forward, (end.position - start.position).normalized)) > 0.99999f &&
                Vector3.Dot(start.forward, end.forward) > 0.99999f;
        }

        /// <summary>
        /// Untested 3D extension of <see cref="GetTangents2D"/>.
        /// </summary>
        /// <param name="startCenter"></param>
        /// <param name="startRadius"></param>
        /// <param name="endCenter"></param>
        /// <param name="endRadius"></param>
        /// <returns></returns>
        public static (Vector3 start, Vector3 end)[] GetTangents3D(
            Vector3 startCenter,
            float startRadius,
            Vector3 endCenter,
            float endRadius
        )
        {
            (Vector2 start, Vector2 end)[] solution =
                GetTangents2D(
                    new Vector2(startCenter.x, startCenter.z),
                    startRadius,
                    new Vector2(endCenter.x, endCenter.z),
                    endRadius
                );

            (Vector3 start, Vector3 end)[] solution3D = new ( Vector3 start, Vector3 end )[4];
            for (int i = 0; i < solution.Length; i++)
            {
                solution3D[i].start = new Vector3(solution[i].start.x, startCenter.y, solution[i].start.y);
                solution3D[i].end = new Vector3(solution[i].end.x, startCenter.y, solution[i].end.y);
            }

            return solution3D;
        }

        /**
         *  Finds tangent segments between two given circles.
         *
         *  Returns an empty, or 2x4, or 4x4 array of doubles representing
         *  the two exterior and two interior tangent segments (in that order).
         *  If some tangents don't exist, they aren't present in the output.
         *  Each segment is represent by a 4-tuple x1,y1,x2,y2.
         *
         *  Exterior tangents exist iff one of the circles doesn't contain
         *  the other. Interior tangents exist iff circles don't intersect.
         *
         *  In the limiting case when circles touch from outside/inside, there are
         *  no interior/exterior tangents, respectively, but just one common
         *  tangent line (which isn't returned at all, or returned as two very
         *  close or equal points by this code, depending on roundoff -- sorry!)
         */
        public static (Vector2 start, Vector2 end)[] GetTangents2D(Vector2 c1, float r1, Vector2 c2, float r2)
        {
            float d = Vector2.Distance(c1, c2);
            if (d <= r1 - r2)
            {
                return new (Vector2 start, Vector2 end)[4];
            }

            Vector2.Distance(c1, c2);

            float vx = (c2.x - c1.x) / d;
            float vy = (c2.y - c1.y) / d;

            (Vector2 start, Vector2 end)[] result = new (Vector2 start, Vector2 end)[4];
            int i = 0;

            // Let A, B be the centers, and C, D be points at which the tangent
            // touches first and second circle, and n be the normal vector to it.
            //
            // We have the system:
            //   n * n = 1          (n is a unit vector)          
            //   C = A + r1 * n
            //   D = B +/- r2 * n
            //   n * CD = 0         (common orthogonality)
            //
            // n * CD = n * (AB +/- r2*n - r1*n) = AB*n - (r1 -/+ r2) = 0,  <=>
            // AB * n = (r1 -/+ r2), <=>
            // v * n = (r1 -/+ r2) / d,  where v = AB/|AB| = AB/d
            // This is a linear equation in unknown vector n.

            for (int sign1 = +1; sign1 >= -1; sign1 -= 2)
            {
                float c = (r1 - sign1 * r2) / d;

                // Now we're just intersecting a line with a circle: v*n=c, n*n=1

                if (c * c > 1.0)
                {
                    continue;
                }

                float h = Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - c * c));

                for (int sign2 = +1; sign2 >= -1; sign2 -= 2)
                {
                    float nx = vx * c - sign2 * h * vy;
                    float ny = vy * c + sign2 * h * vx;

                    result[i].start.x = c1.x + r1 * nx;
                    result[i].start.y = c1.y + r1 * ny;
                    result[i].end.x = c2.x + sign1 * r2 * nx;
                    result[i].end.y = c2.y + sign1 * r2 * ny;
                    i++;
                }
            }

            return result;
        }
    }
}