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

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Readymade.Machinery.Acting;
using Readymade.Machinery.Shared;
using Readymade.Persistence;
using Readymade.Persistence.Pack.Components;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif

using UnityEngine;
using UnityEngine.Serialization;

namespace Readymade.Building.Components
{
    /// <summary>
    /// A <see cref="MonoBehaviour"/> version of a <see cref="InventoryPropProvider"/>.
    /// </summary>
    [SelectionBase]
    public class ResourceSystem : PackableComponent<ResourceSystem.Memento>, IProvider<SoProp>
    {
        [BoxGroup("Provider")]
        [InfoBox("Use this to make an inventory of resources globally available to actors in the game.")]
        [Tooltip("Whether to log debug messages.")]
        [SerializeField]
        private bool debug;

        [FormerlySerializedAs("_providedProps")]
        [BoxGroup("Provider")]
        [Tooltip("The props provided by this component.")]
        #if ODIN_INSPECTOR
        [ListDrawerSettings(ShowPaging = false, ShowFoldout = false)]
#else
        [ReorderableList]
#endif
        [SerializeField]
        private PropCount[] providedProps;

        [FormerlySerializedAs("_onClaimed")]
        [BoxGroup("Provider")]
        [Tooltip("Invoked whenever a prop is claimed.")]
        [SerializeField]
        private ProviderUnityEvent onClaimed;

        [FormerlySerializedAs("_onClaimCommitted")]
        [BoxGroup("Provider")]
        [Tooltip("Invoked whenever a prop claim is committed.")]
        [SerializeField]
        private ProviderUnityEvent onClaimCommitted;

        [FormerlySerializedAs("_onClaimCancelled")]
        [BoxGroup("Provider")]
        [Tooltip("Invoked whenever a prop claim is cancelled.")]
        [SerializeField]
        private ProviderUnityEvent onClaimCancelled;

        [FormerlySerializedAs("_propProviderMask")]
        [SerializeField]
        [BoxGroup("Broker")]
        [InfoBox(
            "Use this to make this system also act as a broker for any PropProvider found in the scene on the given layers.")]
        private LayerMask propProviderMask;

        /// <summary>
        /// Gets the count of a given <see cref="SoProp"/> stored in the backing inventory.
        /// </summary>
        /// <param name="prop"></param>
        /// <returns></returns>
        public long GetAvailableCount(SoProp prop) => _inventoryProvider?.Inventory?.GetAvailableCount(prop) ?? 0;

        /// <summary>
        /// Called whenever a backing inventory item changes. This usually happens on claim operation but will also fire
        /// when the inventory is overriden without a claim.
        /// </summary>
        public event IInventory<SoProp>.InventoryEventHandler Changed;
        public event Action<Phase, (SoProp prop, long count, IActor claimant)> Modified;

        /// <inheritdoc />
        public Pose Pose => default;

        /// <inheritdoc />
        public bool HasPose => false;

        /// <inheritdoc />
        public bool DebugLog
        {
            get => debug;
            set => debug = value;
        }

        /// <summary>
        /// Whether this provider is finite. Always true on a <see cref="ResourceSystem"/>.
        /// </summary>
        public bool IsFinite => true;

        /// <summary>
        /// The props provided by this <see cref="IProvider{TProp}"/>.
        /// </summary>

        public IEnumerable<SoProp> ProvidedProps => providedProps.Select(it => it.Identity);

        /// <summary>
        /// The <see cref="IInventory{TItem}"/> backing the resource system's provider.
        /// </summary>
        public IInventory<SoProp> Inventory => _inventory;

        private readonly Inventory _inventory = new();
        private InventoryPropProvider _inventoryProvider;
        private float _timeout;

        private bool _isInit;

        /// <summary>
        /// Event function.
        /// </summary>
        private void Awake()
        {
            if (!_isInit)
            {
                EnsureInit();
            }
        }

