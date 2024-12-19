using UnityEngine;

namespace Readymade.Building.Components {
    /// <summary>
    /// Abstract base class for implementing and referencing deletion validators. Yet unused.
    /// </summary>
    public abstract class SoDeleteValidator : ScriptableObject {
        /// <summary>
        /// Executes the validation.
        /// </summary>
        /// <param name="placeable">The target placeable to validate.</param>
        /// <returns>Whether the validation succeeded.</returns>
        public abstract bool Validate ( Placeable placeable );
    }
}