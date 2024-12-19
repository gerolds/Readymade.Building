/* MIT License
 * Copyright 2023 Gerold Schneider
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the “Software”), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using System.Collections.Generic;
using System.Linq;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using UnityEngine;

namespace Readymade.Building.Components {
    [CreateAssetMenu (
        menuName = nameof ( Readymade ) + "/" + nameof ( Building ) + "/" + nameof ( SoPlaceableCollection ),
        fileName = "New " + nameof ( SoPlaceableCollection )
    )]
    public class SoPlaceableCollection : ScriptableObject {
        [SerializeField]
        [Tooltip ( "A descriptive name for this collection." )]
        private string displayName;

        [TextArea ( 2, 10 )]
        [SerializeField]
        [Tooltip ( "The tooltip to display for this collection." )]
        private string tooltip;

        [SerializeField]
        [Tooltip ( "The icon to use for this collection." )]
        private Sprite icon;

        [SerializeField]
        [Tooltip ( "The placeables that are part of this collection." )]
        [ValidateInput ( nameof ( ValidatePlaceables ), "Some items appear to be invalid." )]
        [InfoBox ( "Note that the order of the items in the list is the order in which they will be presented to the user. No duplicates or null-entries are allowed." )]
        #if ODIN_INSPECTOR
        [ListDrawerSettings(ShowPaging = false, ShowFoldout = false)]
#else
        [ReorderableList]
#endif
        private List<Placeable> placeables;

        /// <summary>
        /// The icon to use for this collection.
        /// </summary>
        public Sprite Icon => icon;

        /// <summary>
        /// The display name for this collection.
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// A helper function for the <see cref="ValidateInputAttribute"/> that validates the <paramref name="items"/> list.
        /// </summary>
        /// <param name="items">The items to validate.</param>
        /// <returns>True if the <paramref name="items"/> list is valid, false otherwise.</returns>
        private bool ValidatePlaceables ( List<Placeable> items ) => items.All ( it => it != null );

        /// <summary>
        /// The placeables that are part of this collection.
        /// </summary>
        public List<Placeable> Placeables => placeables;

        /// <summary>
        /// The tooltip to display for this collection.
        /// </summary>
        public string Tooltip => tooltip;

        /// <summary>
        /// Checks if this collection contains the given <paramref name="prefab"/>.
        /// </summary>
        /// <param name="prefab">The prefab to check for.</param>
        /// <returns>Whether this collection contains the given <paramref name="prefab"/>.</returns>
        // TODO: This is not very efficient, but that might not matter.
        public bool Contains ( Placeable prefab ) => placeables.Contains ( prefab );
    }
}