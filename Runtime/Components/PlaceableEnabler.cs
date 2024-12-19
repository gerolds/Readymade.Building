using System.Collections.Generic;
using Readymade.Building.Components;
using Readymade.Building;
using UnityEngine;

namespace Readymade.Build
{
    [RequireComponent(typeof(Placeable))]
    public class PlaceableEnabler : MonoBehaviour, IPlaceablePlaced, IPlaceableStarted
    {
        [SerializeField]
        [Tooltip("Objects that will be deactivated when placement is started.")]
        private List<GameObject> disableWhilePlacing;

        [SerializeField]
        [Tooltip("Objects that will be activated when placement is finished.")]
        private List<GameObject> enableWhenPlaced;

        /// <param name="wasStartedByBuilderCallback"></param>
        /// <inheritdoc />
        public void OnPlaceableFinished(bool wasStartedByBuilderCallback)
        {
            disableWhilePlacing.ForEach(
                it =>
                {
                    if (it)
                    {
                        it.gameObject.SetActive(false);
                    }
                }
            );
        }

        /// <param name="wasStartedByBuilderCallback"></param>
        /// <inheritdoc />
        public void OnPlaceableStarted(bool wasStartedByBuilderCallback)
        {
            enableWhenPlaced.ForEach(
                it =>
                {
                    if (it)
                    {
                        it.gameObject.SetActive(true);
                    }
                }
            );
        }
    }
}