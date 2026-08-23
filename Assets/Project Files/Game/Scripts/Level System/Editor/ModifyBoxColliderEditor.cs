using UnityEngine;
using UnityEditor;

namespace WaterFlow.Game
{
    public class ModifyBoxColliderEditor : EditorWindow
    {
        // Giá trị mặc định có thể tùy chỉnh
        private float centerY = 0.4f;
        private float sizeY = 0.8f;

        [MenuItem("Tools/Modify Box Colliders")]
        public static void ShowWindow()
        {
            GetWindow<ModifyBoxColliderEditor>("Modify Box Colliders");
        }

        private void OnGUI()
        {
            GUILayout.Label("Box Collider Settings", EditorStyles.boldLabel);

            centerY = EditorGUILayout.FloatField("Center Y", centerY);
            sizeY = EditorGUILayout.FloatField("Size Y", sizeY);

            EditorGUILayout.Space(10);

            // Hiển thị số lượng objects đã chọn
            int selectedCount = Selection.gameObjects.Length;
            EditorGUILayout.LabelField("Selected Objects:", selectedCount.ToString(), EditorStyles.boldLabel);

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Apply to Selected Prefab(s)", GUILayout.Height(30)))
            {
                ModifyBoxColliders();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Chọn một hoặc nhiều prefab trong Project window hoặc prefab instances trong Scene, " +
                "sau đó nhấn nút Apply để thay đổi tất cả Box Collider trên root game object của từng prefab.",
                MessageType.Info
            );
        }

        private void ModifyBoxColliders()
        {
            GameObject[] selectedObjects = Selection.gameObjects;

            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "Vui lòng chọn ít nhất một GameObject hoặc Prefab!", "OK");
                return;
            }

            int totalModified = 0;
            int totalProcessed = 0;
            int totalSkipped = 0;
            System.Text.StringBuilder logBuilder = new System.Text.StringBuilder();

            logBuilder.AppendLine($"=== Bắt đầu xử lý {selectedObjects.Length} object(s) ===");

            foreach (GameObject selectedObject in selectedObjects)
            {
                if (selectedObject == null)
                {
                    totalSkipped++;
                    continue;
                }

                // Kiểm tra xem có phải là prefab không
                GameObject prefabRoot = null;

                // Nếu chọn prefab asset trong Project window
                if (PrefabUtility.IsPartOfPrefabAsset(selectedObject))
                {
                    prefabRoot = selectedObject;
                }
                // Nếu chọn prefab instance trong Scene
                else if (PrefabUtility.IsPartOfPrefabInstance(selectedObject))
                {
                    prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(selectedObject);
                }
                else
                {
                    // Nếu không phải prefab, vẫn có thể apply cho GameObject thường
                    prefabRoot = selectedObject;
                }

                if (prefabRoot == null)
                {
                    logBuilder.AppendLine($"⚠ Bỏ qua: Không tìm thấy root object cho '{selectedObject.name}'");
                    totalSkipped++;
                    continue;
                }

                // Lấy tất cả Box Collider trên root object (không bao gồm children)
                BoxCollider[] boxColliders = prefabRoot.GetComponents<BoxCollider>();

                if (boxColliders.Length == 0)
                {
                    logBuilder.AppendLine($"⚠ Bỏ qua: Không có Box Collider trên '{prefabRoot.name}'");
                    totalSkipped++;
                    continue;
                }

                // Ghi lại để có thể undo
                Undo.RecordObjects(boxColliders, "Modify Box Colliders");

                int modifiedCount = 0;
                foreach (BoxCollider boxCollider in boxColliders)
                {
                    Vector3 center = boxCollider.center;
                    center.y = centerY;
                    boxCollider.center = center;

                    Vector3 size = boxCollider.size;
                    size.y = sizeY;
                    boxCollider.size = size;

                    modifiedCount++;
                }

                totalModified += modifiedCount;
                totalProcessed++;

                logBuilder.AppendLine($"✓ '{prefabRoot.name}': Đã thay đổi {modifiedCount} Box Collider(s)");

                // Nếu là prefab instance trong scene, mark dirty để lưu thay đổi
                if (PrefabUtility.IsPartOfPrefabInstance(prefabRoot))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(prefabRoot);
                }

                // Mark object dirty để lưu thay đổi
                EditorUtility.SetDirty(prefabRoot);
            }

            logBuilder.AppendLine($"=== Hoàn thành ===");
            logBuilder.AppendLine($"Đã xử lý: {totalProcessed} object(s)");
            logBuilder.AppendLine($"Tổng Box Collider đã thay đổi: {totalModified}");
            logBuilder.AppendLine($"Đã bỏ qua: {totalSkipped} object(s)");

            // In log ra Console
            Debug.Log(logBuilder.ToString());

            // Hiển thị dialog kết quả
            string message = $"Batch Process hoàn thành!\n\n" +
                             $"Đã xử lý: {totalProcessed}/{selectedObjects.Length} object(s)\n" +
                             $"Tổng Box Collider đã thay đổi: {totalModified}\n" +
                             $"Đã bỏ qua: {totalSkipped} object(s)\n\n" +
                             $"Center Y: {centerY}\n" +
                             $"Size Y: {sizeY}\n\n" +
                             $"Xem Console để biết chi tiết.";

            EditorUtility.DisplayDialog("Success", message, "OK");
        }
    }
}