// using WaterFlow.Core;
// using UnityEngine;
//
// namespace WaterFlow.Game
// {
//     /// <summary>
//     /// Handles raycasting and selection during power-up targeting mode.
//     /// This component should be on the same GameObject as RaycastController or GameController.
//     /// </summary>
//     [StaticUnload]
//     public class PowerUpSelectionHandler : MonoBehaviour
//     {
//         private Camera mainCam;
//         private static bool isActive;
//         
//         public static event System.Action<LevelBlockBehavior> OnBlockHovered;
//         public static event System.Action OnHoverCleared;
//         
//         private LevelBlockBehavior lastHoveredBlock;
//         
//         public void Init()
//         {
//             mainCam = Camera.main;
//             isActive = true;
//         }
//         
//         private void Update()
//         {
//             if (!isActive)
//                 return;
//             
//             if (!PowerUpController.Instance.IsInSelectionMode)
//             {
//                 ClearHover();
//                 return;
//             }
//             
//             HandleHover();
//             HandleClick();
//         }
//         
//         private void HandleHover()
//         {
//             Ray ray = mainCam.ScreenPointToRay(InputController.MousePosition);
//             
//             if (Physics.Raycast(ray, out RaycastHit hit))
//             {
//                 LevelBlockBehavior block = hit.transform.GetComponent<LevelBlockBehavior>();
//                 
//                 if (block != null && block != lastHoveredBlock)
//                 {
//                     ClearHover();
//                     lastHoveredBlock = block;
//                     OnBlockHovered?.Invoke(block);
//                 }
//                 else if (block == null)
//                 {
//                     ClearHover();
//                 }
//             }
//             else
//             {
//                 ClearHover();
//             }
//         }
//         
//         private void HandleClick()
//         {
//             if (!InputController.ClickAction.WasPressedThisFrame())
//                 return;
//             
//             Ray ray = mainCam.ScreenPointToRay(InputController.MousePosition);
//             
//             if (Physics.Raycast(ray, out RaycastHit hit))
//             {
//                 LevelBlockBehavior block = hit.transform.GetComponent<LevelBlockBehavior>();
//                 
//                 if (block != null)
//                 {
//                     PowerUpController.Instance.OnBlockSelected(block);
//                 }
//             }
//         }
//         
//         private void ClearHover()
//         {
//             if (lastHoveredBlock != null)
//             {
//                 lastHoveredBlock = null;
//                 OnHoverCleared?.Invoke();
//             }
//         }
//         
//         public static void Enable()
//         {
//             isActive = true;
//         }
//         
//         public static void Disable()
//         {
//             isActive = false;
//         }
//         
//         private static void UnloadStatic()
//         {
//             isActive = false;
//             OnBlockHovered = null;
//             OnHoverCleared = null;
//         }
//     }
// }