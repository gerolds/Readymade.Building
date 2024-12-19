using com.convalise.UnityMaterialSymbols;
using Readymade.Machinery.Acting;
using NaughtyAttributes;
using Readymade.Machinery.Progression;
using Readymade.Utils.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Readymade.Building.Components
{
    /// <summary>
    /// A display that holds components for displaying a <see cref="SoProp"/> and its count. For example to provide a view
    /// into an inventory or to display a change event.
    /// </summary>
    public class PropCountDisplay : MonoBehaviour
    {
        [FormerlySerializedAs("_count")]
        [Tooltip("The text to display the count of the prop.")]
        [Required]
        [SerializeField]
        public TMP_Text count;

        [FormerlySerializedAs("_label")]
        [Tooltip("The text to display the label of the prop.")]
        [Required]
        [SerializeField]
        public TMP_Text label;

        [FormerlySerializedAs("_annotation")]
        [Tooltip("The text to display the annotation of the prop.")]
        [Required]
        [SerializeField]
        public TMP_Text annotation;

        [FormerlySerializedAs("_icon")]
        [Tooltip("The text to display the icon of the prop.")]
        [FormerlySerializedAs("annotation")]
        [Required]
        [SerializeField]
        public MaterialSymbol icon;

        [FormerlySerializedAs("_prop")]
        [Tooltip("The prop for which the information is displayed.")]
        [SerializeField]
        public SoProp prop;

        /// <summary>
        /// The text that displays the annotation.
        /// </summary>
        public TMP_Text Annotation => annotation;

        /// <summary>
        /// The text that displays the label or name of the prop.
        /// </summary>
        public TMP_Text Label => label;

        /// <summary>
        /// The text that displays the count.
        /// </summary>
        public TMP_Text Count => count;

        /// <summary>
        /// The text that displays the count.
        /// </summary>
        public MaterialSymbol Icon => icon;

        /// <summary>
        /// The prop for which the information is displayed. 
        /// </summary>
        public SoProp Prop => prop;

        /// <summary>
        ///  Event function.
        /// </summary>
        public void Awake()
        {
            count?.SetText(string.Empty);
            annotation?.SetText(string.Empty);
            label?.SetText(string.Empty);
            if (icon)
            {
                icon.symbol = new MaterialSymbolData('\ue421', false);
            }
        }
    }
}