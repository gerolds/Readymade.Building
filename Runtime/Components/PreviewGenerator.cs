// Adapted from https://github.com/yasirkula/UnityRuntimePreviewGenerator
// MIT License

//#define DEBUG_BOUNDS

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Readymade.Building.Components {
    /// <summary>
    /// Generates preview images of prefabs. Minor modifications have been made to the original source code.
    /// </summary>
    /// <remarks>Original: https://github.com/yasirkula/UnityRuntimePreviewGenerator</remarks>
    public static class PreviewGenerator {
        private class CameraSetup {
            private Vector3 _position;
            private Quaternion _rotation;

            private Color _backgroundColor;
            private bool _orthographic;
            private float _orthographicSize;
            private float _nearClipPlane;
            private float _farClipPlane;
            private float _aspect;
            private int _cullingMask;
            private CameraClearFlags _clearFlags;

            private RenderTexture _targetTexture;

            public void GetSetup ( Camera camera ) {
                _position = camera.transform.position;
                _rotation = camera.transform.rotation;

                _backgroundColor = camera.backgroundColor;
                _orthographic = camera.orthographic;
                _orthographicSize = camera.orthographicSize;
                _nearClipPlane = camera.nearClipPlane;
                _farClipPlane = camera.farClipPlane;
                _aspect = camera.aspect;
                _cullingMask = camera.cullingMask;
                _clearFlags = camera.clearFlags;

                _targetTexture = camera.targetTexture;
            }

            public void ApplySetup ( Camera camera ) {
                camera.transform.position = _position;
                camera.transform.rotation = _rotation;

                camera.backgroundColor = _backgroundColor;
                camera.orthographic = _orthographic;
                camera.orthographicSize = _orthographicSize;
                camera.aspect = _aspect;
                camera.cullingMask = _cullingMask;
                camera.clearFlags = _clearFlags;

                // Assigning order or nearClipPlane and farClipPlane may matter because Unity clamps near to far and far to near
                if ( _nearClipPlane < camera.farClipPlane ) {
                    camera.nearClipPlane = _nearClipPlane;
                    camera.farClipPlane = _farClipPlane;
                } else {
                    camera.farClipPlane = _farClipPlane;
                    camera.nearClipPlane = _nearClipPlane;
                }

                camera.targetTexture = _targetTexture;
                _targetTexture = null;
            }
        }

        private const int PREVIEW_LAYER = 22;
        private static readonly Vector3 PREVIEW_POSITION = new Vector3 ( -250f, -250f, -250f );

        private static Camera s_renderCamera;
        private static readonly CameraSetup s_cameraSetup = new CameraSetup ();

        private static readonly Vector3[] s_boundingBoxPoints = new Vector3[8];
        private static readonly Vector3[] s_localBoundsMinMax = new Vector3[2];

        private static readonly List<Renderer> s_renderers = new ( 64 );
        private static readonly List<int> s_layers = new ( 64 );

#if DEBUG_BOUNDS
	private static Material boundsDebugMaterial;
#endif

        private static Camera s_internalCamera = null;

        private static Camera InternalCamera {
            get {
                if ( s_internalCamera == null ) {
                    s_internalCamera = new GameObject ( "ModelPreviewGeneratorCamera" ).AddComponent<Camera> ();
                    s_internalCamera.enabled = false;
                    s_internalCamera.nearClipPlane = 0.01f;
                    s_internalCamera.cullingMask = 1 << PREVIEW_LAYER;
                    s_internalCamera.gameObject.hideFlags = HideFlags.HideAndDontSave;
                }

                return s_internalCamera;
            }
        }

        public static Camera PreviewRenderCamera { get; set; }

        private static Vector3 s_previewDirection = new Vector3 ( -0.57735f, -0.57735f, -0.57735f ); // Normalized (-1,-1,-1)

        public static Vector3 PreviewDirection {
            get => s_previewDirection;
            set => s_previewDirection = value.normalized;
        }

        private static float s_padding;

        public static float Padding {
            get => s_padding;
            set => s_padding = Mathf.Clamp ( value, -0.25f, 0.25f );
        }

        private static Color s_backgroundColor = new ( 0.3f, 0.3f, 0.3f, 1f );

        public static Color BackgroundColor {
            get => s_backgroundColor;
            set => s_backgroundColor = value;
        }

        public static bool OrthographicMode { get; set; } = false;

        public static bool UseLocalBounds { get; set; } = false;

        private static float s_renderSuperSampling = 1f;

        public static float RenderSuperSampling {
            get => s_renderSuperSampling;
            set => s_renderSuperSampling = Mathf.Max ( value, 0.1f );
        }

        public static bool MarkTextureNonReadable { get; set; } = true;

        public static Texture2D GenerateMaterialPreview (
            Material material,
            PrimitiveType previewPrimitive,
            int width = 64,
            int height = 64
        ) {
            return GenerateMaterialPreviewInternal ( material, previewPrimitive, null, null, width, height );
        }

        public static Texture2D GenerateMaterialPreviewWithShader (
            Material material,
            PrimitiveType previewPrimitive,
            Shader shader,
            string replacementTag,
            int width = 64,
            int height = 64
        ) {
            return GenerateMaterialPreviewInternal ( material, previewPrimitive, shader, replacementTag, width, height );
        }

        public static void GenerateMaterialPreviewAsync (
            Action<Texture2D> callback,
            Material material,
            PrimitiveType previewPrimitive,
            int width = 64,
            int height = 64
        ) {
            GenerateMaterialPreviewInternal ( material, previewPrimitive, null, null, width, height, callback );
        }

        public static void GenerateMaterialPreviewWithShaderAsync (
            Action<Texture2D> callback,
            Material material,
            PrimitiveType previewPrimitive,
            Shader shader,
            string replacementTag,
            int width = 64,
            int height = 64
        ) {
            GenerateMaterialPreviewInternal ( material, previewPrimitive, shader, replacementTag, width, height, callback );
        }


        private static Texture2D GenerateMaterialPreviewInternal (
            Material material,
            PrimitiveType previewPrimitive,
            Shader shader,
            string replacementTag,
            int width,
            int height,
            Action<Texture2D> asyncCallback = null
        ) {
            GameObject previewModel = GameObject.CreatePrimitive ( previewPrimitive );
            previewModel.gameObject.hideFlags = HideFlags.HideAndDontSave;
            previewModel.GetComponent<Renderer> ().sharedMaterial = material;

            try {
                return GenerateModelPreviewInternal ( previewModel.transform, shader, replacementTag, width, height, false, true,
                    asyncCallback );
            }
            catch ( Exception e ) {
                Debug.LogException ( e );
            }
            finally {
                Object.DestroyImmediate ( previewModel );
            }

            return null;
        }

        public static Texture2D GenerateModelPreview (
            Transform model,
            int width = 64,
            int height = 64,
            bool shouldCloneModel = false,
            bool shouldIgnoreParticleSystems = true
        ) {
            return GenerateModelPreviewInternal ( model, null, null, width, height, shouldCloneModel,
                shouldIgnoreParticleSystems );
        }

        public static Texture2D GenerateModelPreviewWithShader (
            Transform model,
            Shader shader,
            string replacementTag,
            int width = 64,
            int height = 64,
            bool shouldCloneModel = false,
            bool shouldIgnoreParticleSystems = true
        ) {
            return GenerateModelPreviewInternal ( model, shader, replacementTag, width, height, shouldCloneModel,
                shouldIgnoreParticleSystems );
        }

        public static void GenerateModelPreviewAsync (
            Action<Texture2D> callback,
            Transform model,
            int width = 64,
            int height = 64,
            bool shouldCloneModel = false,
            bool shouldIgnoreParticleSystems = true
        ) {
            GenerateModelPreviewInternal ( model, null, null, width, height, shouldCloneModel, shouldIgnoreParticleSystems,
                callback );
        }

        public static void GenerateModelPreviewWithShaderAsync (
            Action<Texture2D> callback,
            Transform model,
            Shader shader,
            string replacementTag,
            int width = 64,
            int height = 64,
            bool shouldCloneModel = false,
            bool shouldIgnoreParticleSystems = true
        ) {
            GenerateModelPreviewInternal ( model, shader, replacementTag, width, height, shouldCloneModel,
                shouldIgnoreParticleSystems, callback );
        }


        private static Texture2D GenerateModelPreviewInternal (
            Transform model,
            Shader shader,
            string replacementTag,
            int width,
            int height,
            bool shouldCloneModel,
            bool shouldIgnoreParticleSystems,
            Action<Texture2D> asyncCallback = null
        ) {
            if ( !model ) {
                if ( asyncCallback != null )
                    asyncCallback ( null );
                return null;
            }

            Texture2D result = null;

            if ( !model.gameObject.scene.IsValid () || !model.gameObject.scene.isLoaded )
                shouldCloneModel = true;

            Transform previewObject;
            if ( shouldCloneModel ) {
                previewObject = ( Transform ) Object.Instantiate ( model, null, false );
                previewObject.gameObject.hideFlags = HideFlags.HideAndDontSave;
            } else {
                previewObject = model;

                s_layers.Clear ();
                GetLayerRecursively ( previewObject );
            }

            bool isStatic = IsStatic ( model );
            bool wasActive = previewObject.gameObject.activeSelf;
            Vector3 prevPos = previewObject.position;
            Quaternion prevRot = previewObject.rotation;

            bool asyncOperationStarted = false;

#if DEBUG_BOUNDS
		Transform boundsDebugCube = null;
#endif

            try {
                SetupCamera ();
                SetLayerRecursively ( previewObject );

                if ( !isStatic ) {
                    previewObject.position = PREVIEW_POSITION;
                    previewObject.rotation = Quaternion.identity;
                }

                if ( !wasActive )
                    previewObject.gameObject.SetActive ( true );

                Quaternion cameraRotation =
                    Quaternion.LookRotation ( previewObject.rotation * s_previewDirection, previewObject.up );
                Bounds previewBounds = new Bounds ();
                if ( !CalculateBounds ( previewObject, shouldIgnoreParticleSystems, cameraRotation, out previewBounds ) ) {
                    if ( asyncCallback != null )
                        asyncCallback ( null );

                    return null;
                }

#if DEBUG_BOUNDS
			if( !boundsDebugMaterial )
			{
				boundsDebugMaterial = new Material( Shader.Find( "Sprites/Default" ) )
				{
					hideFlags = HideFlags.HideAndDontSave,
					color = new Color( 0.5f, 0.5f, 0.5f, 0.5f )
				};
			}

			boundsDebugCube = GameObject.CreatePrimitive( PrimitiveType.Cube ).transform;
			boundsDebugCube.localPosition = previewBounds.center;
			boundsDebugCube.localRotation = m_useLocalBounds ? cameraRotation : Quaternion.identity;
			boundsDebugCube.localScale = previewBounds.size;
			boundsDebugCube.gameObject.layer = PREVIEW_LAYER;
			boundsDebugCube.gameObject.hideFlags = HideFlags.HideAndDontSave;

			boundsDebugCube.GetComponent<Renderer>().sharedMaterial = boundsDebugMaterial;
#endif

                s_renderCamera.aspect = ( float ) width / height;
                s_renderCamera.transform.rotation = cameraRotation;

                CalculateCameraPosition ( s_renderCamera, previewBounds, s_padding );

                s_renderCamera.farClipPlane = ( s_renderCamera.transform.position - previewBounds.center ).magnitude +
                                              ( UseLocalBounds
                                                  ? ( previewBounds.extents.z * 1.01f )
                                                  : previewBounds.size.magnitude );

                RenderTexture activeRT = RenderTexture.active;
                RenderTexture renderTexture = null;
                try {
                    int supersampledWidth = Mathf.RoundToInt ( width * s_renderSuperSampling );
                    int supersampledHeight = Mathf.RoundToInt ( height * s_renderSuperSampling );

                    renderTexture = RenderTexture.GetTemporary ( supersampledWidth, supersampledHeight, 16 );
                    RenderTexture.active = renderTexture;
                    if ( s_backgroundColor.a < 1f )
                        GL.Clear ( true, true, s_backgroundColor );

                    s_renderCamera.targetTexture = renderTexture;

                    if ( !shader )
                        s_renderCamera.Render ();
                    else
                        s_renderCamera.RenderWithShader ( shader, replacementTag ?? string.Empty );

                    s_renderCamera.targetTexture = null;

                    if ( supersampledWidth != width || supersampledHeight != height ) {
                        RenderTexture _renderTexture = null;
                        try {
                            _renderTexture = RenderTexture.GetTemporary ( width, height, 16 );
                            RenderTexture.active = _renderTexture;
                            if ( s_backgroundColor.a < 1f )
                                GL.Clear ( true, true, s_backgroundColor );

                            Graphics.Blit ( renderTexture, _renderTexture );
                        }
                        finally {
                            if ( _renderTexture ) {
                                RenderTexture.ReleaseTemporary ( renderTexture );
                                renderTexture = _renderTexture;
                            }
                        }
                    }


                    if ( asyncCallback != null ) {
                        AsyncGPUReadback.Request ( renderTexture, 0,
                            s_backgroundColor.a < 1f ? TextureFormat.RGBA32 : TextureFormat.RGB24, ( asyncResult ) => {
                                try {
                                    result = new Texture2D ( width, height,
                                        s_backgroundColor.a < 1f ? TextureFormat.RGBA32 : TextureFormat.RGB24, false );
                                    if ( !asyncResult.hasError )
                                        result.LoadRawTextureData ( asyncResult.GetData<byte> () );
                                    else {
                                        Debug.LogWarning (
                                            "Async thumbnail request failed, falling back to conventional method" );

                                        RenderTexture _activeRT = RenderTexture.active;
                                        try {
                                            RenderTexture.active = renderTexture;
                                            result.ReadPixels ( new Rect ( 0f, 0f, width, height ), 0, 0, false );
                                        }
                                        finally {
                                            RenderTexture.active = _activeRT;
                                        }
                                    }

                                    result.Apply ( false, MarkTextureNonReadable );
                                    asyncCallback ( result );
                                }
                                finally {
                                    if ( renderTexture )
                                        RenderTexture.ReleaseTemporary ( renderTexture );
                                }
                            } );

                        asyncOperationStarted = true;
                    } else {
                        result = new Texture2D ( width, height,
                            s_backgroundColor.a < 1f ? TextureFormat.RGBA32 : TextureFormat.RGB24,
                            false );
                        result.ReadPixels ( new Rect ( 0f, 0f, width, height ), 0, 0, false );
                        result.Apply ( false, MarkTextureNonReadable );
                    }
                }
                finally {
                    RenderTexture.active = activeRT;

                    if ( renderTexture ) {
                        if ( !asyncOperationStarted ) {
                            RenderTexture.ReleaseTemporary ( renderTexture );
                        }
                    }
                }
            }
            catch ( Exception e ) {
                Debug.LogException ( e );
            }
            finally {
#if DEBUG_BOUNDS
			if( boundsDebugCube )
				Object.DestroyImmediate( boundsDebugCube.gameObject );
#endif

                if ( shouldCloneModel )
                    Object.DestroyImmediate ( previewObject.gameObject );
                else {
                    if ( !wasActive )
                        previewObject.gameObject.SetActive ( false );

                    if ( !isStatic ) {
                        previewObject.position = prevPos;
                        previewObject.rotation = prevRot;
                    }

                    int index = 0;
                    SetLayerRecursively ( previewObject, ref index );
                }

                if ( s_renderCamera == PreviewRenderCamera )
                    s_cameraSetup.ApplySetup ( s_renderCamera );
            }

            if ( !asyncOperationStarted && asyncCallback != null )
                asyncCallback ( null );

            return result;
        }

        // Calculates AABB bounds of the target object (AABB will include its child objects)
        public static bool CalculateBounds (
            Transform target,
            bool shouldIgnoreParticleSystems,
            Quaternion cameraRotation,
            out Bounds bounds
        ) {
            s_renderers.Clear ();
            target.GetComponentsInChildren ( s_renderers );

            Quaternion inverseCameraRotation = Quaternion.Inverse ( cameraRotation );
            Vector3 localBoundsMin = new Vector3 ( float.MaxValue - 1f, float.MaxValue - 1f, float.MaxValue - 1f );
            Vector3 localBoundsMax = new Vector3 ( float.MinValue + 1f, float.MinValue + 1f, float.MinValue + 1f );

            bounds = new Bounds ();
            bool hasBounds = false;
            for ( int i = 0; i < s_renderers.Count; i++ ) {
                if ( !s_renderers[ i ].enabled )
                    continue;

                if ( shouldIgnoreParticleSystems && s_renderers[ i ] is ParticleSystemRenderer )
                    continue;

                // Local bounds calculation code taken from: https://github.com/Unity-Technologies/UnityCsReference/blob/0355e09029fa1212b7f2e821f41565df8e8814c7/Editor/Mono/InternalEditorUtility.bindings.cs#L710
                if ( UseLocalBounds ) {
                    Bounds localBounds = s_renderers[ i ].localBounds;
                    Transform transform = s_renderers[ i ].transform;
                    s_localBoundsMinMax[ 0 ] = localBounds.min;
                    s_localBoundsMinMax[ 1 ] = localBounds.max;

                    for ( int x = 0; x < 2; x++ ) {
                        for ( int y = 0; y < 2; y++ ) {
                            for ( int z = 0; z < 2; z++ ) {
                                Vector3 point = inverseCameraRotation * transform.TransformPoint (
                                    new Vector3 ( s_localBoundsMinMax[ x ].x, s_localBoundsMinMax[ y ].y,
                                        s_localBoundsMinMax[ z ].z ) );
                                localBoundsMin = Vector3.Min ( localBoundsMin, point );
                                localBoundsMax = Vector3.Max ( localBoundsMax, point );
                            }
                        }
                    }

                    hasBounds = true;
                } else if ( !hasBounds ) {
                    bounds = s_renderers[ i ].bounds;
                    hasBounds = true;
                } else
                    bounds.Encapsulate ( s_renderers[ i ].bounds );
            }

            if ( UseLocalBounds && hasBounds )
                bounds = new Bounds ( cameraRotation * ( ( localBoundsMin + localBoundsMax ) * 0.5f ),
                    localBoundsMax - localBoundsMin );

            return hasBounds;
        }

        // Moves camera in a way such that it will encapsulate bounds perfectly
        public static void CalculateCameraPosition ( Camera camera, Bounds bounds, float padding = 0f ) {
            Transform cameraTR = camera.transform;

            Vector3 cameraDirection = cameraTR.forward;
            float aspect = camera.aspect;

            if ( padding != 0f )
                bounds.size *= 1f + padding * 2f; // Padding applied to both edges, hence multiplied by 2

            Vector3 boundsCenter = bounds.center;
            Vector3 boundsExtents = bounds.extents;
            Vector3 boundsSize = 2f * boundsExtents;

            // Calculate corner points of the Bounds
            if ( UseLocalBounds ) {
                Matrix4x4 localBoundsMatrix = Matrix4x4.TRS ( boundsCenter, camera.transform.rotation, Vector3.one );
                Vector3 point = boundsExtents;
                s_boundingBoxPoints[ 0 ] = localBoundsMatrix.MultiplyPoint3x4 ( point );
                point.x -= boundsSize.x;
                s_boundingBoxPoints[ 1 ] = localBoundsMatrix.MultiplyPoint3x4 ( point );
                point.y -= boundsSize.y;
                s_boundingBoxPoints[ 2 ] = localBoundsMatrix.MultiplyPoint3x4 ( point );
                point.x += boundsSize.x;
                s_boundingBoxPoints[ 3 ] = localBoundsMatrix.MultiplyPoint3x4 ( point );
                point.z -= boundsSize.z;
                s_boundingBoxPoints[ 4 ] = localBoundsMatrix.MultiplyPoint3x4 ( point );
                point.x -= boundsSize.x;
                s_boundingBoxPoints[ 5 ] = localBoundsMatrix.MultiplyPoint3x4 ( point );
                point.y += boundsSize.y;
                s_boundingBoxPoints[ 6 ] = localBoundsMatrix.MultiplyPoint3x4 ( point );
                point.x += boundsSize.x;
                s_boundingBoxPoints[ 7 ] = localBoundsMatrix.MultiplyPoint3x4 ( point );
            } else {
                Vector3 point = boundsCenter + boundsExtents;
                s_boundingBoxPoints[ 0 ] = point;
                point.x -= boundsSize.x;
                s_boundingBoxPoints[ 1 ] = point;
                point.y -= boundsSize.y;
                s_boundingBoxPoints[ 2 ] = point;
                point.x += boundsSize.x;
                s_boundingBoxPoints[ 3 ] = point;
                point.z -= boundsSize.z;
                s_boundingBoxPoints[ 4 ] = point;
                point.x -= boundsSize.x;
                s_boundingBoxPoints[ 5 ] = point;
                point.y += boundsSize.y;
                s_boundingBoxPoints[ 6 ] = point;
                point.x += boundsSize.x;
                s_boundingBoxPoints[ 7 ] = point;
            }

            if ( camera.orthographic ) {
                cameraTR.position = boundsCenter;

                float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
                float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;

                for ( int i = 0; i < s_boundingBoxPoints.Length; i++ ) {
                    Vector3 localPoint = cameraTR.InverseTransformPoint ( s_boundingBoxPoints[ i ] );
                    if ( localPoint.x < minX )
                        minX = localPoint.x;
                    if ( localPoint.x > maxX )
                        maxX = localPoint.x;
                    if ( localPoint.y < minY )
                        minY = localPoint.y;
                    if ( localPoint.y > maxY )
                        maxY = localPoint.y;
                }

                float distance = boundsExtents.magnitude + 1f;
                camera.orthographicSize = Mathf.Max ( maxY - minY, ( maxX - minX ) / aspect ) * 0.5f;
                cameraTR.position = boundsCenter - cameraDirection * distance;
            } else {
                Vector3 cameraUp = cameraTR.up, cameraRight = cameraTR.right;

                float verticalFOV = camera.fieldOfView * 0.5f;
                float horizontalFOV = Mathf.Atan ( Mathf.Tan ( verticalFOV * Mathf.Deg2Rad ) * aspect ) * Mathf.Rad2Deg;

                // Normals of the camera's frustum planes
                Vector3 topFrustumPlaneNormal = Quaternion.AngleAxis ( 90f + verticalFOV, -cameraRight ) * cameraDirection;
                Vector3 bottomFrustumPlaneNormal = Quaternion.AngleAxis ( 90f + verticalFOV, cameraRight ) * cameraDirection;
                Vector3 rightFrustumPlaneNormal = Quaternion.AngleAxis ( 90f + horizontalFOV, cameraUp ) * cameraDirection;
                Vector3 leftFrustumPlaneNormal = Quaternion.AngleAxis ( 90f + horizontalFOV, -cameraUp ) * cameraDirection;

                // Credit for algorithm: https://stackoverflow.com/a/66113254/2373034
                // 1. Find edge points of the bounds using the camera's frustum planes
                // 2. Create a plane for each edge point that goes through the point and has the corresponding frustum plane's normal
                // 3. Find the intersection line of horizontal edge points' planes (horizontalIntersection) and vertical edge points' planes (verticalIntersection)
                //    If we move the camera along horizontalIntersection, the bounds will always with the camera's width perfectly (similar effect goes for verticalIntersection)
                // 4. Find the closest line segment between these two lines (horizontalIntersection and verticalIntersection) and place the camera at the farthest point on that line
                int leftmostPoint = -1, rightmostPoint = -1, topmostPoint = -1, bottommostPoint = -1;
                for ( int i = 0; i < s_boundingBoxPoints.Length; i++ ) {
                    if ( leftmostPoint < 0 && IsOutermostPointInDirection ( i, leftFrustumPlaneNormal ) )
                        leftmostPoint = i;
                    if ( rightmostPoint < 0 && IsOutermostPointInDirection ( i, rightFrustumPlaneNormal ) )
                        rightmostPoint = i;
                    if ( topmostPoint < 0 && IsOutermostPointInDirection ( i, topFrustumPlaneNormal ) )
                        topmostPoint = i;
                    if ( bottommostPoint < 0 && IsOutermostPointInDirection ( i, bottomFrustumPlaneNormal ) )
                        bottommostPoint = i;
                }

                Ray horizontalIntersection =
                    GetPlanesIntersection ( new Plane ( leftFrustumPlaneNormal, s_boundingBoxPoints[ leftmostPoint ] ),
                        new Plane ( rightFrustumPlaneNormal, s_boundingBoxPoints[ rightmostPoint ] ) );
                Ray verticalIntersection =
                    GetPlanesIntersection ( new Plane ( topFrustumPlaneNormal, s_boundingBoxPoints[ topmostPoint ] ),
                        new Plane ( bottomFrustumPlaneNormal, s_boundingBoxPoints[ bottommostPoint ] ) );

                Vector3 closestPoint1, closestPoint2;
                FindClosestPointsOnTwoLines ( horizontalIntersection, verticalIntersection, out closestPoint1,
                    out closestPoint2 );

                cameraTR.position = Vector3.Dot ( closestPoint1 - closestPoint2, cameraDirection ) < 0
                    ? closestPoint1
                    : closestPoint2;
            }
        }

        // Returns whether or not the given point is the outermost point in the given direction among all points of the bounds
        private static bool IsOutermostPointInDirection ( int pointIndex, Vector3 direction ) {
            Vector3 point = s_boundingBoxPoints[ pointIndex ];
            for ( int i = 0; i < s_boundingBoxPoints.Length; i++ ) {
                if ( i != pointIndex && Vector3.Dot ( direction, s_boundingBoxPoints[ i ] - point ) > 0 )
                    return false;
            }

            return true;
        }

        // Credit: https://stackoverflow.com/a/32410473/2373034
        // Returns the intersection line of the 2 planes
        private static Ray GetPlanesIntersection ( Plane p1, Plane p2 ) {
            Vector3 p3Normal = Vector3.Cross ( p1.normal, p2.normal );
            float det = p3Normal.sqrMagnitude;

            return new Ray (
                ( ( Vector3.Cross ( p3Normal, p2.normal ) * p1.distance ) +
                  ( Vector3.Cross ( p1.normal, p3Normal ) * p2.distance ) ) / det, p3Normal );
        }

        // Credit: http://wiki.unity3d.com/index.php/3d_Math_functions
        // Returns the edge points of the closest line segment between 2 lines
        private static void FindClosestPointsOnTwoLines (
            Ray line1,
            Ray line2,
            out Vector3 closestPointLine1,
            out Vector3 closestPointLine2
        ) {
            Vector3 line1Direction = line1.direction;
            Vector3 line2Direction = line2.direction;

            float a = Vector3.Dot ( line1Direction, line1Direction );
            float b = Vector3.Dot ( line1Direction, line2Direction );
            float e = Vector3.Dot ( line2Direction, line2Direction );

            float d = a * e - b * b;

            Vector3 r = line1.origin - line2.origin;
            float c = Vector3.Dot ( line1Direction, r );
            float f = Vector3.Dot ( line2Direction, r );

            float s = ( b * f - c * e ) / d;
            float t = ( a * f - c * b ) / d;

            closestPointLine1 = line1.origin + line1Direction * s;
            closestPointLine2 = line2.origin + line2Direction * t;
        }

        private static void SetupCamera () {
            if ( PreviewRenderCamera ) {
                s_cameraSetup.GetSetup ( PreviewRenderCamera );

                s_renderCamera = PreviewRenderCamera;
                s_renderCamera.nearClipPlane = 0.01f;
                s_renderCamera.cullingMask = 1 << PREVIEW_LAYER;
            } else
                s_renderCamera = InternalCamera;

            s_renderCamera.backgroundColor = s_backgroundColor;
            s_renderCamera.orthographic = OrthographicMode;
            s_renderCamera.clearFlags = s_backgroundColor.a < 1f ? CameraClearFlags.Depth : CameraClearFlags.Color;
        }

        private static bool IsStatic ( Transform obj ) {
            if ( obj.gameObject.isStatic )
                return true;

            for ( int i = 0; i < obj.childCount; i++ ) {
                if ( IsStatic ( obj.GetChild ( i ) ) )
                    return true;
            }

            return false;
        }

        private static void SetLayerRecursively ( Transform obj ) {
            obj.gameObject.layer = PREVIEW_LAYER;
            for ( int i = 0; i < obj.childCount; i++ )
                SetLayerRecursively ( obj.GetChild ( i ) );
        }

        private static void GetLayerRecursively ( Transform obj ) {
            s_layers.Add ( obj.gameObject.layer );
            for ( int i = 0; i < obj.childCount; i++ )
                GetLayerRecursively ( obj.GetChild ( i ) );
        }

        private static void SetLayerRecursively ( Transform obj, ref int index ) {
            obj.gameObject.layer = s_layers[ index++ ];
            for ( int i = 0; i < obj.childCount; i++ )
                SetLayerRecursively ( obj.GetChild ( i ), ref index );
        }
    }
}