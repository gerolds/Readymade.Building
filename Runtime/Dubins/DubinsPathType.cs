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
    /**
     * Each segment motion primitive applies a constant action over an interval of
     * time.
     */
    public enum SegmentType {
        L_SEG,
        S_SEG,
        R_SEG
    }

    /**
     * Describes each possible kind of shortest path.
     * <p>
     * Dubins cars have 3 controls: “turn left at maximum”, “turn right at maximum”,
     * and “go straight”. All the paths traced out by the Dubin’s car are
     * combinations of these three controls. Let’s name the controls: “turn left at
     * maximum” will be L, “turn right at maximum” will be R, and “go straight” will
     * be S. There are only 6 combinations of these controls that describe ALL the
     * shortest paths, and they are: RSR, LSL, RSL, LSR, RLR, and LRL.
     */
    public enum DubinsPathType {
        LSL,
        LSR,
        RSL,
        RSR,
        RLR,
        LRL
    }

    public static class DubinsPathSegments {
        private static readonly SegmentType[][] _values = new SegmentType[6][] {
            new[] {SegmentType.L_SEG, SegmentType.S_SEG, SegmentType.L_SEG},
            new[] {SegmentType.L_SEG, SegmentType.S_SEG, SegmentType.R_SEG},
            new[] {SegmentType.R_SEG, SegmentType.S_SEG, SegmentType.L_SEG},
            new[] {SegmentType.R_SEG, SegmentType.S_SEG, SegmentType.R_SEG},
            new[] {SegmentType.R_SEG, SegmentType.L_SEG, SegmentType.R_SEG},
            new[] {SegmentType.L_SEG, SegmentType.R_SEG, SegmentType.L_SEG}
        };

        public static SegmentType[] GetSegments ( this DubinsPathType pathType ) => _values[ ( int ) pathType ];
    }
}