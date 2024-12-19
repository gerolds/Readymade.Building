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
using NaughtyAttributes;
using UnityEngine;

namespace Readymade.Building.Components {
    /// <summary>
    /// A token object used to query, filter and identify <see cref="Magnet"/> components during placement. Used by <see cref="Placeable"/> and <see cref="Builder"/>.
    /// </summary>
    /// <remarks>Use instances of these to define snapping semantics for your placeable objects.</remarks>
    [CreateAssetMenu (
        menuName = nameof ( Readymade ) + "/" + nameof ( Building ) + "/" + nameof ( SoMagnetIdentity ),
        fileName = "New " + nameof ( SoMagnetIdentity )
    )]
    public class SoMagnetIdentity : ScriptableObject {
        [SerializeField]
        [TextArea ( 2, 5 )]
        [Tooltip ( "A description of the semantics and usage of this magnet identity." )]
        private string description;

        [Tooltip ( "These are used for drawing gizmos in " + nameof ( Magnet ) +
            " and have no influence on snapping behaviour." )]
        [SerializeField]
        [EnumFlags]
        internal MagnetShape Shape;
    }

    /// <summary>
    /// Shape annotation values. Helpful for drawing gizmos and describing a magnet's identity. Can be used as a
    /// starting point in custom validators.
    /// </summary>
    /// <remarks>
    /// These are only used for annotations and drawing gizmos.
    /// </remarks>
    [Flags]
    public enum MagnetShape {
        /// <summary>
        /// The magnet has no defined shape.
        /// </summary>
        Nothing = 0,

        /// <summary>
        /// The magnet represents a point in space without orientation or relationship to a specific geometric feature.
        /// </summary>
        Point = 1 << 0,

        /// <summary>
        /// The magnet represents an axis of some kind, this implies the magnet's x-axis is a rotational constraint.
        /// Axes are typically on the interior of an object. 
        /// </summary>
        Axis = 1 << 1,

        /// <summary>
        /// The magnet represents an edge of some kind, this implies the magnet's x-axis is collinear with said edge. 
        /// </summary>
        Edge = 1 << 2,

        /// <summary>
        /// The magnet represents a corner of some kind, this is similar to <see cref="MagnetShape.Point"/>. 
        /// </summary>
        Corner = 1 << 3,

        /// <summary>
        /// The magnet represents a somewhat flat face. Typically this would sit in the interior of a surface plane with the magnet's forward vector aligned with the face normal.
        /// </summary>
        Face = 1 << 4,

        /// <summary>
        /// The magnet represents a fixture of some kind. Typically simple objects are attached here.
        /// </summary>
        Fixture = 1 << 5,

        /// <summary>
        /// The magnet represents connector of some kind. Typically an extruded object would be attached here, a cable, pipe, conveyor etc.
        /// </summary>
        Connector = 1 << 6,

        /// <summary>
        /// The magnet is on a vertical feature.
        /// </summary>
        Vertical = 1 << 7,

        /// <summary>
        /// The magnet is on a horizontal feature.
        /// </summary>
        Horizontal = 1 << 8,

        /// <summary>
        /// The magnet is on a foundation-like object.
        /// </summary>
        Foundation = 1 << 9,

        /// <summary>
        /// The magnet is on a volumetric frame-like object.
        /// </summary>
        Frame = 1 << 10,

        /// <summary>
        /// The magnet is on a plane-like object (e.g. a wall, door, window or panel).
        /// </summary>
        Plane = 1 << 11,

        /// <summary>
        /// The magnet is on a rod-like object (e.g. a pole, beam, mast or rail).
        /// </summary>
        Rod = 1 << 12,

        /// <summary>
        /// The magnet is on a grid-like object or implies a grid.
        /// </summary>
        Grid = 1 << 13,

        /// <summary>
        /// The magnet is in a spacer position.
        /// </summary>
        Space = 1 << 14,

        /// <summary>
        /// The magnet is some sort of anchor.
        /// </summary>
        Anchor = 1 << 15,

        /// <summary>
        /// The magnet supports light loads or connections.
        /// </summary>
        Light = 1 << 16,

        /// <summary>
        /// The magnet supports heavy loads or connections.
        /// </summary>
        Heavy = 1 << 17,

        /// <summary>
        /// The magnet is represents a deep penetration of the object.
        /// </summary>
        Deep = 1 << 18,

