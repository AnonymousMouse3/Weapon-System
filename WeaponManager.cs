using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MouseLib;
using MyBox;
using Spellslinger.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[Serializable]
public class WeaponSlot
{  
    public string name;
    [ReadOnly] public Weapon CurrentWeapon;
    public List<GameObject> Weapons;
    public GameObject WeaponSlotObject;
    public InputActionReference Action;
    public WeaponComponent.WeaponAimType WeaponSlotCurrentAimType;
    [ReadOnly] public GameObject WeaponSlotAimpointObject;
    public bool AimWithAimingSystem;
}

public class WeaponManager : MonoBehaviour
{
    public delegate void OnHandleWeaponInputs(GameObject validationObject, bool pressOrRelease, InputAction action = null);
    public static OnHandleWeaponInputs onHandleWeaponInputs;
    public delegate void OnSwapWeapons(GameObject validationObject, int weaponSlotIndex, float weaponIndex);
    public static OnSwapWeapons onSwapWeapons;
    public delegate void OnSwitchAmmoType(GameObject validationObject, GameObject newAmmoType, int weaponSlotIndex);
    public static OnSwitchAmmoType onSwitchAmmoType;
    
    public static event Action<GameObject, Weapon> OnRegisterWeaponAiming;
    public static event Action<GameObject, Weapon> OnUnregisterWeaponAiming;
    public static event Action<bool> OnSetAimingAllowed;
    
    public List<WeaponSlot> WeaponSlots
    {
        get => weaponSlots;
        set => weaponSlots  = value;
    }
    
    [SerializeField] private List<WeaponSlot> weaponSlots;
    [SerializeField] private float weaponSwapTime;

    private Task weaponSwapTask = Task.CompletedTask;
    CancellationTokenSource weaponSwapTaskToken = new();
    private bool swappingWeapon;
    private HUDManager hudManager;
    
    void OnEnable()
    {
        onHandleWeaponInputs += HandleWeaponInputs;
        onSwapWeapons += BeginWeaponSwap;
        onSwitchAmmoType += SwitchAmmoType;
    }

    void OnDisable()
    {
        onHandleWeaponInputs -= HandleWeaponInputs;
        onSwapWeapons -= BeginWeaponSwap;
        onSwitchAmmoType += SwitchAmmoType;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        hudManager = FindObjectOfType<HUDManager>(); // bad
        
        if (weaponSlots.IsNullOrEmpty()) return;
        
        int counter = 0;
        foreach (WeaponSlot weaponSlot in weaponSlots)
        {
            if (weaponSlot.Weapons.IsNullOrEmpty()) continue;
            if (weaponSlot.Weapons[0] == null) continue;
            
            GameObject newWeaponGameObject = Instantiate(weaponSlot.Weapons[0], weaponSlot.WeaponSlotObject.transform); // get saved last weapon
            weaponSlot.WeaponSlotObject.transform.GetChild(0).TryGetComponent(out Weapon weapon);
            weaponSlot.CurrentWeapon = weapon;
            weapon.WeaponSlot = weaponSlot;
            // use this to cache/store the current weapon's stats

            if (counter < hudManager.worldCrosshairs.Count)
            {
                weaponSlot.WeaponSlotAimpointObject = hudManager.worldCrosshairs[counter];
            }
            
            if (!weaponSlot.AimWithAimingSystem) continue;
            OnRegisterWeaponAiming?.Invoke(gameObject, weaponSlot.CurrentWeapon);
            
            counter++;
        }
    }

    private void HandleWeaponInputs(GameObject validationObject, bool pressOrRelease, InputAction action = null)
    {
        if (validationObject != gameObject) return;
        
        foreach (WeaponSlot weaponSlot in weaponSlots)
        {
            if (weaponSlot.Weapons.IsNullOrEmpty()) continue;
            
            if (weaponSlot.Action)
            {
                if (weaponSlot.Action.ToInputAction() != action) continue;
            }

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

    private async void BeginWeaponSwap(GameObject validationObject, int weaponSlotIndex, float weaponIndex)
    {
        /*if (!weaponSwapTask.IsCompleted)
        {
            weaponSwapTaskToken.Cancel();
            weaponSwapTask = Task.CompletedTask;
        }*/
        
        weaponSwapTask = SwapWeapons(validationObject, weaponSlotIndex, weaponIndex);
    }

    private async Task SwapWeapons(GameObject validationObject, int weaponSlotIndex, float weaponIndex)
    {
        weaponSwapTaskToken.Token.ThrowIfCancellationRequested();
        if (validationObject != gameObject) return;
        if (swappingWeapon) return;
        
        // Select the weapon slot of interest
        WeaponSlot weaponSlot = weaponSlots[weaponSlotIndex];
        
        // Get the new weapon
        weaponSlot.Weapons[(int)weaponIndex].TryGetComponent(out Weapon newWeaponComponent);
        if (weaponSlot.CurrentWeapon.gameObject == newWeaponComponent.gameObject) return;
        
        // Get the old weapon and unregister it from the aiming system
        Weapon oldWeapon = weaponSlot.CurrentWeapon;
        OnUnregisterWeaponAiming?.Invoke(gameObject, oldWeapon);
        
        // Prevent aiming and shooting until we have swapped weapons
        weaponSlot.CurrentWeapon.ReleaseTrigger(gameObject);
        swappingWeapon = true;
        OnSetAimingAllowed?.Invoke(false);
        
        // Create the new weapon ingame
        GameObject newWeapon = Instantiate(weaponSlot.Weapons[(int)weaponIndex], weaponSlot.WeaponSlotObject.transform);
        newWeapon.transform.SetAsFirstSibling();
        
        // Get the relevant components and set relevant data in the weapon slot
        weaponSlot.WeaponSlotObject.transform.GetChild(0).TryGetComponent(out Weapon weaponComponent);
        weaponSlot.CurrentWeapon = weaponComponent;
        weaponComponent.WeaponSlot = weaponSlot;
        
        // Set weapon slot's aim type
        weaponSlot.WeaponSlotCurrentAimType = weaponSlot.CurrentWeapon.weaponScriptableObject.weaponComponent.currentWeaponAimType;
        
        // Register the new weapon with the aiming system
        OnRegisterWeaponAiming?.Invoke(gameObject, weaponSlot.CurrentWeapon);
        
        // todo save the old weapon!!
        // Remove the old weapon
        Destroy(oldWeapon.gameObject);
        
        // Allow aiming and shooting again after a delay
        await MouseTools.AwaitableTimer(weaponSwapTime);
        swappingWeapon = false;
        OnSetAimingAllowed?.Invoke(true);
    }

    private void SwitchAmmoType(GameObject validationObject, GameObject newAmmoType, int weaponSlotIndex)
    {
        if (!this) return;
        if (validationObject != gameObject) return;
        
        WeaponSlot weaponSlot = weaponSlots[weaponSlotIndex];
        
        if (!weaponSlot.CurrentWeapon.weaponScriptableObject.weaponComponent.projectilePrefabs.Contains(newAmmoType)) return;
        weaponSlot.CurrentWeapon.currentProjectile = newAmmoType;
    }
}
