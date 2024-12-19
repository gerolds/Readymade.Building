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

using System;

namespace Readymade.Building.Dubins {
    class DubinsCurves {
        public const int ErrorNone = 0; // No error
        public const int ErrorColocatedConfigs = 1; // Colocated configurations
        public const int ErrorParametrization = 2; // Path parameterisitation error
        public const int ErrorBadRho = 3; // the rho value is invalid
        public const int ErrorNoPath = 4; // no connection between configurations with this word
        public const double Epsilon = ( 10e-10 );

        /**
         * Generate a path from an initial configuration to a target configuration, with
         * a specified maximum turning radii.
         * <p>
         * A configuration is (<code>x, y, theta</code>), where theta is in radians,
         * with zero along the line x = 0, and counter-clockwise is positive
         *
         * @segmentLengths path - the path object to initialise into
         * @segmentLengths q0   - starting configuration, specified as an array of
         *             <code>x, y, theta</code>
         * @segmentLengths q1   - ending configuration, specified as an array of
         *             <code>x, y, theta</code>
         * @segmentLengths rho  - turning radius of the vehicle (forward velocity divided by
         *             maximum angular velocity)
         * @return non-zero on error
         */
        public static int DubinsShortestPath ( DubinsPath path, double[] q0, double[] q1, double rho ) {
            DubinsIntermediateResults intermediateResults = new DubinsIntermediateResults ();
            double[] segmentLengths = new double[3];
            DubinsPathType bestWord = default;
            int errcode = DubinsIntermediateResults ( intermediateResults, q0, q1, rho );
            if ( errcode != ErrorNone ) {
                return errcode;
            }

            path.ConfigStart[ 0 ] = q0[ 0 ];
            path.ConfigStart[ 1 ] = q0[ 1 ];
            path.ConfigStart[ 2 ] = q0[ 2 ];
            path.Rho = rho;

            double bestCost = Double.MaxValue;
            foreach ( DubinsPathType pathType in Enum.GetValues ( typeof ( DubinsPathType ) ) ) {
                errcode = DubinsWord ( intermediateResults, pathType, segmentLengths );
                if ( errcode == ErrorNone ) {
                    double cost = segmentLengths[ 0 ] + segmentLengths[ 1 ] + segmentLengths[ 2 ];
                    if ( cost < bestCost ) {
                        bestWord = pathType;
                        bestCost = cost;
                        path.SegmentLengths[ 0 ] = segmentLengths[ 0 ];
                        path.SegmentLengths[ 1 ] = segmentLengths[ 1 ];
                        path.SegmentLengths[ 2 ] = segmentLengths[ 2 ];
                        path.PathType = pathType;
                    }
                }
            }

            if ( bestWord == null ) {
                return ErrorNoPath;
            }

            return ErrorNone;
        }

        /**
         * Generate a path with a specified word from an initial configuration to a
         * target configuration, with a specified turning radius.
         *
         * @segmentLengths path     - the path object to initialise into
         * @segmentLengths q0       - starting configuration specified as an array of x, y, theta
         * @segmentLengths q1       - ending configuration specified as an array of x, y, theta
         * @segmentLengths rho      - turning radius of the vehicle (forward velocity divided by
         *                 maximum angular velocity)
         * @segmentLengths pathType - the specific path type to use
         * @return non-zero on error
         */
        public static int DubinsPath ( DubinsPath path, double[] q0, double[] q1, double rho, DubinsPathType pathType ) {
            int errcode;
            DubinsIntermediateResults input = new ();
            errcode = DubinsIntermediateResults ( input, q0, q1, rho );
            if ( errcode == ErrorNone ) {
                double[] parameters = new double[3];
                errcode = DubinsWord ( input, pathType, parameters );
                if ( errcode == ErrorNone ) {
                    path.SegmentLengths[ 0 ] = parameters[ 0 ];
                    path.SegmentLengths[ 1 ] = parameters[ 1 ];
                    path.SegmentLengths[ 2 ] = parameters[ 2 ];
                    path.ConfigStart[ 0 ] = q0[ 0 ];
                    path.ConfigStart[ 1 ] = q0[ 1 ];
                    path.ConfigStart[ 2 ] = q0[ 2 ];
                    path.Rho = rho;
                    path.PathType = pathType;
                }
            }

            return errcode;
        }

