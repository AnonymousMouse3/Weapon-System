using System;
using System.Collections.Generic;
using MouseLib;
using MyBox;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[Serializable]
public class WeaponSlot
{  
    [ReadOnly] public Weapon CurrentWeapon;
    public List<GameObject> Weapons;
    public GameObject WeaponSlotObject;
    public InputActionReference Action;
}

public class WeaponManager : MonoBehaviour
{
    public delegate void OnHandleWeaponInputs(GameObject validationObject, InputAction action, bool pressOrRelease);
    public static OnHandleWeaponInputs onHandleWeaponInputs;
    public delegate void OnSwapWeapons(GameObject validationObject, int weaponSlotIndex, float weaponIndex);
    public static OnSwapWeapons onSwapWeapons;
    
    public static event Action<Weapon, WeaponSlot> OnSetWeapon;
    
    [SerializeField] private List<WeaponSlot> WeaponSlots;
    [SerializeField] private float weaponSwapTime;

    private bool swappingWeapon;
    
    void OnEnable()
    {
        onHandleWeaponInputs += HandleWeaponInputs;
        onSwapWeapons += SwapWeapons;
    }

    void OnDisable()
    {
        onHandleWeaponInputs -= HandleWeaponInputs;
        onSwapWeapons -= SwapWeapons;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        if (WeaponSlots.IsNullOrEmpty()) return;
        
        int counter = 0;
        foreach (WeaponSlot weaponSlot in WeaponSlots)
        {
            GameObject newWeapon = Instantiate(weaponSlot.Weapons[0], weaponSlot.WeaponSlotObject.transform); // get saved last weapon
            weaponSlot.WeaponSlotObject.transform.GetChild(0).TryGetComponent(out Weapon weaponComponent);
            weaponSlot.CurrentWeapon = weaponComponent;
            // use this to cache/store the current weapon's stats
            
            // only set the main weapon slot, not the launcher (temp fix)
            if (counter > 0) continue;
            OnSetWeapon?.Invoke(weaponSlot.CurrentWeapon, weaponSlot);
            counter++;
        }
    }

    private void HandleWeaponInputs(GameObject validationObject, InputAction action, bool pressOrRelease)
    {
        if (validationObject != gameObject) return;
        
        foreach (WeaponSlot weaponSlot in WeaponSlots)
        {
            if (weaponSlot.Action.ToInputAction() != action) continue;

            switch (pressOrRelease)
            {
                case true:
                    if (swappingWeapon) return;
                    weaponSlot.CurrentWeapon.PullTrigger(gameObject);
                    break;
                
                case false:
                    weaponSlot.CurrentWeapon.ReleaseTrigger(gameObject);
                    break;
            }
        }
    }

    private async void SwapWeapons(GameObject validationObject, int weaponSlotIndex, float weaponIndex)
    {
        if (validationObject != gameObject) return;
        
        WeaponSlot weaponSlot = WeaponSlots[weaponSlotIndex];
        
        weaponSlot.Weapons[(int)weaponIndex].TryGetComponent(out Weapon newWeaponComponent);
        if (weaponSlot.CurrentWeapon.weaponScriptableObject == newWeaponComponent.weaponScriptableObject) return;
        
        // Prevent firing until we have swapped weapons
        weaponSlot.CurrentWeapon.ReleaseTrigger(gameObject);
        swappingWeapon = true;
        
        GameObject newWeapon = Instantiate(weaponSlot.Weapons[(int)weaponIndex], weaponSlot.WeaponSlotObject.transform);
        newWeapon.transform.SetAsFirstSibling();
        
        // todo save the old weapon!!
        Destroy(weaponSlot.CurrentWeapon.gameObject);
        
        weaponSlot.WeaponSlotObject.transform.GetChild(0).TryGetComponent(out Weapon weaponComponent);
        weaponSlot.CurrentWeapon = weaponComponent;

        await MouseTools.AwaitableTimer(weaponSwapTime);
        swappingWeapon = false;
        
        OnSetWeapon?.Invoke(weaponSlot.CurrentWeapon, weaponSlot);
    }
}
