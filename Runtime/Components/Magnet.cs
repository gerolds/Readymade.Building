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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using UnityEngine;
using UnityEngine.Serialization;
using Vertx.Debugging;

namespace Readymade.Building.Components
{
    /// <inheritdoc />
    /// <summary>
    /// Marks up a location that other <see cref="Magnet"/> instances may snap to. All magnets have incoming and outgoing types
    /// that can be used to filter snap pairings. Magnets are detected by physics queries so they need a <see cref="Collider"/>
    /// component.
    /// </summary>
    /// <remarks>Magnets can be annotated with various shapes, elements and locations. These are entirely optional and will not
    /// be used in deciding snapping behaviour: The assigned token instances themselves represent the identity of the magnet.
    /// For example, given the top and sides of an object should snap only with each other, the individual magnets would
    /// each need to reference different token instances that represent the respective category, one for the top, one for the
    /// sides. This can be extended to arbitrary complexity.</remarks>
    /// <seealso cref="MagnetShape"/><seealso cref="Collider"/>
    [RequireComponent(typeof(Collider))]
    public class Magnet : MonoBehaviour
    {
        private Collider _collider;
        private Placeable _placeable;

        [BoxGroup("Passive")]
        [SerializeField]
#if ODIN_INSPECTOR
        [ListDrawerSettings(ShowPaging = false, ShowFoldout = false)]
#else
        [ReorderableList]
#endif
        [Tooltip(
            "The magnet identifies itself with these tokens. Used in comparisons with other magnets and their snapping rules.")]
        private SoMagnetIdentity[] identifier;

        [BoxGroup("Passive")]
        [SerializeField]
#if ODIN_INSPECTOR
        [ListDrawerSettings(ShowPaging = false, ShowFoldout = false)]
#else
        [ReorderableList]
#endif
        [Tooltip(
            "This magnet will accept snapping from any of the listed magnet identities. If the list is empty, all magnets will be accepted.")]
        private SoMagnetIdentity[] acceptFrom;

        [BoxGroup("Passive")]
        [SerializeField]
#if ODIN_INSPECTOR
        [ListDrawerSettings(ShowPaging = false, ShowFoldout = false)]
#else
        [ReorderableList]
#endif
        [Tooltip(
            "This magnet will reject snapping from any of the listed magnet identities. If the list is empty no magnets will be rejected.")]
        private SoMagnetIdentity[] rejectFrom;

        [BoxGroup("Active")]
        [SerializeField]
#if ODIN_INSPECTOR
        [ListDrawerSettings(ShowPaging = false, ShowFoldout = false)]
#else
        [ReorderableList]
#endif
        [HideIf(nameof(isGrid))]
        [Tooltip(
            "This magnet will want to snap to any of the listed magnet identities. If the list is empty the magnet will not snap to anything.")]
        private SoMagnetIdentity[] snapTo;

        [BoxGroup("Active")]
        [SerializeField]
        [ShowIf(nameof(WillSnap))]
        [Tooltip("The axis around which snapped objects should be rotated. Default is " + nameof(RotateAxis.WorldUp) +
            ".")]
        private RotateAxis rotateAxis = RotateAxis.WorldUp;

        [BoxGroup("Active")]
        [SerializeField]
        [ShowIf(nameof(WillSnap))]
        [Tooltip("The axis with which snapped objects should be aligned. Default is " + nameof(AlignWith.WorldUp) +
            ".")]
        private AlignWith alignWith = AlignWith.WorldUp;

        [InfoBox("Grids are an experimental feature that is still a work in progress and not ready for use yet.",
#if ODIN_INSPECTOR
            InfoMessageType.Warning
#else
            EInfoBoxType.Warning
#endif
        )]
        [BoxGroup("Grid")]
        [Tooltip(
            "Whether this magnet declares a grid to which other magnets may snap. If true the magnet can no longer actively snap to others, it merely accepts snapping. Default is false.")]
        [SerializeField]
        [ValidateInput(nameof(ValidateIsGrid), "When using a grid the magnet must have a BoxCollider")]
        private bool isGrid;

