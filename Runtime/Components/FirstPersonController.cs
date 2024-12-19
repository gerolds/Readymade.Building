using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Readymade.Building.Components {
    [Obsolete ( "Use a proper implementation of a character controller. Recommended: KinematicCharacterController." )]
    [RequireComponent ( typeof ( CharacterController ) )]
    public class FirstPersonController : MonoBehaviour {
        [FormerlySerializedAs ( "mouseSensitivity" )]
        [SerializeField]
        private float lookSensitivity = 100f;

        [SerializeField]
        private float movementSpeed = 5f;

        [SerializeField]
        private Transform playerCamera = null;

        [SerializeField]
        private InputActionReference moveAction;

        [SerializeField]
        private InputActionReference lookAction;

        private CharacterController _characterController;
        private float _xRotation = 0f;

        void Start () {
            _characterController = GetComponent<CharacterController> ();
        }

        void Update () {
            Look ();
            Move ();
        }

        private void Look () {
            Vector2 lookInput = GetLookInput ();

            _xRotation -= lookInput.y;
            _xRotation = Mathf.Clamp ( _xRotation, -90f, 90f );

            playerCamera.localRotation = Quaternion.Euler ( _xRotation, 0, 0 );
            transform.Rotate ( Vector3.up * lookInput.x, Space.World );
        }

        private void Move () {
            Vector3 movementInput = GetMovementInput ();
            Vector3 move = transform.right * movementInput.x + transform.forward * movementInput.z;

            _characterController.Move ( move * ( movementSpeed * Time.deltaTime ) );
        }

        private Vector2 GetLookInput () {
            Vector2 v = lookAction.action.ReadValue<Vector2> ();
            v *= lookSensitivity * Time.deltaTime;
            return v;
        }

        private Vector3 GetMovementInput () {
            Vector2 v = moveAction.action.ReadValue<Vector2> ();
            return new Vector3 ( v.x, 0, v.y );
        }
    }
}