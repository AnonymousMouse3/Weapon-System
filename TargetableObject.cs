using System;
using System.Collections.Generic;
using MyBox;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;


public class IconWithName
{
    public string name;
    public GameObject icon;
}

public class TargetableObject : MonoBehaviour
{
    public delegate void OnEnableCanvas(GameObject player, GameObject targetObject, GameObject element);
    public static OnEnableCanvas onEnableCanvas;
    public delegate void OnDisableCanvas(GameObject ignoredObject, GameObject targetObject, GameObject element);
    public static OnDisableCanvas onDisableCanvas;
    
    [SerializeField] private GameObject iconParent;
    [SerializeField] private List<IconWithName> targetUIElements;

    private void OnEnable()
    {
        onEnableCanvas += CreateIcon;
        onDisableCanvas += RemoveIcon;
    }

    private void OnDisable()
    {
        onEnableCanvas -= CreateIcon;
        onDisableCanvas -= RemoveIcon;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        targetUIElements = new List<IconWithName>();
    }
    
    // this whole system uses string comparison on the prefab's name instead of anything smarter, would like to rework it
    // as it would require each weapon to have a unique lock-on icon if there were multiple, for example
    private void CreateIcon(GameObject player, GameObject targetObject, GameObject icon)
    {
        if (targetObject != gameObject) return;

        if (!targetUIElements.IsNullOrEmpty())
        {
            // don't allow duplicates
            foreach (IconWithName element in targetUIElements)
            {
                if (element.name == icon.name) return;
            }
        }
        
        IconWithName iconInstance = new IconWithName();
        iconInstance.icon = Instantiate(icon, iconParent.transform);
        iconInstance.name = icon.name;
        targetUIElements.Add(iconInstance);
    }

    private void RemoveIcon(GameObject player, GameObject targetObject, GameObject icon)
    {
        if (targetObject != gameObject) return;
        if (targetUIElements.IsNullOrEmpty()) return;
        if (!icon) return;
        

        IconWithName elementToRemove = new IconWithName();

        foreach (IconWithName element in targetUIElements)
        {
            if (element.name != icon.name) continue;
        
            elementToRemove = element;
            element.icon.SetActive(false);
        }
        
        if (!elementToRemove.icon) return;

        targetUIElements.Remove(elementToRemove);
        Destroy(elementToRemove.icon);
    }
}
