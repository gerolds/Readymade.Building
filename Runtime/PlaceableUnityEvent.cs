using System;
using Readymade.Building.Components;
using UnityEngine.Events;

namespace Readymade.Building
{
    /// <summary>
    /// Unity event concretion for <see cref="Placeable"/> arguments.
    /// </summary>
    [Serializable]
    public class PlaceableUnityEvent : UnityEvent<Placeable>
    {
    }
}