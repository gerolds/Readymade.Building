#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.VFX;

namespace Readymade.Building.Components
{
    /// <summary>
    /// Triggers effects on placeables when gets placed, deleted and disconnected.
    /// </summary>
    public class PlaceableEffects : MonoBehaviour, IPlaceableConnected, IPlaceableDeleted, IPlaceablePlaced
    {
        /// <summary>
        /// Place effect.
        /// </summary>
        [FormerlySerializedAs("_placeFx")]
        [BoxGroup("Effects")]
        [SerializeField]
        private VisualEffect placeFx;

        /// <summary>
        /// Delete effect.
        /// </summary>
        [FormerlySerializedAs("_destroyFx")]
        [BoxGroup("Effects")]
        [SerializeField]
        private VisualEffect destroyFx;

        /// <summary>
        /// Connect effect.
        /// </summary>
        [FormerlySerializedAs("_disconnectedFx")]
        [BoxGroup("Effects")]
        [SerializeField]
        private VisualEffect disconnectedFx;

        /// <summary>
        /// Place effect.
        /// </summary>
        [FormerlySerializedAs("_placeClip")]
        [BoxGroup("Audio")]
        [SerializeField]
        private AudioClip placeClip;

        /// <summary>
        /// Delete effect.
        /// </summary>
        [FormerlySerializedAs("_destroyClip")]
        [BoxGroup("Audio")]
        [SerializeField]
        private AudioClip destroyClip;

        /// <summary>
        /// Connect effect.
        /// </summary>
        [FormerlySerializedAs("_disconnectClip")]
        [BoxGroup("Audio")]
        [SerializeField]
        private AudioClip disconnectClip;


        [BoxGroup("Audio")] [SerializeField] private AudioSource audioSource;


        /// <summary>
        /// Starts the disconnect effect.
        /// </summary>
        /// <param name="isConnected"></param>
        public void OnPlaceableConnected(bool isConnected)
        {
            if (!isConnected)
            {
                if (disconnectedFx)
                {
                    disconnectedFx.Play();
                }

                if (audioSource && destroyClip)
                {
                    audioSource.PlayOneShot(destroyClip);
                }
            }
        }

        /// <summary>
        /// Starts the delete effect.
        /// </summary>
        /// <param name="wasDeletedByBuilderCallback"></param>
        [Button]
        public void OnPlaceableDeleted(bool wasDeletedByBuilderCallback)
        {
            if (destroyFx)
            {
                destroyFx.Play();
            }

            if (audioSource && destroyClip)
            {
                audioSource.PlayOneShot(destroyClip);
            }
        }

        /// <summary>
        /// Starts the placed effect.
        /// </summary>
        /// <param name="wasStartedByBuilderCallback"></param>
        [Button]
        public void OnPlaceableFinished(bool wasStartedByBuilderCallback)
        {
            if (placeFx)
            {
                placeFx.Play();
            }

            if (audioSource && placeClip)
            {
                audioSource.PlayOneShot(placeClip);
            }
        }
    }
}