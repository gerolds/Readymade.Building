using NaughtyAttributes;
using Readymade.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Readymade.Building.Components
{
    /// <summary>
    /// A display holding references to components for displaying the builder toolbar.
    /// </summary>
    public class ToolbarDisplay : MonoBehaviour
    {
        [Tooltip("The " + nameof(CanvasToggle) +
                 " that captures input events and can be used to control visibility and interactivity of the toolbar as a whole.")]
        [SerializeField]
        [Required]
        private CanvasToggle toolMenuToggle;

        [Tooltip("The layout group that holds the tool buttons.")]
        [SerializeField]
        [Required]
        private GridLayoutGroup toolsLayout;

        [Tooltip("The layout group that holds the collection filter toggles.")]
        [SerializeField]
        [Required]
        private GridLayoutGroup collectionsLayout;

        [Tooltip("The layout group that holds the group filter toggles.")]
        [SerializeField]
        [Required]
        private GridLayoutGroup groupsLayout;

        [Tooltip("The infobox display to use for the shared info.")]
        [SerializeField]
        [Required]
        private ToolInfoboxDisplay sharedInfobox;

        /// <summary>
        /// The layout group that holds the tool buttons.
        /// </summary>
        public GridLayoutGroup ToolsLayout => toolsLayout;

        /// <summary>
        /// The layout group that holds the collection filter toggles.
        /// </summary>
        public GridLayoutGroup CollectionsLayout => collectionsLayout;

        /// <summary>
        /// The layout group that holds the group filter toggles.
        /// </summary>
        public GridLayoutGroup GroupsLayout => groupsLayout;

        /// <summary>
        /// The <see cref="CanvasToggle"/> that captures input events and can be used to control visibility and interactivity of the toolbar as a whole.
        /// </summary>
        public CanvasToggle ToolMenuToggle => toolMenuToggle;

        /// <summary>
        /// The infobox display to use for the shared info.
        /// </summary>
        public ToolInfoboxDisplay SharedInfobox => sharedInfobox;
    }
}