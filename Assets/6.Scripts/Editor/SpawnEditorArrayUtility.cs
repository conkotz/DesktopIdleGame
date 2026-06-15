using UnityEditor;
using UnityEngine;

internal static class SpawnEditorArrayUtility
{
    public static void ScheduleRemoveElement(SerializedProperty element)
    {
        if (!TryGetParentArray(element, out SerializedProperty array, out int index))
            return;

        ScheduleRemoveAtIndex(array, index);
    }

    public static void ScheduleRemoveAtIndex(SerializedProperty array, int index)
    {
        if (array == null || !array.isArray || index < 0 || index >= array.arraySize)
            return;

        SerializedObject serializedObject = array.serializedObject;
        string arrayPath = array.propertyPath;
        int removeIndex = index;

        EditorApplication.delayCall += () =>
        {
            if (serializedObject == null || serializedObject.targetObject == null)
                return;

            serializedObject.Update();
            SerializedProperty liveArray = serializedObject.FindProperty(arrayPath);
            if (liveArray == null || !liveArray.isArray || removeIndex < 0 || removeIndex >= liveArray.arraySize)
                return;

            liveArray.DeleteArrayElementAtIndex(removeIndex);
            serializedObject.ApplyModifiedProperties();
            LevelSpawnGroupPlanDrawer.InvalidateCachedLists();
        };
    }

    public static bool TryGetParentArray(SerializedProperty element, out SerializedProperty array, out int index)
    {
        array = null;
        index = -1;

        if (element == null)
            return false;

        string path = element.propertyPath;
        const string marker = ".Array.data[";
        int markerIndex = path.LastIndexOf(marker, System.StringComparison.Ordinal);
        if (markerIndex < 0)
            return false;

        int close = path.IndexOf(']', markerIndex);
        if (close < 0)
            return false;

        string indexText = path.Substring(markerIndex + marker.Length, close - markerIndex - marker.Length);
        if (!int.TryParse(indexText, out index))
            return false;

        string arrayPath = path.Substring(0, markerIndex);
        array = element.serializedObject.FindProperty(arrayPath);
        return array != null && array.isArray;
    }

    public static bool IsValidArrayIndex(SerializedProperty array, int index) =>
        array != null && array.isArray && index >= 0 && index < array.arraySize;
}
