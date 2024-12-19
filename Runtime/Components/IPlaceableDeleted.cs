namespace Readymade.Building.Components
{
    /// <summary>
    /// Contains callbacks invoked by <see cref="IPlaceable"/> implementations.
    /// </summary>
    public interface IPlaceableDeleted
    {
        /// <summary>
        /// Called by an <see cref="IPlaceable"/> instance on on all its components that implement it when it is being deleted.
        /// </summary>
        /// <param name="fromBuilder">Whether the event was triggered by manual placement via <see cref="Builder"/>.</param>
        void OnPlaceableDeleted(bool fromBuilder);
    }
}