using UnityEditor;
using UnityEngine;
using Naninovel;
using System;
using System.Linq;
using System.Collections.Generic;

[InitializeOnLoad]
public static class LayeredActorHierarchyEditor
{
    private enum LayeredObjectType { Root, Group, Layer, }
    
    private const string plusIcon = "d_Toolbar Plus@2x", minusIcon = "d_Toolbar Minus@2x", nextIcon = "tab_next@2x",
        prefabIcon = "d_Prefab Icon", folderIcon = "Folder On Icon", layerIcon = "d_Image Icon", cameraIcon = "SceneViewCamera@2x", addFileIcon = "Record On";

    private static Rect selectionRect;
    private static GameObject selection;
    private static LayeredObjectType selectedType;

    static LayeredActorHierarchyEditor()
    {
        EditorApplication.hierarchyWindowItemOnGUI += HandleHierarchyWindowItemOnGUI;
    }

    private static void HandleHierarchyWindowItemOnGUI(int instanceID, Rect rect)
    {
        selection = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        selectionRect = rect;

        if (selection != null)
        {
            if (selection.GetComponentInParent<LayeredActorBehaviour>() == null) return;
            if (selection.transform.TryGetComponent<LayeredActorBehaviour>(out _)) DrawActorControls();
            else if (selection.TryGetComponent<Renderer>(out var renderer)) DrawLayerOptions(renderer);
            else if (selection.TryGetComponent<Camera>(out _)) DrawCamera();
            else DrawGroupOptions();

            GUIStyle style = new GUIStyle { fontSize = 7 };

            void DrawActorControls()
            {
                selectedType = LayeredObjectType.Root;

                DrawIcon(prefabIcon);
                DrawIcon(addFileIcon, iconIndex:1, onClick:() => AddCompositionMap());

                var allChildRenderers = selection.GetAllChildRenderers();
                DrawPlusOption(() => allChildRenderers.ToList().ForEach(s => s.enabled = true));
                DrawMinusOption(() => allChildRenderers.ToList().ForEach(s => s.enabled = false));
            }

            void DrawGroupOptions()
            {
                selectedType = LayeredObjectType.Group;

                DrawIcon(folderIcon); 

                var allChildRenderers = selection.GetAllChildRenderers();
                DrawNextOption(() => selection.ToggleSingleLayer());
                DrawPlusOption(() => allChildRenderers.ToList().ForEach(s => s.enabled = true));
                DrawMinusOption(() => allChildRenderers.ToList().ForEach(s => s.enabled = false));
            }

            void DrawLayerOptions(Renderer renderer)
            {
                selectedType = LayeredObjectType.Layer;

                DrawIcon(layerIcon);

                DrawNextOption(() => selection.ToggleSingleLayer());
                DrawPlusOption(() => renderer.enabled = true);
                DrawMinusOption(() => renderer.enabled = false);
            }

            void DrawCamera() 
            {
                DrawIcon(cameraIcon);
            }
        }
    }

    private static void AddCompositionMap()
    {
        SerializedObject serializedObject = new SerializedObject(selection.GetComponentInParent<LayeredActorBehaviour>());
        EditorGUIUtility.PingObject(serializedObject.targetObject);
        Selection.activeObject = serializedObject.targetObject;

        SerializedProperty compositionMap = serializedObject.FindProperty("compositionMap");
        compositionMap.InsertArrayElementAtIndex(compositionMap.arraySize);
        var element = compositionMap.GetArrayElementAtIndex(compositionMap.arraySize-1);

        while (element.NextVisible(true))
        {
            if (element.propertyPath.Contains("Key")) element.stringValue = "NewCompositionMap" + (compositionMap.arraySize).ToString();
            if (element.propertyPath.Contains("Composition")) element.stringValue = selection.GetComponentInParent<LayeredActorBehaviour>().Composition;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawNextOption(Action onNext)
    {
        var childRenderers = selection.GetAllChildRenderers();
        if (childRenderers.Count() == 0) return;

        var isToggled = childRenderers.Any(r => r.enabled == true) && selection.AnyOtherGroupRendererEnabled() == 0;
        GUI.color = isToggled ? Color.yellow : Color.white;

        DrawIcon(nextIcon, iconIndex:1, onClick:onNext);
    }

    private static void DrawPlusOption(Action onPlus)
    {
        var childRenderers = selection.GetAllChildRenderers();
        if (childRenderers.Count() == 0) return;

        bool isEnabled = selectedType == LayeredObjectType.Root ? childRenderers.All(s => s.enabled) : childRenderers.Any(s => s.enabled);
        if (selectedType == LayeredObjectType.Root) GUI.color = isEnabled ? Color.green : Color.white;
        else GUI.color = isEnabled && selection.AnyOtherGroupRendererEnabled() > 0 ? Color.green : Color.white;

        DrawIcon(plusIcon, iconIndex:2, onClick:onPlus);
    }

    private static void DrawMinusOption(Action onMinus)
    {
        var childRenderers = selection.GetAllChildRenderers();
        if (childRenderers.Count() == 0) return;

        bool isDisabled = childRenderers.All(s => !s.enabled);
        if (selectedType == LayeredObjectType.Root) GUI.color = isDisabled ? Color.red : Color.white;
        else GUI.color = isDisabled ? Color.red : Color.white;

        DrawIcon(minusIcon, iconIndex:3, onClick:onMinus);
    }

    private static void DrawIcon(string iconName, int iconIndex = 0, Action onClick = null)
    {
        var icon = EditorGUIUtility.IconContent(iconName).image as Texture2D;

        Rect rect = new Rect(selectionRect);
        rect.x = rect.xMax - GetIconPosition();
        rect.width = 15;

        Event e = Event.current;

        GUI.DrawTexture(rect, icon);

        if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
        {
            if (onClick != null) onClick();
            e.Use();
        }

        GUI.color = Color.white;

        int GetIconPosition()
        {
            if (iconIndex == 0) return 45;
            else if (iconIndex == 1) return 30;
            else if (iconIndex == 2) return 15;
            else if (iconIndex == 3) return 0;
            else return 0;
        }
    }

    private static Renderer[] GetAllChildRenderers(this GameObject obj) => obj.GetComponentsInChildren<Renderer>();

    private static List<Renderer> GetOtherRenderersInGroup(this GameObject obj)
    {
        var group = obj.transform.parent.gameObject;
        var Renderers = new List<Renderer>();
        var firstChildren = group.GetFirstChildren();

        foreach (var child in firstChildren)
        {
            if (child.name == obj.name) continue;
            foreach (var renderer in child.GetAllChildRenderers())
                Renderers.Add(renderer);
        }
        return Renderers;
    }

    private static int AnyOtherGroupRendererEnabled(this GameObject obj)
    {
        var group = obj.transform.parent.gameObject;
        var firstChildren = group.GetFirstChildren();

        int count = 0;

        for (int i = 0; i < firstChildren.Count; i++)
        {
            if (firstChildren[i].name == obj.name) continue;
            if (firstChildren[i].GetAllChildRenderers().Any(s => s.enabled))
                count++;
        }

        return count;
    }

    private static List<GameObject> GetFirstChildren(this GameObject obj)
    {
        var firstChildren = new List<GameObject>();
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            firstChildren.Add(obj.transform.GetChild(i).gameObject);
        }
        return firstChildren;
    }

    private static void ToggleSingleLayer(this GameObject obj)
    {
        var children = obj.GetOtherRenderersInGroup();
        foreach (var layer in children) layer.enabled = false;

        foreach (var renderer in obj.GetAllChildRenderers()) renderer.enabled = true;
    }
}