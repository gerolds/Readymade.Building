namespace Readymade.Building.Components {
    /// <summary>
    /// Contains callbacks invoked by <see cref="IPlaceable"/> implementations.
    /// </summary>
    public interface IPlaceableUpdated {
        /// <summary>
        /// Called by an <see cref="IPlaceable"/> instance on all its components that implement it when it is being updated.
        /// </summary>
        void OnPlaceableUpdated ();
    }
}