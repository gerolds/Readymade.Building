using UnityEngine;

namespace Readymade.Building.Components {
    /// <summary>
    /// Abstract base class for implementing and referencing snap validators. Yet unused.
    /// </summary>
    public abstract class SoSnapValidator : ScriptableObject {
        /// <summary>
        /// Executes the validation.
        /// </summary>
        /// <param name="target">The target magnet to validate.</param>
        /// <returns>Whether the validation succeeded.</returns>
        public abstract bool Validate ( Magnet target );
    }
}