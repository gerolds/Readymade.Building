using TMPro;
using UnityEngine;

namespace Readymade.Building.Components {
    /// <summary>
    /// A display that holds components for displaying a simple label text.
    /// </summary>
    public class LabelDisplay : MonoBehaviour {
        [Tooltip ( "The text to display the label." )]
        [SerializeField]
        private TMP_Text label;

        /// <summary>
        /// The text that displays the label.
        /// </summary>
        public TMP_Text Label => label;
    }
}