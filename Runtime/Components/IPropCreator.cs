using Readymade.Machinery.Acting;

namespace Readymade.Building.Components
{
    /// <summary>
    /// Represents the facility to create quantities of props to be stored in an inventory or account.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IPropCreator<T> where T : IProp
    {
        /// <summary>
        /// The prop which can be created. 
        /// </summary>
        public T Prop { get; }

        /// <summary>
        /// Creates a number of props.
        /// </summary>
        /// <param name="quantity">The number of props to create.</param>
        /// <remarks>Note that the implementation of this interface should not actually produce instances of objects but
        /// instead simply increment a count on a virtual representation of identical objects.</remarks>
        public void Create(int quantity);
    }
}