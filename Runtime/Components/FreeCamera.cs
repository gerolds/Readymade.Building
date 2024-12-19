using NaughtyAttributes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Readymade.Building.Components {
    /// <inheritdoc />
    /// <summary>
    /// Utility Free Camera component.
    /// </summary>
    public class FreeCamera : MonoBehaviour {
        private const float MouseSensitivityMultiplier = 0.01f;

        /// <summary>
        /// Input modes available for <see cref="FreeCamera"/>.
        /// </summary>
        private enum InputMode {
            /// <summary>
            /// Use the built-in (predefined) input actions.
            /// </summary>
            Automatic,

            /// <summary>
            /// Use input actions from a <see cref="InputActionAsset"/>.
            /// </summary>
            ActionReferences,

            /// <summary>
            /// Use locally defined custom input actions.
            /// </summary>
            LocalActions
        }

        /// <summary>
        /// Rotation speed when using the mouse.
        /// </summary>
        [FormerlySerializedAs ( "lookSpeedMouse" )]
        [Tooltip ( "Base look speed." )]
        [BoxGroup ( "Sensitivity" )]
        [SerializeField]
        [Range ( 1f, 100f )]
        public float lookSpeed = 4.0f;

        /// <summary>
        /// Movement speed.
        /// </summary>
        [Tooltip ( "Base move speed that camera has immediately after starting to move. Will be modified by acceleration." )]
        [BoxGroup ( "Sensitivity" )]
        [Range ( 0f, 10f )]
        [SerializeField]
        public float baseMoveSpeed = 1.0f;

        /// <summary>
        /// Scale factor of the turbo mode.
        /// </summary>
        [Tooltip ( "Speed up factor when turbo mode is engaged. Default is 4." )]
        [BoxGroup ( "Sensitivity" )]
        [Range ( 1f, 10f )]
        [SerializeField]
        public float turbo = 4.0f;

        [Tooltip (
            "Acceleration in units per sec^2 by which the camera speed changes. A value of 0 creates a constant speed. Default is 4." )]
        [BoxGroup ( "Sensitivity" )]
        [SerializeField]
        [Range ( 0f, 10f )]
        private float acceleration = .5f;

        [Tooltip ( "The decay factor by which the acceleration will be modified based on exponential current speed. " +
                   "A value of 1 keeps it constant. Values below decrease, values above 1 increase acceleration. Default is 1." )]
        [BoxGroup ( "Sensitivity" )]
        [SerializeField]
        [Range ( .5f, 2f )]
        private float accelerationDecayFactor = 1f;

        [Tooltip ( "Time in seconds from full speed to dead stop. Default is 0.15." )]
        [BoxGroup ( "Sensitivity" )]
        [Range ( 0f, 1f )]
        [SerializeField]
        private float deceleration = .15f;

        [Tooltip ( "Step by which the base speed of the camera is incremented/decremented. Default is 0.25." )]
        [BoxGroup ( "Sensitivity" )]
        [Range ( 0f, 10f )]
        [SerializeField]
        private float moveSpeedIncrement = 0.25f;

        [Tooltip ( "Which mode to use for input gathering." )]
        [BoxGroup ( "Input Actions" )]
        [SerializeField]
        private InputMode _input;

        /// <summary>
        /// Rotation speed when using a controller.
        /// </summary>
        [FormerlySerializedAs ( "lookSpeedGamepad" )]
        [Tooltip ( "Sensitivity to gamepad look inputs relative to the base look speed." )]
        [BoxGroup ( "Input Actions" )]
        [SerializeField]
        [ShowIf ( nameof ( _input ), InputMode.Automatic )]
        [Range ( 1f, 100f )]
        public float gamepadLookSensitivity = 4.0f;

        [BoxGroup ( "Input Actions" )]
        [SerializeField]
        [ShowIf ( nameof ( _input ), InputMode.ActionReferences )]
        private bool _enableActionRefs;

        [BoxGroup ( "Input Actions" )]
        [SerializeField]
        [ShowIf ( nameof ( _input ), InputMode.ActionReferences )]
        private InputActionReference _lookActionRef;

        [BoxGroup ( "Input Actions" )]
        [SerializeField]
        [ShowIf ( nameof ( _input ), InputMode.ActionReferences )]
        private InputActionReference _moveActionRef;

        [BoxGroup ( "Input Actions" )]
        [SerializeField]
        [ShowIf ( nameof ( _input ), InputMode.ActionReferences )]
        private InputActionReference _speedActionRef;

        [BoxGroup ( "Input Actions" )]
        [SerializeField]
        [ShowIf ( nameof ( _input ), InputMode.ActionReferences )]
        private InputActionReference _yMoveActionRef;

        [InfoBox ( "The Turbo- and EnableLook-actions are only used for keyboard & mouse input." )]
        [BoxGroup ( "Input Actions" )]
        [SerializeField]
        [ShowIf ( nameof ( _input ), InputMode.ActionReferences )]
        private InputActionReference _turboActionRef;

        [BoxGroup ( "Input Actions" )]
        [SerializeField]
        [ShowIf ( nameof ( _input ), InputMode.ActionReferences )]
        [EnableIf ( nameof ( requireMode ) )]
        private InputActionReference _enableLookActionRef;

        [BoxGroup ( "Input Actions" )]
        [SerializeField]
        [ShowIf ( nameof ( _input ), InputMode.LocalActions )]
        private InputAction _lookAction;

        [BoxGroup ( "Input Actions" )]
        [SerializeField]
        [ShowIf ( nameof ( _input ), InputMode.LocalActions )]
        private InputAction _moveAction;

        [BoxGroup ( "Input Actions" )]
        [SerializeField]
        [ShowIf ( nameof ( _input ), InputMode.LocalActions )]
        private InputAction _speedAction;

        [BoxGroup ( "Input Actions" )]
        [SerializeField]
        [ShowIf ( nameof ( _input ), InputMode.LocalActions )]
        private InputAction _yMoveAction;

        [InfoBox ( "The Turbo- and EnableLook-actions are only used for keyboard & mouse input." )]
        [BoxGroup ( "Input Actions" )]
        [SerializeField]
        [ShowIf ( nameof ( _input ), InputMode.LocalActions )]
        private InputAction _turboAction;

        [BoxGroup ( "Input Actions" )]
        [SerializeField]
        [ShowIf ( nameof ( _input ), InputMode.LocalActions )]
        [EnableIf ( nameof ( requireMode ) )]
        private InputAction _enableLookAction;

        [BoxGroup ( "Position Constraint" )]
        [Tooltip ( "Whether to constrain the camera position within a bounding volume." )]
        [SerializeField]
        private bool clampPosition = true;

        [BoxGroup ( "Position Constraint" )]
        [Tooltip ( "Maximum y-range that the camera is allowed to be in. Combine with " + nameof ( originDistance ) +
                   " to define a spherical segment." )]
        [SerializeField]
        [ShowIf ( nameof ( clampPosition ) )]
        private Vector2 yRange = new ( 0, 100f );

        [BoxGroup ( "Position Constraint" )]
        [Tooltip ( "Maximum distance the camera can be from the origin. Combine with " + nameof ( clampPosition ) +
                   " to define a spherical segment." )]
        [SerializeField]
        [ShowIf ( nameof ( clampPosition ) )]
        private float originDistance = 400f;

        [BoxGroup ( "Position Constraint" )]
        [Tooltip (
            "Ground colliders that the camera can't pass through. These are expected to be terrains or planes without overhangs. Collisions with with non-ground colliders are not (yet) supported." )]
        [SerializeField]
        private LayerMask groundMask;

        [BoxGroup ( "Position Constraint" )]
        [Tooltip ( "Minimal distance from any ground collider." )]
        [SerializeField]
        private float groundDistance = 1f;

        [FormerlySerializedAs ( "requireLookMode" )]
        [FormerlySerializedAs ( "modalFreeLook" )]
        [BoxGroup ( "Modality" )]
        [Tooltip ( "When enabled a modal toggle is required to activate free-look." )]
        [SerializeField]
        private bool requireMode = false;

        [FormerlySerializedAs ( "requireLocked" )]
        [BoxGroup ( "Modality" )]
        [Tooltip (
            "Enable free-look only when the cursor is locked. Caution: Cursor locking will need to be managed elsewhere!" )]
        [SerializeField]
        private bool requireCursorLock = true;

        [FormerlySerializedAs ( "requireLookFocus" )]
        [FormerlySerializedAs ( "requireFocus" )]
        [BoxGroup ( "Modality" )]
        [Tooltip (
            "Enable free-look only when the application is focused." )]
        [SerializeField]
        private bool requireAppFocus = true;

        [ShowNonSerializedField]
        private float _smoothMoveSpeed;

        [ShowNonSerializedField]
        private float _transientMoveSpeed;

        [ShowNonSerializedField]
        private Vector3 _currentVelocity;

        [ShowNonSerializedField]
        private float _decelerationVelocity;

        private float _inputRotateAxisX;
        private float _inputRotateAxisY;
        private float _inputChangeSpeed;
        private float _inputVertical;
        private float _inputHorizontal;
        private float _inputYAxis;
        private bool _inputIsTurbo;
        private bool _inputIsFreeLookEnabled;


        /// <summary>
        /// Event function.
        /// </summary>
        private void OnEnable () {
            RegisterInputs ();
        }

        /// <summary>
        /// Defined the builtin input actions and subscribes to them or uses the provided action references.
        /// </summary>
        private void RegisterInputs () {
            if ( _input == InputMode.Automatic ) {
                InputActionMap map = new ( nameof ( FreeCamera ) + " Automatic" );

                _lookAction = map.AddAction ( "look" );
                _lookAction.AddBinding ( "<Mouse>/delta" );
                _lookAction.AddBinding ( "<Gamepad>/rightStick" )
                    .WithProcessor ( $"scaleVector2(x={gamepadLookSensitivity:f0}, y={gamepadLookSensitivity:f0})" );

                _moveAction = map.AddAction ( "move" );
                _moveAction.AddBinding ( "<Gamepad>/leftStick" );
                _moveAction.AddCompositeBinding ( "Dpad" )
                    .With ( "Up", "<Keyboard>/w" )
                    .With ( "Up", "<Keyboard>/upArrow" )
                    .With ( "Down", "<Keyboard>/s" )
                    .With ( "Down", "<Keyboard>/downArrow" )
                    .With ( "Left", "<Keyboard>/a" )
                    .With ( "Left", "<Keyboard>/leftArrow" )
                    .With ( "Right", "<Keyboard>/d" )
                    .With ( "Right", "<Keyboard>/rightArrow" );

                _speedAction = map.AddAction ( "speed" );
                _speedAction.AddBinding ( "<Gamepad>/dpad" );
                _speedAction.AddCompositeBinding ( "Dpad" )
                    .With ( "Up", "<Keyboard>/home" )
                    .With ( "Down", "<Keyboard>/end" );

                _yMoveAction = map.AddAction ( "yMove" );
                _yMoveAction.AddCompositeBinding ( "Dpad" )
                    .With ( "Up", "<Keyboard>/pageUp" )
                    .With ( "Down", "<Keyboard>/pageDown" )
                    .With ( "Up", "<Keyboard>/e" )
                    .With ( "Down", "<Keyboard>/q" )
                    .With ( "Up", "<Gamepad>/rightTrigger" )
                    .With ( "Down", "<Gamepad>/leftTrigger" );

                _enableLookAction = map.AddAction ( "enableFreeLook", InputActionType.Button );
                _enableLookAction.AddBinding ( "<Mouse>/rightButton" );

                _turboAction = map.AddAction ( "turbo", InputActionType.Button );
                _turboAction.AddBinding ( "<Keyboard>/leftShift" );
            }

            if ( _input is InputMode.ActionReferences ) {
                _moveAction = _moveActionRef.action;
                _lookAction = _lookActionRef.action;
                _speedAction = _speedActionRef.action;
                _yMoveAction = _yMoveActionRef.action;
                _turboAction = _turboActionRef.action;
                _enableLookAction = _enableLookActionRef.action;
            }

            if ( _input is InputMode.ActionReferences && _enableActionRefs ) {
                _moveActionRef.action.Enable ();
                _lookActionRef.action.Enable ();
                _speedActionRef.action.Enable ();
                _yMoveActionRef.action.Enable ();
                _turboActionRef.action.Enable ();
                _enableLookActionRef.action.Enable ();
            }

            if ( _input is InputMode.LocalActions or InputMode.Automatic ) {
                _moveAction?.Enable ();
                _lookAction?.Enable ();
                _speedAction?.Enable ();
                _yMoveAction?.Enable ();
                _turboAction?.Enable ();
                _enableLookAction?.Enable ();
            }
        }


        /// <summary>
        /// Polls the configured input actions for their current values.
        /// </summary>
        private void UpdateInputs () {
            _inputRotateAxisX = 0.0f;
            _inputRotateAxisY = 0.0f;
            //leftShiftBoost = false;
            _inputIsTurbo = false;
            _inputIsFreeLookEnabled = false;

            Vector2 lookDelta = _lookAction.ReadValue<Vector2> ();
            _inputRotateAxisX = lookDelta.x * lookSpeed * MouseSensitivityMultiplier;
            _inputRotateAxisY = lookDelta.y * lookSpeed * MouseSensitivityMultiplier;

            _inputIsTurbo = _turboAction.IsPressed ();
            _inputIsFreeLookEnabled =
                ( !requireCursorLock || Cursor.lockState == CursorLockMode.Locked ) &&
                ( !requireAppFocus || Application.isFocused ) &&
                ( !requireMode || _enableLookAction.IsPressed () );

            _inputChangeSpeed = _speedAction.ReadValue<Vector2> ().y;

            Vector2 moveDelta = _moveAction.ReadValue<Vector2> ();
            _inputHorizontal = moveDelta.x;
            _inputVertical = moveDelta.y;
            _inputYAxis = _yMoveAction.ReadValue<Vector2> ().y;
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void Update () {
            UpdateInputs ();
            UpdateCamera ();
        }

        /// <summary>
        /// Update the camera position and rotation based on the current input.
        /// </summary>
        private void UpdateCamera () {
            if ( _inputChangeSpeed != 0.0f ) {
                baseMoveSpeed += _inputChangeSpeed * moveSpeedIncrement;
                if ( baseMoveSpeed < moveSpeedIncrement ) {
                    baseMoveSpeed = moveSpeedIncrement;
                }
            }

            bool hasMoveInput = _inputVertical != 0.0f || _inputHorizontal != 0.0f || _inputYAxis != 0.0f;
            bool hasRotationInput = _inputIsFreeLookEnabled && ( _inputRotateAxisX != 0.0f || _inputRotateAxisY != 0.0f );
            bool hasAnyInput = hasRotationInput || hasMoveInput;

            if ( hasAnyInput ) {
                if ( _inputIsFreeLookEnabled ) {
                    float rotationX = transform.localEulerAngles.x;
                    float newRotationY = transform.localEulerAngles.y + _inputRotateAxisX;

                    // Weird clamping code due to weird Euler angle mapping...
                    float newRotationX = ( rotationX - _inputRotateAxisY );
                    if ( rotationX <= 90.0f && newRotationX >= 0.0f )
                        newRotationX = Mathf.Clamp ( newRotationX, 0.0f, 90.0f );
                    if ( rotationX >= 270.0f )
                        newRotationX = Mathf.Clamp ( newRotationX, 270.0f, 360.0f );

                    transform.localRotation = Quaternion.Euler ( newRotationX, newRotationY, transform.localEulerAngles.z );
                }
            }

            if ( hasMoveInput ) {
                _currentVelocity = Vector3.zero;
                if ( _transientMoveSpeed < baseMoveSpeed ) {
                    _transientMoveSpeed = baseMoveSpeed;
                }


                _transientMoveSpeed += Mathf.Pow ( accelerationDecayFactor, _transientMoveSpeed ) * acceleration * Time.deltaTime;

                _currentVelocity += transform.forward * _inputVertical;
                _currentVelocity += transform.right * _inputHorizontal;
                _currentVelocity += transform.up * _inputYAxis;
            } else {
                _transientMoveSpeed = Mathf.SmoothDamp (
                    _transientMoveSpeed,
                    0,
                    ref _decelerationVelocity,
                    deceleration,
                    float.MaxValue
                );
            }

            float turboFactor = _inputIsTurbo ? turbo : 1f;
            transform.position += _currentVelocity * ( _transientMoveSpeed * Time.deltaTime * turboFactor );

            if ( clampPosition ) {
                transform.position = new Vector3 (
                    transform.position.x,
                    Mathf.Clamp ( transform.position.y, yRange.x, yRange.y ),
                    transform.position.z
                );

                transform.position = Vector3.ClampMagnitude ( transform.position, originDistance );
            }

            bool hasGround = Physics.Raycast ( transform.position, Vector3.down, out RaycastHit hit, originDistance, groundMask );
            if ( hasGround ) {
                if ( hit.distance < groundDistance ) {
                    transform.position = hit.point + Vector3.up * ( groundDistance * 1.01f );
                }
            } else {
                // fix any ground clipping by teleporting back above ground.
                if ( Physics.Raycast ( transform.position + Vector3.up * originDistance, Vector3.down, out RaycastHit seeker,
                        originDistance * 2f, groundMask ) ) {
                    transform.position = hit.point + Vector3.up * ( groundDistance * 1.01f );
                }
            }
        }
    }
}