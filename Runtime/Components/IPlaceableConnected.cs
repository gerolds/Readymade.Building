namespace Readymade.Building.Components {
    /// <summary>
    /// Contains callbacks invoked by <see cref="IPlaceable"/> implementations.
    /// </summary>
    public interface IPlaceableConnected {
        /// <summary>
        /// Called by an <see cref="IPlaceable"/> instance on all its components that implement it when placeable is disconnected.
        /// </summary>
        void OnPlaceableConnected ( bool isConnected );
    }
}