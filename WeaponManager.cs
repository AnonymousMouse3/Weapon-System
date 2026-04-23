using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;
using MouseLib;
using MyBox;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[Serializable]
public class WeaponSlot
{  
    public string name;
    [ReadOnly] public Weapon CurrentWeapon;
    [ReadOnly] public Weapon CurrentWeaponPrefab;
    [ReadOnly] public bool SlotOnCooldown;
    [ReadOnly] public bool SwappingWeapon;
    public List<Weapon> WeaponPrefabs;
    public int MaxWeaponsInSlot;
    public GameObject WeaponSlotObject;
    public WeaponScriptableObject.WeaponAimType WeaponSlotCurrentAimType;
    public bool AimWithAimingSystem;
    public CancellationTokenSource WeaponSlotSwapCTS;
}

public class WeaponManager : MonoBehaviour
{
    public delegate void OnHandleWeaponInputs(GameObject validationObject, bool pressOrRelease, InputAction action = null);
    public static OnHandleWeaponInputs onHandleWeaponInputs;
    public delegate void OnAddWeaponToSlot(GameObject validationObject, string weaponSlotName, Weapon weapon, int index, bool swapToNewWeapon = false);
    public static OnAddWeaponToSlot onAddWeaponToSlot;
    public delegate void OnRemoveWeaponFromSlot(GameObject validationObject, string weaponSlotName, Weapon weapon, int index);
    public static OnRemoveWeaponFromSlot onRemoveWeaponFromSlot;
    public delegate void OnReplaceTargetWeapon(GameObject validationObject, string weaponSlotName,  Weapon replacement, Weapon target);
    public static OnReplaceTargetWeapon onReplaceTargetWeapon;
    public delegate void OnReplaceWeaponInSlot(GameObject validationObject, string weaponSlotName, Weapon replacement, int index);
    public static OnReplaceWeaponInSlot onReplaceWeaponInSlot;
    public delegate void OnSwapWeapons(GameObject validationObject, string weaponSlotName, int weaponIndex);
    public static OnSwapWeapons onSwapWeapons;
    public delegate void OnCycleWeapons(GameObject validationObject, string weaponSlotName, int direction);
    public static OnCycleWeapons onCycleWeapons;
    public delegate void OnSwitchAmmoType(GameObject validationObject, GameObject newAmmoType, int weaponSlotIndex);
    public static OnSwitchAmmoType onSwitchAmmoType;
    public delegate void OnBeginGlobalCooldown(GameObject validationObject, float cooldown);
    public static OnBeginGlobalCooldown onBeginGlobalCooldown;
    public delegate void OnBeginWeaponSlotGlobalCooldown(GameObject validationObject, WeaponSlot weaponSlot, float cooldown);
    public static OnBeginWeaponSlotGlobalCooldown onBeginWeaponSlotGlobalCooldown;

    
    
    public static event Action<GameObject, GameObject> OnRegisterWeaponAiming;
    public static event Action<GameObject, GameObject> OnUnregisterWeaponAiming;
    public static event Action<bool> OnSetAimingAllowed;
    public static event Action<Weapon> OnCleanupTargetLocks;
    public static event Action<WeaponScriptableObject> OnCurrentSpellChange;
    public List<WeaponSlot> WeaponSlots
    {
        get => weaponSlots;
        set => weaponSlots  = value;
    }
    
    [SerializeField] private bool allWeaponsOnGlobalCooldown;
    [SerializeField] public List<WeaponSlot> weaponSlots;

    private Task weaponSwapTask;
    private Task globalCooldownTask;
    private CancellationTokenSource globalCooldownCTS;
    private Task weaponSlotCooldownTask;
    private CancellationTokenSource weaponSlotCooldownCTS;
    
    #if SPELL_SYSTEM
    private HUDManager hudManager;
    #endif
    
    #if SQUADS
    private bool squadMode;
    #endif
    
    
    void OnEnable()
    {
        onHandleWeaponInputs += HandleWeaponInputs;
        onAddWeaponToSlot += AddWeaponToSlot;
        onRemoveWeaponFromSlot += RemoveWeaponFromSlot;
        onReplaceWeaponInSlot += ReplaceWeaponInSlot;
        onReplaceTargetWeapon += ReplaceTargetWeapon;
        onSwapWeapons += BeginWeaponSwap;
        onCycleWeapons += BeginWeaponCycle;
        onBeginGlobalCooldown += BeginGlobalCooldown;
        onBeginWeaponSlotGlobalCooldown += BeginWeaponSlotGlobalCooldown;
    }

