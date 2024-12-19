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
using System.Linq;
using System.Threading;
using App.Core.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Cysharp.Threading.Tasks;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using Readymade.Machinery.Acting;
using Readymade.Utils.Patterns;
using Readymade.Persistence;
using Readymade.Building;
using Readymade.Machinery.Shared;
using UnityEngine.Pool;
using Vertx.Debugging;

namespace Readymade.Building.Components
{
    /// <inheritdoc cref="IPlaceable"/>
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PackIdentity))]
    public class Placeable : PackableComponent<IPlaceable.Memento>, IPlaceable, ITooltip
    {
        private const float BOUNDS_EXPANSION = 0.125f;

        /// <summary>
        /// Concretion of a <see cref="UnityEvent{T0}"/>.
        /// </summary>
        [Serializable]
        public class ConnectedUnityEvent : UnityEvent<bool>
        {
        }

        /// <summary>
        /// A modifier that can be applied to the Builder's overlap radius when this placeable is selected.
        /// </summary>
        public enum OverlapRadiusModifier
        {
            /// <summary>
            /// No modification was selected, this indicates a default value or "no choice".
            /// </summary>
            Undefined = 0,

            /// <summary>
            /// No modification. Use the base radius.
            /// </summary>
            None = Builder.MaxOverlapModifier / Builder.MaxOverlapModifier,

            /// <summary>
            /// Use half the base radius.
            /// </summary>
            Half = Builder.MaxOverlapModifier / 8,

            /// <summary>
            /// Use quarter of the base radius.
            /// </summary>
            Quarter = Builder.MaxOverlapModifier / 4,

            /// <summary>
            /// Use an eighth of the base radius.
            /// </summary>
            Eighth = Builder.MaxOverlapModifier / 2,

            /// <summary>
            /// Use a sixteenth of the base radius.
            /// </summary>
            Sixteenth = Builder.MaxOverlapModifier,

            /// <summary>
            /// Use 1.0625x the base radius.
            /// </summary>
            PlusSixteenth = Builder.MaxOverlapModifier + Builder.MaxOverlapModifier / Builder.MaxOverlapModifier,

            /// <summary>
            /// Use 1.125x the base radius.
            /// </summary>
            PlusEighth = Builder.MaxOverlapModifier + Builder.MaxOverlapModifier / 8,

            /// <summary>
            /// Use 1.25x the base radius.
            /// </summary>
            PlusQuarter = Builder.MaxOverlapModifier + Builder.MaxOverlapModifier / 4,

            /// <summary>
            /// Use 1.5x the base radius.
            /// </summary>
            PlusHalf = Builder.MaxOverlapModifier + Builder.MaxOverlapModifier / 2,

            /// <summary>
            /// Use 2x the base radius.
            /// </summary>
            Double = Builder.MaxOverlapModifier * 2,

            /// <summary>
            /// Use 3x the base radius.
            /// </summary>
            Triple = Builder.MaxOverlapModifier * 3,

            /// <summary>
            /// Use 4x the base radius.
            /// </summary>
            Quadruple = Builder.MaxOverlapModifier * 4,
        }


        [Tooltip("A descriptive name for this placeable.")]
        [SerializeField]
        private string displayName;

        [Tooltip("A short description of this placeable.")]
        [SerializeField]
        [TextArea(3, 15)]
        private string tooltip;

        [BoxGroup("Progression")]
        [Tooltip("The keys that unlock this placeable.")]
        [SerializeField]
#if ODIN_INSPECTOR
        [AssetList]
#else
        [ReorderableList]
#endif
        private SoProp[] unlockedBy;

        [BoxGroup("Resources")]
        [FormerlySerializedAs("placementPlacementCost")]
        [Tooltip("The resources needed place the object. Leave empty for no cost.")]
#if ODIN_INSPECTOR
        //[ListDrawerSettings(ShowFoldout = false, ShowItemCount = true, ShowPaging = false)]
        [TableList(AlwaysExpanded = true, ShowPaging = false)]
#else
        [ReorderableList]
#endif
        [SerializeField]
        private PropCount[] placementCost;

        [BoxGroup("Resources")]
        [Tooltip("The resources needed to delete the object. Leave empty for no cost.")]
        [SerializeField]
#if ODIN_INSPECTOR
        [TableList(AlwaysExpanded = true, ShowPaging = false)]
#else
        [ReorderableList]
#endif
        private PropCount[] deletionCost;

        [BoxGroup("Resources")]
        [Tooltip(
            "The percentage of the placement cost that is refunded when the object is deleted. Default is 1 (full refund).")]
        [SerializeField]
        [Range(0, 1f)]
        private float deletionRefund = 1f;

        [BoxGroup("Pointer")]
        [Tooltip("Modifies the standard overlap radius defined in the Builder component when this placeable is " +
            "selected. This should roughly correspond to the mean distance between snap positions that this " +
            "placeable will want to snap to. Useful for making placement of small objects feel more precise " +
            "and for allowing free-form placement close to snap points. " +
            "Default is " + nameof(OverlapRadiusModifier.None))]
        [SerializeField]
        private OverlapRadiusModifier overlapModifier = OverlapRadiusModifier.None;

        [BoxGroup("Blocking")]
        [SerializeField]
        [Tooltip("Whether this placeable responds to blocking colliders.")]
        private bool respectBlockers = true;

        [BoxGroup("Blocking")]
        [SerializeField]
        [Tooltip("The shape that is used to determine whether this placeable is blocked for placement.")]
        [EnableIf(nameof(respectBlockers))]
#if ODIN_INSPECTOR
        [InlineButton(nameof(AddBlocker), "Create", ShowIf = "!" + nameof(blockingShape))]
#endif
        private BoxCollider blockingShape;

        private void AddBlocker()
        {
            var go = new GameObject("Blocker", typeof(BoxCollider));
            go.transform.SetParent(transform);
            blockingShape = go.GetComponent<BoxCollider>();
            blockingShape.isTrigger = true;
        }

        [BoxGroup("Blocking")]
        [SerializeField]
        [Tooltip("Whether to gather all box-colliders in children that match " + nameof(blockingMask) +
            " and check all of them. ")]
        [EnableIf(nameof(respectBlockers))]
        private bool compositeBlockingShape = true;

        [Tooltip("The layer mask that identifies objects that block placement.")]
        [BoxGroup("Blocking")]
        [SerializeField]
        [EnableIf(nameof(respectBlockers))]
        private LayerMask blockingMask;

        [BoxGroup("Statics")]
        [SerializeField]
        [Tooltip("Whether this placeable must always touch another placeable to stay alive.")]
        private bool canFloat = true;

        [BoxGroup("Statics")]
        [SerializeField]
        [Tooltip("When enabled this placeable can only be placed when snapped to another element.")]
        private bool mustSnap;

        [Tooltip("The layer mask that identifies objects that should be considered to be stable ground.")]
        [BoxGroup("Statics")]
        [SerializeField]
        [ShowIf(nameof(CanBeGrounded))]
        private LayerMask groundMask;

        /// <summary>
        /// Whether this placeable can be placed on the ground. The value of this property is constant at runtime.
        /// </summary>
        private bool CanBeGrounded => !canFloat && !mustSnap;

        [field: BoxGroup("Statics")]
        [field: HideIf(nameof(canFloat))]
        [field: SerializeField]
        [field: Tooltip("The prefab to activate when this placeable is floating.")]
        public GameObject WhileDisconnected { get; set; }

        [BoxGroup("Statics")]
        [HideIf(nameof(canFloat))]
        [SerializeField]
        [Tooltip("The delay after which this placeable will be destroyed if it is floating.")]
        private float destroyFloatingDelay = 4f;

        [BoxGroup("Connector")]
        [SerializeField]
        [Tooltip(
            "When enabled this placeable will require two clicks to build, the first will define the start location and the second will be the end.")]
        private bool isConnector;

        [BoxGroup("Connector")]
        [SerializeField]
        [ShowIf(nameof(isConnector))]
        [Tooltip("Whether this connection is required to touch a magnet at both ends to stay alive.")]
        private bool requireConnection;

        [BoxGroup("Connector")]
        [Tooltip("Whether to constrain placement of the second point to the start-handle's y-axis.")]
        [SerializeField]
        [ShowIf(nameof(isConnector))]
        private bool constrainToAxis;

        [BoxGroup("Connector")]
        [Tooltip("The grid to snap the connector end point to. When 0, no snapping is applied. Default is 0.")]
        [SerializeField]
        [ShowIf(nameof(isConnector))]
        [Min(0)]
        private float constrainToGrid = 0;

        [BoxGroup("Connector")]
        [Tooltip("Min/Max distance between the connector end and start. Default is (0, 50).")]
        [SerializeField]
        [ShowIf(nameof(isConnector))]
        private Vector2 constrainToDistance = Vector2.right * 50f;

        [BoxGroup("Connector")]
        [SerializeField]
        [ShowIf(nameof(isConnector))]
        [Tooltip("This magnet will be used for snapping the start position of this placeable.")]
        private Magnet startHandle;

        [BoxGroup("Connector")]
        [SerializeField]
        [ShowIf(nameof(isConnector))]
        [InfoBox(
            "This magnet will be used for snapping the end position of this placeable and be ignored for placing the start.")]
        private Magnet endHandle;

        [BoxGroup("Validation")]
#if ODIN_INSPECTOR
        [AssetList]
#else
        [ReorderableList]
#endif
        [SerializeField]
        [Tooltip("These validators decide whether the a given snap option is valid for this placeable.")]
        private SoSnapValidator[] snapValidators;

        [BoxGroup("Validation")]
#if ODIN_INSPECTOR
        [AssetList]
#else
        [ReorderableList]
#endif
        [SerializeField]
        [Tooltip("These validators decide whether the item can be deleted by the player.")]
        private SoDeleteValidator[] deleteValidators;

        [FormerlySerializedAs("isUserPlaceable")]
        [FormerlySerializedAs("isPlaceable")]
        [BoxGroup("Internal")]
        [Tooltip(
            "Whether this object is placeable by the player. This acts as an AND-condition to any validators that represent dynamically evaluated conditions.")]
        [SerializeField]
        private bool isPlayerPlaceable = true;

        [FormerlySerializedAs("isDeletable")]
        [BoxGroup("Internal")]
        [Tooltip(
            "Whether this object is deletable by the player. This acts as an AND-condition to any validators that represent dynamically evaluated conditions.")]
        [SerializeField]
        private bool isPlayerDeletable = true;

        [BoxGroup("Visuals")]
#if ODIN_INSPECTOR
        [ListDrawerSettings(ShowFoldout = false, ShowItemCount = true, ShowPaging = false, IsReadOnly = true)]
#else
        [ReorderableList]
#endif
        [SerializeField]
        [Tooltip("The rendered models of this placeable. Used for highlighting and various effects.")]
        private GameObject[] models;

        [BoxGroup("Gameplay")]
        [SerializeField]
#if ODIN_INSPECTOR
        [InlineButton(nameof(AddGameplay), "Create", ShowIf = nameof(whenPlaced) + ".Count == 0")]
        [ChildGameObjectsOnly]
#else
        [ReorderableList]
#endif
        [Tooltip(
            "The GameObjects to activate when the placeable is successfully placed. This allows keeping gameplay disabled while the object is being placed.")]
        private List<GameObject> whenPlaced;

        private void AddGameplay()
        {
            var go = new GameObject("Gameplay");
            go.transform.SetParent(transform);
            whenPlaced.Add(go);
        }

        [BoxGroup("Events")]
        [InfoBox("Any components implementing " + nameof(IPlaceableStarted) + ", " + nameof(IPlaceableUpdated) + ", " +
            nameof(IPlaceablePlaced) + " or " + nameof(IPlaceableAborted) + " will have their handlers called.")]
        [Space]
        [SerializeField]
        [Tooltip("Called when this placeable is instantiated.")]
        private UnityEvent onStarted;

        [BoxGroup("Events")]
        [SerializeField]
        [Tooltip("Called every time while this placeable is instantiated and has changed its position.")]
        private UnityEvent onUpdate;

        [BoxGroup("Events")]
        [SerializeField]
        [Tooltip("Called if placement has completed successfully.")]
        private UnityEvent onPlaced;

        [BoxGroup("Events")]
        [SerializeField]
        [Tooltip("Called when the placeable has been deleted.")]
        private UnityEvent onDeleted;

        [BoxGroup("Events")]
        [SerializeField]
        [Tooltip("Called if placement has started but was aborted before finishing.")]
        private UnityEvent onAborted;

        [BoxGroup("Events")]
        [SerializeField]
        [Tooltip("Called before this placeable is instantiated.")]
        private UnityEvent onValidate;

        [BoxGroup("Events")]
        [SerializeField]
        [Tooltip("Called before this placeable is instantiated.")]
        private ConnectedUnityEvent onConnected;

        /// <summary>
        /// Whether to print debug messages.
        /// </summary>
        [SerializeField]
        private bool debug;

        private static Collider[] s_blockingHits = new Collider[64];
        private List<Magnet> _magnets = new();
        private List<Collider> _surfaces = new();
        private Dictionary<Magnet, Magnet> _contactMap = new();
        private bool _isConfigured;
        private float _startedFloating;
        private float _momentaryDeletionRefund;
        private IPlaceable.Memento _placeableGameObjectConfiguration;
        private CancellationTokenSource _floatCts = new();
        private bool _isContactsDirty;
        private bool _wasStartedByBuilderCallback;
        private bool _wasDeletedByBuilderCallback;
        private PlaceableSystem _system;

        /// <summary>
        /// All active magnets under this placeable.
        /// </summary>
        public IList<Magnet> Magnets => _magnets;

        /// <summary>
        /// A descriptive name for this placeable.
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// Whether this placeable is currently blocked for placement in the position it is in. This property is dynamically updated.
        /// </summary>
        public bool IsBlocked => CheckBlocked();

        /// <summary>
        /// Whether this placeable is on stable ground or provides stable ground by itself because it <see cref="CanFloat"/>. This is a cached property assigned immediately after placement.
        /// </summary>
        public bool IsStableGround { get; private set; }

        /// <summary>
        /// Whether this placeable can float. If true, it will not be deleted when it is not connected to stable ground.
        /// </summary>
        public bool CanFloat => canFloat;

        /// <summary>
        /// Whether this placeable must snap to a magnet to be confirmed.
        /// </summary>
        public bool MustSnap => mustSnap;

        /// <summary>
        /// The world position that is considered to be the center of the object. Should typically be the center of mass.
        /// </summary>
        public Vector3 Center => blockingShape.transform.TransformPoint(blockingShape.center);

        /// <summary>
        /// The layer mask that identifies objects that block placement.
        /// </summary>
        public LayerMask BlockingMask => blockingMask;

        /// <summary>
        /// The layer mask that identifies objects that should be considered to be stable ground.
        /// </summary>
        public LayerMask GroundMask => groundMask;

        /// <summary>
        /// Resources needed to place the object. Empty if no cost.
        /// </summary>
        public IEnumerable<PropCount> PlacementCost => placementCost;

        /// <summary>
        /// Resources needed to delete the object. Empty if no cost.
        /// </summary>
        public IEnumerable<PropCount> DeletionCost => deletionCost;

        /// <summary>
        /// Percentage of the placement cost that is refunded when the object is deleted.
        /// </summary>
        public float DeletionRefund => _momentaryDeletionRefund;

        /// <summary>
        /// The description of this placeable.
        /// </summary>
        public string Tooltip => tooltip;

        /// <summary>
        /// The end-handle of this placeable. Only valid if <see cref="IsConnector"/> is true.
        /// </summary>
        public Magnet EndHandle => endHandle;

        /// <summary>
        /// The start-handle of this placeable. Only valid if <see cref="IsConnector"/> is true.
        /// </summary>
        public Magnet StartHandle => startHandle;

        /// <summary>
        /// Whether this placeable is a connector and requires 2 confirmations to place, i.e placement of a start and end.
        /// </summary>
        public bool IsConnector => isConnector;

        /// <summary>
        /// All rendered models of this placeable.
        /// </summary>
        public GameObject[] Models => models;

        /// <summary>
        /// All magnets (except its own) that this placeable is currently touching.
        /// </summary>
        /// <returns>An enumeration of magnets.</returns>
        /// <remarks>Use magnet's <see cref="Magnet.Placeable"/> property to access the cached placeable component on the other object.</remarks>
        public IEnumerable<Magnet> GetContacts() => _contactMap.Keys;

        /// <summary>
        /// Whether the end-handle has its placement constrained to an axis. 
        /// </summary>
        public bool IsConstrainedEndHandle => isConnector && constrainToAxis;

        /// <summary>
        /// The increment at which a constrained end-handle can be placed.
        /// </summary>
        public float GridConstraintEndHandle => constrainToGrid;

        /// <summary>
        /// The distance range at which an end handle can be placed.
        /// </summary>
        public Vector2 DistanceRangeEndHandle => constrainToDistance;

        /// <summary>
        /// The transient (reference-)position of this placeable.
        /// </summary>
        public Vector3 Position => transform.position;

        /// <summary>
        /// The current distance between the start and end handle. Will be 0 if <see cref="IsConnector"/> is false. 
        /// </summary>
        public float ConnectorLength =>
            IsConnector ? Vector3.Distance(EndHandle.transform.position, transform.position) : 0;

        /// <summary>
        /// The overlap radius modifier to apply to the Builder's overlap radius when this placeable is selected.
        /// </summary>
        public float OverlapModifier
        {
            get
            {
                if (overlapModifier == OverlapRadiusModifier.Undefined)
                {
                    return (int)OverlapRadiusModifier.None;
                }

                return (int)overlapModifier <= Builder.MaxOverlapModifier
                    ? (int)overlapModifier
                    : (int)overlapModifier / (float)Builder.MaxOverlapModifier;
            }
        }

        /// <summary>
        /// The <see cref="IAssetIdentity.AssetID"/> of the <see cref="PackIdentity"/> on this placeable.
        /// </summary>
        public Guid AssetID => GetComponent<PackIdentity>().AssetID;

        /// <summary>
        /// Whether this placeable can be placed by a player.
        /// </summary>
        public bool IsPlayerPlaceable => isPlayerPlaceable;

        /// <summary>
        /// Whether this placeable can be deleted by a player.
        /// </summary>
        public bool IsPlayerDeletable => isPlayerDeletable;

        /// <summary>
        /// The key that unlocks this placeable.
        /// </summary>
        public IEnumerable<SoProp> UnlockedBy => unlockedBy;

        /// <summary>
        /// Whether this placeable has had its <see cref="OnPlaced"/> routine called. This property will return false while the
        /// placeable is still in ghost-mode during interactive placement by the player.
        /// </summary>
        public bool IsPlaced { get; private set; }

        /// <summary>
        /// Checks whether this placeable is touching any other.
        /// </summary>
        /// <returns>Whether this placeable is touching any other, false otherwise.</returns>
        /// <remarks>If so configured a connector-type <see cref="Placeable"/> may require the <see cref="StartHandle"/> and
        /// <see cref="EndHandle"/> to both be touching another magnet.</remarks>
        public bool IsTouchingAny => _contactMap.Count > 0 && (!isConnector || IsConnected);

        /// <summary>
        /// Whether this placeable is touching a given <see cref="Magnet"/>.
        /// </summary>
        /// <param name="magnet">The other <see cref="Magnet"/> to check against.</param>
        /// <returns>Whether the <paramref name="magnet"/> is touching, false otherwise.</returns>
        public bool IsTouching(Magnet magnet) => _contactMap.ContainsKey(magnet);

        /// <summary>
        /// Checks whether this placeable is a connector and well-connected, i.e. all required magnets are touching another
        /// magnet. Only makes sense to use if <see cref="IsConnector"/> is true.
        /// </summary>
        public bool IsConnected => isConnector && (!requireConnection || !StartHandle || !EndHandle ||
            _contactMap.Any(it => it.Value == StartHandle) &&
            _contactMap.Any(it => it.Value == EndHandle));

        /// <summary>
        /// Called internally when placement of this instance was aborted.
        /// </summary>
        public void OnAborted()
        {
            try
            {
                GetComponentsInChildren<Component>()
                    .OfType<IPlaceableAborted>()
                    .ForEach(it => it.OnPlaceableAborted());
                onAborted.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                Debug.LogError($"[{nameof(Placeable)}] An exception occured while aborting placement.", this);
            }
        }

        /// <inheritdoc cref="IPlaceable"/>
        public void OnUpdate()
        {
            try
            {
                GetComponentsInChildren<Component>()
                    .OfType<IPlaceableUpdated>()
                    .ForEach(it =>
                    {
                        if (it != null)
                        {
                            it.OnPlaceableUpdated();
                        }
                    });
                onUpdate.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                Debug.LogError($"[{nameof(Placeable)}] An exception occured while updating placement.", this);
            }
        }

        /// <summary>
        /// Called internally when placement of this instance was confirmed/finished.
        /// </summary>
        public void OnPlaced()
        {
            try
            {
                // A placeable can only ever be placed once per lifecycle, so duplicate calls should be ignored.
                if (IsPlaced)
                {
                    return;
                }

                IsPlaced = true;
                whenPlaced.Where(it => it != null).ForEach(it => it.SetActive(true));
                IsStableGround = canFloat || CheckGrounded();
                GetComponentsInChildren<Component>()
                    .OfType<IPlaceablePlaced>()
                    .ForEach(it => it.OnPlaceableFinished(_wasStartedByBuilderCallback));
                onPlaced.Invoke();
                if (_system)
                {
                    _system.SetPlaced(this);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                Debug.LogError($"[{nameof(Placeable)}] An exception occured while confirming placement: {e.Message}",
                    this);
            }
        }

        /// <summary>
        /// Called internally when this instance is being deleted.
        /// </summary>
        public void OnDelete()
        {
            try
            {
                if (_wasDeletedByBuilderCallback)
                {
                    Debug.LogError($"[{nameof(Placeable)}] Repeated calls to OnDelete() are not expected.", this);
                    return;
                }

                _wasDeletedByBuilderCallback = true;
                GetComponentsInChildren<Component>()
                    .OfType<IPlaceableDeleted>()
                    .ForEach(it => it.OnPlaceableDeleted(_wasDeletedByBuilderCallback));
                onDeleted.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[{nameof(Placeable)}] An exception occured while deleting a placed object: {e.Message}",
                    this);
            }
        }

        /// <summary>
        /// Called internally once when placement of this instance has started.
        /// </summary>
        public void OnStarted()
        {
            try
            {
                if (_wasStartedByBuilderCallback)
                {
                    Debug.LogError($"[{nameof(Placeable)}] Repeated calls to OnStarted() are not expected.", this);
                    return;
                }

                // if this flag is NOT set, we call OnPlaced() in Start(), otherwise we expect the builder to call OnPlaced() at the appropriate time.
                _wasStartedByBuilderCallback = true;

                GetComponentsInChildren<Component>()
                    .OfType<IPlaceableStarted>()
                    .ForEach(it => it.OnPlaceableStarted(_wasStartedByBuilderCallback));
                onStarted.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(Placeable)}] An exception occured while starting placement: {e.Message}",
                    this);
            }
        }

        /// <summary>
        /// Checks whether this placeable is currently on stable ground.
        /// </summary>
        /// <returns></returns>
        private bool CheckGrounded()
        {
            Bounds aabb = GetBounds();
            return DrawPhysics.CheckBox(aabb.center, aabb.extents, Quaternion.identity, groundMask);
        }

        /// <summary>
        /// Gets the containing bounds of all colliders on this placeable.
        /// </summary>
        /// <returns></returns>
        public Bounds GetBounds(bool includeTriggers = false, bool includeInactive = false)
        {
            Bounds aabb = new(transform.position, Vector3.zero);
            Collider[] colliders = GetComponentsInChildren<Collider>(includeInactive);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (includeTriggers || !colliders[i].isTrigger)
                {
                    aabb.Encapsulate(colliders[i].bounds);
                }
            }

            aabb.Expand(BOUNDS_EXPANSION);
            return aabb;
        }

        private IEnumerable<BoxCollider> GetBlockers()
        {
            if (!respectBlockers)
            {
                return Enumerable.Empty<BoxCollider>();
            }

            IEnumerable<BoxCollider> blockerBoxes = !compositeBlockingShape
                ? new[] { blockingShape }
                : GetComponentsInChildren<BoxCollider>()
                    .Where(it => (blockingMask & (1 << it.gameObject.layer)) > 0);
            return blockerBoxes;
        }

        /// <summary>
        /// Checks whether the placeable is blocked in its current position.
        /// </summary>
        public bool CheckBlocked()
        {
            if (!respectBlockers)
            {
                return false;
            }

            IEnumerable<BoxCollider> blockerBoxes = GetBlockers();

            foreach (BoxCollider blocker in blockerBoxes)
            {
                Vector3 center = blocker.transform.TransformPoint(blocker.center);
                Vector3 halfExtents = blocker.size * 0.5f;
                Quaternion orientation = blocker.transform.rotation;
                int count = DrawPhysics.OverlapBoxNonAlloc(
                    center: center,
                    halfExtents: halfExtents,
                    orientation: orientation,
                    results: s_blockingHits,
                    mask: blockingMask,
                    queryTriggerInteraction: QueryTriggerInteraction.Collide
                );
                if (count > 0)
                {
                    D.raw(new Shape.Box(center, halfExtents, orientation), Color.yellow, 1f);
                    D.raw(new Shape.Axis(center, orientation, true, Shape.Axes.All, .5f), 1f);

                    for (int i = 0; i < count; i++)
                    {
                        D.raw(s_blockingHits[i].GetComponent<Collider>(), Color.red, 1f);
                    }
                }
                /* DISABLED to reduce gizmo clutter, but helpful for debugging
                else {
                    D.raw ( new Shape.Box ( center, halfExtents, orientation ), Color.green, 1f );
                    D.raw ( new Shape.Point ( center ), Color.green, 1f );
                    D.raw ( new Shape.Axis ( center, orientation, true, Shape.Axes.All, .5f ), 1f );
                }
                */

                // greedy search, return true on first hit.
                if (count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void Awake()
        {
            _momentaryDeletionRefund = deletionRefund;
            whenPlaced?.ForEach(it => it.SetActive(false));

            if (!Models.Any())
            {
                FindModel();
            }

            // for connectivity detection we need a rigidbody.
            if (!canFloat)
            {
                EnsureRigidbody();
            }

            // magnets below endHandle go into _endHandleMagnets, others into _magnets.
            GetComponentsInChildren(true, _magnets); // include inactive GameObjects
            for (int i = _magnets.Count - 1; i >= 0; i--)
            {
                if (isConnector && endHandle && _magnets[i] == endHandle ||
                    !_magnets[i].gameObject.activeSelf ||
                    !_magnets[i].enabled)
                {
                    // remove connector end-handle
                    // remove inactive GameObjects
                    // remove disabled behaviours
                    _magnets.RemoveAt(i);
                }
            }

            _magnets.TrimExcess();

            if (debug)
            {
                Debug.Log($"[{nameof(Placeable)}] {_magnets.Count} magnets found on {name}", this);
            }

            GetComponentsInChildren(true, _surfaces);
            for (int i = _surfaces.Count - 1; i >= 0; i--)
            {
                if (_surfaces[i].gameObject.layer != gameObject.layer)
                {
                    // remove any colliders that do not share the same layer as the main object
                    _surfaces.RemoveAt(i);
                }
            }

            _surfaces.TrimExcess();

            if (WhileDisconnected)
            {
                WhileDisconnected.SetActive(false);
            }
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void Start()
        {
            if (Services.TryGet(out _system))
            {
                _system.Register(this);
            }

            // we allow all other start callbacks to complete before we trigger our custom placeable lifecycle.
            ContinueNextFrame().Forget();

            return;

            async UniTaskVoid ContinueNextFrame()
            {
                await UniTask.NextFrame();
                // when this placeable is instantiated without receiving builder-callbacks we need to still make sure that all
                // initialization is performed correctly. To that end we synthesise a placement event whenever Start() is called
                // before OnStart(), which is expected to be called immediately after instantiation by the Builder.
                if (!_wasStartedByBuilderCallback)
                {
                    if (debug)
                    {
                        Debug.Log(
                            $"[{nameof(Placeable)}] OnPlaced event synthesised for {name}. It is assumed that this object was instantiated automatically and not placed by a player.",
                            this);
                    }

                    OnPlaced();
                }
            }
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnDestroy()
        {
            _floatCts.Cancel();

            if (debug)
            {
                Debug.Log($"[{nameof(Placeable)}] Destroyed {name}", this);
            }

            if (_system)
            {
                _system.Unregister(this);
            }
        }

        /// <summary>
        /// Ensure that this placeable has a rigidbody and that it is configured correctly.
        /// </summary>
        private void EnsureRigidbody()
        {
            if (!TryGetComponent(out Rigidbody rb))
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }

            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            rb.detectCollisions = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.useGravity = false;
        }

        /// <summary>
        /// Checks whether this placeable is connected to stable ground. It is considered connected if it directly or
        /// transitively is connected to an object that can float or is grounded.
        /// </summary>
        /// <returns>Whether this placeable is connected to stable ground.</returns>
        /// <remarks>Performs a simple, greedy, breath-first-search when called. Do not call in a performance critical context.</remarks>
        public bool CheckConnectedToStableGround()
        {
            if (IsStableGround)
            {
                return true;
            }

            if (!IsTouchingAny)
            {
                return false;
            }

            HashSet<Placeable> visited = HashSetPool<Placeable>.Get();
            List<Placeable> open = ListPool<Placeable>.Get();

            visited.Add(this);
            open.Add(this);
            while (open.TryPop(out var next))
            {
                visited.Add(next);
                if (next.IsStableGround)
                {
                    return true;
                }
                else
                {
                    foreach (Magnet contact in next._contactMap.Keys)
                    {
                        if (!visited.Contains(contact.Placeable))
                        {
                            open.Push(contact.Placeable);
                            open.Add(contact.Placeable);
                        }
                    }
                }
            }

            HashSetPool<Placeable>.Release(visited);
            ListPool<Placeable>.Release(open);

            return false;
        }

        /// <summary>
        /// Editor helper function to find all models in children.
        /// </summary>
        [Button]
        public void FindModel()
        {
            models = GetComponentsInChildren<Renderer>()?
                .Where(it => it is MeshRenderer or SkinnedMeshRenderer)
                .Select(it => it.gameObject)
                .ToArray();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
        }

        /// <summary>
        /// Editor helper function to find a blocking shape.
        /// </summary>
        [Button]
        public void FindBlockingShape()
        {
            blockingShape = GetComponentInChildren<BoxCollider>();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
        }

        /// <summary>
        /// Called by <see cref="Magnet"/> components on children of this placeable when they loose contact to a <see cref="Magnet"/> on another <see cref="Placeable"/>.
        /// </summary>
        /// <param name="childMagnet"></param>
        /// <param name="otherMagnet">The magnet that lost contact.</param>
        public void OnEndTouching(Magnet childMagnet, Magnet otherMagnet)
        {
            if (_contactMap.ContainsKey(otherMagnet))
            {
                _contactMap.Remove(otherMagnet);
                OnContactsChangedAsync().Forget();
            }
        }

        /// <summary>
        /// Called by <see cref="Magnet"/> components on children of this placeable when they come in contact with a <see cref="Magnet"/> on another <see cref="Placeable"/>.
        /// </summary>
        /// <param name="childMagnet"></param>
        /// <param name="otherMagnet">The magnet that came into contact.</param>
        public void OnStartTouching(Magnet childMagnet, Magnet otherMagnet)
        {
            if (!_contactMap.ContainsKey(otherMagnet))
            {
                // check directionality & identity whether contact can be made.
                if (childMagnet.CanSnapTo(otherMagnet))
                {
                    _contactMap[otherMagnet] = childMagnet;
                    OnContactsChangedAsync().Forget();
                }
            }
        }

        /// <summary>
        /// Called whenever the contacts (touching magnets) of this placeable have changed. This method is called
        /// asynchronously and implements the logic for floating and auto-deletion.
        /// </summary>
        private async UniTaskVoid OnContactsChangedAsync()
        {
            // aggregate events and wait a frame to allow all physics events to trigger in case this was a prefab placement or load event.

            if (_isContactsDirty)
            {
                return;
            }

            _isContactsDirty = true;
            await UniTask.NextFrame(PlayerLoopTiming.FixedUpdate);


            // check if this object is still valid.
            if (!this)
            {
                return;
            }

            // kill placeable if it can't float
            if (!CheckConnectedToStableGround())
            {
                if (WhileDisconnected)
                {
                    WhileDisconnected.SetActive(true);
                }

                onConnected.Invoke(false);
                GetComponents<Component>()
                    .OfType<IPlaceableConnected>()
                    .ForEach(it => it.OnPlaceableConnected(false));

                DestroyFloatingAsync(_floatCts.Token).Forget();
            }
            else
            {
                if (WhileDisconnected)
                {
                    WhileDisconnected.SetActive(false);
                }

                onConnected.Invoke(true);
                GetComponents<Component>()
                    .OfType<IPlaceableConnected>()
                    .ForEach(it => it.OnPlaceableConnected(true));

                _floatCts.Cancel();
                _floatCts = new CancellationTokenSource();
            }

            _isContactsDirty = false;

            return;

            async UniTaskVoid DestroyFloatingAsync(CancellationToken ct)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(destroyFloatingDelay),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    ct
                );


                // trigger physics collision exit events...
                if (!this)
                {
                    return;
                }

                if (TryGetComponent(out Rigidbody rb))
                {
                    rb.detectCollisions = false;
                    await UniTask.NextFrame(PlayerLoopTiming.FixedUpdate, ct);
                    await UniTask.NextFrame(PlayerLoopTiming.Update, ct);
                }

                // ...then destroy
                Destroy(gameObject);
            }

            _isContactsDirty = false;
        }


        /// <summary>
        /// Whether this placeable is touching a given other placeable.
        /// </summary>
        /// <param name="placeable">The other placeable to check against.</param>
        /// <returns>Whether the <paramref name="placeable"/> is touching, false otherwise.</returns>
        public bool IsTouching(Placeable placeable)
        {
            foreach (Magnet contact in _contactMap.Keys)
            {
                if (contact.Placeable == placeable)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Set the position of the placeable to the given point.
        /// </summary>
        /// <param name="point">The world position to assign.</param>
        public void SetPosition(Vector3 point)
        {
            transform.position = point;
        }

        /// <summary>
        /// Set the end handle position of the placeable to the given point.
        /// </summary>
        /// <param name="point">The world position to assign.</param>
        public void SetEndPosition(Vector3 point)
        {
            endHandle.transform.position = point;
        }

        /// <inheritdoc />
        protected override void OnUnpack(IPlaceable.Memento package, AssetLookup lookup)
        {
            transform.SetPositionAndRotation(package.RootPose.position, package.RootPose.rotation);
            if (isConnector)
            {
                Debug.Assert(endHandle != null, "endHandle != null");
                endHandle.transform.SetPositionAndRotation(package.EndPose.position, package.EndPose.rotation);
            }
        }

        /// <inheritdoc />
        protected override IPlaceable.Memento OnPack()
        {
            Package = new IPlaceable.Memento
            {
                EndPose = isConnector && endHandle
                    ? PoseExtensions.PoseFrom(endHandle.transform)
                    : default,
                RootPose = PoseExtensions.PoseFrom(transform),
                IsPlayerDeletable = isPlayerDeletable,
                IsPlayerPlaceable = isPlayerPlaceable,
                CanFloat = canFloat,
                Description = $"{transform.gameObject.scene.name}::{transform.GetPath()}",
            };
            return Package;
        }

        private void OnDrawGizmosSelected()
        {
            IEnumerable<BoxCollider> blockerBoxes = GetBlockers();
            foreach (BoxCollider blocker in blockerBoxes)
            {
                D.raw(blocker, Color.red);
            }
        }
    }
}