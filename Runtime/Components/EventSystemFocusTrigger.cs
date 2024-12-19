using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Readymade.Building.Components {
    /// <summary>
    /// Put this on a <see cref="GameObject"/> with a <see cref="Selectable"/> component do toggle a target gameObject while the
    /// object is selected or hovered over.
    /// </summary>
    [RequireComponent ( typeof ( Selectable ) )]
    public class EventSystemFocusTrigger :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler {
        /// <summary>
        /// Defines the mode of operation of this component.
        /// </summary>
        private enum Mode {
            /// <summary>
            /// Triggers will be invoked when the object is selected or hovered over.
            /// </summary>
            Both,

            /// <summary>
            /// Triggers will be invoked when the object is hovered over.
            /// </summary>
            OnHoverOnly,

            /// <summary>
            /// Triggers will be invoked when the object is selected.
            /// </summary>
            OnSelectOnly,
        }

        // ---
        [Tooltip ( "The target gameObject to activate/deactivate when the object is selected or hovered over. " )]
        [SerializeField]
        public GameObject controlTarget;

        [Tooltip ( "The event to invoke when the object is selected or hovered over. " )]
        [SerializeField]
        private UnityEvent onFocusGained;

        [Tooltip ( "The event to invoke when the object is deselected or the pointer leaves the object." )]
        [SerializeField]
        private UnityEvent onFocusLost;

        [SerializeField]
        private Mode mode = Mode.Both;

        /// <summary>
        /// Invoked when the object is selected or hovered over.
        /// </summary>
        public event Action FocusGained;

        /// <summary>
        /// Invoked when the object is deselected or the pointer leaves the object.
        /// </summary>
        public event Action FocusLost;


        /// <inheritdoc />
        public void OnPointerEnter ( PointerEventData eventData ) {
            if ( mode is Mode.Both or Mode.OnHoverOnly ) {
                OnFocusGained ();
            }
        }

        /// <inheritdoc />
        public void OnPointerExit ( PointerEventData eventData ) {
            if ( mode is Mode.Both or Mode.OnHoverOnly ) {
                OnFocusLost ();
            }
        }

        /// <inheritdoc />
        public void OnSelect ( BaseEventData eventData ) {
            if ( mode is Mode.Both or Mode.OnSelectOnly ) {
                OnFocusGained ();
            }
        }

        /// <inheritdoc />
        public void OnDeselect ( BaseEventData eventData ) {
            if ( mode is Mode.Both or Mode.OnSelectOnly ) {
                OnFocusLost ();
            }
        }

        /// <summary>
        /// Called when the <see cref="Selectable"/> is selected or hovered over or selected.
        /// </summary>
        private void OnFocusGained () {
            onFocusGained.Invoke ();
            FocusGained?.Invoke ();
            if ( controlTarget ) {
                controlTarget.SetActive ( true );
            }
        }

        /// <summary>
        /// Called when the <see cref="Selectable"/> is deselected or the pointer leaves the object.
        /// </summary>
        private void OnFocusLost () {
            onFocusLost.Invoke ();
            FocusLost?.Invoke ();
            if ( controlTarget ) {
                controlTarget.SetActive ( false );
            }
        }
    }
}