        /// <summary>
        /// The magnet is represents a shallows penetration of the object.
        /// </summary>
        Shallow = 1 << 19,

        /// <summary>
        /// The magnet is part of or declares a stack-like relationship.
        /// </summary>
        Stack = 1 << 20,

        /// <summary>
        /// The magnet is part of or declares a queue-like relationship.
        /// </summary>
        Queue = 1 << 21,

        /// <summary>
        /// The magnet is in a top position.
        /// </summary>
        Top = 1 << 22,

        /// <summary>
        /// The magnet is in a bottom position.
        /// </summary>
        Bottom = 1 << 23,

        /// <summary>
        /// The magnet is in a side position.
        /// </summary>
        Side = 1 << 24,
    }

    /// <summary>
    /// Magnet location and orientation annotation values. Helpful for drawing gizmos and describing a magnet's identity. Can be used as a
    /// starting point in custom validators.</summary>
    /// <remarks>
    /// These are entirely optional for <see cref="Placeable"/> and <see cref="Builder"/>.
    /// </remarks>
    [Flags]
    public enum MagnetLocation {
        /// <summary>An undefined location.</summary>
        None = 0,

        /// <summary>Towards the front.</summary>
        Anterior = 1 << 0,

        /// <summary>Towards the rear.</summary>
        Posterior = 1 << 1,

        /// <summary>Towards the belly.</summary>
        Dorsal = 1 << 2,

        /// <summary>Towards the back.</summary>
        Ventral = 1 << 3,

        /// <summary>Towards left.</summary>
        Left = 1 << 4,

        /// <summary>Towards right.</summary>
        Right = 1 << 5,

        /// <summary>Towards the main mass.</summary>
        Proximal = 1 << 6,

        /// <summary>Away from the main mass.</summary>
        Distal = 1 << 7,

        /// <summary>Towards the center.</summary>
        Central = 1 << 8,

        /// <summary>Away from the center.</summary>
        Peripheral = 1 << 9,

        /// <summary>Towards the mid-line.</summary>
        Medial = 1 << 10,

        /// <summary>Towards the sides (left/right) relative to the mid-line.</summary>
        Lateral = 1 << 11,

        /// <summary>On the inside of a structure.</summary>
        Inside = 1 << 12,

        /// <summary>On the outside of a structure.</summary>
        Outside = 1 << 13,

        /// <summary>At the end of a structure.</summary>
        Terminal = 1 << 14,

        /// <summary>Along a cardinal axis.</summary>
        Axial = 1 << 15,

        /// <summary>Around a cardinal axis.</summary>
        Radial = 1 << 16,

        /// <summary>On the inside of a cavity.</summary>
        Luminal = 1 << 17,

        /// <summary>Pertaining to asymmetry.</summary>
        Chiral = 1 << 18,
    }

    /// <summary>
    /// Magnet element/category annotation values. Helpful for describing a magnet's identity. Can be used as a
    /// starting point in custom validators.
    /// </summary>
    /// <remarks>
    /// These are entirely optional for <see cref="Placeable"/> and <see cref="Builder"/>.
    /// </remarks>
    [Flags]
    public enum MagnetElement {
        None = 0,
        Conveyor = 1 << 0, // Transmitted
        Heat = 1 << 1,
        Liquid = 1 << 2,
        Pressure = 1 << 3,
        Information = 4,
        Power = 1 << 5,
        Solid = 1 << 6,
        Special = 1 << 7,
        Teleport = 1 << 8,
        Tension = 1 << 9,
        Cargo = 1 << 10,
        People = 1 << 11,
        Airway = 1 << 12, // Vehicle pathways
        Pathway = 1 << 13,
        Railway = 1 << 14,
        Roadway = 1 << 15,
        Waterway = 1 << 16,
        Voidway = 1 << 17,
        Torque = 1 << 18, // Mechanical energy
        Translation = 1 << 19,
        Explosion = 1 << 20,
        Coarse = 1 << 21, // Quality
        Fine = 1 << 22,
        Strong = 1 << 23,
        Weak = 1 << 24,
        Important = 1 << 25,
        Light = 1 << 26,
        Heavy = 1 << 27,
        Joint = 1 << 28, // Joints
        Cut = 1 << 29,
        Weld = 1 << 30,
        Structure = 1 << 31,
    }
}