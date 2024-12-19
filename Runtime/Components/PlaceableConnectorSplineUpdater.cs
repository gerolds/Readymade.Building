using NaughtyAttributes;
using Readymade.Build;
using Readymade.Building.Components;
using UnityEngine;
using UnityEngine.Splines;

[RequireComponent ( typeof ( Placeable ) )]
public class PlaceableConnectorSpline : MonoBehaviour, IPlaceableUpdated {
    [SerializeField]
    private ConnectPoses poseConnector;

    [SerializeField]
    private SplineExtrude splineExtrude;

    [SerializeField]
    private SplineExtrudeShape splineShapeExtrude;

    private void Reset () {
        FindComponents ();
    }

    [Button]
    private void FindComponents () {
        if ( !poseConnector ) {
            poseConnector = GetComponentInChildren<ConnectPoses> ();
        }

        if ( !splineExtrude ) {
            splineExtrude = GetComponentInChildren<SplineExtrude> ();
        }

        if ( !splineShapeExtrude ) {
            splineShapeExtrude = GetComponentInChildren<SplineExtrudeShape> ();
        }
    }

    /// <inheritdoc />
    public void OnPlaceableUpdated () {
        if ( poseConnector ) {
            poseConnector.Rebuild ();
        }

        if ( splineExtrude ) {
            splineExtrude.Rebuild ();
        }

        if ( splineShapeExtrude ) {
            splineShapeExtrude.Rebuild ();
        }
    }
}