        /**
         * Calculate the length of an initialised path.
         *
         * @segmentLengths path - the path to find the length of
         */
        public static double DubinsPathLength ( DubinsPath path ) {
            double length = 0.0;
            length += path.SegmentLengths[ 0 ];
            length += path.SegmentLengths[ 1 ];
            length += path.SegmentLengths[ 2 ];
            length = length * path.Rho;
            return length;
        }

        /**
         * Return the length of a specific segment in an initialized path.
         *
         * @segmentLengths path - the path to find the length of
         * @segmentLengths i    - the segment you to get the length of (0-2)
         */
        public static double DubinsSegmentLength ( DubinsPath path, int i ) {
            if ( ( i < 0 ) || ( i > 2 ) ) {
                return Double.MaxValue;
            }

            return path.SegmentLengths[ i ] * path.Rho;
        }

        /**
         * Return the normalized length of a specific segment in an initialized path.
         *
         * @segmentLengths path - the path to find the length of
         * @segmentLengths i    - the segment you to get the length of (0-2)
         */
        public static double DubinsSegmentLengthNormalized ( DubinsPath path, int i ) {
            if ( i < 0 || i > 2 ) {
                return Double.MaxValue;
            }

            return path.SegmentLengths[ i ];
        }

        /**
         * Extract an integer that represents which path type was used.
         *
         * @segmentLengths path - an initialised path
         * @return - one of LSL, LSR, RSL, RSR, RLR or LRL
         */
        public static DubinsPathType GetDubinsPathType ( DubinsPath path ) {
            return path.PathType;
        }

        /**
         * Calculate the configuration along the path, using the parameter t.
         *
         * @segmentLengths path - an initialised path
         * @segmentLengths t    - a length measure, where 0 <= t < dubins_path_length(path)
         * @segmentLengths q    - the configuration result
         * @returns - non-zero if 't' is not in the correct range
         */
        public static int DubinsPathSample ( DubinsPath path, double t, double[] q ) {
            /* tprime is the normalised variant of the parameter t */
            double tPrime = t / path.Rho;
            double[] qi = new double[3]; // The translated initial configuration
            double[] q1 = new double[3]; // end-of segment 1
            double[] q2 = new double[3]; // end-of segment 2
            SegmentType[] types = path.PathType.GetSegments ();
            double p1;
            double p2;

            if ( t < 0 || t > DubinsPathLength ( path ) ) {
                return ErrorParametrization;
            }

            /* initial configuration */
            qi[ 0 ] = 0.0;
            qi[ 1 ] = 0.0;
            qi[ 2 ] = path.ConfigStart[ 2 ];

            /* generate the target configuration */
            p1 = path.SegmentLengths[ 0 ];
            p2 = path.SegmentLengths[ 1 ];
            DubinsSegment ( p1, qi, q1, types[ 0 ] );
            DubinsSegment ( p2, q1, q2, types[ 1 ] );
            if ( tPrime < p1 ) {
                DubinsSegment ( tPrime, qi, q, types[ 0 ] );
            } else if ( tPrime < ( p1 + p2 ) ) {
                DubinsSegment ( tPrime - p1, q1, q, types[ 1 ] );
            } else {
                DubinsSegment ( tPrime - p1 - p2, q2, q, types[ 2 ] );
            }

            /*
             * scale the target configuration, translate back to the original starting point
             */
            q[ 0 ] = q[ 0 ] * path.Rho + path.ConfigStart[ 0 ];
            q[ 1 ] = q[ 1 ] * path.Rho + path.ConfigStart[ 1 ];
            q[ 2 ] = ModTwoPi ( q[ 2 ] );

            return ErrorNone;
        }

