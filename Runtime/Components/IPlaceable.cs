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
using Readymade.Machinery.Acting;
using UnityEngine;

namespace Readymade.Building.Components {
    /// <summary>
    /// Represents an object that can be placed by a <see cref="Builder"/>.
    /// </summary>
        public interface IPlaceable {
        /// <summary>
        /// A data object used for storing the placement state of a <see cref="IPlaceable"/>.
        /// </summary>
        [Serializable]
        public struct Memento {
            /// <summary>
            /// The pose in world space of the end handle.
            /// </summary>
            public Pose EndPose;

            /// <summary>
            /// The pose of the placeable reference transform.
            /// </summary>
            public Pose RootPose;

            /// <summary>
            /// Whether the placeable can float.
            /// </summary>
            public bool CanFloat;

            /// <summary>
            /// Whether the placeable can be deleted by the player. This allows overriding the default configuration of a placeable.
            /// </summary>
            public bool IsPlayerDeletable;

            /// <summary>
            /// Whether the placeable can be placed by the player. This allows overriding the default configuration of a placeable.
            /// </summary>
            public bool IsPlayerPlaceable;

            /// <summary>
            /// The path of the placeable in the scene hierarchy. This is used for debugging purposes.
            /// </summary>
            public string Description;
        }

        /// <summary>
        /// Called when placement of this instance was cancelled.
        /// </summary>
        public void OnAborted ();

        /// <summary>
        /// Called when placement of this instance was updated.
        /// </summary>
        public void OnUpdate ();

        /// <summary>
        /// Called when placement of this instance was finished.
        /// </summary>
        public void OnPlaced ();

        /// <summary>
        /// Called when placement of this instance was started.
        /// </summary>
        public void OnStarted ();

        /// <summary>
        /// All magnets that are part of this instance.
        /// </summary>
        public IList<Magnet> Magnets { get; }

        /// <summary>
        /// A descriptive name for this instance.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Whether this instance is blocked by other objects.
        /// </summary>
        public bool IsBlocked { get; }

        /// <summary>
        /// Checks whether this instance is blocked by other objects.
        /// </summary>
        /// <returns>Whether this instance is blocked by other objects.</returns>
        public bool CheckBlocked ();

        /// <summary>
        /// The layer mask used for blocking checks.
        /// </summary>
        public LayerMask BlockingMask { get; }

        /// <summary>
        /// The placement cost of this instance.
        /// </summary>
        public IEnumerable<PropCount> PlacementCost { get; }

        /// <summary>
        /// The deletion cost of this instance.
        /// </summary>
        public IEnumerable<PropCount> DeletionCost { get; }

        /// <summary>
        /// The refund amount when deleting this instance.
        /// </summary>
        public float DeletionRefund { get; }

        /// <summary>
        /// A description of this instance.
        /// </summary>
        public string Tooltip { get; }

        /// <summary>
        /// The end handle of this instance. Only used for connectors.
        /// </summary>
        public Magnet EndHandle { get; }

        /// <summary>
        /// Whether this instance is a connector.
        /// </summary>
        public bool IsConnector { get; }
    }
}