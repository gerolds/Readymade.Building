namespace Readymade.Building.Components {
    /// <summary>
    /// Contains callbacks invoked by <see cref="IPlaceable"/> implementations.
    /// </summary>
    public interface IPlaceableStarted {
        /// <summary>
        /// Called by an <see cref="IPlaceable"/> instance on all its components that implement it when placement is started.
        /// </summary>
        /// <param name="fromBuilder">Whether the event was triggered by manual placement via <see cref="Builder"/>.</param>
        void OnPlaceableStarted(bool fromBuilder);
    }
}