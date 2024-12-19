using Readymade.Machinery.Acting;
using Readymade.Utils.Patterns;
using NaughtyAttributes;
using UnityEngine;

namespace Readymade.Building.Components {
    /// <summary>
    /// A component that can be used to create <see cref="IProp"/> counts in a <see cref="ResourceSystem"/>.
    /// Depends on a <see cref="ResourceSystem"/> instance existing in the scene and being registered in the
    /// <see cref="ServiceLocator"/>.
    /// </summary>
    public class ResourceCreator : MonoBehaviour, IPropCreator<IProp> {
        [InfoBox ( "For this component to work a " + nameof ( ResourceSystem ) + " must be registered in " +
                   nameof ( Services ) + " and exist in the scene." )]
        [Tooltip ( "The prop to create when calling " + nameof ( Create ) )]
        [SerializeField]
        private SoProp prop;

        /// <inheritdoc />
        public IProp Prop => prop;

        /// <inheritdoc />
        [Button ( "Create One", EButtonEnableMode.Playmode )]
        public void Create ( int quantity = 1 ) {
            ResourceSystem resourceSystem = Services.Get<ResourceSystem> ();
            if ( resourceSystem.Inventory.CanPut ( prop, quantity ) ) {
                resourceSystem.Inventory.TryPut ( prop, quantity );
            }
        }
    }
}