        /**
         * Walk along the path at a fixed sampling interval, calling the callback
         * function at each interval.
         *
         * The sampling process continues until the whole path is sampled, or the
         * callback returns a non-zero value
         *
         * @segmentLengths path      - the path to sample
         * @segmentLengths stepSize  - the distance along the path for subsequent samples
         * @segmentLengths cb        - the callback function to call for each sample
         * @segmentLengths user_data - optional information to pass on to the callback
         *
         * @returns - zero on successful completion, or the result of the callback
         */
        public static int DubinsPathSampleMany ( DubinsPath path, double stepSize, IDubinsPathSamplingCallback cb ) {
            int errcode;
            double[] q = new double[3];
            double x = 0.0;
            double length = DubinsPathLength ( path );
            while ( x < length ) {
                DubinsPathSample ( path, x, q );
                errcode = cb.invoke ( q, x );
                if ( errcode != 0 ) {
                    return errcode;
                }

                x += stepSize;
            }

            return 0;
        }

        /**
         * Convenience function to identify the endpoint of a path.
         *
         * @segmentLengths path - an initialised path
         * @segmentLengths q    - the configuration result
         */
        public static int DubinsPathEndpoint ( DubinsPath path, double[] q ) {
            return DubinsPathSample ( path, DubinsPathLength ( path ) - Epsilon, q );
        }

        /**
         * Convenience function to extract a subset of a path.
         *
         * @segmentLengths path    - an initialised path
         * @segmentLengths t       - a length measure, where 0 < t < dubins_path_length(path)
         * @segmentLengths newpath - the resultant path
         */
        public static int DubinsExtractSubpath ( DubinsPath path, double t, DubinsPath newPath ) {
            /* calculate the true parameter */
            double tPrime = t / path.Rho;

            if ( ( t < 0 ) || ( t > DubinsPathLength ( path ) ) ) {
                return ErrorParametrization;
            }

            /* copy most of the data */
            newPath.ConfigStart[ 0 ] = path.ConfigStart[ 0 ];
            newPath.ConfigStart[ 1 ] = path.ConfigStart[ 1 ];
            newPath.ConfigStart[ 2 ] = path.ConfigStart[ 2 ];
            newPath.Rho = path.Rho;
            newPath.PathType = path.PathType;

            /* fix the parameters */
            newPath.SegmentLengths[ 0 ] = Math.Min ( path.SegmentLengths[ 0 ], tPrime );
            newPath.SegmentLengths[ 1 ] = Math.Min ( path.SegmentLengths[ 1 ], tPrime - newPath.SegmentLengths[ 0 ] );
            newPath.SegmentLengths[ 2 ] = Math.Min ( path.SegmentLengths[ 2 ],
                tPrime - newPath.SegmentLengths[ 0 ] - newPath.SegmentLengths[ 1 ] );
            return 0;
        }

        private static int DubinsWord ( DubinsIntermediateResults inter, DubinsPathType pathType, double[] outLengths ) {
            int errcode;
            switch ( pathType ) {
                case DubinsPathType.LSL:
                    errcode = Dubins_LSL ( inter, outLengths );
                    break;
                case DubinsPathType.RSL:
                    errcode = Dubins_RSL ( inter, outLengths );
                    break;
                case DubinsPathType.LSR:
                    errcode = Dubins_LSR ( inter, outLengths );
                    break;
                case DubinsPathType.RSR:
                    errcode = Dubins_RSR ( inter, outLengths );
                    break;
                case DubinsPathType.LRL:
                    errcode = Dubins_LRL ( inter, outLengths );
                    break;
                case DubinsPathType.RLR:
                    errcode = Dubins_RLR ( inter, outLengths );
                    break;
                default:
                    errcode = ErrorNoPath;
                    break;
            }

            return errcode;
        }

