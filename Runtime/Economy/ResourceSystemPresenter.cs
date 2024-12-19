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
using Readymade.Machinery.Acting;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace Readymade.Building.Components
{
    /// <summary>
    /// Makes information about a <see cref="ResourceSystem"/> visible and interactive to the user.
    /// </summary>
    public class ResourceSystemPresenter : MonoBehaviour
    {
        [Tooltip("The resource system to present.")]
        [SerializeField]
        [Required]
        private ResourceSystem resourceSystem;

        [Tooltip("The prefab to use for displaying a prop and its count.")]
        [SerializeField]
        [Required]
        private PropCountDisplay propPrefab;

        [Tooltip("The container to parent the prop displays to.")]
        [SerializeField]
        [Required]
        private LayoutGroup propContainer;

        private readonly HashSet<PropCountDisplay> _displays = new();

        /// <summary>
        /// Event function.
        /// </summary>
        private void Awake()
        {
            resourceSystem.Changed += ChangedHandler;
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void Start()
        {
            PropCountDisplay[] displays = propContainer.GetComponentsInChildren<PropCountDisplay>();
            foreach (PropCountDisplay display in displays)
            {
                display.Label.SetText(display.Prop?.DisplayName ?? "-");
                display.Count.SetText("-");
            }

            RefreshDisplays();
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnEnable()
        {
            RefreshDisplays();
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnDisable()
        {
            ClearDisplays();
        }

        /// <summary>
        /// Clears all displays (does not destroy them).
        /// </summary>
        private void ClearDisplays()
        {
            foreach (PropCountDisplay display in _displays)
            {
                display.Label.SetText("-");
                display.Count.SetText("-");
            }
        }

        /// <summary>
        /// Refreshes the displayed values in all prop displays.
        /// </summary>
        private void RefreshDisplays()
        {
            foreach (SoProp prop in resourceSystem.ProvidedProps)
            {
                ChangedHandler(
                    Phase.Set,
                    new IInventory<SoProp>.InventoryEventArgs(
                        inventory: resourceSystem.Inventory,
                        item: prop,
                        delta: 0,
                        claimed: resourceSystem.Inventory.GetClaimedCount(prop),
                        available: resourceSystem.Inventory.GetAvailableCount(prop)
                    )
                );
            }
        }

        /// <summary>
        /// Called whenever the state of the <see cref="ResourceSystem"/> backing inventory changes.
        /// </summary>
        private void ChangedHandler(Phase message,
            IInventory<SoProp>.InventoryEventArgs args)
        {
            PropCountDisplay[] displays = propContainer.GetComponentsInChildren<PropCountDisplay>();
            PropCountDisplay display = displays.FirstOrDefault(it => ReferenceEquals(it.Prop, args.Identity));

            if (display == default)
            {
                display = CreateDisplay(args.Identity);
            }

            display.Count.SetText("{0}", args.Available);

            return; // local functions only from here on. 

            PropCountDisplay CreateDisplay(SoProp prop)
            {
                PropCountDisplay instance = Instantiate(propPrefab, propContainer.transform);
                instance.prop = prop;
                instance.Label.text = prop.DisplayName;
                instance.Icon.symbol = prop.IconSymbol;
                _displays.Add(instance);
                return instance;
            }
        }
    }
}