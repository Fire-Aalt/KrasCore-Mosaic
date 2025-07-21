using System;
using KrasCore.Mosaic.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KrasCore.Mosaic.Authoring
{
    public class MosaicCursorTest : MonoBehaviour
    {
        public IntGridDefinition def;


        private TilemapCommandBufferSingleton singleton;

        public void Start()
        {
            singleton =
                World.DefaultGameObjectInjectionWorld.EntityManager.GetSingleton<TilemapCommandBufferSingleton>();
        }

        // Reference to the camera; if null, Camera.main will be used.
        public Camera raycastCamera;

        public InputActionReference OnClick;
        public InputActionReference OnMousePos;

        private void OnEnable()
        {
            OnClick.action.performed += CursorClick;
            OnMousePos.action.performed += SetMousePosition;
        }
        
        private void OnDisable()
        {
            OnClick.action.performed -= CursorClick;
            OnMousePos.action.performed -= SetMousePosition;
        }
        
        private Vector2 _mousePosition;
        private void SetMousePosition(InputAction.CallbackContext ctx)
        {
            _mousePosition = ctx.ReadValue<Vector2>();
        }
        
        private void CursorClick(InputAction.CallbackContext ctx)
        {
            Vector3? hitPoint = GetCursorXZIntersection();
            if (hitPoint.HasValue)
            {
                singleton =
                    World.DefaultGameObjectInjectionWorld.EntityManager.GetSingleton<TilemapCommandBufferSingleton>();
                Debug.DrawLine(raycastCamera.transform.position, hitPoint.Value, Color.red, 2f);
                Debug.Log("hitPoint: " + new int2((int)math.round(hitPoint.Value.x), (int)math.round(hitPoint.Value.z)));
                singleton.SetIntGridValue(def.Hash, new int2((int)math.round(hitPoint.Value.x), (int)math.round(hitPoint.Value.z)), 1);
            }
        }

        /// <summary>
        /// Shoots a ray from the current cursor position and returns
        /// the intersection point with the XZ plane (y = 0), or null if none.
        /// </summary>
        /// <returns>World-space intersection point, or null.</returns>
        public Vector3? GetCursorXZIntersection()
        {
            // Determine which camera to use
            raycastCamera = raycastCamera != null ? raycastCamera : Camera.main;
            
            // Create a ray from the screen point
            Ray ray = raycastCamera.ScreenPointToRay(_mousePosition);

            // Define the XZ plane at y = 0
            Plane xzPlane = new Plane(Vector3.up, Vector3.zero);

            // Check for intersection
            if (xzPlane.Raycast(ray, out float enter))
            {
                // Get the point along the ray where it intersects
                Vector3 hitPoint = ray.GetPoint(enter);
                return hitPoint;
            }

            // No intersection
            return null;
        }
    }
}