        private static int DubinsIntermediateResults ( DubinsIntermediateResults results, double[] q0, double[] q1, double rho ) {
            double dx;
            double dy;
            double D;
            double d;
            double theta;
            double alpha;
            double beta;
            if ( rho <= 0.0 ) {
                return ErrorBadRho;
            }

            dx = q1[ 0 ] - q0[ 0 ];
            dy = q1[ 1 ] - q0[ 1 ];
            D = Math.Sqrt ( dx * dx + dy * dy );
            d = D / rho;
            theta = 0;

            /* test required to prevent domain errors if dx=0 and dy=0 */
            if ( d > 0 ) {
                theta = ModTwoPi ( Math.Atan2 ( dy, dx ) );
            }

            alpha = ModTwoPi ( q0[ 2 ] - theta );
            beta = ModTwoPi ( q1[ 2 ] - theta );

            results.alpha = alpha;
            results.beta = beta;
            results.d = d;
            results.sa = Math.Sin ( alpha );
            results.sb = Math.Sin ( beta );
            results.ca = Math.Cos ( alpha );
            results.cb = Math.Cos ( beta );
            results.c_ab = Math.Cos ( alpha - beta );
            results.d_sq = d * d;

            return ErrorNone;
        }

        private static void DubinsSegment ( double t, double[] qi, double[] qt, SegmentType type ) {
            double st = Math.Sin ( qi[ 2 ] );
            double ct = Math.Cos ( qi[ 2 ] );
            if ( type == SegmentType.L_SEG ) {
                qt[ 0 ] = +Math.Sin ( qi[ 2 ] + t ) - st;
                qt[ 1 ] = -Math.Cos ( qi[ 2 ] + t ) + ct;
                qt[ 2 ] = t;
            } else if ( type == SegmentType.R_SEG ) {
                qt[ 0 ] = -Math.Sin ( qi[ 2 ] - t ) + st;
                qt[ 1 ] = +Math.Cos ( qi[ 2 ] - t ) - ct;
                qt[ 2 ] = -t;
            } else if ( type == SegmentType.S_SEG ) {
                qt[ 0 ] = ct * t;
                qt[ 1 ] = st * t;
                qt[ 2 ] = 0.0;
            }

            qt[ 0 ] += qi[ 0 ];
            qt[ 1 ] += qi[ 1 ];
            qt[ 2 ] += qi[ 2 ];
        }

        private static int Dubins_LSL ( in DubinsIntermediateResults inter, double[] outLengths ) {
            double tmp0;
            double tmp1;
            double p_sq;

            tmp0 = inter.d + inter.sa - inter.sb;
            p_sq = 2 + inter.d_sq - ( 2 * inter.c_ab ) + ( 2 * inter.d * ( inter.sa - inter.sb ) );

            if ( p_sq >= 0 ) {
                tmp1 = Math.Atan2 ( ( inter.cb - inter.ca ), tmp0 );
                outLengths[ 0 ] = ModTwoPi ( tmp1 - inter.alpha );
                outLengths[ 1 ] = Math.Sqrt ( p_sq );
                outLengths[ 2 ] = ModTwoPi ( inter.beta - tmp1 );
                return ErrorNone;
            }

            return ErrorNoPath;
        }

        private static int Dubins_RSR ( in DubinsIntermediateResults inter, double[] outLengths ) {
            double tmp0 = inter.d - inter.sa + inter.sb;
            double p_sq = 2 + inter.d_sq - ( 2 * inter.c_ab ) + ( 2 * inter.d * ( inter.sb - inter.sa ) );
            if ( p_sq >= 0 ) {
                double tmp1 = Math.Atan2 ( ( inter.ca - inter.cb ), tmp0 );
                outLengths[ 0 ] = ModTwoPi ( inter.alpha - tmp1 );
                outLengths[ 1 ] = Math.Sqrt ( p_sq );
                outLengths[ 2 ] = ModTwoPi ( tmp1 - inter.beta );
                return ErrorNone;
            }

            return ErrorNoPath;
        }

