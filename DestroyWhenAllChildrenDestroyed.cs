using MouseLib;
using UnityEngine;

public class DestroyWhenAllChildrenDestroyed : MonoBehaviour
{
    [SerializeField] private float checkInterval = 0.1f;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CheckChildren();
    }

    private async void CheckChildren()
    {
        while (transform.childCount > 0)
        {
            await MouseTools.AwaitableTimer(checkInterval);
        }
        
        Destroy(gameObject);
    }
}