        // editor-only internal use
        private bool ValidateIsGrid(bool value)
        {
            return !value || _collider is BoxCollider;
        }

        [FormerlySerializedAs("gridSpacing")]
        [BoxGroup("Grid")]
        [ShowIf(nameof(isGrid))]
        [Tooltip("The spacing between grid points. Default is " + nameof(Vector2.one))]
        [SerializeField]
        private Vector2 gridDivisions = Vector2.one * 2f;

        private HashSet<SoMagnetIdentity> _acceptFromAny;
        private HashSet<SoMagnetIdentity> _snapToAny;
        private HashSet<SoMagnetIdentity> _rejectFromAny;
        private HashSet<SoMagnetIdentity> _identity;

        /// <summary>
        /// This magnet identifies as <see cref="Magnet"/> of this shape.
        /// </summary>
        public ISet<SoMagnetIdentity> Identity => _identity;

        /// <summary>
        /// This magnet accepts snapping from <see cref="Magnet"/> instances of this shape.
        /// </summary>
        public ISet<SoMagnetIdentity> AcceptFrom => _acceptFromAny;

        /// <summary>
        /// This magnet rejects snapping from <see cref="Magnet"/> instances of this shape.
        /// </summary>
        public ISet<SoMagnetIdentity> RejectFrom => _rejectFromAny;

        /// <summary>
        /// This magnet wants to snap to <see cref="Magnet"/> instances of this shape.
        /// </summary>
        public ISet<SoMagnetIdentity> SnapTo => _snapToAny;

        /// <summary>
        /// The alignment settings of this magnet. Will be used when this magnet is used for snapping to another.
        /// </summary>
        public AlignWith AlignWith => alignWith;

        /// <summary>
        /// The axis around which the object may be rotated.
        /// </summary>
        public RotateAxis RotateAxis => rotateAxis;

        /// <summary>
        /// The parent <see cref="Placeable"/> this magnet belongs to.
        /// </summary>
        public Placeable Placeable => _placeable;

        /// <summary>
        /// Whether the magnet declares a grid.
        /// </summary>
        public bool IsGrid => isGrid;

        /// <summary>
        /// The bounds collider of the grid. This is only defined when <see cref="IsGrid"/> is true.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the magnet is not a grid.</exception>
        public BoxCollider GridBounds
        {
            get
            {
                if (!IsGrid)
                {
                    throw new InvalidOperationException("Can only get grid bounds on a grid-magnet.");
                }

                return (BoxCollider)_collider;
            }
        }