    void OnDisable()
    {
        onHandleWeaponInputs -= HandleWeaponInputs;
        onAddWeaponToSlot -= AddWeaponToSlot;
        onRemoveWeaponFromSlot -= RemoveWeaponFromSlot;
        onReplaceWeaponInSlot -= ReplaceWeaponInSlot;
        onReplaceTargetWeapon -= ReplaceTargetWeapon;
        onSwapWeapons -= BeginWeaponSwap;
        onCycleWeapons -= BeginWeaponCycle;
        onBeginGlobalCooldown -= BeginGlobalCooldown;
        onBeginWeaponSlotGlobalCooldown -= BeginWeaponSlotGlobalCooldown;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        #if SPELL_SYSTEM
        hudManager = FindObjectOfType<HUDManager>(); // bad
        #endif
        
        if (weaponSlots.IsNullOrEmpty()) return;

        foreach (WeaponSlot weaponSlot in weaponSlots)
        {
            if (weaponSlot.WeaponPrefabs.IsNullOrEmpty()) continue;
            if (weaponSlot.WeaponPrefabs[0] == null) continue;

            BeginWeaponSwap(gameObject, weaponSlot.name, 0); // swap to last saved weapon
        }
    }

    private void SetupAndInstantiateWeapon(WeaponSlot weaponSlot, Weapon weaponPrefab)
    {
        // Create the new weapon ingame
        GameObject newWeaponGameObject = Instantiate(weaponPrefab.gameObject, weaponSlot.WeaponSlotObject.transform);
        newWeaponGameObject.TryGetComponent(out Weapon weapon);
        
        // Get the relevant components and set relevant data in the weapon slot
        weapon.transform.SetAsFirstSibling();
        weaponSlot.CurrentWeapon = weapon;
        weaponSlot.CurrentWeaponPrefab = weaponPrefab;
        weaponSlot.WeaponSlotCurrentAimType = weaponSlot.CurrentWeapon.weaponScriptableObject.aimType;
        
        if (weapon.weaponScriptableObject.weaponAimpointIcon)
        {
            weapon.worldAimpointInstance = Instantiate(weapon.weaponScriptableObject.weaponAimpointIcon);
            weapon.worldAimpointInstance.TryGetComponent(out weapon.worldAimpointInstanceImage);
        }
            
        if (!weaponSlot.AimWithAimingSystem) return;
        
        // Register the new weapon with the aiming system
        OnRegisterWeaponAiming?.Invoke(gameObject, newWeaponGameObject);
    }

    // feed inputs to the weapon and feed only the right type of input (press or release, etc)
    // do NOT perform weapon condition checks here (ammo checks, mana checks), these go in Weapon
    private void HandleWeaponInputs(GameObject validationObject, bool pressOrRelease, InputAction action)
    {
        if (validationObject != gameObject) return;
        
        foreach (WeaponSlot weaponSlot in weaponSlots)
        {
            if (weaponSlot.WeaponPrefabs.IsNullOrEmpty()) continue;
            
            /*if (weaponSlot.Action)
            {
                if (weaponSlot.Action.ToInputAction() != action) continue;
            }*/

            if (weaponSlot.SwappingWeapon) return;
            if (weaponSlot.SlotOnCooldown) return;
                    
            weaponSlot.CurrentWeapon.ProcessWeaponAction(gameObject, action, pressOrRelease);
        }
    }

    private async void BeginWeaponSwap(GameObject validationObject, string weaponSlotName, int weaponIndex)
    {
        if (validationObject != gameObject) return;
        // Select the weapon slot of interest
        WeaponSlot weaponSlot = FindWeaponSlotByName(weaponSlotName);
        if (weaponSlot == null) return;
        
        weaponSlot.WeaponSlotSwapCTS?.Cancel();
        
        weaponSlot.WeaponSlotSwapCTS = new CancellationTokenSource();
        weaponSwapTask = SwapWeapons(weaponSlot.WeaponSlotSwapCTS.Token, validationObject, weaponSlot, weaponIndex);
    }
    