        private static int Dubins_LSR ( in DubinsIntermediateResults inter, double[] outLengths ) {
            double p_sq = -2 + ( inter.d_sq ) + ( 2 * inter.c_ab ) + ( 2 * inter.d * ( inter.sa + inter.sb ) );
            if ( p_sq >= 0 ) {
                double p = Math.Sqrt ( p_sq );
                double tmp0 = Math.Atan2 ( ( -inter.ca - inter.cb ), ( inter.d + inter.sa + inter.sb ) ) - Math.Atan2 ( -2.0, p );
                outLengths[ 0 ] = ModTwoPi ( tmp0 - inter.alpha );
                outLengths[ 1 ] = p;
                outLengths[ 2 ] = ModTwoPi ( tmp0 - ModTwoPi ( inter.beta ) );
                return ErrorNone;
            }

            return ErrorNoPath;
        }

        private static int Dubins_RSL ( in DubinsIntermediateResults inter, double[] outLengths ) {
            double p_sq = -2 + inter.d_sq + ( 2 * inter.c_ab ) - ( 2 * inter.d * ( inter.sa + inter.sb ) );
            if ( p_sq >= 0 ) {
                double p = Math.Sqrt ( p_sq );
                double tmp0 = Math.Atan2 ( ( inter.ca + inter.cb ), ( inter.d - inter.sa - inter.sb ) ) - Math.Atan2 ( 2.0, p );
                outLengths[ 0 ] = ModTwoPi ( inter.alpha - tmp0 );
                outLengths[ 1 ] = p;
                outLengths[ 2 ] = ModTwoPi ( inter.beta - tmp0 );
                return ErrorNone;
            }

            return ErrorNoPath;
        }

        private static int Dubins_RLR ( in DubinsIntermediateResults input, double[] outLengths ) {
            double tmp0 = ( 6.0 - input.d_sq + 2 * input.c_ab + 2 * input.d * ( input.sa - input.sb ) ) / 8.0;
            double phi = Math.Atan2 ( input.ca - input.cb, input.d - input.sa + input.sb );
            if ( Math.Abs ( tmp0 ) <= 1 ) {
                double p = ModTwoPi ( ( 2 * Math.PI ) - Math.Acos ( tmp0 ) );
                double t = ModTwoPi ( input.alpha - phi + ModTwoPi ( p / 2.0 ) );
                outLengths[ 0 ] = t;
                outLengths[ 1 ] = p;
                outLengths[ 2 ] = ModTwoPi ( input.alpha - input.beta - t + ModTwoPi ( p ) );
                return ErrorNone;
            }

            return ErrorNoPath;
        }

        private static int Dubins_LRL ( in DubinsIntermediateResults inter, double[] outLengths ) {
            double tmp0 = ( 6.0 - inter.d_sq + 2 * inter.c_ab + 2 * inter.d * ( inter.sb - inter.sa ) ) / 8.0;
            double phi = Math.Atan2 ( inter.ca - inter.cb, inter.d + inter.sa - inter.sb );
            if ( Math.Abs ( tmp0 ) <= 1 ) {
                double p = ModTwoPi ( 2 * Math.PI - Math.Acos ( tmp0 ) );
                double t = ModTwoPi ( -inter.alpha - phi + p / 2.0 );
                outLengths[ 0 ] = t;
                outLengths[ 1 ] = p;
                outLengths[ 2 ] = ModTwoPi ( ModTwoPi ( inter.beta ) - inter.alpha - t + ModTwoPi ( p ) );
                return ErrorNone;
            }

            return ErrorNoPath;
        }

        private static double FloorMod ( double x, double y ) {
            return x - y * Math.Floor ( x / y );
        }

        private static double ModTwoPi ( double theta ) {
            return FloorMod ( theta, 2 * Math.PI );
        }
    }

    public interface IDubinsPathSamplingCallback {
        int invoke ( double[] q, double t );
    }

    public class DubinsIntermediateResults {
        public double alpha;
        public double beta;
        public double d;
        public double sa;
        public double sb;
        public double ca;
        public double cb;
        public double c_ab;
        public double d_sq;
    }
}