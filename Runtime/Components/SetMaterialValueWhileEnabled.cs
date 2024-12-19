using NaughtyAttributes;
using UnityEngine;

namespace Readymade.Building.Components {
    /// <summary>
    /// Helper and prototyping component that allows changing of material properties depending on the active state of the GameObject.
    /// </summary>
    public class SetMaterialValueWhileEnabled : MonoBehaviour {
        [SerializeField]
        [Tooltip ( "Whether to use the MonoBehaviour's OnEnable and OnDisable events to set the material value." )]
        private bool useBehaviourEvents = true;

        [Tooltip ( "The name of the property to set. This must be a float value property." )]
        [SerializeField]
        private string valueProperty = "_Blend";

        [Tooltip ( "The materials to set the property on." )]
        [SerializeField]
        private Material[] materials;

        [Tooltip ( "The value to set when the GameObject is disabled or when " + nameof ( SetOn ) + " is called." )]
        [SerializeField]
        private float valueOn = .5f;

        [Tooltip ( "The value to set when the GameObject is disabled or when " + nameof ( SetOff ) + " is called." )]
        [SerializeField]
        private float valueOff = 0f;

        private int _propertyID;

        /// <summary>
        /// Event function.
        /// </summary>
        private void Awake () {
            _propertyID = Shader.PropertyToID ( valueProperty );
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnEnable () {
            if ( useBehaviourEvents ) {
                SetOn ();
            }
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnDisable () {
            if ( useBehaviourEvents ) {
                SetOff ();
            }
        }

        /// <summary>
        /// Applies the On-value to the material property. Called automatically on <see cref="OnEnable"/> if <see cref="useBehaviourEvents"/> is true.
        /// </summary>
        [Button ( "Test ON" )]
        public void SetOn () {
            foreach ( Material material in materials ) {
                if ( material.HasFloat ( _propertyID ) ) {
                    // use cached ID if possible.
                    material.SetFloat ( _propertyID, valueOn );
                } else if ( material.HasFloat ( valueProperty ) ) {
                    // in case the property name was changed at runtime.
                    material.SetFloat ( valueProperty, valueOn );
                } else {
                    // property does not exist or is not supported.
                }
            }
        }

        /// <summary>
        /// Applies the Off-value to the material property. Called automatically on <see cref="OnDisable"/> if <see cref="useBehaviourEvents"/> is true.
        /// </summary>
        [Button ( "Test OFF" )]
        public void SetOff () {
            foreach ( Material material in materials ) {
                if ( material.HasFloat ( _propertyID ) ) {
                    // use cached ID if possible.
                    material.SetFloat ( _propertyID, valueOff );
                } else if ( material.HasFloat ( valueProperty ) ) {
                    // in case the property name was changed at runtime.
                    material.SetFloat ( valueProperty, valueOff );
                } else {
                    // property does not exist or is not supported.
                }
            }
        }

        /// <summary>
        /// Applies the corresponding On/Off-value to the material property.
        /// </summary>
        public void Set ( bool isOn ) {
            if ( isOn ) {
                SetOn ();
            } else {
                SetOff ();
            }
        }
    }
}