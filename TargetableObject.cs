using System;
using System.Collections.Generic;
using UnityEngine;

public class TargetableObject : MonoBehaviour
{
    public delegate void OnEnableCanvas(GameObject player, GameObject targetObject);
    public static OnEnableCanvas onEnableCanvas;
    public delegate void OnDisableCanvas(GameObject ignoredObject, GameObject targetObject);
    public static OnDisableCanvas onDisableCanvas;
    
    [SerializeField] private List<GameObject> targetUIElements;

    private void OnEnable()
    {
        onEnableCanvas += EnableCanvas;
        onDisableCanvas += DisableCanvas;
    }

    private void OnDisable()
    {
        onEnableCanvas -= EnableCanvas;
        onDisableCanvas -= DisableCanvas;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        foreach (GameObject element in targetUIElements)
        {
            element.SetActive(false);
        }
    }
    
    private void EnableCanvas(GameObject player, GameObject targetObject)
    {
        if (targetObject != gameObject) { DisableCanvas(gameObject, gameObject); return;}
        
        foreach (GameObject element in targetUIElements)
        {
            element.SetActive(true);
        }
    }

    private void DisableCanvas(GameObject ignoredObject, GameObject targetObject)
    {
        if (targetObject != gameObject) return;
        
        foreach (GameObject element in targetUIElements)
        {
            element.SetActive(false);
        }
    }
}