        /// <summary>
        /// The spacing of the magnet's grid. This is only defined when <see cref="IsGrid"/> is true.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the magnet is not a grid.</exception>
        public Vector2 GridSpacing
        {
            get
            {
                if (!IsGrid)
                {
                    throw new InvalidOperationException("Can only get grid spacing on a grid-magnet.");
                }

                return gridDivisions;
            }
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void Awake() => Init();

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnEnable() => Init();

        private void Init()
        {
            _acceptFromAny = acceptFrom.ToHashSet();
            _snapToAny = snapTo.ToHashSet();
            _rejectFromAny = rejectFrom.ToHashSet();
            _identity = identifier.ToHashSet();
            _collider = GetComponent<Collider>();
            _collider.isTrigger = true;
            _placeable = GetComponentInParent<Placeable>();
        }

        /// <summary>
        /// Whether this magnet has defined some active snapping.
        /// </summary>
        /// <returns></returns>
        private bool WillSnap() => snapTo?.Any() ?? false;

        /// <summary>
        /// Checks whether a given seeker accepts snapping from a given target.
        /// </summary>
        /// <param name="seeker">The seeker <see cref="Magnet"/> instance.</param>
        /// <param name="target">The target <see cref="Magnet"/> instance.</param>
        /// <returns>Whether the <paramref name="seeker"/> accepts snapping from the <paramref name="target"/>.</returns>
        public static bool TargetAcceptsSeeker([AllowNull] Magnet seeker, [AllowNull] Magnet target)
        {
            if (seeker == null || target == null)
            {
                return false;
            }

            bool hasWhitelist = target.AcceptFrom is { Count: > 0 };
            bool hasBlacklist = target.RejectFrom is { Count: > 0 };

            return (hasWhitelist, hasBlacklist) switch
            {
                (true, true) => target.AcceptFrom.Overlaps(seeker.Identity) &&
                    !target.RejectFrom.Overlaps(seeker.Identity),
                (false, true) => !target.RejectFrom.Overlaps(seeker.Identity),
                (true, false) => target.AcceptFrom.Overlaps(seeker.Identity),
                (false, false) => false
            };
        }

        /// <summary>
        /// Checks whether a given seeker wants to snap to a given target.
        /// </summary>
        /// <param name="seeker">The seeker <see cref="Magnet"/> instance.</param>
        /// <param name="target">The target <see cref="Magnet"/> instance.</param>
        /// <returns>Whether the <paramref name="seeker"/> wants to snap to the <paramref name="target"/>.</returns>
        public static bool SeekerWantsTarget([AllowNull] Magnet seeker, [AllowNull] Magnet target)
        {
            if (seeker == null || target == null)
            {
                return false;
            }

            if (seeker.IsGrid)
            {
                return false;
            }

            bool hasWishlist = seeker.SnapTo is { Count: > 0 };
            return hasWishlist switch
            {
                true => seeker.SnapTo.Overlaps(target.Identity),
                false => false
            };
        }

        /// <summary>
        /// Checks whether a magnet can snap to a given other.
        /// </summary>
        /// <param name="from">The magnet doing the active snap seeking.</param>
        /// <param name="to">The magnet that will remain passive and serve as the snap target.</param>
        /// <returns>Whether the magnets can snap.</returns>
        public static bool CanSnapTo(Magnet from, Magnet to) =>
            from.SnapTo.Any() &&
            from.SnapTo.Overlaps(to.Identity) &&
            (!to.AcceptFrom.Any() || to.AcceptFrom.Overlaps(from.Identity)) &&
            (!to.RejectFrom.Any() || !to.RejectFrom.Overlaps(from.Identity));

        /// <summary>
        /// Checks whether this magnet can snap to a given other.
        /// </summary>
        /// <param name="other">The other magnet.</param>
        /// <returns>Whether the magnet can snap.</returns>
        public bool CanSnapTo(Magnet other) => CanSnapTo(this, other);

        /// <summary>
        /// The pose of this magnet.
        /// </summary>
        public Pose GetPose() => new(transform.position, transform.rotation);

        /// <summary>
        /// The forward-vector of this magnet.
        /// </summary>
        public Vector3 GetForward() => transform.forward;

        /// <summary>
        /// The right-vector of this magnet.
        /// </summary>
        public Vector3 GetRight() => transform.right;

        /// <summary>
        /// The up-vector of this magnet.
        /// </summary>
        public Vector3 GetUp() => transform.up;

        /// <summary>
        /// The rotation of this magnet.
        /// </summary>
        public Quaternion GetRotation() => transform.rotation;

        /// <summary>
        /// The position of this magnet.
        /// </summary>
        public Vector3 GetPosition() => transform.position;

        /// <summary>
        /// Transforms a point from the magnet's local space to world space.
        /// </summary>
        public Vector3 TransformPoint(Vector3 point) => transform.TransformPoint(point);

        /// <summary>
        /// Transforms a point from world space to the magnet's local space.
        /// </summary>
        public Vector3 InverseTransformPoint(Vector3 point) => transform.InverseTransformPoint(point);

        /// <summary>
        /// Returns the closest point in world space inside the magnet's bounds towards a given point. Will return the magnet's
        /// transform position unless the magnet declares a grid, in which case the closest grid location that still lies
        /// within the bounds of the magnet's collider will be returned. Grid points will always lie on a plane at the
        /// transform.position and transform.forward as its normal.
        /// </summary>
        /// <param name="point">The point to check against.</param>
        /// <returns>The closest point</returns>
        public Vector3 GetNearestSnapPosition(Vector3 point)
        {
            if (!isGrid)
            {
                return transform.position;
            }
            else
            {
                Plane plane = new(transform.forward, transform.position);
                Vector3 closestPointInBounds = _collider.ClosestPoint(point);
                Vector3 closestPointOnPlane = plane.ClosestPointOnPlane(closestPointInBounds);
                Vector3 closestPointLocal = transform.InverseTransformPoint(closestPointOnPlane);
                Vector3 snapPointLocal = new(
                    Mathf.Round(closestPointLocal.x * gridDivisions.x) / gridDivisions.x,
                    Mathf.Round(closestPointLocal.y * gridDivisions.y) / gridDivisions.y,
                    0
                );
                return transform.TransformPoint(snapPointLocal);
            }
        }

        private void OnDrawGizmos()
        {
            MagnetShape identityShapeUnion = MagnetShape.Nothing;

            if (identifier != null)
            {
                for (int i = 0; i < identifier.Length; i++)
                {
                    identityShapeUnion |= identifier[i]?.Shape ?? 0;
                }
            }

            Transform trs = transform;
            if (isGrid)
            {
                Vector2 gridStep = new(1f / gridDivisions.x, 1f / gridDivisions.y);
                if (!_collider)
                {
                    _collider = GetComponent<Collider>();
                }

                if (_collider is BoxCollider boxCollider)
                {
                    Vector2 gridSize = new Vector2(boxCollider.size.x, boxCollider.size.y);
                    Vector2 gridExtents = gridSize * 0.5f;
                    D.raw(new Shape.Box(trs.position, new Vector2(.3f, .3f), trs.rotation, false), Color.cyan);
                    D.raw(new Shape.Box(trs.position, new Vector2(.25f, .25f), trs.rotation, false), Color.cyan);
                    //D.raw(new Shape.Box(trs.position, gridExtents, trs.rotation, false), new Color(0, 1f, 1f, 0.3f));
                    float xOff = 0;
                    float yOff = 0;
                    for (int y = 0; y < 32; y++)
                    {
                        bool yIsEven = y % 2 == 0;
                        int yHalf = y / 2;
                        yOff = (yIsEven ? gridStep.y : -gridStep.y) * yHalf;
                        if (Mathf.Abs(yOff) > gridExtents.y + 0.01f)
                        {
                            break;
                        }

                        for (int x = 0; x < 32; x++)
                        {
                            bool xIsEven = x % 2 == 0;
                            int xHalf = x / 2;
                            if (x == 1 || y == 1)
                            {
                                // skip values that would produce duplicate points on the 0-axes
                                continue;
                            }

                            xOff = (xIsEven ? gridStep.x : -gridStep.x) * xHalf;
                            if (Mathf.Abs(xOff) > gridExtents.x + 0.01f)
                            {
                                break;
                            }

                            Vector3 gridPoint = trs.position + trs.rotation * new Vector2(xOff, yOff);
                            D.raw(new Shape.Point(gridPoint, .05f), Color.cyan);
                            if (_acceptFromAny is { Count: > 0 })
                            {
                                D.raw(new Shape.Circle(gridPoint, Camera.current.transform.forward, .08f), Color.white);
                            }
                        }
                    }
                }
            }

            if (!isGrid && acceptFrom is { Length: > 0 })
            {
                D.raw(new Shape.Circle(trs.position, Camera.current.transform.forward, .08f), Color.white);
            }

            if (!isGrid && snapTo is { Length: > 0 })
            {
                D.raw(new Shape.Circle(trs.position, Camera.current.transform.forward, .1f), Color.yellow);
            }

            if (identifier is { Length: > 0 } && (identityShapeUnion & (MagnetShape.Edge | MagnetShape.Axis)) > 0)
            {
                D.raw(new Shape.Line(trs.position + trs.right * 0.2f, trs.position - trs.right * 0.2f), Shape.XColor);
            }

            if (identifier is { Length: > 0 } && (identityShapeUnion & (MagnetShape.Edge)) > 0)
            {
                D.raw(new Shape.Line(trs.position + trs.right * 0.2f, trs.position + trs.forward * 0.2f), Shape.ZColor);
                D.raw(new Shape.Line(trs.position - trs.right * 0.2f, trs.position + trs.forward * 0.2f), Shape.ZColor);
            }

            if (identifier is { Length: > 0 } && (identityShapeUnion & (MagnetShape.Face | MagnetShape.Fixture)) > 0)
            {
                D.raw(new Shape.Circle(trs.position, trs.forward, .2f), Shape.ZColor);
                D.raw(new Shape.Line(trs.position + trs.right * 0.2f, trs.position + trs.forward * 0.2f), Shape.ZColor);
                D.raw(new Shape.Line(trs.position - trs.right * 0.2f, trs.position + trs.forward * 0.2f), Shape.ZColor);
            }

            if (identifier is { Length: > 0 } && (identityShapeUnion & MagnetShape.Axis) > 0)
            {
                D.raw(
                    new Shape.Circle(trs.position, trs.right, trs.up, .05f), Shape.XColor);
            }

            if (identifier is { Length: > 0 } && (identityShapeUnion & (MagnetShape.Corner | MagnetShape.Point)) > 0)
            {
                D.raw(new Shape.Point(trs.position, 0.1f), Color.white);
            }

            if (identifier is { Length: > 0 } && (identityShapeUnion & (MagnetShape.Connector)) > 0)
            {
                D.raw(new Shape.Line(trs.position, trs.position + trs.right * 0.2f), Shape.XColor);
                D.raw(new Shape.Line(trs.position, trs.position + trs.forward * 0.2f), Shape.ZColor);
                D.raw(new Shape.Line(trs.position + trs.right * 0.2f, trs.position + trs.forward * 0.2f), Shape.ZColor);
            }
        }

        public void OnTriggerExit(Collider other)
        {
            if (_placeable &&
                other.TryGetComponent<Magnet>(out Magnet otherMagnet) &&
                otherMagnet.GetComponentInParent<Placeable>() != _placeable
            )
            {
                _placeable.OnEndTouching(this, otherMagnet);
            }
        }

        public void OnTriggerEnter(Collider other)
        {
            if (_placeable &&
                other.TryGetComponent<Magnet>(out Magnet otherMagnet) &&
                otherMagnet.GetComponentInParent<Placeable>() != _placeable
            )
            {
                _placeable.OnStartTouching(this, otherMagnet);
            }
        }
    }

    /// <summary>
    /// How to align the magnet during snapping.
    /// </summary>
    public enum AlignWith
    {
        /// <summary>
        /// Always maintain alignment with the world-up and do not modify the orientation at all otherwise.
        /// </summary>
        WorldUp,

        /// <summary>
        /// Align the Z-vector of both magnets. Use this for objects that should be able to rotate in place.
        /// </summary>
        MagnetForward,

        /// <summary>
        /// Align the X-vector of both magnets. Use this for objects that should be able to rotate in place.
        /// </summary>
        MagnetRight,

        /// <summary>
        /// Align the Y-vector of both magnets. Use this for objects that should be able to rotate in place.
        /// </summary>
        MagnetUp,

        /// <summary>
        /// Align the XY-planes of both magnets.
        /// </summary>
        MagnetFace,
    }

    /// <summary>
    /// The axis around which the snapped object can still be rotated.
    /// </summary>
    public enum RotateAxis
    {
        /// <summary>
        /// The world up direction.
        /// </summary>
        WorldUp,

        /// <summary>
        /// Use the snap-alignment to define the rotation axis.
        /// </summary>
        Aligned,

        /// <summary>
        /// The incoming object cannot be rotated.
        /// </summary>
        None,
    }
}