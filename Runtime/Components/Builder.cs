using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Pool;
using Readymade.Machinery.Acting;
using Readymade.Machinery.FSM;
using Readymade.Machinery.Shared;
using Cysharp.Threading.Tasks;
using MathNet.Numerics;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using Readymade.Persistence;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Readymade.Building;
using Readymade.Databinding;
using Readymade.Utils.Feedback;
using Unity.Mathematics;
using UnityEngine.Internal;
using UnityEngine.Serialization;
using UnityFx.Outline;
using Vertx.Debugging;

namespace Readymade.Building.Components
{
    [Serializable]
    public class UnityBoundsEvent : UnityEvent<Bounds>
    {
    }

    /// <summary>
    /// A generic prefab-builder. Can place any prefab that is marked up with <see cref="Magnet"/> and <see cref="Placeable"/>
    /// components.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is designed to be placed in a scene and referenced with adjacent systems. Multiple instances can be created to
    /// support multiple build modes or placeable object selections.
    /// </para>
    /// <para>
    /// The high level state of the builder is driven by a finite state machine, see <see cref="DefineStateMachine"/>.
    /// Signals to the FSM are synthesized and received from the outside in <see cref="Update"/> and passed into
    /// <see cref="UpdatePlacement"/> which handles the actual placement cycle. Use these aforementioned methods as entry points
    /// for exploring this behaviour.
    /// </para>
    /// <para>This class cooperates closely with <see cref="Magnet"/> and <see cref="Placeable"/>.</para>
    /// <para>For now input and UI are still part of this class, however they are designed to be easily extractable into
    /// separate components. Toolbar-UI is generated automatically from the referenced prefab collections. Input is hard-coded.
    /// </para>
    /// <para>Design: As much as possible, methods do not depend to instance state and instead receive their state object as
    /// argument. This is done to separate concerns and localize state transformation. All serialized config parameters are
    /// assumed to be immutable/constant at runtime.</para>
    /// </remarks>
    /// <seealso cref="SoPlaceableCollection"/>
    public class Builder : MonoBehaviour
    {
        private const float RaySphereThreshold = 0.01f;
        private const float SnapEpsilon = 0.01f;
        private const float MinConnectorLength = 0.25f;
        public const int MaxOverlapModifier = 16;

        [Tooltip(
            "The " + nameof(IActor) +
            " that this builder instance belongs to. Typically the player. Used to claim resources, run validation and log actions.")]
        [SerializeField]
        private PlayerActor actor;

        [SerializeField] private Transform container;

        [SerializeField] private float maxBuildRange = 40f;

        [SerializeField] private LayerMask surfaceMask;

        [SerializeField] private LayerMask blockingMask;

        [SerializeField] private LayerMask magnetMask;

        [SerializeField] private Transform hitGizmo;

        [SerializeField] private Transform gizmoForWorldMagnet;

        [SerializeField] private Transform gizmoForPreviewMagnet;

        [SerializeField] private GizmoAlignment gizmoAlignment;

        [Sirenix.OdinInspector.InfoBox(
            "These settings are helpful when using the builder in a context without a (mouse) pointer.")]
        [BoxGroup("Menu Control")]
        [SerializeField]
#if ODIN_INSPECTOR
        [ListDrawerSettings(ShowPaging = false, ShowFoldout = false)]
#else
        [ReorderableList]
#endif
        private Behaviour[] cameraMovement;

        [BoxGroup("Menu Control")]
        [SerializeField]
        private bool lockCameraInMenu = true;

        [BoxGroup("Menu Control")]
        [SerializeField]
        private bool lockPlacementInMenu = true;

        [BoxGroup("Feedback")]
        [SerializeField]
        private GameObject whileDelete;

        [BoxGroup("Feedback")]
        [SerializeField]
        private GameObject whilePlacing;

        [BoxGroup("Pointer")]
        [Tooltip(
            "The radius of the sphere cast from the pointer to into the world that detects a build surface. This should be a small value roughly 1/10th to 1/20th the size of Overlap Radius. Default is 0.2. If the value is very small, a raycast will be used.")]
        [SerializeField]
        [Min(0.0f)]
        private float pointerRayRadius = 0.2f;

        [FormerlySerializedAs("_overlapRadius")]
        [BoxGroup("Pointer")]
        [Tooltip(
            "The radius of a sphere ScriptableObject centered on the pointer hit point in which magnets are detected and snapping is " +
            "evaluated. This should roughly match the basic coarse-grid unit to which all object models conform in " +
            "size. The value can be modified relatively for each Placeable component. Default is 4.0.")]
        [SerializeField]
        [Min(0.05f)]
        private float overlapRadius = 4f;

        [BoxGroup("Pointer")]
        [Tooltip(
            "How many degrees are added to the rotation for each scroll event. Should be the same as Angle Increment. Default is 15.")]
        [Min(0)]
        [SerializeField]
        private float rotateSpeed = 15f;

        [BoxGroup("Pointer")]
        [Tooltip("The angle increment when rotating an object. Default is 15.")]
        [Min(0)]
        [SerializeField]
        private int angleIncrement = 15;

        [BoxGroup("Snapping")]
        [SerializeField]
        private bool staySnappedWhenBlocked = true;

        [BoxGroup("Snapping")]
        [SerializeField]
        private bool flipFaceAlignment;

        [BoxGroup("Snapping")]
        [SerializeField]
        private bool useWorldGrid;

        [BoxGroup("Snapping")]
        [SerializeField]
        [ShowIf(nameof(useWorldGrid))]
        private Vector3 worldGridDivisions = new(2f, 2f, 2f);

        [BoxGroup("Snapping")]
        [Range(-1f, 1f)]
        [SerializeField]
        [Tooltip(
            "Modifies the snapping behaviour to favour the magnet closer to the camera vs. the magnet with less rotational offset.\n" +
            "<b>[0]</b> both values contribute equally to the decision\n" +
            "<b>[-1]</b> only the magnet closer to the camera is considered\n" +
            "<b>[+1]</b> only the magnet with less rotational offset is considered")]
        private float snapBias = 0;

        [BoxGroup("Focus FX")]
        [Tooltip("The layer on which to place renderers of focus objects.")]
        [SerializeField]
        [NaughtyAttributes.Layer]
        private int focusLayer;

        [BoxGroup("Focus FX")]
        [SerializeField]
        private Material focusOverlay;

        [BoxGroup("Focus FX")]
        [SerializeField]
        private OutlineSettings focusOutline;

        [BoxGroup("Focus FX")]
        [SerializeField]
        [ColorUsage(true, true)]
        private Color highlightColor = new(.3f, .6f, .9f, 1f);

        [BoxGroup("Focus FX")]
        [SerializeField]
        [ColorUsage(true, true)]
        [Tooltip("The color to apply to the focus effect when in delete-mode.")]
        private Color rejectedColor = new(.5f, .5f, .5f, 1f);

        [BoxGroup("Focus FX")]
        [SerializeField]
        [ColorUsage(true, true)]
        [Tooltip("The color to apply to the focus effect when in delete-mode.")]
        private Color focusColor = new(1f, 1f, 1f, 1f);

        [BoxGroup("Focus FX")]
        [SerializeField]
        [ColorUsage(true, true)]
        [Tooltip("The color to apply to the focus effect when in delete-mode.")]
        private Color deleteColor = new(1.498039f, 0.02496732f, 0, 1f);

        [BoxGroup("Sound Fx")]
        [SerializeField]
        [Tooltip("The audio source to use for playing builder sound effects.")]
        private AudioSource audioSource;

        [BoxGroup("Sound Fx")]
        [SerializeField]
        [Tooltip("Sound effect for selection of an object.")]
        private AudioClip toolSelectedSfx;

        [BoxGroup("Sound Fx")]
        [SerializeField]
        [Tooltip("Sound effect for cancellation of object placement.")]
        private AudioClip toolCancelledSfx;

        [BoxGroup("Sound Fx")]
        [SerializeField]
        [Tooltip("Sound effect for successful deletion of an object.")]
        private AudioClip deleteFx;

        [BoxGroup("Sound Fx")]
        [SerializeField]
        [Tooltip("Sound effect for successful placement of an object.")]
        private AudioClip confirmedSfx;

        [BoxGroup("Sound Fx")]
        [SerializeField]
        [Tooltip("Sound effect to provide generic error feedback.")]
        private AudioClip errorSfx;

        [BoxGroup("Sound Fx")]
        [SerializeField]
        [Tooltip(
            "Sound effect to play when the object a magnet it can (semantically) snap to. A blocker may still prevent snapping.")]
        private AudioClip snapSfx;

        [BoxGroup("Sound Fx")]
        [SerializeField]
        [Tooltip(
            "Sound effect to play when the object has actually snapped to a magnet and changed its position because of it.")]
        private AudioClip actualSnapSfx;

        [BoxGroup("Sound Fx")]
        [SerializeField]
        [Tooltip("Sound effect to play when the object is aligned to any grid or axis.")]
        private AudioClip alignSfx;

        [BoxGroup("Events")]
        [Tooltip(
            "Delay that has to elapse after the last placement event before the OnPlaced event is invoked. This useful " +
            "to delay running heavy world scanning/update processes until the player is temporarily done with placement.")]
        [SerializeField]
        private float onPlacedDelay;

        [BoxGroup("Events")]
        [Tooltip("Invoked when placement/deletion of an object has completed and the delay has elapsed")]
        [SerializeField]
        private UnityBoundsEvent onWorldChanged;

        [BoxGroup("Events")]
        [Tooltip("Invoked immediately when an object was deleted.")]
        [SerializeField]
        private UnityBoundsEvent onDeleted;

        [BoxGroup("Events")]
        [Tooltip("Invoked immediately when an object was placed.")]
        [SerializeField]
        private UnityBoundsEvent onPlaced;

        [Tooltip("The resource system to query for resources to satisfy placement costs.")]
        [SerializeField]
        [Required]
        private ResourceSystem resourceSystem;

        [Tooltip(
            "The asset lookup to use for object instantiation. Should be the same as the one used by the persistence system.")]
        [SerializeField]
        [Required]
        private AssetLookup assetLookup;

        [BoxGroup("Editor Tools")]
        [Tooltip("Whether to keep prefab links when spawning in the editor. This is does nothing in a build.")]
        [SerializeField]
        private bool keepPrefabLinks;

        [BoxGroup("FX")] [SerializeField] private FloatingTextSpawner textSpawner;

        private Placeable _instance;
        private Material _previewOriginalMaterial;
        private Collider[] _overlapHits = new Collider[64];
        private List<RaycastResult> _uiCheck;
        private Camera _camera;
        private Placeable _aim;
        private float _lastDirtyTime;
        private bool _isWorldDirty;
        private Bounds _dirtyBounds;
        private IPlaceable.Memento _placeableConfig;
        private Dictionary<GameObject, int> _focusObjects = new();
        private bool _isPrefabMode;
        private InputState _inputState;
        private InputState _previousInputState;
        private StateMachine<State, Trigger> _fsm;
        private bool _isHit;
        private RaycastHit _rayHitInfo;
        private Ray _ray;
        private PlaceState _placeState;
        private InputDevice _lastUpdatedDevice;
        private Placeable _previousToolPrefab;
        private Placeable _prefab;

        private HashSet<Placeable> _copyablePrefabs;

        // a monotonic increasing ID of placed instances to help with debugging.
        private int _placedInstanceID;

        /// <summary>
        /// Resource system to query for resources to satisfy placement costs.
        /// </summary>
        public IProvider<SoProp> ResourceProvider => resourceSystem;

        /// <summary>
        /// Resource system inventory that is queried for resources to satisfy placement costs.
        /// </summary>
        public IStockpile<SoProp> ResourceStockpile => resourceSystem.Inventory;

        /// <summary>
        /// Resource system inventory that is queried for resources to satisfy placement costs.
        /// </summary>
        public IInventory<SoProp> ResourceInventory => resourceSystem.Inventory;

        /// <summary>
        /// The container to which all placeables are parented.
        /// </summary>
        public Transform Container
        {
            get => container;
            set => container = value;
        }

        /// <summary>
        /// The baseline magnet search-radius of the pointer raycast.
        /// </summary>
        public float OverlapRadius => overlapRadius;

        /// <summary>
        /// Whether the builder is currently in the placing state.
        /// </summary>
        public bool IsPlacing => _fsm.IsInState(State.Placing);

        /// <summary>
        /// The current input state the builder is working with.
        /// </summary>
        public InputState Input => _inputState;

        /// <summary>
        /// The prefab currently being placed by the builder.
        /// </summary>
        public Placeable ActivePrefab => _prefab;

        /// <summary>
        /// Layer mask used to detect surfaces for placement.
        /// </summary>
        public LayerMask SurfaceMask => surfaceMask;

        /// <summary>
        /// Layer mask used to detect blocking objects.
        /// </summary>
        public LayerMask BlockingMask => blockingMask;

        /// <summary>
        /// Layer mask used to detect magnets.
        /// </summary>
        public LayerMask MagnetMask => magnetMask;

        /// <summary>
        /// Invoked whenever the selected tool changes.
        /// </summary>
        public event Action ToolChanged;

        /// <summary>
        /// Invoked whenever placement has changed.
        /// </summary>
        public event Action<PlacementUpdateEventArgs> PlacementPerformed;

