using UnityEngine;
using UnityEngine.UI;

namespace Readymade.Building.Components {
    /// <summary>
    /// UI container for displaying different kind of cursors.
    /// </summary>
    [RequireComponent ( typeof ( RectTransform ) )]
    public class UICursor : MonoBehaviour {
        /// <summary>
        /// Reference to the progress image to access the fill amount.
        /// </summary>
        [field: SerializeField, Header ( "Components" )]
        public Image ProgressImage { get; private set; }

        /// <summary>
        /// Reference to the progress object to set if isActive.
        /// </summary>
        [field: SerializeField]
        public RectTransform ProgressTransform { get; private set; }

        /// <summary>
        /// Reference to the hover object to set if isActive.
        /// </summary>
        [field: SerializeField]
        public RectTransform HoverTransform { get; private set; }

        /// <summary>
        /// Reference to the crosshair object to set if isActive.
        /// </summary>
        [field: SerializeField]
        public RectTransform CrosshairTransform { get; private set; }


        /// <summary>
        /// Unity Event.
        /// </summary>
        private void OnEnable () {
            Init ();
        }

        /// <summary>
        /// Initialises the UI to default.
        /// </summary>
        public void Init () {
            SetHover ( false );
            SetProgress ( 1.0f );
        }

        /// <summary>
        /// Toogles the display of the cursor objects.
        /// </summary>
        /// <param name="isOn"></param> Wether to displayed or not.
        public void ToggleCursor ( bool isOn ) {
            HoverTransform.gameObject.SetActive ( isOn );
            CrosshairTransform.gameObject.SetActive ( isOn );
            ProgressTransform.gameObject.SetActive ( isOn );
        }

        /// <summary>
        /// Sets the hover state of the crosshair.
        /// </summary>
        /// <param name="isHovering"></param> Wether to display the hover effect or not.
        public void SetHover ( bool isHovering ) {
            HoverTransform.gameObject.SetActive ( isHovering );
            CrosshairTransform.gameObject.SetActive ( !isHovering );
            ProgressTransform.gameObject.SetActive ( false );
        }

        /// <summary>
        /// Sets the value of the progressbar.
        /// Disables the bar when progress is >= 1.0f.
        /// </summary>
        /// <param name="progress"></param> The amount between 0.0f and 1.0f.
        public void SetProgress ( float progress ) {
            if ( progress < 1.0f ) {
                ProgressTransform.gameObject.SetActive ( true );
                ProgressImage.fillAmount = progress;
            } else {
                ProgressTransform.gameObject.SetActive ( false );
            }
        }
    }
}