        /// <summary>
        /// Initialize this <see cref="ResourceSystem"/>. Called automatically in Awake(), but can be called earlier on-demand.
        /// </summary>
        public void EnsureInit()
        {
            _isInit = true;
            Broker = new BasePropBroker(propProviderMask)
            {
                DebugLog = debug
            };
            IEnumerable<IProvider<SoProp>> discoveredProviders =
                FindObjectsByType<Component>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .OfType<IProvider<SoProp>>();

            foreach (IProvider<SoProp> provider in discoveredProviders)
            {
                Broker.AddProvider(provider);
            }

            if (providedProps.Any())
            {
                _inventoryProvider = new InventoryPropProvider((Pose)default, _inventory);
                _inventoryProvider.Modified += Modified;
                _inventoryProvider.Modified += UpdatedEventHandler;
                _inventoryProvider.Inventory.Modified += InventoryChangedEventHandler;
                ResetInventory();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public IPropBroker<SoProp> Broker { get; private set; }

        /// <summary>
        /// Resets the inventory to the statically configured counts.
        /// </summary>
        private void ResetInventory()
        {
            foreach (PropCount entry in providedProps)
            {
                if (entry.Identity != null)
                {
                    _inventoryProvider.Inventory.ForceSet(entry.Identity, entry.Count);
                }
            }
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnDestroy()
        {
            Dispose();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_inventoryProvider != null)
            {
                _inventoryProvider.Modified -= Modified;
                _inventoryProvider.Modified -= UpdatedEventHandler;
                _inventoryProvider.Dispose();
            }
        }

        /// <summary>
        /// Called when a prop was claimed.
        /// </summary>
        /// <param name="phase"></param>
        /// <param name="args"></param>
        private void UpdatedEventHandler(Phase phase,
            (SoProp prop, long quantity, IActor claimant) args)
        {
            switch (phase)
            {
                case Phase.Claimed:
                    onClaimed.Invoke(new ProviderEventArgs
                    {
                        Prop = args.prop as SoProp,
                        Quantity = args.quantity,
                        Claimant = args.claimant as Component
                    });
                    break;
                case Phase.Committed:
                    onClaimCommitted.Invoke(new ProviderEventArgs
                    {
                        Prop = args.prop as SoProp,
                        Quantity = args.quantity,
                        Claimant = args.claimant as Component
                    });
                    break;
                case Phase.Released:
                    onClaimCancelled.Invoke(new ProviderEventArgs
                    {
                        Prop = args.prop as SoProp,
                        Quantity = args.quantity,
                        Claimant = args.claimant as Component
                    });
                    break;
                case Phase.Put:
                case Phase.Set:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
            }
        }



        /// <summary>
        /// Called when the backing inventory has changed.
        /// </summary>
        private void InventoryChangedEventHandler(Phase message,
            IInventory<SoProp>.InventoryEventArgs args)
        {
            Changed?.Invoke(message, args);
        }

        /// <inheritdoc />
        public bool TryClaimProp(
            [NotNull] SoProp prop,
            [NotNull] IActor actor,
            long quantity,
            out PropClaim<SoProp, IActor> claim
        )
        {
            if (quantity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), "Must be at least 1");
            }

            bool success = _inventoryProvider.TryClaimProp(prop, actor, quantity, out claim);

            if (success)
            {
                Debug.Log(
                    $"[{GetType().GetNiceName()}] {name} responded to prop {prop.Name} claim by {actor.Name} with pose {claim.CommitPose.position}");
            }
            else
            {
                Debug.LogWarning(
                    $"[{GetType().GetNiceName()}] {name} failed to provide prop {prop.Name} claimed by {actor.Name}.");
            }

            return success;
        }

        /// <inheritdoc />
        public bool CanProvide(SoProp prop) => _inventoryProvider.CanProvide(prop);

        /// <inheritdoc />
        protected override void OnUnpack(Memento package, AssetLookup lookup)
        {
            ResetInventory();
            for (int i = 0; i < package.ID.Length; i++)
            {
                PropCount propCount = providedProps.FirstOrDefault(it => it.Identity.ID == package.ID[i]);
                if (propCount.Identity)
                {
                    _inventoryProvider.Inventory.ForceSet(propCount.Identity, package.Count[i]);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(ResourceSystem)}] Prop {package.ID[i]} not found.", this);
                }
            }
        }

        /// <inheritdoc />
        protected override Memento OnPack()
        {
            return new Memento
            {
                ID = _inventory.Unclaimed.Select(it => it.Prop.ID).ToArray(),
                Count = _inventory.Unclaimed.Select(it => it.Count).ToArray(),
            };
        }

        /// <summary>
        /// Captures the state of a <see cref="ResourceSystem"/>.
        /// </summary>
        [Serializable]
        public struct Memento
        {
            public Guid[] ID;
            public long[] Count;
        }
    }
}