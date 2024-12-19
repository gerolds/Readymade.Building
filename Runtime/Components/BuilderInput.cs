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

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using Readymade.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Readymade.Building.Components
{
    /// <summary>
    /// Injects input into the <see cref="Builder"/> component. This decouples the input from the builder and allows for
    /// easy switching between input modes.
    /// </summary>
    [RequireComponent(typeof(Builder))]
    public class BuilderInput : MonoBehaviour
    {
        /// <summary>
        /// The control modes supported by the builder.
        /// </summary>
        internal enum ControlMode
        {
            /// <summary>
            /// No control mode is defined.
            /// </summary>
            Undefined,

            /// <summary>
            /// Gamepad control mode.
            /// </summary>
            Gamepad,

            /// <summary>
            /// Mouse and keyboard control mode.
            /// </summary>
            MouseAndKeyboard
        }

        /// <summary>
        /// Specifies an input source. 
        /// </summary>
        private enum InputMode
        {
            /// <summary>
            /// Use predefined (hard-coded) input actions.
            /// </summary>
            Automatic,

            /// <summary>
            /// Use input actions provided by an <see cref="InputActionAsset"/>.
            /// </summary>
            ActionReferences,

            /// <summary>
            /// Use locally defined input actions.
            /// </summary>
            LocalActions
        }

        [BoxGroup("Input")]
        [SerializeField]
        [Tooltip("The input mode to use. Default is " + nameof(InputMode.Automatic) + ".")]
        private InputMode inputMode = InputMode.Automatic;

        [BoxGroup("Input")]
        [SerializeField]
        [Tooltip("The " + nameof(CanvasToggle) + " that controls and observes the toolbar canvas.")]
        private CanvasToggle toolbarCanvas;

        [ShowIf(nameof(inputMode), InputMode.ActionReferences)]
        [BoxGroup("Input")]
        [SerializeField]
        [Tooltip("Trigger input to confirm the current mode.")]
        private InputActionReference confirmActionRef;

        [ShowIf(nameof(inputMode), InputMode.ActionReferences)]
        [BoxGroup("Input")]
        [SerializeField]
        [Tooltip("Trigger input to cancel the current mode.")]
        private InputActionReference cancelActionRef;

        [ShowIf(nameof(inputMode), InputMode.ActionReferences)]
        [BoxGroup("Input")]
        [SerializeField]
        [Tooltip("Button input to enable delete-mode.")]
        private InputActionReference deleteModeActionRef;

        [ShowIf(nameof(inputMode), InputMode.ActionReferences)]
        [BoxGroup("Input")]
        [SerializeField]
        [Tooltip("Button input to enable alignment-mode.")]
        private InputActionReference alignModeActionRef;

        [ShowIf(nameof(inputMode), InputMode.ActionReferences)]
        [BoxGroup("Input")]
        [SerializeField]
        [Tooltip("Trigger input to copy selected object.")]
        private InputActionReference copyActionRef;

        [ShowIf(nameof(inputMode), InputMode.ActionReferences)]
        [BoxGroup("Input")]
        [SerializeField]
        [Tooltip("Axis input for controlling rotation.")]
        private InputActionReference rotateActionRef;

        [ShowIf(nameof(inputMode), InputMode.ActionReferences)]
        [BoxGroup("Input")]
        [SerializeField]
        [Tooltip("Vector2 input providing a pointer position.")]
        private InputActionReference pointerPositionActionRef;

        [ShowIf(nameof(inputMode), InputMode.ActionReferences)]
        [BoxGroup("Input")]
        [SerializeField]
        [Tooltip("Trigger input for menu tab-switch to the left.")]
        private InputActionReference tabLeftActionRef;

        [ShowIf(nameof(inputMode), InputMode.ActionReferences)]
        [BoxGroup("Input")]
        [SerializeField]
        [Tooltip("Trigger input for menu tab-switch to the right.")]
        private InputActionReference tabRightActionRef;

        [ShowIf(nameof(inputMode), InputMode.ActionReferences)]
        [BoxGroup("Input")]
        [SerializeField]
        [Tooltip("Vector2 input for controlling menu navigation.")]
        private InputActionReference menuNavActionRef;

        [ReadOnly]
        [SerializeField]
        [Tooltip("Ui layers to include when checking if the pointer is over ui. CURRENLTY UNUSED")]
        private int uiMask;

        private Builder _builder;
        private InputDevice _lastUpdatedDevice;
        private ControlMode _controlMode;
        private Camera _camera;
        private Builder.InputState _state;

        /// <summary>
        /// Event function.
        /// </summary>
        private void Awake()
        {
            _builder = GetComponent<Builder>();
            _camera = Camera.main;
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void Update()
        {
            GetInputs(ref _state);
            _builder.SetInput(ref _state);
        }

        /// <summary>
        /// Aggregates all input sources into a single input state. 
        /// </summary>
        /// <returns>The input state.</returns>
        private void GetInputs(ref Builder.InputState newState)
        {
            UpdateInputDevice();

            bool isOverUi = EventSystem.current.IsPointerOverGameObject();
            newState = new Builder.InputState
            {
                Version = newState.Version + 1,
                PointerIsOverUi = isOverUi,
                ToolMenuIsOpen = toolbarCanvas?.IsEnabled ?? false,
                IsConfirmThisFrame = !isOverUi &&
                (
                    (Gamepad.current?.buttonSouth.wasPressedThisFrame ?? false) ||
                    (Mouse.current?.leftButton.wasPressedThisFrame ?? false)
                ),
                IsEscThisFrame =
                    (Gamepad.current?.buttonEast.wasPressedThisFrame ?? false) ||
                    (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false) ||
                    (Mouse.current?.rightButton.wasPressedThisFrame ?? false),
                IsDelete = !isOverUi &&
                (
                    (Gamepad.current?.leftTrigger.isPressed ?? false) ||
                    (Keyboard.current?.xKey.isPressed ?? false)
                ),
                HasDeleteStartedThisFrame = !isOverUi &&
                (
                    (Gamepad.current?.leftTrigger.wasPressedThisFrame ?? false) ||
                    (Keyboard.current?.xKey.wasPressedThisFrame ?? false)
                ),
                HasDeleteEndedThisFrame =
                    (Gamepad.current?.leftTrigger.wasReleasedThisFrame ?? false) ||
                    (Keyboard.current?.xKey.wasReleasedThisFrame ?? false),
                IsAlign =
                    (Gamepad.current?.rightTrigger.isPressed ?? false) ||
                    (Keyboard.current?.leftShiftKey.isPressed ?? false),
                IsCopyThisFrame =
                    (Gamepad.current?.buttonWest.isPressed ?? false) ||
                    (Keyboard.current?.fKey.wasPressedThisFrame ?? false),
                ScrollDelta = (Gamepad.current?.leftShoulder.wasPressedThisFrame ?? false ? -1f : 0) +
                    (Gamepad.current?.rightShoulder.wasPressedThisFrame ?? false ? 1f : 0) +
                    Mouse.current?.scroll.value.y ?? 0,
                PointerDelta = (Mouse.current?.delta.value ?? Vector2.zero) / Screen.height,
                HotkeyThisFrame = BuilderPresenter.INVALID,
                PointerScreenPosition =
                    Cursor.lockState == CursorLockMode.Locked // pointer position in locked mode has jitter
                        ? _camera.ViewportToScreenPoint(new Vector3(.5f, .5f, 0))
                        : new Vector3(
                            Mouse.current?.position.value.x ?? 0,
                            Mouse.current?.position.value.y ?? 0,
                            0
                        ),
                PointerViewPosition =
                    Cursor.lockState == CursorLockMode.Locked // pointer position in locked mode has jitter
                        ? new Vector3(.5f, .5f, 0)
                        : _camera.ScreenToViewportPoint(
                            new Vector3(
                                Mouse.current?.position.value.x ?? 0,
                                Mouse.current?.position.value.y ?? 0,
                                0
                            )
                        ),
                PointerRay = Cursor.lockState == CursorLockMode.Locked // pointer position in locked mode has jitter
                    ? _camera.ViewportPointToRay(new Vector3(.5f, .5f, 0))
                    : _camera.ScreenPointToRay(
                        new Vector3(
                            Mouse.current?.position.value.x ?? 0,
                            Mouse.current?.position.value.y ?? 0,
                            0
                        )
                    ),
                TabLeftThisFrame = Keyboard.current?.yKey.wasPressedThisFrame ?? false,
                TabRightThisFrame = Keyboard.current?.leftAltKey.wasPressedThisFrame ?? false,
                MenuNav = new Vector2Int(
                    (Keyboard.current?.dKey.isPressed ?? false ? 1 : 0) +
                    (Keyboard.current?.aKey.isPressed ?? false ? -1 : 0),
                    (Keyboard.current?.wKey.isPressed ?? false ? 1 : 0) +
                    (Keyboard.current?.sKey.isPressed ?? false ? -1 : 0)
                ),
                MenuNavThisFrame =
                    (Keyboard.current?.dKey.wasPressedThisFrame ?? false) ||
                    (Keyboard.current?.aKey.wasPressedThisFrame ?? false) ||
                    (Keyboard.current?.wKey.wasPressedThisFrame ?? false) ||
                    (Keyboard.current?.sKey.wasPressedThisFrame ?? false)
            };

            newState.HotkeyThisFrame = BuilderPresenter.INVALID;
            if (!Keyboard.current?.anyKey.wasPressedThisFrame ?? true)
            {
                // no key was pressed
            }
            else if (Keyboard.current?[Key.Digit1].wasPressedThisFrame ?? false)
            {
                newState.HotkeyThisFrame = 1;
            }
            else if (Keyboard.current?[Key.Digit2].wasPressedThisFrame ?? false)
            {
                newState.HotkeyThisFrame = 2;
            }
            else if (Keyboard.current?[Key.Digit3].wasPressedThisFrame ?? false)
            {
                newState.HotkeyThisFrame = 3;
            }
            else if (Keyboard.current?[Key.Digit4].wasPressedThisFrame ?? false)
            {
                newState.HotkeyThisFrame = 4;
            }
            else if (Keyboard.current?[Key.Digit5].wasPressedThisFrame ?? false)
            {
                newState.HotkeyThisFrame = 5;
            }
            else if (Keyboard.current?[Key.Digit6].wasPressedThisFrame ?? false)
            {
                newState.HotkeyThisFrame = 6;
            }
            else if (Keyboard.current?[Key.Digit7].wasPressedThisFrame ?? false)
            {
                newState.HotkeyThisFrame = 7;
            }
            else if (Keyboard.current?[Key.Digit8].wasPressedThisFrame ?? false)
            {
                newState.HotkeyThisFrame = 8;
            }
            else if (Keyboard.current?[Key.Digit9].wasPressedThisFrame ?? false)
            {
                newState.HotkeyThisFrame = 9;
            }
        }

        /// <summary>
        /// Determines the current control mode (mouse, keyboard, gamepad). Updates <see cref="_lastUpdatedDevice"/> and <see cref="_controlMode"/> accordingly.
        /// </summary>
        private void UpdateInputDevice()
        {
            double mostRecentInput = 0;
            InputDevice nextDevice = default;
            foreach (var inputDevice in InputSystem.devices)
            {
                if (mostRecentInput < inputDevice.lastUpdateTime)
                {
                    mostRecentInput = inputDevice.lastUpdateTime;
                    nextDevice = inputDevice;
                }
            }

            if (nextDevice == null)
                return;

            _lastUpdatedDevice = nextDevice;
            _controlMode = _lastUpdatedDevice switch
            {
                Keyboard => ControlMode.MouseAndKeyboard,
                Mouse => ControlMode.MouseAndKeyboard,
                Gamepad => ControlMode.Gamepad,
                _ => ControlMode.Undefined
            };

            if (_controlMode == ControlMode.Undefined)
            {
                Debug.Log(
                    $"[{nameof(Builder)}] Unknown device '{_lastUpdatedDevice?.description.deviceClass ?? "NULL"}'");
            }
        }
    }
}