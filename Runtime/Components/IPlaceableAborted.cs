namespace Readymade.Building.Components {
    /// <summary>
    /// Contains callbacks invoked by <see cref="IPlaceable"/> implementations.
    /// </summary>
    public interface IPlaceableAborted {
        /// <summary>
        /// Called by an <see cref="IPlaceable"/> instance on all its components that implement it when it is being aborted.
        /// </summary>
        void OnPlaceableAborted ();
    }
}