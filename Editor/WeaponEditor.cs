
using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

[CustomEditor(typeof(Weapon))]
public class WeaponEditor : Editor
{
    public VisualTreeAsset VisualTree;

    /*public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new VisualElement();

        VisualTree.CloneTree(root);
        
        return root;
    }*/
}
