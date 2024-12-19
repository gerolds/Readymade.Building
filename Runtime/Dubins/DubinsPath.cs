/* MIT License
 * This a C# port from C++ code by Andrew Walker found at https://github.com/AndrewWalker/Dubins-Curves
 * Also informed by Michael Carleton's Java adaptation https://github.com/micycle1/Dubins-Curves
 *
 * C#-Adaptation: Copyright 2023, Gerold Schneider
 * Java-Adaptation: Copyright 2022, Michael Carleton
 * Original: Copyright 2008-2018, Andrew Walker
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

namespace Readymade.Building.Dubins {
    /// <summary>
    /// A Dubins path is the shortest curve that connects two points in the 2D plane,
    /// with a constraint on the curvature of the path and with prescribed initial
    /// and terminal tangents to the path.</summary>
    /// <remarks>
    /// Dubins paths consist of 3 adjoined segments, that can be either left-curving,
    /// right-curving or straight. There are only 6 combinations of these segments
    /// that describe all possible Dubins paths.
    /// </remarks>
    public class DubinsPath {
        /// <summary>The initial configuration</summary>
        public readonly double[] ConfigStart = new double[3];

        /// <summary>The lengths of the three segments</summary>
        public readonly double[] SegmentLengths = new double[3];

        ///<summary>Model forward velocity / model angular velocity</summary>
        public double Rho;

        private DubinsPath () {
        }


        /// <summary>
        /// Generates the shortest possible Dubins path between a starting and ending configuration.</summary>
        /// <remarks>
        /// A configuration is <c>[x, y, theta]</c>, where theta is heading
        /// direction in radians, with zero along the line <c>x = 0</c> (facing east), and
        /// counter-clockwise is positive
        /// </remarks>
        /// <param name="configStart">starting configuration, specified as an array of <c>[x, y, theta]</c></param>  
        /// <param name="configEnd">ending configuration, specified as an array of <c>[x, y, theta]</c></param>  
        /// <param name="rho">turning radius of the vehicle (forward velocity divided by maximum angular velocity)</param>  
        /// <seealso cref="DubinsPath"/>
        public DubinsPath ( double[] configStart, double[] configEnd, double rho ) {
            DubinsCurves.DubinsShortestPath ( this, configStart, configEnd, rho );
        }

        /// <summary>
        /// Generates a Dubins path between a starting and ending configuration, having a specific path type; it is not necessarily
        /// the shortest path.
        /// </summary>
        /// <remarks>
        /// A configuration is <c>[x, y, theta]</c>, where theta is heading direction in radians, with zero along the line
        /// <c>x = 0</c> (facing east), and counter-clockwise is positive.
        /// </remarks>
        /// <param name="configStart">starting configuration, specified as an array of <c>[x, y, theta]</c>.</param>  
        /// <param name="configEnd">ending configuration, specified as an array of <c>[x, y, theta]</c>.</param>  
        /// <param name="rho">turning radius of the vehicle (forward velocity divided by maximum angular velocity).</param>
        /// <param name="pathType">the path type.</param>
        /// <seealso cref="DubinsPath"/>
        public DubinsPath ( double[] configStart, double[] configEnd, double rho, DubinsPathType pathType ) {
            DubinsCurves.DubinsPath ( this, configStart, configEnd, rho, pathType );
        }

        /// <summary>the length of this path</summary>
        public double GetLength () {
            return DubinsCurves.DubinsPathLength ( this );
        }

        /// <summary>Finds the length of a specific segment from the path.</summary>
        /// <param name="segment">The index (0-2) of the desired segment</param>
        public double GetSegmentLength ( int segment ) {
            return DubinsCurves.DubinsSegmentLength ( this, segment );
        }

        /// <summary>Finds the normalized length of a specific segment from the path.</summary>
        /// <param name="segment">The index (0-2) of the desired segment</param>
        public double GetNormalisedSegmentLength ( int segment ) {
            return DubinsCurves.DubinsSegmentLengthNormalized ( this, segment );
        }

        /// <summary>The path type of the path </summary>
        public DubinsPathType PathType { get; set; }

        ///<summary>Calculates the configuration along the path, using the parameter <paramref name="t"/>.</summary>
        /// <param name="t">A length measure, where <c> 0 &lt;= t &lt; length(path)</c></param>
        /// <returns><c>(x, y, theta)</c>, where theta is the gradient/tangent of
        ///         the path at <c>(x, y)</c></returns>
        public double[] Sample ( double t ) {
            double[] q = new double[3];
            DubinsCurves.DubinsPathSample ( this, t, q );
            return q;
        }

        /**
         * Walks along the path at a fixed sampling interval, calling the callback
         * function at each interval.
         *
         * The sampling process continues until the whole path is sampled, or the
         * callback returns a non-zero value
         *
         * @param stepSize the distance along the path between each successive sample
         * @param callback the callback function to call for each sample
         */
        public void SampleMany ( double stepSize, IDubinsPathSamplingCallback callback ) {
            DubinsCurves.DubinsPathSampleMany ( this, stepSize, callback );
        }

        /**
         *
         * @return <code>(x, y, theta)</code> at the endpoint of the path
         */
        public double[] GetEndpoint () {
            double[] endpoint = new double[3];
            DubinsCurves.DubinsPathEndpoint ( this, endpoint );
            return endpoint;
        }


        ///  <summary>Extracts a subset of the path.</summary>
        /// <param name="t">A length measure, where <c>0 &lt; t &lt; length(path)</c></param>
        public DubinsPath ExtractSubpath ( double t ) {
            DubinsPath extract = new DubinsPath ();
            DubinsCurves.DubinsExtractSubpath ( this, t, extract );
            return extract;
        }
    }
}