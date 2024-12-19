using Readymade.Machinery.Acting;
using Readymade.Machinery.Shared;
using UnityEngine;

namespace Readymade.Building.Components {
    /// <summary>
    /// A stub class that represents the player to the acting system (i.e. lets the player act inside the same systems as the other actors (NPCs)).
    /// </summary>
    public class PlayerActor : Actor {
        /// <summary>
        /// The player's display name.
        /// </summary>
        [SerializeField]
        private string displayName = "The Player";

        /// <inheritdoc />
        public override string Name => displayName;

        public override void OnFx(ActorFx fx)
        {
        }

        public override Animator Animator => null;
    }
}