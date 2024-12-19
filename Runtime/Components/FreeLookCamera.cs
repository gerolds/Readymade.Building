using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Readymade.Building.Components {
    [Obsolete]
    public class FreeLookCamera : MonoBehaviour {
        private Vector2 _mouseAbsolute;
        private Vector2 _smoothLook;

        [SerializeField]
        private Vector2 clampInDegrees = new Vector2 ( 360, 180 );

        [SerializeField]
        private Vector2 sensitivity = new ( 2, 2 );

        [SerializeField]
        private Vector2 smoothing = new ( 3, 3 );

        [SerializeField]
        private bool requireLockedCursor = true;

        private Vector2 _targetDirection;

        [SerializeField]
        private InputActionReference lookAction;


        private void Start () {
            _targetDirection = transform.rotation.eulerAngles;
        }

        private void LateUpdate () {
            if ( requireLockedCursor && Cursor.lockState != CursorLockMode.Locked ) {
                return;
            }

            Quaternion targetOrientation = Quaternion.Euler ( _targetDirection );
            Vector2 lookInput = lookAction.action.ReadValue<Vector2> ();

            lookInput = Vector2.Scale ( lookInput, new Vector2 ( sensitivity.x * smoothing.x, sensitivity.y * smoothing.y ) );

            _smoothLook.x = Mathf.Lerp ( _smoothLook.x, lookInput.x, 1f / smoothing.x );
            _smoothLook.y = Mathf.Lerp ( _smoothLook.y, lookInput.y, 1f / smoothing.y );

            // Find the absolute mouse movement value from point zero.
            _mouseAbsolute += _smoothLook;

            // Clamp and apply the local x value first, so as not to be affected by world transforms.
            if ( clampInDegrees.x < 360f )
                _mouseAbsolute.x = Mathf.Clamp ( _mouseAbsolute.x, -clampInDegrees.x * 0.5f, clampInDegrees.x * 0.5f );

            Quaternion xRotation = Quaternion.AngleAxis ( -_mouseAbsolute.y, targetOrientation * Vector3.right );
            transform.localRotation = xRotation;

            if ( clampInDegrees.y < 360f ) {
                _mouseAbsolute.y = Mathf.Clamp ( _mouseAbsolute.y, -clampInDegrees.y * 0.5f, clampInDegrees.y * 0.5f );
            }

            Quaternion yRotation = Quaternion.AngleAxis ( _mouseAbsolute.x, transform.InverseTransformDirection ( Vector3.up ) );
            transform.localRotation *= yRotation;
            transform.rotation *= targetOrientation;
        }
    }
}