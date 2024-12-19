using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;

namespace Readymade.Building.Components {
    [CreateAssetMenu (
        menuName = nameof ( Readymade ) + "/" + nameof ( Readymade.Building ) + "/" + nameof ( SoPlaceableGroup ),
        fileName = "New " + nameof ( SoPlaceableGroup )
    )]
    public class SoPlaceableGroup : ScriptableObject {
        [Tooltip ( "A descriptive name for this group." )]
        [SerializeField]
        private string displayName;

        [Tooltip ( "The tooltip to display for this group." )]
        [TextArea ( 2, 10 )]
        [SerializeField]
        private string tooltip;

        [Tooltip ( "The icon to use for this group." )]
        [SerializeField]
        private Sprite icon;
        
        [SerializeField]
        [ValidateInput ( nameof ( ValidateCollections ), "Some collections appear to be invalid." )]
        [InfoBox (
            "Note that the order of the items in the list is the order in which they will be presented to the user. No duplicates or null-entries are allowed." )]
        private List<SoPlaceableCollection> collections;

        /// <summary>
        /// The icon to use for this group.
        /// </summary>
        public Sprite Icon => icon;

        /// <summary>
        /// A descriptive name for this group.
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// A helper function for the <see cref="ValidateInputAttribute"/> that validates the <paramref name="items"/> list.
        /// </summary>
        /// <param name="items">The items to validate.</param>
        /// <returns>True if the <paramref name="items"/> list is valid, false otherwise.</returns>
        private bool ValidateCollections ( List<SoPlaceableCollection> items ) {
            return items.All ( it => it != null && it.Placeables.Any () && it.Placeables.All ( jt => jt != null ) );
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnValidate () {
            collections = collections.Distinct ().ToList ();
        }

        /// <summary>
        /// The collections that are part of this group.
        /// </summary>
        public List<SoPlaceableCollection> Collections => collections;

        /// <summary>
        /// The tooltip to display for this group.
        /// </summary>
        public string Tooltip => tooltip;
    }
}