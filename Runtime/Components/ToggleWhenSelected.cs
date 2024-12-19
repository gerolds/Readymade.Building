using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Readymade.Building.Components {
    /// <summary>
    /// Automates activating a <see cref="Toggle"/> when the object is selected.
    /// </summary>
    [RequireComponent ( typeof ( Toggle ) )]
    public class ToggleWhenSelected : MonoBehaviour, ISelectHandler {
        private Toggle _toggle;

        [Tooltip ( "A unity event to invoke when the object is selected." )]
        [SerializeField]
        private UnityEvent onSelected;

        /// <summary>
        /// Called whenever the object is selected.
        /// </summary>
        /// <param name="eventData">Event data.</param>
        public void OnSelect ( BaseEventData eventData ) {
            _toggle = GetComponent<Toggle> ();
            if ( _toggle ) {
                _toggle.isOn = true;
            }

            onSelected.Invoke ();
        }
    }
}