    private void BeginWeaponCycle(GameObject validationObject, string weaponSlotName, int direction)
    {
        if (validationObject != gameObject) return;
        // Select the weapon slot of interest
        WeaponSlot weaponSlot = FindWeaponSlotByName(weaponSlotName);
        if (weaponSlot == null) return;

        int weaponIndex = 0;
        
        switch (direction)
        {
            // Cycle backwards
            case -1:
                // If we find the lower end of the array, wrap around to the top
                // Otherwise, swap to the spell below the current spell
                if (weaponSlot.WeaponPrefabs.IndexOf(weaponSlot.CurrentWeaponPrefab) - 1 < 0)
                {
                    weaponIndex = weaponSlot.WeaponPrefabs.Count - 1;
                }
                else
                {
                    weaponIndex = weaponSlot.WeaponPrefabs.IndexOf(weaponSlot.CurrentWeaponPrefab) - 1;
                }
                break;
            
            // Cycle forwards
            case 1:
                // If we find the upper end of the array, wrap around to the bottom
                // Otherwise, swap to the spell above the current spell
                if (weaponSlot.WeaponPrefabs.IndexOf(weaponSlot.CurrentWeaponPrefab) + 1 >= weaponSlot.WeaponPrefabs.Count)
                {
                    weaponIndex = 0;
                }
                else
                {
                    weaponIndex = weaponSlot.WeaponPrefabs.IndexOf(weaponSlot.CurrentWeaponPrefab) + 1;
                }
                break;
        }
        
        weaponSlot.WeaponSlotSwapCTS?.Cancel();
        
        weaponSlot.WeaponSlotSwapCTS = new CancellationTokenSource();
        weaponSwapTask = SwapWeapons(weaponSlot.WeaponSlotSwapCTS.Token, validationObject, weaponSlot, weaponIndex);
    }

    private async Task SwapWeapons(CancellationToken ct, GameObject validationObject, WeaponSlot weaponSlot, int weaponIndex)
    {
        if (validationObject != gameObject) return;
        if (weaponSlot == null) return;
        if (!weaponSlot.WeaponPrefabs[weaponIndex]) return;
        float oldWeaponUnequipTime = 0;
        
        // Get the old weapon and unregister it from the aiming system
        Weapon oldWeapon = weaponSlot.CurrentWeapon;
        
        if (oldWeapon)
        {
            OnUnregisterWeaponAiming?.Invoke(gameObject, oldWeapon.gameObject);
            weaponSlot.CurrentWeapon.ReleaseTrigger(gameObject);
        }
        
        // Prevent aiming and shooting until we have swapped weapons
        weaponSlot.SwappingWeapon = true;
        OnSetAimingAllowed?.Invoke(false);
        
        // todo save the old weapon!!
        // Remove the old weapon
        if (oldWeapon)
        {
            oldWeapon.gameObject.transform.DOKill();
            Destroy(oldWeapon.gameObject);
        
            oldWeapon.worldAimpointInstance.transform.DOKill();
            Destroy(oldWeapon.worldAimpointInstance);
        
            OnCleanupTargetLocks?.Invoke(oldWeapon);
            // remove all target

            oldWeaponUnequipTime = oldWeapon.weaponScriptableObject.weaponUnequipTime;
        }
        
        SetupAndInstantiateWeapon(weaponSlot, weaponSlot.WeaponPrefabs[(int)weaponIndex]);
        
        if (weaponSlot.name == "Spells")
        {
            OnCurrentSpellChange?.Invoke(weaponSlot.CurrentWeapon.weaponScriptableObject);
        }
        
        // Allow aiming and shooting again after a delay
        await MouseTools.AwaitableTimer(oldWeaponUnequipTime + weaponSlot.CurrentWeapon.weaponScriptableObject.weaponEquipTime);
        if (ct.IsCancellationRequested) return;
        
        weaponSlot.SwappingWeapon = false;
        OnSetAimingAllowed?.Invoke(true);
    }
    
    private void BeginGlobalCooldown(GameObject validationObject, float cooldown)
    {
        globalCooldownCTS?.Cancel();
        globalCooldownCTS = new CancellationTokenSource();
        globalCooldownTask = Cooldown(globalCooldownCTS.Token, allWeaponsOnGlobalCooldown, cooldown);
    }
    