        /// <summary>
        /// Event function.
        /// </summary>
        private void Awake()
        {
            Debug.Assert(cameraMovement.All(it => it != null),
                "ASSERTION FAILED: cameraMovement.All ( it => it != null )",
                this);
            _camera = Camera.main;
            DefineStateMachine();
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnEnable()
        {
            _fsm.Fire(Trigger.Enable);
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void Start()
        {
            _fsm.Fire(Trigger.Start);
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnDisable()
        {
            _fsm.Fire(Trigger.Cancel);
            _fsm.Fire(Trigger.Disable);
            Debug.Log($"[{nameof(Builder)}]: Disabled", this);
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnDestroy()
        {
            _fsm.Fire(Trigger.Final);
        }

        /// <summary>
        /// Injects a list of prefabs into the builder that can be copied. When left empty, no copying is possible.
        /// </summary>
        /// <param name="copyablePrefabs">The copyable prefabs.</param>
        public void SetCopyablePrefabs(IEnumerable<Placeable> copyablePrefabs)
        {
            Cancel();
            _copyablePrefabs = copyablePrefabs.ToHashSet();
        }

        /// <summary>
        /// Injects the current input state into the builder.
        /// </summary>
        /// <param name="inputState">The input state.</param>
        public void SetInput(ref InputState inputState)
        {
            _inputState = inputState;
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void Update()
        {
            // Update pointer world ray-cast 

            // Note: When deleting also cast against the focus layer, if we don't we'll loose the focus object and we don't want
            //       that while aiming for deletion unlike during placement.
            LayerMask mask = surfaceMask | (_inputState.IsDelete ? 1 << focusLayer : 0);
            _isHit = !_inputState.PointerIsOverUi && TryUpdateRayCast(mask, _inputState, out _ray, out _rayHitInfo);

            // fire internal state machine signals

            // if the placeable instance ever gets destroyed from the outside, we detect it and handle it. 
            if (!_instance && _fsm.IsInState(State.Placing))
            {
                Debug.LogWarning($"[{nameof(Builder)}] Placeable instance lost, cancelling placement.", this);
                _fsm.Fire(Trigger.InstanceLost);
            }

            if (_inputState.IsEscThisFrame)
            {
                _fsm.Fire(Trigger.Cancel);
            }


            if (lockCameraInMenu)
            {
                foreach (Behaviour behaviour in cameraMovement)
                {
                    behaviour.enabled = !_inputState.ToolMenuIsOpen;
                }
            }

            if (_inputState.IsCopyThisFrame && _isHit)
            {
                Placeable placeableToCopy = _rayHitInfo.collider.GetComponentInParent<Placeable>();
                if (!placeableToCopy)
                {
                    Debug.Log($"[{nameof(Builder)}] Copying rejected (NO_PLACEABLE)");
                    NotifyUpdateObservers(PlacementPhase.PlacementFailed, PlacementFailure.NoPlaceable);
                }
                else if (!placeableToCopy.IsPlayerPlaceable)
                {
                    Debug.Log($"[{nameof(Builder)}] Copying rejected (NOT_PLACEABLE_BY_PLAYER)");
                    NotifyUpdateObservers(PlacementPhase.PlacementFailed, PlacementFailure.NotPlaceableByPlayer);
                }
                else
                {
                    // when copying, we have to remove focus from the original object, otherwise both will be selected.
                    if (placeableToCopy == _aim)
                    {
                        ClearFocusObject(ref _aim);
                    }
                    else
                    {
                        RemoveFocus(placeableToCopy.Models);
                    }

                    TryFindToolForPlaceable(placeableToCopy, out Placeable tool);
                    SetTool(tool);
                }
            }

            if (_inputState.HasDeleteStartedThisFrame)
            {
                _fsm.Fire(Trigger.StartDeleting);
            }

            if (_inputState.HasDeleteEndedThisFrame)
            {
                _fsm.Fire(Trigger.Cancel);
            }

            if (_isHit)
            {
                _fsm.Fire(Trigger.IsHit);
            }
            else
            {
                _fsm.Fire(Trigger.NoHit);
            }

            _fsm.Fire(Trigger.Update);

            if (_inputState.IsConfirmThisFrame)
            {
                _fsm.Fire(Trigger.Confirmed);
            }

            // fire external notifications

            if (_isWorldDirty && _lastDirtyTime + onPlacedDelay < Time.time)
            {
                onWorldChanged.Invoke(_dirtyBounds);
                _isWorldDirty = false;
                _dirtyBounds = default;
            }
        }

        /// <summary>
        /// Declares all high-level states that drive the building behaviour of this class.
        /// </summary>
        private void DefineStateMachine()
        {
            _fsm = new StateMachine<State, Trigger>(State.Initial);
            _fsm.OnTransitioned += transition =>
                Debug.Log(
                    $"[{nameof(Builder)}] Transitioned from {transition.Source} to {transition.Destination} on trigger {transition.Trigger}");
            _fsm.OnError += message => Debug.LogError($"[{nameof(Builder)}] {message}");
            _fsm.Configure(State.Initial)
                .Ignore(Trigger.Enable)
                .Ignore(Trigger.Disable)
                .Ignore(Trigger.Cancel)
                .Ignore(Trigger.InstanceLost)
                .Permit(Trigger.Start, State.Ready)
                .OnExit(() =>
                {
                    if (whileDelete)
                    {
                        whileDelete.SetActive(false);
                    }

                    gizmoAlignment.gameObject.SetActive(false);
                    gizmoForPreviewMagnet.gameObject.SetActive(false);
                    gizmoForWorldMagnet.gameObject.SetActive(false);
                    hitGizmo.gameObject.SetActive(false);
                })
                ;

            _fsm.Configure(State.Disabled)
                .Ignore(Trigger.Confirmed)
                .Ignore(Trigger.StartDeleting)
                .Ignore(Trigger.StartPlacing)
                .Ignore(Trigger.Cancel)
                .Ignore(Trigger.Start)
                .Ignore(Trigger.ToolSelected)
                .Ignore(Trigger.ToolDeselected)
                .Ignore(Trigger.Update)
                .Ignore(Trigger.IsHit)
                .Permit(Trigger.Final, State.Final)
                .Permit(Trigger.Enable, State.Ready)
                .OnEntry(() =>
                {
                    ClearFocusObject(ref _aim);
                    RemoveFocusAll();
                });

            _fsm.Configure(State.Ready)
                .Ignore(Trigger.Update)
                .Ignore(Trigger.Cancel)
                .Ignore(Trigger.ToolDeselected)
                .Ignore(Trigger.InstanceLost)
                .Permit(Trigger.Disable, State.Disabled)
                .Permit(Trigger.StartDeleting, State.Deleting)
                .Permit(Trigger.Final, State.Final)
                .PermitIf(Trigger.ToolSelected, State.Placing, () => _prefab != default)
                .PermitIf(Trigger.StartPlacing, State.Placing, () => _prefab != default)
                .Permit(Trigger.IsHit, State.ReadyIsHit)
                .Permit(Trigger.NoHit, State.ReadyNoHit)
                .OnEntry(() =>
                {
                    SetFocusModeFocus();

                    // add focus layer to surface mask so the pointer ray will not ignore highlighted objects
                    // this is only desirable in standby mode
                    surfaceMask |= (1 << focusLayer);
                    ToolChanged?.Invoke();
                })
                .OnExit(() =>
                {
                    // we again remove the focus layer from the surface mask so the pointer ray will no longer
                    // detect highlighted objects
                    surfaceMask &= ~(1 << focusLayer);
                })
                ;

            _fsm.Configure(State.ReadyNoHit)
                .SubstateOf(State.Ready)
                .Ignore(Trigger.Cancel)
                .Ignore(Trigger.NoHit)
                .Ignore(Trigger.Confirmed)
                .Ignore(Trigger.Update)
                .OnEntry(() =>
                {
                    ClearFocusObject(ref _aim);
                    hitGizmo.gameObject.SetActive(false);
                    ResetHitDerivedState();
                })
                .OnExit(() => { })
                ;

            _fsm.Configure(State.ReadyIsHit)
                .SubstateOf(State.Ready)
                .Ignore(Trigger.Cancel)
                .Ignore(Trigger.Confirmed)
                .Ignore(Trigger.IsHit)
                .InternalTransition(Trigger.Update, () =>
                {
                    UpdateHitGizmo(_rayHitInfo);
                    UpdateAim(_rayHitInfo, ref _aim, true);
                })
                .OnEntry(() =>
                {
                    hitGizmo.gameObject.SetActive(true);
                    if (_aim)
                    {
                        AddFocus(_aim.Models);
                    }
                })
                .OnExit(() => { })
                ;


            _fsm.Configure(State.Deleting)
                .Ignore(Trigger.Update)
                .Ignore(Trigger.ToolDeselected)
                .Permit(Trigger.Cancel, State.Ready)
                .Permit(Trigger.IsHit, State.DeletingIsHit)
                .Permit(Trigger.NoHit, State.DeletingNoHit)
                .Permit(Trigger.Final, State.Final)
                .OnEntry(() =>
                {
                    SetFocusModeDelete();
                    if (whileDelete)
                    {
                        whileDelete.SetActive(true);
                    }

                    // Also enable whilePlacing, because the user should also have the same
                    // information while deleting.
                    if (whilePlacing)
                    {
                        whilePlacing.SetActive(true);
                    }

                    DestroyPlaceable(ref _instance, ref _placeState);
                    if (_aim)
                    {
                        AddFocus(_aim.Models);
                    }
                })
                .OnExit((t) =>
                {
                    RemoveFocusAll();
                    if (whileDelete)
                    {
                        whileDelete.SetActive(false);
                    }

                    if (whilePlacing)
                    {
                        whilePlacing.SetActive(false);
                    }
                })
                ;

            _fsm.Configure(State.DeletingNoHit)
                .SubstateOf(State.Deleting)
                .Ignore(Trigger.NoHit)
                .Ignore(Trigger.Update)
                .Ignore(Trigger.Confirmed)
                .OnEntry(() =>
                {
                    hitGizmo.gameObject.SetActive(false);
                    ClearFocusObject(ref _aim);
                    ResetHitDerivedState();
                })
                ;

            _fsm.Configure(State.DeletingIsHit)
                .SubstateOf(State.Deleting)
                .Ignore(Trigger.ToolSelected)
                .Ignore(Trigger.ToolDeselected)
                .Ignore(Trigger.IsHit)
                .InternalTransition(Trigger.Confirmed, () =>
                {
                    if (_aim && _aim.IsPlayerDeletable && CheckDeleteAffordable(_aim))
                    {
                        ApplyCost(_aim, _aim.DeletionCost);
                        ApplyRefund(_aim, _aim.PlacementCost);
                        DeleteAimObjectAsync(_aim).Forget();
                    }
                    else
                    {
                        Debug.Log($"[{nameof(Builder)}] Deletion rejected (" +
                            $"{(_aim ? "_" : "NO_AIM")}" +
                            $"{(_aim && _aim.IsPlayerDeletable ? "_" : "NOT_PLAYER_DELETABLE")}" +
                            $"{(_aim && CheckDeleteAffordable(_aim) ? "_" : "NOT_AFFORDABLE")}" +
                            ")");

                        if (!_aim)
                        {
                            NotifyUpdateObservers(PlacementPhase.DeletionFailed, PlacementFailure.NoAim);
                        }
                        else if (!_aim.IsPlayerDeletable)
                        {
                            NotifyUpdateObservers(PlacementPhase.DeletionFailed, PlacementFailure.NotDeletableByPlayer);
                        }
                        else if (!CheckDeleteAffordable(_aim))
                        {
                            NotifyUpdateObservers(PlacementPhase.DeletionFailed, PlacementFailure.NotAffordable);
                        }
                        else
                        {
                            NotifyUpdateObservers(PlacementPhase.Deleted, PlacementFailure.None);
                        }
                    }
                })
                .InternalTransition(Trigger.Update, () =>
                {
                    UpdateHitGizmo(_rayHitInfo);
                    UpdateAim(_rayHitInfo, ref _aim, true);
                    if (CheckDeleteAffordable(_aim))
                    {
                        SetFocusModeDelete();
                    }
                    else
                    {
                        SetFocusModeRejected();
                    }
                })
                .OnEntry(() => { hitGizmo.gameObject.SetActive(true); })
                ;

            _fsm.Configure(State.Placing)
                .Ignore(Trigger.Update)
                .Permit(Trigger.Cancel, State.Ready)
                .Permit(Trigger.IsHit, State.PlacingIsHit)
                .Permit(Trigger.NoHit, State.PlacingNoHit)
                .Permit(Trigger.ToolDeselected, State.Ready)
                .Permit(Trigger.StartDeleting, State.DeletingIsHit)
                .Permit(Trigger.Final, State.Final)
                .InternalTransition(Trigger.ToolSelected, () =>
                {
                    EnsureToolSelectionWasRecognized();
                    SetPlacementStateDirty();
                    _instance.gameObject.SetActive(false);
                    ToolChanged?.Invoke();
                })
                .OnEntry(() =>
                {
                    SetFocusModeHighlight();
                    EnsureToolSelectionWasRecognized();
                    SetPlacementStateDirty();
                    whilePlacing.SetActive(true);
                    ToolChanged?.Invoke();
                })
                .OnExit(() =>
                {
                    DestroyPlaceable(ref _instance, ref _placeState);
                    SetTool(default);
                    gizmoAlignment.ClearLine();
                    gizmoForPreviewMagnet.gameObject.SetActive(false);
                    gizmoForWorldMagnet.gameObject.SetActive(false);
                    whilePlacing.SetActive(false);
                })
                ;

            _fsm.Configure(State.PlacingIsHit)
                .SubstateOf(State.Placing)
                .Ignore(Trigger.IsHit)
                .InternalTransition(Trigger.ToolSelected, () =>
                {
                    EnsureToolSelectionWasRecognized();
                    SetPlacementStateDirty();
                })
                .InternalTransition(Trigger.Update, () =>
                {
                    SetPlacementStateDirty();
                    Debug.Assert(_instance != null, "_placeable != null");
                    UpdateAim(_rayHitInfo, ref _aim, false);
                    UpdateHitGizmo(_rayHitInfo);
                    UpdatePlacement(_instance, _inputState, _placeState, _rayHitInfo.point);
                    _instance.OnUpdate();
                })
                .InternalTransition(Trigger.Confirmed, () =>
                {
                    if (!lockPlacementInMenu || !_inputState.ToolMenuIsOpen)
                    {
                        bool notBlocked = !_instance.IsBlocked;
                        bool hasSnapIfRequired = CheckSnapIfRequired(in _instance, in _placeState);

                        if (notBlocked && hasSnapIfRequired && _instance.IsPlayerPlaceable &&
                            CheckAffordable(_instance))
                        {
                            if (_instance.IsConnector && _placeState.ConfirmCount == 0)
                            {
                                _placeState.ConfirmCount++;
                                Debug.Log($"[{nameof(Builder)}] Initial placement of {_instance.name} confirmed");
                            }
                            else
                            {
                                ApplyCost(_instance, _instance.PlacementCost);
                                DropPlaceable(_instance);
                                InstantiatePlaceablePrefab(_prefab, out _instance, ref _placeState);
                                SetPlacementStateDirty();
                                Debug.Log($"[{nameof(Builder)}] Final placement of {_instance.name} confirmed");
                            }
                        }
                        else
                        {
                            Debug.Log($"[{nameof(Builder)}] Placement rejected (" +
                                $"{(notBlocked ? "_" : "BLOCKED")} " +
                                $"{(hasSnapIfRequired ? "_" : "CANT_SNAP")} " +
                                $"{(CheckAffordable(_instance) ? "_" : "NOT_AFFORDABLE")} " +
                                $"{(_instance.IsPlayerPlaceable ? "_" : "NOT_PLAYER_PLACEABLE")}" +
                                ")");

                            if (!notBlocked)
                            {
                                NotifyUpdateObservers(PlacementPhase.PlacementFailed, PlacementFailure.Blocked);
                            }
                            else if (!hasSnapIfRequired)
                            {
                                NotifyUpdateObservers(PlacementPhase.PlacementFailed, PlacementFailure.NoSnap);
                            }
                            else if (!CheckAffordable(_instance))
                            {
                                NotifyUpdateObservers(PlacementPhase.PlacementFailed, PlacementFailure.NotAffordable);
                            }
                            else if (!_instance.IsPlayerPlaceable)
                            {
                                NotifyUpdateObservers(PlacementPhase.PlacementFailed,
                                    PlacementFailure.NotPlaceableByPlayer);
                            }
                            else
                            {
                                NotifyUpdateObservers(PlacementPhase.Placed, PlacementFailure.None);
                            }
                        }
                    }
                })
                .OnEntry(() =>
                {
                    _instance.gameObject.SetActive(true);
                    hitGizmo.gameObject.SetActive(true);
                })
                ;

            _fsm.Configure(State.PlacingNoHit)
                .SubstateOf(State.Placing)
                .Ignore(Trigger.NoHit)
                .InternalTransition(Trigger.Update, () =>
                {
                    // NOTE: In this state we assume that the constrained end handle position is updated based on the pointer
                    // screen position and not the world hit. To do this, when placing a constrained connector end while having
                    // no pointer world hit, synthesize a virtual hit on a sphere around the camera with radius equal to twice
                    // the magnet detection radius. This sphere is sized arbitrarily. The expected usage of the point so
                    // generated is to figure an input for a constrained end handle placement.

                    if (_instance &&
                        _instance.IsConnector &&
                        _instance.IsConstrainedEndHandle &&
                        _placeState.ConfirmCount > 0
                    )
                    {
                        // NOTE: we do not handle this sub-state with a separate state in the FSM since that would not help to make it
                        // any less complicated (state explosion).
                        Debug.Assert(
                            _instance && _instance.IsConnector && _placeState.ConfirmCount == 1,
                            "ASSERTION FAILED: Valid end handle placement state"
                        );
                        float noHitVirtualHitSphereRadius = overlapRadius * 2f;
                        Vector3 virtualHitPoint =
                            _inputState.PointerRay.origin +
                            _inputState.PointerRay.direction * noHitVirtualHitSphereRadius;
                        UpdatePlacement(_instance, _inputState, _placeState, virtualHitPoint);
                        _instance.OnUpdate();
                    }
                })
                .InternalTransition(Trigger.Confirmed, () =>
                {
                    // We can only plausibly confirm placement of a constrained end handle here, so we filter out all others. 

                    if (_instance &&
                        _instance.IsConnector &&
                        _instance.IsConstrainedEndHandle &&
                        _placeState.ConfirmCount > 0
                    )
                    {
                        // NOTE: we do not handle this sub-state with a separate state in the FSM since that would not help to make it
                        // any less complicated (state explosion).
                        Debug.Assert(
                            _instance && _instance.IsConnector && _placeState.ConfirmCount == 1,
                            "ASSERTION FAILED: Valid end handle placement state"
                        );
                        bool notBlocked = !_instance.IsBlocked;
                        bool hasSnapIfRequired = (!_instance.MustSnap ||
                            (_placeState.SelectedWorldMagnet && _placeState.SelectedPlaceableMagnet));

                        if (notBlocked && hasSnapIfRequired)
                        {
                            DropPlaceable(_instance);
                            InstantiatePlaceablePrefab(_prefab, out _instance, ref _placeState);
                            Debug.Log($"[{nameof(Builder)}] Final placement of {_instance.name} confirmed");
                        }
                        else
                        {
                            if (!notBlocked)
                            {
                                NotifyUpdateObservers(PlacementPhase.PlacementFailed,
                                    failureReason: PlacementFailure.Blocked);
                            }
                            else
                            {
                                NotifyUpdateObservers(PlacementPhase.PlacementFailed, PlacementFailure.NoSnap);
                            }
                        }
                    }
                })
                .InternalTransition(Trigger.ToolSelected, () =>
                {
                    EnsureToolSelectionWasRecognized();
                    _instance.gameObject.SetActive(false);
                })
                .OnEntry(() =>
                {
                    // we keep the placeable active if we're in constrained end-handle placement mode.
                    // Otherwise, we hide it while having no hit.
                    if (_instance)
                    {
                        bool isPlacingConstrainedEndHandle = _instance.IsConnector &&
                            _instance.IsConstrainedEndHandle &&
                            _placeState.ConfirmCount > 0;
                        _instance.gameObject.SetActive(isPlacingConstrainedEndHandle);
                    }

                    hitGizmo.gameObject.SetActive(false);
                    ClearFocusObject(ref _aim);
                    ResetHitDerivedState();
                })
                ;

            _fsm.Configure(State.Final)
                .OnEntry(() =>
                {
                    ClearFocusObject(ref _aim);
                    RemoveFocusAll();
                });

            return;

            // Called by from state <see cref="State.Placing"/> when the placement state has changed.
            void SetPlacementStateDirty()
            {
                Debug.Assert(_instance != null, "_placeable != null");
                UpdateAim(_rayHitInfo, ref _aim, false);
                UpdateHitGizmo(_rayHitInfo);
                UpdatePlacement(_instance, _inputState, _placeState, _rayHitInfo.point);
                _instance.OnUpdate();
            }
        }

        /// <summary>
        /// Checks whether the given placeable is in a valid snap-state. This does not check blocked state.
        /// </summary>
        private bool CheckSnapIfRequired(in Placeable instance, in PlaceState state)
        {
            bool hasSnapIfRequired =
                !instance.MustSnap ||
                ( // is magnet close enough
                    state.SelectedWorldMagnet &&
                    state.SelectedPlaceableMagnet &&
                    Vector3.Distance(
                        state.SelectedWorldMagnet.transform.position,
                        state.SelectedPlaceableMagnet.transform.position
                    ) < SnapEpsilon
                ) ||
                ( // is grid magnet close enough
                    state.SelectedWorldMagnet &&
                    state.SelectedWorldMagnet.IsGrid &&
                    state.SelectedPlaceableMagnet &&
                    Vector3.Distance(
                        state.SelectedWorldMagnet.GetNearestSnapPosition(
                            state.SelectedPlaceableMagnet.transform.position),
                        state.SelectedPlaceableMagnet.transform.position
                    ) < SnapEpsilon
                );
            return hasSnapIfRequired;
        }

        /// <summary>
        /// Activate a specific tool and fires notification events to all subscribers.
        /// </summary>
        /// <param name="prefab">The tool to activate</param>
        public void SetTool([CanBeNull] Placeable prefab)
        {
            _previousToolPrefab = _prefab;
            _prefab = prefab;
            if (prefab)
            {
                _fsm.Fire(Trigger.ToolSelected);
            }
            else
            {
                _fsm.Fire(Trigger.ToolDeselected);
            }
        }

        /// <summary>
        /// Update the current prefab instance to a new prefab in response to a tool change. This will be called automatically
        /// inside the relevant states.
        /// </summary>
        /// <remarks>This should be called primarily as a means to guarantee no state transition effects have been missed.</remarks>
        private void EnsureToolSelectionWasRecognized()
        {
            if (_previousToolPrefab != _prefab)
            {
                Debug.LogWarning(
                    $"[{nameof(Builder)}] Selection has changed unexpectedly, this indicates a declaration error in the state machine.");
                DestroyPlaceable(ref _instance, ref _placeState);
                if (_prefab != null)
                {
                    InstantiatePlaceablePrefab(_prefab, out _instance, ref _placeState);
                    PlayAudio(toolSelectedSfx);
                }

                ToolChanged?.Invoke();
            }
        }

        /// <summary>
        /// Set the current focus render mode to "Blocked".
        /// </summary>
        private void SetFocusModeBlocked(bool isBlocked)
        {
            if (_instance)
            {
                //TryAddFocus ( _placeable.Model );
                if (isBlocked || !CheckAffordable(_instance))
                {
                    SetFocusModeRejected();
                }
                else
                {
                    SetFocusModeHighlight();
                }
            }
        }

        /// <summary>
        /// Set the current focus render mode to "Delete".
        /// </summary>
        private void SetFocusModeDelete()
        {
            focusOutline.OutlineColor = deleteColor;
            focusOutline.OutlineWidth = Mathf.Max(1, focusOutline.OutlineWidth);
            focusOverlay.color = deleteColor;
            if (hitGizmo)
            {
                hitGizmo.GetComponentInChildren<Renderer>(true).sharedMaterial.color = deleteColor;
            }
        }

        /// <summary>
        /// Set the current focus render mode to "Focus".
        /// </summary>
        private void SetFocusModeFocus()
        {
            focusOutline.OutlineColor = focusColor;
            focusOutline.OutlineWidth = Mathf.Max(1, focusOutline.OutlineWidth);
            focusOverlay.color = focusColor;
            if (hitGizmo)
            {
                Renderer gizmoRenderer = hitGizmo.GetComponentInChildren<Renderer>();
                Debug.Assert(gizmoRenderer, "ASSERTION FAILED: gizmoRenderer != null", hitGizmo);
                Debug.Assert(gizmoRenderer.sharedMaterial, "ASSERTION FAILED: gizmoRenderer.sharedMaterial != null",
                    hitGizmo);
                gizmoRenderer.sharedMaterial.color = focusColor;
            }
        }

        /// <summary>
        /// Set the current focus render mode to "Highlight".
        /// </summary>
        private void SetFocusModeHighlight()
        {
            focusOutline.OutlineColor = highlightColor;
            focusOutline.OutlineWidth = Mathf.Max(1, focusOutline.OutlineWidth);
            focusOverlay.color = highlightColor;
            if (hitGizmo)
            {
                hitGizmo.GetComponentInChildren<Renderer>().sharedMaterial.color = highlightColor;
            }
        }

        /// <summary>
        /// Set the current focus render mode to "Rejected".
        /// </summary>
        private void SetFocusModeRejected()
        {
            focusOutline.OutlineColor = rejectedColor;
            focusOutline.OutlineWidth = Mathf.Max(1, focusOutline.OutlineWidth);
            focusOverlay.color = rejectedColor;
            if (hitGizmo)
            {
                hitGizmo.GetComponentInChildren<Renderer>().sharedMaterial.color = rejectedColor;
            }
        }

        /// <summary>Adds the given object to the focus layer.</summary>
        /// <remarks>Idempotent.</remarks>
        private void AddFocus(params GameObject[] objects)
        {
            foreach (GameObject it in objects)
            {
                // We cannot trust that objects only contains valid items
                // - May have been configured incorrectly in the editor
                // - Build flags of automatically generated objects may interfere with the serialized data
                if (!it)
                {
                    continue;
                }

                if (_focusObjects.TryAdd(it, it.layer))
                {
                    it.layer = focusLayer;
                    // Debug.Log ( $"[{nameof(Builder)}] Added {go.name}" );
                }
            }
        }

        /// <summary>Restores the original layer of the given objects. It is expected that <see cref="AddFocus"/> was previously called on all of these objects.</summary>
        /// <remarks>Idempotent.</remarks>
        private void RemoveFocus(params GameObject[] objects)
        {
            foreach (GameObject it in objects)
            {
                if (_focusObjects.TryGetValue(it, out int layer))
                {
                    if (it)
                    {
                        it.layer = layer;
                        _focusObjects.Remove(it);
                        // Debug.Log ( $"[{nameof(Builder)}] Removed {go.name}" );
                    }
                }
            }
        }

        /// <summary>
        /// Removes the focus from a target.
        /// </summary>
        /// <param name="target">The target object to unfocus.</param>
        private void ClearFocusObject(ref Placeable target)
        {
            if (target)
            {
                RemoveFocus(target.Models);
            }

            target = default;
        }

        /// <remarks>Idempotent.</remarks>
        public void RemoveFocusAll()
        {
            foreach ((GameObject go, int layer) in _focusObjects)
            {
                if (go)
                {
                    go.layer = layer;
                    // Debug.Log ( $"[{nameof(Builder)}] Removed all {go.name}" );
                }
            }

            _focusObjects.Clear();
        }


        /// <summary>
        /// Finds the tool ID (collection + index) that can be used to place a given <see cref="Placeable"/> instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="prefab"></param>
        /// <returns></returns>
        private bool TryFindToolForPlaceable(Placeable instance, out Placeable prefab)
        {
            prefab = default;
            if (_copyablePrefabs == null || !_copyablePrefabs.Any())
            {
                return false;
            }

            if (instance != default)
            {
                prefab = _copyablePrefabs.FirstOrDefault(it => it.AssetID == instance.AssetID);
                return prefab != default;
            }

            return false;
        }

        /// <summary>
        /// Performs a pointer raycast based on the current input state.
        /// </summary>
        /// <param name="mask"></param>
        /// <param name="inputState"></param>
        /// <param name="ray"></param>
        /// <param name="hit"></param>
        /// <returns>Whether something was hit.</returns>
        private bool TryUpdateRayCast(LayerMask mask, InputState inputState, out Ray ray, out RaycastHit hit)
        {
            ray = _camera.ScreenPointToRay(inputState.PointerScreenPosition);
            hit = default;

            return pointerRayRadius > RaySphereThreshold
                ? DrawPhysics.SphereCast(
                    ray: ray,
                    radius: pointerRayRadius,
                    maxDistance: maxBuildRange,
                    layerMask: mask,
                    queryTriggerInteraction: QueryTriggerInteraction.Collide,
                    hitInfo: out hit
                )
                : DrawPhysics.Raycast(
                    ray: ray,
                    maxDistance: maxBuildRange,
                    layerMask: mask,
                    queryTriggerInteraction: QueryTriggerInteraction.Collide,
                    hitInfo: out hit
                );
        }

        /// <summary>
        /// Cancel placement immediately.
        /// </summary>
        public void Cancel()
        {
            _fsm?.Fire(Trigger.Cancel);
        }

        /// <summary>
        /// The main placement update handler. All live tool updates are happening here once input is gathered.
        /// </summary>
        /// <remarks>
        /// This is technically a very stateful method that might seem to benefit from an explicit FSM. However implementing
        /// everything as sub-states of the FSM would fracture the imperative bits and that would actually obscure what is
        /// going on (we have tried). The state of this method is however exposed in its arguments and it does <b>not</b>
        /// rely on class state besides immutable configuration. This appears to be a reasonable middle ground that offers
        /// the best interface for future modification.
        /// </remarks>
        /// <param name="inputState">The current <see cref="InputState"/> state.</param>
        /// <param name="state">The transient <see cref="PlaceState"/> that this method depends on and updates from frame to frame.</param>
        /// <param name="surfacePoint">The hit point of the current pointer raycast.</param>
        /// <param name="placeable">The placeable instance that is currently "in placement" which will receive the
        /// updates from this method.</param>
        private void UpdatePlacement(Placeable placeable, InputState inputState, PlaceState state, Vector3 surfacePoint)
        {
            Debug.Assert(placeable != null, "_placeable != null", this);

            // we make a high visibility variable here to signify primary differentiation in behaviour -> placing a simple
            // object vs. a connector. The design goal here is to keep almost everything relating to placement validation,
            // filtering and analysis exactly the same for both cases and only branch where actual differences occur. This
            // keeps the related bits of code closer together which should make it easier to follow.
            bool IS_PLACING_END = placeable.IsConnector && state.ConfirmCount > 0;

            if (inputState.ScrollDelta != 0)
            {
                PlayAudio(alignSfx);
            }

            // sign will return 1 for input of 0 so we synthesize a f(0) = 0 result.
            float scrollDirection = inputState.ScrollDelta == 0 ? 0f : inputState.ScrollDelta > 0f ? 1f : -1f;
            state.Heading = (state.Heading - scrollDirection * rotateSpeed) % 360f;
            float snapHeading = Mathf.Round(state.Heading / angleIncrement) * angleIncrement;
            state.HeadingVector = Quaternion.AngleAxis(snapHeading, Vector3.up) * Vector3.forward;

            Quaternion beforeRot = placeable.transform.rotation;

            if (IS_PLACING_END)
            {
                if (!placeable.IsConstrainedEndHandle)
                {
                    AlignForwardWith(placeable.EndHandle.transform, state.HeadingVector);
                }
            }
            else
            {
                AlignForwardWith(placeable.transform, state.HeadingVector);
            }

            // Find the best world magnet and store it in the placement state
            // Find the best world magnet and store it in the placement state
            bool isWorldMagnetChanged = false;
            bool canSnap = false;
            bool didSnap = false; // we need this for synthesising an "actual snap" event
            {
                // we make the placeable's magnet sticky while the world magnet is unchanged, this prevents snapping glitches
                // due to floating point precision when multiple magnets are within equal distance.
                (Magnet magnet, Vector3 localOffset) bestWorldMagnet = GetBestWorldMagnet(surfacePoint, in _instance);

                // if we are snapping to the active placeable instance we ignore the snap, this happens when placing
                // a connector ends close together. This prevents zero-length connectors and reentry-loops.
                if (bestWorldMagnet.magnet && bestWorldMagnet.magnet.transform.IsChildOf(placeable.transform))
                {
                    bestWorldMagnet = default;
                }

                // if the world magnet is too close to the start handle of the connector we ignore it, this prevents
                // zero-length connectors
                if (placeable.IsConnector && IS_PLACING_END && bestWorldMagnet.magnet)
                {
                    float snappedConnectorLength = Vector3.Distance(
                        placeable.StartHandle.transform.position,
                        bestWorldMagnet.magnet.transform.position
                    );
                    if (snappedConnectorLength < MinConnectorLength)
                    {
                        bestWorldMagnet = default;
                    }
                }

                isWorldMagnetChanged = bestWorldMagnet.magnet != state.SelectedWorldMagnet;

                if (isWorldMagnetChanged)
                {
                    PlayAudio(snapSfx);
                }

                if (!inputState.IsAlign || state.AlignmentRay.direction == default || !state.SelectedWorldMagnet)
                {
                    canSnap = isWorldMagnetChanged;
                    state.SelectedWorldMagnet = bestWorldMagnet.magnet;
                    state.SelectedWorldMagnetOffset = bestWorldMagnet.localOffset;
                    gizmoForWorldMagnet.gameObject.SetActive(state.SelectedWorldMagnet);
                }
            }

            // reset alignment gizmo while we have not input that explicitly keeps it active
            if (!inputState.IsAlign)
            {
                state.AlignmentRay = new Ray(default, default);
            }

            // if we have a world magnet to snap to we figure out the best local magnet for that world magnet.
            // The best one would be the first one that does is not blocked when snapped to. In case of multiple choices
            // we favour the one that puts the placeable closer to the camera plane, which mean for example that if we look
            // at a snap point from below, and snapping the top/bottom are equally valid, we prefer the the placement below
            // since it is closer to the camera. 
            if (state.SelectedWorldMagnet)
            {
                if (inputState.IsAlign && state.AlignmentRay.direction == default)
                {
                    state.AlignmentRay = new Ray(
                        state.SelectedWorldMagnet.transform.position,
                        state.SelectedWorldMagnet.transform.forward
                    );
                }

                Vector3 snapPoint = state.SelectedWorldMagnet.TransformPoint(state.SelectedWorldMagnetOffset);
                gizmoAlignment.SetLine(snapPoint, snapPoint + state.SelectedWorldMagnet.GetForward());
                gizmoAlignment.gameObject.SetActive(true);

                //Ray rayToMagnet = new ( _rayHitInfo.point, _rayHitInfo.point - selectedWorldPos );
                //Vector3 toMagnetInPlane = ( _rayHitInfo.point - selectedWorldPos ).ProjectOntoPlane ( Vector3.up ).normalized;
                D.raw(new Shape.Line(surfacePoint, snapPoint), Color.yellow, Time.deltaTime);
                D.raw(new Shape.Circle(snapPoint, _camera.transform.forward, .5f), Color.yellow);
                gizmoForWorldMagnet.transform.position = snapPoint;

                bool isPlaceableMagnetChanged = false;
                if (isWorldMagnetChanged || inputState.ScrollDelta != 0 || true)
                {
                    if (IS_PLACING_END)
                    {
                        Debug.Assert(placeable.EndHandle != null, "ASSERTION FAILED: _placeable.EndHandle != null",
                            this);

                        state.SelectedPlaceableMagnet = IsMagnetMatch(placeable.EndHandle, state.SelectedWorldMagnet)
                            ? placeable.EndHandle
                            : default;
                    }
                    else
                    {
                        Magnet oldMagnet = state.SelectedPlaceableMagnet;
                        TrySelectBestMagnetForSnapping(
                            placeable: placeable,
                            snapPoint: snapPoint,
                            snapTarget: in state.SelectedWorldMagnet,
                            bestMagnet: out state.SelectedPlaceableMagnet,
                            state.HeadingVector,
                            surfacePoint: surfacePoint
                        );
                        isPlaceableMagnetChanged = oldMagnet != state.SelectedPlaceableMagnet;
                    }
                }

                // place object
                if (state.SelectedPlaceableMagnet)
                {
                    Vector3 before = placeable.transform.position;
                    if (IS_PLACING_END)
                    {
                        SnapPlaceableEndWith(
                            to: state.SelectedWorldMagnet,
                            placeable: placeable,
                            state.HeadingVector,
                            state.SelectedWorldMagnetOffset
                        );
                    }
                    else
                    {
                        SnapPlaceableWith(
                            from: state.SelectedPlaceableMagnet,
                            to: state.SelectedWorldMagnet,
                            placeable: placeable,
                            state.HeadingVector,
                            state.SelectedWorldMagnetOffset
                        );
                    }

                    Vector3 after = placeable.transform.position;
                    didSnap = before != after;
                }

                // place gizmo
                gizmoForPreviewMagnet.gameObject.SetActive(state.SelectedPlaceableMagnet);
                if (state.SelectedPlaceableMagnet)
                {
                    gizmoForPreviewMagnet.position = state.SelectedPlaceableMagnet.transform.position;
                }

                // revert snap position when blocked
                if (placeable.CheckBlocked() || !state.SelectedPlaceableMagnet)
                {
                    if (!staySnappedWhenBlocked)
                    {
                        if (IS_PLACING_END)
                        {
                            SetEndPositionToPoint(placeable, surfacePoint);
                        }
                        else
                        {
                            SetPositionToPoint(placeable, surfacePoint);
                        }
                    }
                }
                else if (didSnap)
                {
                    // we only play this if the snapped position wasn't reverted
                    PlayAudio(actualSnapSfx);
                    NotifyUpdateObservers(PlacementPhase.Snap);
                }

                // we trigger this if a new magnet was found that can be snapped to, but the position did not change
                if (!didSnap && canSnap)
                {
                    NotifyUpdateObservers(PlacementPhase.CanSnap);
                }

                // set rendering style
                SetFocusModeBlocked(placeable.IsBlocked);
            }
            else
            {
                // if we don't have a world magnet to snap to we fall back to the basic pointer-ray hit-point.

                // TODO: add proper alignment snapping
                if (inputState.IsAlign && state.AlignmentRay.direction != default)
                {
                    float distanceToAlignmentRay = Vector3
                        .Cross(state.AlignmentRay.direction, surfacePoint - state.AlignmentRay.origin).magnitude;
                    if (distanceToAlignmentRay < 1f)
                    {
                        surfacePoint = ClosestPointAlongLine(state.AlignmentRay.origin,
                            state.AlignmentRay.origin + state.AlignmentRay.direction, surfacePoint, out _);
                    }
                }

                gizmoAlignment.ClearLine();
                gizmoAlignment.gameObject.SetActive(false);
                gizmoForPreviewMagnet.gameObject.SetActive(false);

                if (IS_PLACING_END)
                {
                    SetEndPositionToPoint(placeable, surfacePoint);
                }
                else
                {
                    SetPositionToPoint(placeable, surfacePoint);
                }

                if (placeable.MustSnap || placeable.IsBlocked)
                {
                    SetFocusModeBlocked(true);
                }
                else
                {
                    SetFocusModeBlocked(false);
                }
            }

            // check if the object has rotated as a result of player input
            Quaternion afterRot = placeable.transform.rotation;
            if (inputState.ScrollDelta != 0 && beforeRot != afterRot)
            {
                NotifyUpdateObservers(PlacementPhase.Rotate);
            }

            if (state.SelectedPlaceableMagnet)
            {
                D.raw(
                    new Shape.Circle(
                        state.SelectedPlaceableMagnet.GetPosition(),
                        _camera.transform.forward,
                        .45f
                    ),
                    Color.yellow
                );
            }
        }

        private void UpdateHitGizmo(RaycastHit rayHitInfo)
        {
            if (!rayHitInfo.collider)
            {
                return;
            }

            // gizmos -> always
            D.raw(new Shape.Text(rayHitInfo.point, rayHitInfo.collider.name));

            if (hitGizmo)
            {
                hitGizmo.position = rayHitInfo.point;
                hitGizmo.right = _camera.transform.right;
                hitGizmo.forward = rayHitInfo.normal;
            }
        }

        private void ResetHitDerivedState()
        {
            if (_placeState != null)
            {
                _placeState.AlignmentRay = new Ray(default, default);
            }

            if (gizmoAlignment)
            {
                gizmoAlignment.ClearLine();
            }
        }

        private void SetPositionToPoint(Placeable placeable, Vector3 point)
        {
            placeable.SetPosition(ApplyWorldGrid(point));
        }

        private void SetEndPositionToPoint(Placeable placeable, Vector3 point)
        {
            Vector3 modifiedPoint = ApplyWorldGrid(point);

            if (placeable.IsConstrainedEndHandle)
            {
                float worldToPointer = Vector3.Distance(placeable.Position, modifiedPoint);
                float screenToPointer = Vector2.Distance(
                    _camera.WorldToViewportPoint(placeable.Position),
                    _camera.WorldToViewportPoint(modifiedPoint)
                );
                if (placeable.GridConstraintEndHandle != 0)
                {
                    float rangeScale =
                        Mathf.Max(placeable.DistanceRangeEndHandle.y, placeable.DistanceRangeEndHandle.x) *
                        2f;
                    float clampedAndSnappedDistance = Mathf.Clamp(
                        Mathf.Round(screenToPointer * rangeScale / placeable.GridConstraintEndHandle) *
                        placeable.GridConstraintEndHandle,
                        placeable.DistanceRangeEndHandle.x,
                        placeable.DistanceRangeEndHandle.y
                    );
                    placeable.SetEndPosition(
                        placeable.transform.position +
                        clampedAndSnappedDistance * placeable.EndHandle.transform.forward
                    );
                }
                else
                {
                    float clampedDistance = Mathf.Clamp(
                        screenToPointer,
                        placeable.DistanceRangeEndHandle.x,
                        placeable.DistanceRangeEndHandle.y
                    );
                    placeable.SetEndPosition(
                        placeable.Position +
                        clampedDistance * placeable.EndHandle.transform.forward
                    );
                }
            }
            else
            {
                placeable.SetEndPosition(modifiedPoint);
            }
        }

        /// <summary>
        /// Modifies a continuous world position to snap to a discrete world grid, as defined by <see cref="worldGridDivisions"/>.
        /// </summary>
        /// <param name="point">The point to modify.</param>
        /// <returns>The grid-snapped point.</returns>
        private Vector3 ApplyWorldGrid(Vector3 point)
        {
            Vector3 modifiedPoint = point;
            if (useWorldGrid)
            {
                modifiedPoint = new(
                    Mathf.Round(point.x * worldGridDivisions.x) / worldGridDivisions.x,
                    Mathf.Round(point.y * worldGridDivisions.y) / worldGridDivisions.y,
                    Mathf.Round(point.z * worldGridDivisions.z) / worldGridDivisions.z
                );
            }

            return modifiedPoint;
        }

        private bool IsBlockedWith(Magnet from, Magnet to, Placeable root, Vector3 forward)
        {
            // detect blocking volume overlap
            Pose originalPose = new(root.transform.position, root.transform.rotation);
            SnapPlaceableWith(from, to, root, forward);
            bool isBlocked = root.CheckBlocked();
            root.transform.SetPositionAndRotation(originalPose.position, originalPose.rotation);
            return isBlocked;
        }

        /// <summary>
        /// Calculates the angle between a placeable's current transform and its modified transform when snapped to a given <see cref="target"/> magnet. 
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="target"></param>
        /// <param name="originPlaceable"></param>
        /// <param name="forward"></param>
        /// <returns>The angle delta in degrees between origin and target.</returns>
        private float GetSnapAngleDelta(Magnet origin, Magnet target, Placeable originPlaceable, Vector3 forward)
        {
            // detect blocking volume overlap
            Pose originalPose = new(originPlaceable.transform.position, originPlaceable.transform.rotation);
            SnapPlaceableWith(origin, target, originPlaceable, forward);
            float delta = Quaternion.Angle(originalPose.rotation, originPlaceable.transform.rotation);
            originPlaceable.transform.SetPositionAndRotation(originalPose.position, originalPose.rotation);
            return delta;
        }

        /// <summary>
        /// Snaps a <paramref name="placeable"/>'s end handle by matching its position with an external magnet. 
        /// </summary>
        /// <param name="to">The external magnet to snap to.</param>
        /// <param name="placeable">The placeable owning the end handle.</param>
        /// <param name="forward">The vector describing the currently set input heading. Used to orient the end handle
        /// when its magnet's <see cref="Magnet.AlignWith"/> is <see cref="AlignWith.WorldUp"/>.
        /// </param>
        /// <param name="additionalOffset">An offset from the to-Magnet's local position to snap to.</param>
        /// <exception cref="ArgumentOutOfRangeException">When an internal error is detected.</exception>
        private void SnapPlaceableEndWith(
            Magnet to,
            Placeable placeable,
            Vector3 forward,
            Vector3 additionalOffset = default
        )
        {
            Debug.Assert(placeable.IsConnector, "placeable.IsConnector");
            Debug.Assert(placeable.EndHandle != null, "placeable.EndHandle != null");

            Transform pivot = placeable.EndHandle.transform;
            switch (placeable.EndHandle.AlignWith)
            {
                case AlignWith.MagnetForward:
                    ApplyForwardOffsetAlignment(pivot, to.transform, pivot, false);
                    break;
                case AlignWith.MagnetRight:
                    ApplyRightOffsetAlignment(pivot, to.transform, pivot, flip: false);
                    break;
                case AlignWith.MagnetUp:
                    ApplyUpOffsetAlignment(pivot, to.transform, pivot, true);
                    break;
                case AlignWith.MagnetFace:
                    ApplyFaceOffsetAlignment(pivot, to.transform, pivot, true);
                    break;
                default:
                    AlignForwardWith(pivot, forward);
                    break;
            }

            ApplyPosOffset(pivot, to.transform, pivot, additionalOffset);
        }

        /// <summary>
        /// Snaps a <paramref name="placeable"/> by matching one of its magnets with an external target magnet. 
        /// </summary>
        /// <param name="from">The placeable's magnet to use for snapping.</param>
        /// <param name="to">The external magnet to snap to.</param>
        /// <param name="placeable">The placeable to actually move.</param>
        /// <param name="heading">The vector describing the currently set input heading. Used to orient the placeable.</param>
        /// <param name="additionalOffset">An offset from the to-Magnets local position to snap to.</param>
        /// <exception cref="ArgumentOutOfRangeException">When an internal error is detected.</exception>
        private void SnapPlaceableWith(
            Magnet from,
            Magnet to,
            Placeable placeable,
            Vector3 heading,
            Vector3 additionalOffset = default
        )
        {
            Debug.Assert(from.transform.IsChildOf(placeable.transform),
                "from.transform.IsChildOf(placeable.transform)");
            Transform pivot = placeable.transform;

            switch (from.AlignWith)
            {
                case AlignWith.WorldUp:
                    AlignForwardWith(pivot, heading);
                    break;
                case AlignWith.MagnetForward:
                    ApplyForwardOffsetAlignment(from.transform, to.transform, pivot, true);
                    break;
                case AlignWith.MagnetRight:
                    ApplyRightOffsetAlignment(from.transform, to.transform, pivot, flip: false);
                    break;
                case AlignWith.MagnetUp:
                    ApplyUpOffsetAlignment(from.transform, to.transform, pivot, true);
                    break;
                case AlignWith.MagnetFace:
                    ApplyFaceOffsetAlignment(
                        from.transform,
                        to.transform,
                        pivot,
                        flipFaceAlignment,
                        additionalOffset
                    );
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ApplyPosOffset(from.transform, to.transform, pivot, additionalOffset);
        }

        /// <summary>
        /// Aligns a <paramref name="root"/> transform by matching a child transform's position and forward-rotation
        /// with a target transform's such that the forward-vectors of the transforms are collinear. 
        /// </summary>
        /// <param name="from">The child to match with the <paramref name="to"/>-transform.</param>
        /// <param name="to">The target transform to match with the <paramref name="from"/>-transform.</param>
        /// <param name="root">The transform that will actually be moved. Must be an ancestor of the
        /// <paramref name="from"/>-transform.</param>
        /// <param name="flip">Whether to flip the forward direction in the calculation.</param>
        // TODO: this does not work right in all cases
        private void ApplyForwardOffsetAlignment(Transform from, Transform to, Transform root, bool flip = false)
        {
            Debug.Assert((!from || !root) || from.IsChildOf(root), "ASSERTION FAILED: 'from' is child of 'root'");
            Debug.Assert((!to || !from) || !to.IsChildOf(root), "ASSERTION FAILED: 'to' is not child of 'root'");
            Quaternion localOffset = Quaternion.FromToRotation(root.transform.forward, -from.transform.forward);
            Quaternion flipOffset = flip
                ? Quaternion.AngleAxis(180f, to.up)
                : Quaternion.identity;
            root.rotation = flipOffset * localOffset * to.transform.rotation;
            root.up = to.up; // fix orientation glitches where the root.rotation would invert its y-axis
        }

        /// <summary>
        /// Aligns a <paramref name="root"/> transform by matching a child transform's position and rotation with a target
        /// transform's such that the right-vectors of the transforms are collinear.
        /// </summary>
        /// <param name="from">The child to match with the <paramref name="to"/>-transform.</param>
        /// <param name="to">The target transform to match with the <paramref name="from"/>-transform.</param>
        /// <param name="root">The transform that will actually be moved. Must be an ancestor of the
        /// <paramref name="from"/>-transform.</param>
        /// <param name="flip">Whether to flip the forward direction in the calculation.</param>
        // TODO: this does not work right in all cases
        private void ApplyRightOffsetAlignment(Transform from, Transform to, Transform root, bool flip = false)
        {
            Debug.Assert((!from || !root) || from.IsChildOf(root), "ASSERTION FAILED: 'from' is child of 'root'");
            Debug.Assert((!to || !from) || !to.IsChildOf(root), "ASSERTION FAILED: 'to' is not child of 'root'");
            Vector3 up = root.up;
            Quaternion localOffset = Quaternion.FromToRotation(root.transform.right, from.transform.right);

            bool dotFlip = Vector3.Dot(to.transform.right, from.transform.right) < 0;
            Quaternion flipOffset = flip && !dotFlip
                ? Quaternion.AngleAxis(180f, to.up)
                : Quaternion.identity;

            Quaternion rightAlignCorrection = flipOffset * localOffset * to.transform.rotation;
            root.rotation = Quaternion.LookRotation(rightAlignCorrection * Vector3.forward, up);
        }

        /// <summary>
        /// Aligns a <paramref name="root"/> transform by matching a child transform's position and rotation with a target
        /// transform's such that the up-vectors of the transforms are collinear.
        /// </summary>
        /// <param name="from">The child to match with the <paramref name="to"/>-transform.</param>
        /// <param name="to">The target transform to match with the <paramref name="from"/>-transform.</param>
        /// <param name="root">The transform that will actually be moved. Must be an ancestor of the
        /// <paramref name="from"/>-transform.</param>
        /// <param name="flip">Whether to flip the forward direction in the calculation.</param>
        // TODO: this does not work right in all cases
        private void ApplyUpOffsetAlignment(
            Transform from,
            Transform to,
            Transform root,
            bool flip = false
        )
        {
            Debug.Assert((!from || !root) || from.IsChildOf(root), "ASSERTION FAILED: 'from' is child of 'root'");
            Debug.Assert((!to || !from) || !to.IsChildOf(root), "ASSERTION FAILED: 'to' is not child of 'root'");
            Quaternion localOffset = Quaternion.FromToRotation(root.transform.up, from.transform.up);
            Quaternion flipOffset = flip
                ? Quaternion.AngleAxis(180f, to.right)
                : Quaternion.identity;
            root.rotation = flipOffset * localOffset * to.transform.rotation;
            root.right = to.right;
        }

        /// <summary>
        /// Aligns a <paramref name="root"/> transform by matching a child transform's position and rotation with a target transform's such that the right- and up-vectors of the transforms are collinear.
        /// </summary>
        /// <param name="from">The child to match with the <paramref name="to"/>-transform.</param>
        /// <param name="to">The target transform to match with the <paramref name="from"/>-transform.</param>
        /// <param name="root">The transform that will actually be moved. Must be an ancestor of the
        /// <paramref name="from"/>-transform.</param>
        /// <param name="flip">Whether to flip the forward direction in the calculation.</param>
        /// <param name="additionalOffset">An optional offset to apply to the snap position in the local space of the
        /// <paramref name="to"/>-transform.</param>
        private void ApplyFaceOffsetAlignment(
            Transform from,
            Transform to,
            Transform root,
            bool flip = false,
            Vector3 additionalOffset = default
        )
        {
            Debug.Assert((!from || !root) || from.IsChildOf(root), "ASSERTION FAILED: 'from' is child of 'root'");
            Debug.Assert((!to || !from) || !to.IsChildOf(root), "ASSERTION FAILED: 'to' is not child of 'root'");
            // first we convert the root from world- to from-space
            Vector3 localFwd = from.InverseTransformDirection(flip ? -root.forward : root.forward);
            Vector3 localUp = from.InverseTransformDirection(root.up);
            Vector3 localPos = from.InverseTransformPoint(root.position);
            // root is now in from-space
            // then we convert root in from-space back to world-space using to-space.
            Vector3 worldFwd = to.TransformDirection(localFwd);
            Vector3 worldUp = to.TransformDirection(localUp);
            //Vector3 reflectedAddOffInWorld = to.TransformDirection ( Vector3.Reflect ( additionalOffset, Vector3.forward ) );
            Vector3 worldPos = to.TransformPoint(localPos); // + reflectedAddOffInWorld;

            root.transform.SetPositionAndRotation(
                worldPos,
                Quaternion.LookRotation(worldFwd, worldUp)
            );
        }

        // TODO: this does not work right in all cases
        private void AlignForwardWith(Transform root, Vector3 direction)
        {
            root.up = Vector3.up;
            root.forward = direction;
        }

        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="root"></param>
        /// <param name="additionalOffset">An optional offset to apply to the snap position in the local space of the
        /// <paramref name="to"/>-<see cref="Transform"/>.</param>
        private void ApplyPosOffset(Transform from, Transform to, Transform root, Vector3 additionalOffset)
        {
            Debug.Assert(!from || !root || from.IsChildOf(root), "ASSERTION FAILED: 'from' is child of 'root'");
            Debug.Assert(!to || !root || !to.IsChildOf(root), "ASSERTION FAILED: 'to' is not child of 'root'");
            Vector3 offset = from.transform.position - root.transform.position;
            Vector3 nextPos = to.transform.position - offset;
            root.position = nextPos;
            // TODO: additionalOffset is calculated wrongly when the from-root has a non-zero heading
            root.Translate(to.TransformDirection(additionalOffset), Space.World);
        }

        private bool TrySelectBestMagnetForSnapping(
            Placeable placeable,
            Vector3 snapPoint,
            in Magnet snapTarget,
            out Magnet bestMagnet,
            Vector3 forward,
            Vector3 surfacePoint
        )
        {
            // Debug.Log ( "[{nameof(Builder)}] Update snapping" );
            bestMagnet = default;
            float bestRating = float.PositiveInfinity;
            float bestDot = 0;
            float bestDistance = float.PositiveInfinity;
            Ray rayToMagnet = new(snapPoint, snapPoint - snapTarget.transform.position);
            // first we get the best magnet from the full set
            Magnet rayCandidate = GetBestMagnetMatchAlongRay(snapTarget, rayToMagnet, _instance.Magnets);
            bool skipRayCandidate = true;
            // then we try to find the best magnet that will snap without being blocked
            if (skipRayCandidate || !rayCandidate ||
                IsBlockedWith(from: rayCandidate, to: snapTarget, root: _instance, forward))
            {
                //Debug.Log("searching for a better magnet...");

                // we use the average position of all magnets that match the snap target to calculate the position delta below.
                // We store this as a local offset so we don't have to repeat that calculation for every magnet.
                Vector3 localMatchCenter =
                    placeable.transform.InverseTransformPoint(CalculateMatchingMagnetCenter(placeable, snapTarget));

                for (int i = 0; i < placeable.Magnets.Count; i++)
                {
                    bool isMatch = IsMagnetMatch(seeker: placeable.Magnets[i], target: snapTarget);
                    // if we found one, we carry it forward with a rating
                    if (isMatch && !IsBlockedWith(from: placeable.Magnets[i], to: snapTarget, root: placeable,
                        forward))
                    {
                        D.raw(
                            new Shape.Circle(placeable.Magnets[i].transform.position, _camera.transform.forward, .3f),
                            Color.white, .5f);
                        // we want to snap with an orientation that closely matches the preset one.
                        float angleDelta = GetSnapAngleDelta(placeable.Magnets[i], snapTarget, placeable, forward);
                        float angleRating = Mathf.Clamp01(angleDelta / 180f);

                        // we want to snap in a position that is as close as possible to the surface point we are aiming at
                        (Vector3 posDelta, Vector3 camDelta) = GetSnapDelta(
                            placeable.Magnets[i],
                            snapTarget,
                            placeable,
                            forward,
                            surfacePoint,
                            localMatchCenter
                        );

                        float centerPosDot = Vector3.Dot(posDelta, _rayHitInfo.normal);
                        float centerPosDistance =
                            posDelta.magnitude / (overlapRadius / (float)placeable.OverlapModifier);
                        float cameraPosDistance =
                            camDelta.magnitude / (overlapRadius / (float)placeable.OverlapModifier);
                        float posRating;

                        if (centerPosDistance.AlmostEqual(bestDistance, SnapEpsilon))
                        {
                            if (centerPosDot.AlmostEqual(bestDot, SnapEpsilon))
                            {
                                posRating = cameraPosDistance;
                            }
                            else if (centerPosDot > 0f)
                            {
                                posRating = 0f - centerPosDistance;
                            }
                            else
                            {
                                posRating = maxBuildRange - centerPosDistance;
                            }
                        }
                        else
                        {
                            posRating = centerPosDistance;
                        }

                        float ratingBlend = Mathf.Lerp(
                            posRating,
                            angleRating,
                            RemapHelper.RemapUnclamped(-1f, 1f, 0f, 1f, snapBias)
                        );
                        if (ratingBlend < bestRating)
                        {
                            bestRating = ratingBlend;
                            bestDot = centerPosDot;
                            bestDistance = centerPosDistance;
                            bestMagnet = placeable.Magnets[i];
                        }
                    }
                    else if (isMatch)
                    {
                        // Debug.Log($"[{nameof(Builder)}] Search at {i} ({placeable.Magnets[i].name}) is blocked");
                    }
                }

                if (bestMagnet == default)
                {
                    bestMagnet = rayCandidate;
                }
            }
            else
            {
                bestMagnet = rayCandidate;
            }

            return bestMagnet != default;

            (Vector3 centerDelta, Vector3 camDelta) GetSnapDelta(Magnet origin, Magnet target,
                Placeable originPlaceable,
                Vector3 fwd, Vector3 point,
                Vector3 localMatchCenter)
            {
                Pose originalPose = new(originPlaceable.transform.position, originPlaceable.transform.rotation);
                SnapPlaceableWith(origin, target, originPlaceable, fwd);
                Vector3 worldMatchCenter = originPlaceable.transform.TransformPoint(localMatchCenter);
                Vector3 centerDelta = worldMatchCenter - point;
                Vector3 camDelta = worldMatchCenter - _camera.transform.position;
                D.raw(new Shape.Point(point), 0.25f);
                D.raw(new Shape.Point(worldMatchCenter), 0.25f);
                D.raw(new Shape.Line(point, worldMatchCenter), 0.5f);
                D.raw(new Shape.Text(worldMatchCenter, centerDelta), 0.5f);
                originPlaceable.transform.SetPositionAndRotation(originalPose.position, originalPose.rotation);
                return (centerDelta, camDelta);
            }
        }

        /// <summary>
        /// Calculates the average position of all magnets on a given placeable that match a given snap target.
        /// </summary>
        /// <param name="placeable">The placeable holding the magnets.</param>
        /// <param name="snapTarget">The magnet to snap to.</param>
        /// <returns>The average position of the matched magnets.</returns>
        private float3 CalculateMatchingMagnetCenter(Placeable placeable, Magnet snapTarget)
        {
            int matchCount = 0;
            double3 matchAggregate = double3.zero;
            for (int i = 0; i < placeable.Magnets.Count; i++)
            {
                bool isMatch = IsMagnetMatch(seeker: placeable.Magnets[i], target: snapTarget);
                if (isMatch)
                {
                    matchCount++;
                    matchAggregate += new double3(placeable.Magnets[i].transform.position);
                }
            }

            Vector3 matchAverage = matchCount > 0
                ? (float3)(matchAggregate / new double3(matchCount))
                : transform.position;
            return matchAverage;
        }

        private async UniTaskVoid DeleteAimObjectAsync(Placeable aim)
        {
            if (aim)
            {
                Debug.Log($"[{nameof(Builder)}] Deletion of {_aim.name} confirmed");
                Bounds bounds = CalculateObjectBounds(_aim);
                PlayAudio(deleteFx);
                OnWorldChanged(bounds);
                RemoveFocus(_aim.Models);

                // trigger physics collision exit events...
                if (aim.TryGetComponent(out Rigidbody rb))
                {
                    rb.detectCollisions = false;
                    await UniTask.NextFrame(PlayerLoopTiming.FixedUpdate);
                    await UniTask.NextFrame(PlayerLoopTiming.Update);
                }

                // ...then destroy
                // (we check if the object still exist as it may have been destroyed transitively already)
                if (aim)
                {
                    aim.OnDelete();
                }

                // (we check if the object still exist as it may have been destroyed transitively already)
                if (aim)
                {
                    Bounds aimBounds = aim.GetBounds();
                    Destroy(aim.gameObject);
                    onDeleted.Invoke(aimBounds);
                }
            }
            else
            {
                Debug.Log($"[{nameof(Builder)}] Deletion failed; No aim object");
            }
        }

        private bool CheckDeleteAffordable([CanBeNull] Placeable placeable)
        {
            return placeable && placeable.DeletionCost.Where(it => it.Identity).All(it =>
            {
                long cost = GetModifiedCost(placeable, it);
                return ResourceStockpile.GetAvailableCount(it.Identity) > GetModifiedCost(placeable, it) &&
                    ResourceInventory.CanPut(it.Identity, cost);
            });
        }

        private bool CheckAffordable([CanBeNull] Placeable placeable)
        {
            return placeable &&
                placeable.PlacementCost.Where(it => it.Identity).All(
                    it => ResourceStockpile.GetAvailableCount(it.Identity) > GetModifiedCost(placeable, it));
        }

        private Bounds CalculateObjectBounds(
            [System.Diagnostics.CodeAnalysis.NotNull]
            Placeable focus,
            bool includeMeshes = true,
            bool includeColliders = true,
            bool includeTriggers = false
        )
        {
            Bounds b = default;
            Component[] components = focus.GetComponentsInChildren<Component>();
            if (includeColliders)
            {
                foreach (Collider it in components.OfType<Collider>())
                {
                    if (!it.isTrigger || includeTriggers)
                    {
                        b.Encapsulate(it.bounds);
                    }
                }
            }

            if (includeMeshes)
            {
                foreach (Renderer it in components.OfType<Renderer>())
                {
                    b.Encapsulate(it.bounds);
                }
            }


            return b;
        }

        private void OnWorldChanged(Bounds bounds)
        {
            _isWorldDirty = true;
            _lastDirtyTime = Time.time;
            _dirtyBounds.Encapsulate(bounds);
        }

        /// <summary>
        /// Gets the best world magnet relative to a point given in world space.
        /// </summary>
        /// <param name="point">The point to check for.</param>
        /// <param name="instance">The placeable instance currently being placed.</param>
        /// <returns>A tuple with the selected magnet and a local-space offset corresponding to a grid location inside
        /// the magnet's bounds. If the magnet is not a grid this offset will always be zero.</returns>
        private (Magnet magnet, Vector3 localOffset) GetBestWorldMagnet(Vector3 point, in Placeable instance)
        {
            Magnet bestWorldMagnet = default;
            float bestDistance = float.PositiveInfinity;
            Vector3 bestOffset = Vector3.zero;
            float overlapModifier = 1f / (instance ? (int)instance.OverlapModifier : 1);
            float modifiedOverlapRadius = overlapRadius * overlapModifier;

            int hitCount = DrawPhysics.OverlapSphereNonAlloc(point, modifiedOverlapRadius, _overlapHits, magnetMask);
            D.raw(point, Color.red, .1f);

            if (hitCount > 0)
            {
                HashSet<SoMagnetIdentity> magnetSnapToUnion = HashSetPool<SoMagnetIdentity>.Get();
                HashSet<SoMagnetIdentity> magnetIdentityUnion = HashSetPool<SoMagnetIdentity>.Get();
                HashSet<Magnet> magnetSelfUnion = HashSetPool<Magnet>.Get();
                magnetSelfUnion.UnionWith(instance.Magnets);
                Debug.Assert(instance.Magnets is { Count: > 0 }, $"{_instance.GetInstanceID():X} has no magnets", this);

                foreach (Magnet magnet in instance.Magnets)
                {
                    magnetIdentityUnion.UnionWith(magnet.Identity);
                    magnetSnapToUnion.UnionWith(magnet.SnapTo);
                }

                for (int i = 0; i < hitCount; i++)
                {
                    Collider hit = _overlapHits[i];
                    if (
                        hit.TryGetComponent(out Magnet candidate) &&
                        !magnetSelfUnion.Contains(candidate) &&
                        candidate.AcceptFrom.Overlaps(magnetIdentityUnion) &&
                        !candidate.RejectFrom.Overlaps(magnetIdentityUnion) &&
                        magnetSnapToUnion.Overlaps(candidate.Identity)
                    )
                    {
                        // we've found a magnet pair that is semantically matched up, now we have to heuristically select
                        // the one we want by evaluating each candidate. For now we simply use euclidean distance. However
                        // if the to-magnet is a grid, this is still a bit more complicated...

                        if (candidate.IsGrid)
                        {
                            // if this is a grid magnet we find the closest grid location within the grid's bounds
                            //Vector3 closestPoint = candidate.GridBounds.ClosestPoint ( point );
                            Vector3 closestPoint = candidate.GetNearestSnapPosition(point);
                            float distance = Vector3.Distance(closestPoint, point);
                            if (distance < bestDistance)
                            {
                                bestWorldMagnet = candidate;
                                bestDistance = distance;
                                bestOffset = candidate.transform.InverseTransformPoint(closestPoint);
                            }
                        }
                        else
                        {
                            // if this is a point magnet we simply use the transform position
                            float distance = Vector3.Distance(candidate.transform.position, point);
                            if (distance < bestDistance)
                            {
                                bestWorldMagnet = candidate;
                                bestDistance = distance;
                                bestOffset = Vector3.zero;
                            }
                        }
                    }
                }

                HashSetPool<SoMagnetIdentity>.Release(magnetSnapToUnion);
                HashSetPool<SoMagnetIdentity>.Release(magnetIdentityUnion);
                HashSetPool<Magnet>.Release(magnetSelfUnion);
            }

            return (magnet: bestWorldMagnet, localOffset: bestOffset);
        }

        private Magnet GetBestMagnetNearPoint(Magnet target, Vector3 point, Span<Magnet> magnets)
        {
            if (magnets == null)
            {
                throw new ArgumentNullException(nameof(magnets));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Magnet best = default;
            float bestValue = float.PositiveInfinity;
            foreach (Magnet seeker in magnets)
            {
                // skip non-accepted magnets
                if (!TargetAcceptsSeeker(seeker, target))
                {
                    continue;
                }

                // skip non-wanted magnets
                if (!SeekerWantsTarget(seeker, target))
                {
                    continue;
                }

                // filter any magnets that have closely matching normal and position.
                if (Vector3.Distance(target.transform.position, seeker.transform.position) < 0.1f &&
                    Vector3.Dot(target.transform.forward, seeker.transform.forward) > 0.5f)
                {
                    continue;
                }

                float distance = Vector3.Distance(seeker.transform.position, point);
                if (distance < bestValue)
                {
                    best = seeker;
                    bestValue = distance;
                }
            }

            if (best)
            {
                BoxCollider bestCollider = best.GetComponent<BoxCollider>();
                if (bestCollider)
                {
                    D.raw(bestCollider, Color.red, .5f);
                }
            }

            return best;
        }

        private Magnet GetBestMagnetMatchAlongRay(Magnet target, Ray ray, IList<Magnet> magnets)
        {
            if (magnets == null)
            {
                throw new ArgumentNullException(nameof(magnets));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Magnet best = default;
            float bestValue = float.PositiveInfinity;
            foreach (Magnet seeker in magnets)
            {
                // skip non-accepted magnets
                if (!TargetAcceptsSeeker(seeker, target))
                {
                    continue;
                }

                // skip non-wanted magnets
                if (!SeekerWantsTarget(seeker, target))
                {
                    continue;
                }

                // filter any magnets that have closely matching normal and position.
                /*if (
                    Vector3.Distance ( target.transform.position, magnet.transform.position ) < 0.1f &&
                    Vector3.Dot ( target.transform.forward, magnet.transform.forward ) > 0.5f ) {
                    continue;
                }
                */

                D.raw(new Shape.Ray(ray), Color.magenta, .2f);
                float distance = DistanceToRay(seeker.transform.position, ray);
                if (distance < bestValue)
                {
                    best = seeker;
                    bestValue = distance;
                }
            }

            if (best)
            {
                BoxCollider bestCollider = best.GetComponent<BoxCollider>();
                if (bestCollider)
                {
                    D.raw(bestCollider, Color.red);
                }
            }

            return best;
        }

        private static bool IsMagnetMatch(in Magnet seeker, in Magnet target)
        {
            if (seeker == null)
            {
                throw new ArgumentNullException(nameof(seeker));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            // skip non-accepted magnets
            if (!TargetAcceptsSeeker(seeker, target))
            {
                return false;
            }

            // skip non-wanted magnets
            if (!SeekerWantsTarget(seeker, target))
            {
                return false;
            }

            return seeker;
        }

        /// <remarks>
        /// Target accepts seeker if the seeker's <see cref="Magnet.Identity"/> mask is a superset of the target's <see cref="Magnet.AcceptFrom"/> mask.
        /// </remarks>
        private static bool TargetAcceptsSeeker(in Magnet seeker, in Magnet target)
        {
            // whether all of target.AcceptFrom are in seeker.Identity 
            //return target.AcceptFrom != MagnetShape.Nothing && ( seeker.Identity | target.AcceptFrom ) == seeker.Identity;
            return Magnet.TargetAcceptsSeeker(seeker, target);
        }

        /// <remarks>
        /// Seeker wants target if the target's <see cref="Magnet.Identity"/> mask is a superset of the seeker's <see cref="Magnet.SnapTo"/> mask.
        ///     </remarks>
        private static bool SeekerWantsTarget(in Magnet seeker, in Magnet target)
        {
            // return ( seeker.SnapTo & target.Identity ) != 0;

            // whether all of seeker.SnapTo are in target.Identity 
            //return seeker.SnapTo != MagnetShape.Nothing && ( seeker.SnapTo | target.Identity ) == target.Identity;
            return Magnet.SeekerWantsTarget(seeker, target);
        }

        private Magnet GetFirstMagnetMatch(Magnet target, in Span<Magnet> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Magnet best = default;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (!IsMagnetMatch(in candidates[i], target))
                    continue;

                best = candidates[i];
            }

            return best;
        }

        // distance of a point to a ray of infinite length  
        private float DistanceToRay(Vector3 point, Ray ray)
            => Vector3.Cross(point - ray.origin, ray.direction).magnitude;

        private float DistanceToRay(Vector3 point, Vector3 pointOnRayA, Vector3 pointOnRayB)
            => Vector3.Cross(point - pointOnRayA, (pointOnRayB - pointOnRayA).normalized).magnitude;

        private void DestroyPlaceable(ref Placeable placeable, ref PlaceState placeState)
        {
            if (placeable)
            {
                placeable.OnAborted();
                Destroy(placeable.gameObject);
                placeable = default;
                //placeState = default;
                PlayAudio(toolCancelledSfx);
                NotifyUpdateObservers(PlacementPhase.Cancelled);
            }
        }

        private void DropPlaceable(Placeable placeable)
        {
            if (placeable)
            {
                _placedInstanceID++;
                _instance.transform.localScale = Vector3.one;
                placeable.name = $"{placeable.name} {_placedInstanceID} COMPLETE";

                // Apply final configuration via Unpack()

                IPlaceable.Memento placeablePackage = new()
                {
                    EndPose = placeable.EndHandle ? PoseExtensions.PoseFrom(placeable.EndHandle.transform) : default,
                    RootPose = PoseExtensions.PoseFrom(placeable.transform)
                };

                // Note:
                // - We expect .Unpack to NOT call OnPlaced, we do it explicitly here.
                // - This OnPlaced call is somewhat early, there are still changes being applied to the instance after this,
                //   so potentially changes made in OnPlaced-handlers may be overwritten.
                // - we inject configuration via Unpack to unify the object instantiation code paths.
                placeable.Unpack(placeablePackage, assetLookup);
                placeable.OnPlaced();

                // finish up build mode changes

                Collider[] colliders = placeable.GetComponentsInChildren<Collider>();
                foreach (Collider c in colliders)
                {
                    c.enabled = true;
                }

                RemoveFocus(placeable.Models);
                OnWorldChanged(CalculateObjectBounds(placeable));
                EnableCollidersInMask(placeable.gameObject, SurfaceMask, true);

                onPlaced.Invoke(placeable.GetBounds());

                _instance = null;
            }
        }

        private void PlayAudio(AudioClip audioClip)
        {
            if (audioClip && (audioSource || TryGetComponent(out audioSource)))
            {
                audioSource.PlayOneShot(audioClip);
                //Debug.Log ( audioClip.name );
            }
        }


        private void ApplyCost(Placeable placeable, IEnumerable<PropCount> lineItems)
        {
            List<PropClaim<SoProp, IActor>> claims = new();
            foreach (PropCount lineItem in lineItems)
            {
                Debug.Assert(lineItem.Count >= 0, "ASSERTION FAILED: lineItem.Count >= 0");
                long quantity = GetModifiedCost(placeable, lineItem);
                if (ResourceProvider.TryClaimProp(
                    lineItem.Identity,
                    IActor.None,
                    quantity,
                    out PropClaim<SoProp, IActor> claim)
                )
                {
                    claims.Add(claim);
                }
                else
                {
                    foreach (PropClaim<SoProp, IActor> existingClaim in claims)
                    {
                        existingClaim.Cancel();
                    }

                    Debug.Log($"[{nameof(Builder)}] Claimed {lineItem} for {placeable.name}");
                    return;
                }
            }

            for (int i = 0; i < claims.Count; i++)
            {
                PropClaim<SoProp, IActor> claim = claims[i];
                if (claim.TryCommit())
                {
                    SpawnResourceText(-claim.Quantity, claim.ClaimedProp);
                    Debug.Log(
                        $"[{nameof(Builder)}] Committed {claim.Quantity}x {claim.ClaimedProp} for {placeable.name}");
                }
                else
                {
                    Debug.LogError(
                        $"[{nameof(Builder)}] Failed to commit {claim.Quantity}x {claim.ClaimedProp} for {placeable.name}");
                }
            }
        }

        /// <summary>
        /// Spawn a floating text object at the given position with the given value. The value is formatted according to the prop's unit.
        /// </summary>
        /// <param name="value">The value to display.</param>
        /// <param name="prop">The prop defining the icon and unit of the value.</param>
        private void SpawnResourceText(long value, SoProp prop)
        {
            Vector3 spawnPos = _rayHitInfo.point + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.5f);

            if (_instance ? _instance : _aim)
            {
                textSpawner?.SpawnText(
                    value,
                    spawnPos,
                    default,
                    prop.IconSymbol
                );
            }
        }

        /// <summary>
        /// Applies a refund for a given placeable based on the line items and the placeable's refund factor.
        /// </summary>
        /// <param name="target">The placeable to refund.</param>
        /// <param name="lineItems">The refund line items to process for refunds.</param>
        private void ApplyRefund(Placeable target, IEnumerable<PropCount> lineItems)
        {
            foreach (PropCount lineItem in lineItems)
            {
                long quantity = GetModifiedCost(target, lineItem);
                int refund = Mathf.RoundToInt(quantity * target.DeletionRefund);
                if (ResourceInventory.TryPut(lineItem.Identity, refund))
                {
                    SpawnResourceText(refund, lineItem.Identity);
                }
                else
                {
                    Debug.LogError(
                        $"[{nameof(Builder)}] Failed to refund {quantity}x {lineItem.Identity.Name} for {target.name}");
                    return;
                }
            }
        }

        /// <summary>
        /// Calculates the factored cost of a given placeable based on its unit placement cost multiplied by its size (length) when it is an extruded object.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="lineItem"></param>
        /// <returns></returns>
        private static long GetModifiedCost(Placeable target, PropCount lineItem)
        {
            int distanceFactor = Mathf.Max(1, target.IsConnector ? Mathf.CeilToInt(target.ConnectorLength) : 1);
            long quantity = lineItem.Count * distanceFactor;
            return quantity;
        }

        private void InstantiatePlaceablePrefab(
            Placeable prefab,
            out Placeable placeable,
            ref PlaceState state
        )
        {
            Debug.Assert(!_instance, "ASSERTION FAILED: old placeable instance still exists.");
            if (prefab)
            {
                prefab.gameObject.SetActive(false);

#if UNITY_EDITOR
                // in the editor we use the prefab utility to instantiate the prefab, so we can keep the prefab links
                // intact, which allows us using runtime tools to create new prefabs from the placed objects.
                if (keepPrefabLinks)
                {
                    UnityEngine.Object instance =
                        UnityEditor.PrefabUtility.InstantiatePrefab(prefab.gameObject, Container);
                    placeable = ((GameObject)instance).GetComponent<Placeable>();
                }
                else
                {
                    placeable = Instantiate(prefab, Vector3.zero, Quaternion.identity, Container);
                }
#else
                placeable = Instantiate(prefab, Vector3.zero, Quaternion.identity, Container);
#endif
                //placeable.Configure ( persistenceSystem );
                placeable.gameObject.SetActive(true);
                prefab.gameObject.SetActive(true);

                Debug.Assert(placeable.Magnets != null, $"{placeable.GetInstanceID():X} still has no magnets", this);
                PlaceState tmp = state;
                state = new()
                {
                    Heading = tmp?.Heading ?? default
                };
                EnableCollidersInMask(placeable.gameObject, SurfaceMask, false);
                AddFocus(placeable.Models);
                Collider[] colliders = placeable.GetComponentsInChildren<Collider>();
                foreach (Collider co in colliders)
                {
                    co.enabled = false;
                }

                placeable.name = $"{prefab.name} BUILD";
                placeable.OnStarted();
            }
            else
            {
                throw new ArgumentNullException(nameof(prefab));
            }
        }

        private static void EnableCollidersInMask(GameObject root, LayerMask mask, bool isEnabled)
        {
            root.GetComponentsInChildren<Collider>().ForEach(it =>
            {
                if (MaskContains(mask, it.gameObject.layer))
                {
                    it.enabled = isEnabled;
                }
            });
        }

        internal static bool MaskContains(LayerMask layerMask, int layerID) => (layerMask & (1 << layerID)) != 0;

        internal static int Mod(int a, int n) => (Mathf.Abs(a * n) + a) % n;

        internal static float Mod(float a, float n) => a - n * Mathf.Floor(a / n);

        /// <summary>
        /// Finds the closest point on an infinitely long line to a given point.
        /// </summary>
        public static Vector3 ClosestPointAlongLine(
            Vector3 start,
            Vector3 end,
            Vector3 point,
            out float distance
        )
        {
            Ray ray = new(start, end - start);
            distance = Vector3.Dot(point - ray.origin, ray.direction);
            return ray.origin + ray.direction * distance;
        }

        /// <summary>
        /// Finds the closest point on a line segment to a given point.
        /// </summary>
        public static Vector3 ClosestPointOnLine(
            Vector3 start,
            Vector3 end,
            Vector3 point,
            out float distanceAlongLine
        )
        {
            Vector3 line = end - start;
            float length = line.magnitude;
            line.Normalize();

            Vector3 toPoint = point - start;
            distanceAlongLine = Vector3.Dot(toPoint, line);
            float distanceClamped = Mathf.Clamp(distanceAlongLine, 0f, length);
            return start + line * distanceClamped;
        }


        private void UpdateAim(RaycastHit rayHitInfo, ref Placeable target, bool addFocus = false)
        {
            Placeable prevTarget = target;
            bool targetChanged = false;
            if (rayHitInfo.collider != default)
            {
                target = rayHitInfo.collider.GetComponentInParent<Placeable>();
                targetChanged = prevTarget != target;
            }

            if (targetChanged)
            {
                if (prevTarget)
                {
                    RemoveFocus(prevTarget.Models);
                }

                if (target && addFocus)
                {
                    AddFocus(target.Models);
                }
            }
        }

        /// <summary>
        /// Invokes an update event with a context struct.
        /// </summary>
        /// <param name="phase">The update message type.</param>
        /// <param name="failureReason">If action failed, what happened?</param>
        private void NotifyUpdateObservers(
            PlacementPhase phase,
            PlacementFailure failureReason = PlacementFailure.None
        )
        {
            PlacementUpdateEventArgs updateEventArgs = new()
            {
                Source = this,
                Phase = phase,
                FailureReason = failureReason
            };

            PlacementPerformed?.Invoke(updateEventArgs);
        }

        /// <summary>
        /// Signals that the builder's internal state machine can respond to.
        /// </summary>
        private enum Trigger
        {
            /// <summary>
            /// The placement of the current object is confirmed.
            /// </summary>
            Confirmed,

            /// <summary>
            /// The user has requested the delete mode to start.
            /// </summary>
            StartDeleting,

            /// <summary>
            /// The user has requested the place mode to start.
            /// </summary>
            StartPlacing,

            /// <summary>
            /// The user has requested that the current state be cancelled.
            /// </summary>
            Cancel,

            /// <summary>
            /// This instance of the builder should initialize now.
            /// </summary>
            Start,

            /// <summary>
            /// This instance of the builder should finalize now.
            /// </summary>
            Final,

            /// <summary>
            /// A tool was selected.
            /// </summary>
            ToolSelected,

            /// <summary>
            /// The builder should update in the context of a another frame being rendered (UnityEngine Update() signal)
            /// </summary>
            Update,

            /// <summary>
            /// Another placeable object was hit by the pointer raycast.
            /// </summary>
            IsHit,

            /// <summary>
            /// No other placeable object was hit by the pointer raycast.
            /// </summary>
            NoHit,

            /// <summary>
            /// The builder was enabled.
            /// </summary>
            Disable,

            /// <summary>
            /// The builder was disabled.
            /// </summary>
            Enable,

            /// <summary>
            /// The current tool was deselected.
            /// </summary>
            ToolDeselected,
            InstanceLost
        }

        /// <summary>
        /// States that the builder can be in.
        /// </summary>
        private enum State
        {
            /// <summary>
            /// The builder is not yet initialized.
            /// </summary>
            Initial,

            /// <summary>
            /// The builder is ready accept a tool selection.
            /// </summary>
            Ready,

            /// <summary>
            /// The builder is currently in delete-mode.
            /// </summary>
            Deleting,

            /// <summary>
            /// The builder is currently placing an object.
            /// </summary>
            Placing,

            /// <summary>
            /// The builder is being finalized/destroyed.
            /// </summary>
            Final,

            /// <summary>
            /// Sub-state of <see cref="Deleting"/>. The builder is aiming at a placed object.
            /// </summary>
            DeletingIsHit,

            /// <summary>
            /// Sub-state of <see cref="Deleting"/>. The builder is <b>not</b> aiming at a placed object.
            /// </summary>
            DeletingNoHit,

            /// <summary>
            /// Sub-state of <see cref="Placing"/>. The builder is <b>not</b> aiming at a placed object.
            /// </summary>
            PlacingNoHit,

            /// <summary>
            /// Sub-state of <see cref="Placing"/>. The builder is aiming at a placed object.
            /// </summary>
            PlacingIsHit,

            /// <summary>
            /// Sub-state of <see cref="Ready"/>. The builder is <b>not</b> aiming at a placed object.
            /// </summary>
            ReadyNoHit,

            /// <summary>
            /// Sub-state of <see cref="Ready"/>. The builder is aiming at a placed object.
            /// </summary>
            ReadyIsHit,

            /// <summary>
            /// The builder is currently disabled.
            /// </summary>
            Disabled
        }

        /// <summary>
        /// The transient state of a placement operation.
        /// </summary>
        private class PlaceState
        {
            /// <summary>
            /// The currently selected world <see cref="Magnet"/>. I.e. the magnet that the current tool-prefab will want to snap to.
            /// </summary>
            public Magnet SelectedWorldMagnet;

            /// <summary>
            /// The currently selected magnet on the placeable. I.e. the magnet that the current tool-prefab will want to use for snapping to the <see cref="SelectedWorldMagnet"/>.
            /// </summary>
            public Magnet SelectedPlaceableMagnet;

            /// <summary>
            /// The world-space heading vector currently derived from <see cref="Heading"/>.
            /// </summary>
            public Vector3 HeadingVector = Vector3.forward;

            /// <summary>
            /// The local offset of the selected world magnet (relative to its prefab's root transform).
            /// </summary>
            public Vector3 SelectedWorldMagnetOffset;

            /// <summary>
            /// The ray that describes the alignment axis of the current tool.
            /// </summary>
            public Ray AlignmentRay;

            /// <summary>
            /// The virtual heading updated by rotation inputs.
            /// </summary>
            public float Heading = 0;

            /// <summary>
            /// Counter for confirmation triggers since the placement of the current tool started.
            /// </summary>
            public int ConfirmCount;
        }

        /// <summary>
        /// Input state for the builder. This aggregates all inputs into one struct to make it easier to pass around.
        /// </summary>
        public struct InputState
        {
            /// <summary>
            /// A unique, monotonic identifier for this input state. Version 0 is interpreted as 'undefined'.
            /// </summary>
            public long Version;

            /// <summary>
            /// Whether the pointer is currently over a UI element.
            /// </summary>
            public bool PointerIsOverUi;

            /// <summary>
            /// Whether the tool menu is currently open.
            /// </summary>
            public bool ToolMenuIsOpen;

            /// <summary>
            /// Whether the current tool was confirmed this frame.
            /// </summary>
            public bool IsConfirmThisFrame;

            /// <summary>
            /// Whether escape was triggered this frame.
            /// </summary>
            public bool IsEscThisFrame;

            /// <summary>
            /// Whether delete mode should be active.
            /// </summary>
            public bool IsDelete;

            /// <summary>
            /// Whether delete mode was requested this frame.
            /// </summary>
            public bool HasDeleteStartedThisFrame;

            /// <summary>
            /// Whether delete mode was cancelled this frame.
            /// </summary>
            public bool HasDeleteEndedThisFrame;

            /// <summary>
            /// Whether the current tool should be in aligned-mode.
            /// </summary>
            public bool IsAlign;

            /// <summary>
            /// Whether the copy command was triggered this frame.
            /// </summary>
            public bool IsCopyThisFrame;

            /// <summary>
            /// The rotate delta since the last update.
            /// </summary>
            public float ScrollDelta;

            /// <summary>
            /// The hotkey that was pressed this frame. -1 if none. 
            /// </summary>
            public int HotkeyThisFrame;

            /// <summary>
            /// The pointer delta since the last updated.
            /// </summary>
            public Vector2 PointerDelta;

            /// <summary>
            /// The pointer's position in view space.
            /// </summary>
            public Vector3 PointerViewPosition;

            /// <summary>
            /// The pointer's position is screen space.
            /// </summary>
            public Vector3 PointerScreenPosition;

            /// <summary>
            /// The pointer ray in world space.
            /// </summary>
            public Ray PointerRay;

            /// <summary>
            /// Whether the tab-left command was triggered this frame.
            /// </summary>
            public bool TabLeftThisFrame;

            /// <summary>
            /// Whether the tab-right command was triggered this frame.
            /// </summary>
            public bool TabRightThisFrame;

            /// <summary>
            /// The input vector for the menu navigation.
            /// </summary>
            public Vector2Int MenuNav;

            /// <summary>
            /// Whether menu nav was triggered this frame.
            /// </summary>
            public bool MenuNavThisFrame;
        }

        /// <summary>
        /// Context message for the builder when an item was placed or removed.
        /// </summary>
        public struct PlacementUpdateEventArgs
        {
            /// <summary>
            /// The source of this context.
            /// </summary>
            public Builder Source;

            /// <summary>
            /// The purpose of this context update
            /// </summary>
            public PlacementPhase Phase;

            /// <summary>
            /// Whether action has failed.
            /// </summary>
            public PlacementFailure FailureReason;
        }

        /// <summary>
        /// Specifies the builder update context type.
        /// Used by <see cref="PlacementUpdateEventArgs"/>
        /// </summary>
        public enum PlacementPhase
        {
            /// <summary>
            /// A tool has been selected.
            /// </summary>
            Started,

            /// <summary>
            /// The selected tool of the builder has been cancelled.
            /// </summary>
            Cancelled,

            /// <summary>
            /// The placement action could not perform.
            /// </summary>
            PlacementFailed,

            /// <summary>
            /// A valid magnet has been found.
            /// </summary>
            CanSnap,

            /// <summary>
            /// The placeable snapped to a magnet.
            /// </summary>
            Snap,

            /// <summary>
            /// The placeable has been rotated.
            /// </summary>
            Rotate,

            /// <summary>
            /// The deletion of a placeable has failed.
            /// </summary>
            DeletionFailed,

            /// <summary>
            /// A placeable was deleted successfully.
            /// </summary>
            Deleted,

            /// <summary>
            /// A placeable was placed successfully.    
            /// </summary>
            Placed
        }

        /// <summary>
        /// Reasons why placement, copy or deletion of a placeable is not possible.
        /// Used by <see cref="PlacementUpdateEventArgs"/>.
        /// </summary>
        public enum PlacementFailure
        {
            /// <summary>
            /// Did not fail.
            /// </summary>
            None,

            /// <summary>
            /// Cannot find object or surface
            /// </summary>
            NoAim,

            /// <summary>
            /// Player cannot afford the action.
            /// </summary>
            NotAffordable,

            /// <summary>
            /// Place/Copy: Not a valid placeable.
            /// </summary>
            NoPlaceable,

            /// <summary>
            /// Place: Player not allowed to place the placeable.
            /// </summary>
            NotPlaceableByPlayer,

            /// <summary>
            /// Place: Placeable was blocked or is out of bounds.
            /// </summary>
            Blocked,

            /// <summary>
            /// Place: NoSnap = The Placeable did not snap
            /// </summary>
            NoSnap,

            /// <summary>
            /// Delete: The player is not allowed to delete the object.
            /// </summary>
            NotDeletableByPlayer
        }
    }
/*
    internal interface IResourceSystem {
        int GetCount ( SoProp argProp );
        bool TryClaimProp ( SoProp lineItemProp, IActor none, int lineItemCount, out PropClaim<SoProp, IActor> propClaim );
    }
*/
}