    private void BeginWeaponSlotGlobalCooldown(GameObject validationObject, WeaponSlot weaponSlot, float cooldown)
    {
        weaponSlotCooldownCTS?.Cancel();
        weaponSlotCooldownCTS = new CancellationTokenSource();
        weaponSlotCooldownTask = Cooldown(globalCooldownCTS.Token, weaponSlot.SlotOnCooldown, cooldown);
    }

    private async Task Cooldown(CancellationToken ct, bool boolToManage, float cooldown)
    {
        boolToManage = false;
        Debug.Log("locked!");

        await MouseTools.AwaitableTimer(cooldown);
        
        if (ct.IsCancellationRequested) return;
        
        boolToManage = true;
        Debug.Log("unlocked!");
    }

    public WeaponSlot FindWeaponSlotByName(string name)
    {
        foreach (WeaponSlot slot in weaponSlots)
        {
            if (slot.name != name) continue;
            return slot;
        }
        
        return null;
    }
    
    private void AddWeaponToSlot(GameObject validationObject, string weaponSlotName, Weapon weaponToAdd, int index = -1, bool swapToNewWeapon = false)
    {
        if (validationObject != gameObject) return;
        WeaponSlot weaponSlot = FindWeaponSlotByName(weaponSlotName);
        
        if (weaponSlot.WeaponPrefabs.Count >= weaponSlot.MaxWeaponsInSlot) return;
        
        if (index != -1)
        {
            weaponSlot.WeaponPrefabs.Insert(index, weaponToAdd);
            return;
        }
        
        weaponSlot.WeaponPrefabs.Add(weaponToAdd);
        
        if (!swapToNewWeapon) return;
        BeginWeaponSwap(gameObject, weaponSlotName, weaponSlot.WeaponPrefabs.IndexOf(weaponToAdd));
    }
    
    private void RemoveWeaponFromSlot(GameObject validationObject, string weaponSlotName, Weapon targetWeapon, int index = -1)
    {
        if (validationObject != gameObject) return;
        Weapon weaponToRemove = null;
        WeaponSlot weaponSlot = FindWeaponSlotByName(weaponSlotName);

        // if an index is specified
        if (index != -1)
        {
            weaponToRemove = weaponSlot.WeaponPrefabs[index];
            weaponSlot.WeaponPrefabs.RemoveAt(index);

            if (weaponSlot.CurrentWeapon == weaponToRemove)
            {
                BeginWeaponCycle(gameObject, weaponSlotName, 1);
            }
            
            return;
        }
        
        // for now, this removes the first matching weapon - unsure if this works
        foreach (Weapon weapon in weaponSlot.WeaponPrefabs)
        {
            if (weapon.weaponScriptableObject.weaponName != targetWeapon.weaponScriptableObject.weaponName) continue;
            weaponToRemove = weapon;
        }
        
        weaponSlot.WeaponPrefabs.Remove(weaponToRemove);

        if (weaponSlot.CurrentWeapon == weaponToRemove)
        {
            BeginWeaponCycle(gameObject, weaponSlotName, 1);
        }
    }
    
    private void ReplaceTargetWeapon(GameObject validationObject, string weaponSlotName, Weapon replacement, Weapon target)
    {
        /*if (validationObject != gameObject) return;
        WeaponSlot weaponSlot = FindWeaponSlotByName(weaponSlotName);
        Weapon weaponToRemove;
        
        // for now, this removes the first matching weapon - unsure if this works
        foreach (Weapon weapon in weaponSlot.Weapons)
        {
            if (weapon.weaponScriptableObject.weaponComponent.name != target.weaponScriptableObject.weaponComponent.name) continue;
            weaponToRemove = weapon;
        }
        
        weaponToRemove*/
    }
    
    private void ReplaceWeaponInSlot(GameObject validationObject, string weaponSlotName, Weapon replacement, int index)
    {
        if (validationObject != gameObject) return;
        Weapon weaponToRemove = null;
        WeaponSlot weaponSlot = FindWeaponSlotByName(weaponSlotName);

        
        weaponToRemove = weaponSlot.WeaponPrefabs[index];
        weaponSlot.WeaponPrefabs.RemoveAt(index);
        
        weaponSlot.WeaponPrefabs.Insert(index, replacement);
        
        BeginWeaponSwap(gameObject, weaponSlotName, index);
    }
}
