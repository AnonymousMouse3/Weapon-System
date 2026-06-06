using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;
using MouseLib;
using MyBox;
using Spellslinger.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

[Serializable]
public class WeaponGroup
{  
    public string name;
    [ReadOnly] public Weapon CurrentWeapon;
    [ReadOnly] public Weapon CurrentWeaponPrefab;
    [ReadOnly] public bool WeaponGroupOnCooldown;
    [ReadOnly] public bool SwappingWeapon;
    public List<Weapon> WeaponPrefabs;
    public int MaxWeaponsInGroup;
    public GameObject WeaponGroupObject;
    public bool AimWithAimingSystem;
    public CancellationTokenSource WeaponGroupSwapCTS;
}

public class WeaponManager : MonoBehaviour
{
    public delegate void OnHandleWeaponInputs(GameObject validationObject, InputAction.CallbackContext context);
    public static OnHandleWeaponInputs onHandleWeaponInputs;
    public delegate void OnAddWeaponToGroup(GameObject validationObject, string weaponGroupName, Weapon weapon, int index, bool swapToNewWeapon = false);
    public static OnAddWeaponToGroup onAddWeaponToGroup;
    public delegate void OnRemoveWeaponFromGroup(GameObject validationObject, string weaponGroupName, Weapon weapon, int index);
    public static OnRemoveWeaponFromGroup onRemoveWeaponFromGroup;
    public delegate void OnReplaceTargetWeapon(GameObject validationObject, string weaponGroupName,  Weapon replacement, Weapon target);
    public static OnReplaceTargetWeapon onReplaceTargetWeapon;
    public delegate void OnReplaceWeaponInGroup(GameObject validationObject, string weaponGroupName, Weapon replacement, int index);
    public static OnReplaceWeaponInGroup onReplaceWeaponInGroup;
    public delegate void OnHandleWeaponReloadInputs(GameObject validationObject, InputAction action = null);
    public static OnHandleWeaponReloadInputs onHandleWeaponReloadInputs;
    public delegate void OnSwapWeapons(GameObject validationObject, string weaponGroupName, int weaponIndex);
    public static OnSwapWeapons onSwapWeapons;
    public delegate void OnCycleWeapons(GameObject validationObject, string weaponGroupName, int direction);
    public static OnCycleWeapons onCycleWeapons;
    public delegate void OnSwitchAmmoType(GameObject validationObject, GameObject newAmmoType, int weaponGroupIndex);
    public static OnSwitchAmmoType onSwitchAmmoType;
    public delegate void OnBeginGlobalCooldown(GameObject validationObject, float cooldown);
    public static OnBeginGlobalCooldown onBeginGlobalCooldown;
    public delegate void OnBeginWeaponGroupGlobalCooldown(GameObject validationObject, WeaponGroup weaponGroup, float cooldown);
    public static OnBeginWeaponGroupGlobalCooldown onBeginWeaponGroupGlobalCooldown;
    
    public static event Action<GameObject, GameObject> OnRegisterWeaponAiming;
    public static event Action<GameObject, GameObject> OnUnregisterWeaponAiming;
    public static event Action<bool> OnSetAimingAllowed;
    public static event Action<WeaponPart> OnCleanupTargetLocks;
    public static event Action<WeaponScriptableObject> OnCurrentSpellChange;
    public List<WeaponGroup> WeaponGroups
    {
        get => weaponGroups;
        set => weaponGroups  = value;
    }
    
    [SerializeField] private bool allWeaponsOnGlobalCooldown;
    [SerializeField] public List<WeaponGroup> weaponGroups;

    private Task weaponSwapTask;
    private Task globalCooldownTask;
    private CancellationTokenSource globalCooldownCTS;
    private Task weaponGroupCooldownTask;
    private CancellationTokenSource weaponGroupCooldownCTS;
    public bool weaponsUnloaded;
    
    #if SPELL_SYSTEM
    private HUDManager hudManager;
    #endif
    
    #if SQUADS
    private bool squadMode;
    #endif
    
    
    void OnEnable()
    {
        onHandleWeaponInputs += HandleWeaponInputs;
        onAddWeaponToGroup += AddWeaponToGroup;
        onRemoveWeaponFromGroup += RemoveWeaponFromGroup;
        onReplaceWeaponInGroup += ReplaceWeaponInGroup;
        onReplaceTargetWeapon += ReplaceTargetWeapon;
        onHandleWeaponReloadInputs += HandleWeaponReloadInputs;
        onSwapWeapons += BeginWeaponSwap;
        onCycleWeapons += BeginWeaponCycle;
        onBeginGlobalCooldown += BeginGlobalCooldown;
        onBeginWeaponGroupGlobalCooldown += BeginWeaponGroupGlobalCooldown;
    }

    void OnDisable()
    {
        onHandleWeaponInputs -= HandleWeaponInputs;
        onAddWeaponToGroup -= AddWeaponToGroup;
        onRemoveWeaponFromGroup -= RemoveWeaponFromGroup;
        onReplaceWeaponInGroup -= ReplaceWeaponInGroup;
        onReplaceTargetWeapon -= ReplaceTargetWeapon;
        onHandleWeaponReloadInputs -= HandleWeaponReloadInputs;
        onSwapWeapons -= BeginWeaponSwap;
        onCycleWeapons -= BeginWeaponCycle;
        onBeginGlobalCooldown -= BeginGlobalCooldown;
        onBeginWeaponGroupGlobalCooldown -= BeginWeaponGroupGlobalCooldown;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        #if SPELL_SYSTEM
        hudManager = FindObjectOfType<HUDManager>(); // bad
        #endif
        
        if (weaponGroups.IsNullOrEmpty()) return;

        foreach (WeaponGroup weaponGroup in weaponGroups)
        {
            if (weaponGroup.WeaponPrefabs.IsNullOrEmpty()) continue;
            if (weaponGroup.WeaponPrefabs[0] == null) continue;

            BeginWeaponSwap(gameObject, weaponGroup.name, 0); // swap to last saved weapon
        }
    }

    private void SetupAndInstantiateWeapon(WeaponGroup weaponGroup, Weapon weaponPrefab)
    {
        // Create the new weapon ingame
        GameObject newWeaponGameObject = Instantiate(weaponPrefab.gameObject, weaponGroup.WeaponGroupObject.transform);
        newWeaponGameObject.TryGetComponent(out Weapon weapon);
        
        // Get the relevant components and set relevant data in the weapon group
        weapon.transform.SetAsFirstSibling();
        weaponGroup.CurrentWeapon = weapon;
        weaponGroup.CurrentWeaponPrefab = weaponPrefab;

        weaponGroup.CurrentWeapon.weaponScriptableObject.weaponOwner = gameObject;
        
        foreach (WeaponPart weaponPart in weapon.weaponScriptableObject.WeaponParts)
        {
            if (!weaponPart.drawAimpoint || !weaponPart.aimpointIcon) continue;
            
            weaponPart.worldAimpointInstance = Instantiate(weaponPart.aimpointIcon);
            weaponPart.worldAimpointInstance.TryGetComponent(out weaponPart.worldAimpointInstanceImage);
        }
            
        if (!weaponGroup.AimWithAimingSystem) return;
        
        // Register the new weapon with the aiming system
        OnRegisterWeaponAiming?.Invoke(gameObject, newWeaponGameObject);
    }
    
    public void LoadAllExistingWeapons()
    {
        foreach (WeaponGroup group in WeaponGroups)
        {
            Weapon weapon = group.CurrentWeapon;
            
            OnRegisterWeaponAiming?.Invoke(gameObject, weapon.gameObject);
            weapon.gameObject.SetActive(true);

            foreach (WeaponPart weaponPart in weapon.weaponScriptableObject.WeaponParts)
            {
                if (!weaponPart.drawAimpoint || !weaponPart.worldAimpointInstance) continue;
                weaponPart.worldAimpointInstance.SetActive(true);
            }
            weaponsUnloaded = false;
        }
    }

    public void UnloadAllWeapons()
    {
        foreach (WeaponGroup group in WeaponGroups)
        {
            Weapon oldWeapon = group.CurrentWeapon;
            
            OnUnregisterWeaponAiming?.Invoke(gameObject, oldWeapon.gameObject);
            group.CurrentWeapon.ReleaseTrigger(gameObject);
            
            oldWeapon.gameObject.transform.DOKill();
            oldWeapon.gameObject.SetActive(false);
        
            foreach (WeaponPart weaponPart in oldWeapon.weaponScriptableObject.WeaponParts)
            {
                OnCleanupTargetLocks?.Invoke(weaponPart);
                
                if (!weaponPart.drawAimpoint || !weaponPart.worldAimpointInstance) continue;
                weaponPart.worldAimpointInstance.transform.DOKill();
                weaponPart.worldAimpointInstance.SetActive(false);
            }
            
            // remove all target
            weaponsUnloaded = true;
        }
    }

    // feed inputs to the weapon and feed only the right type of input (press or release, etc)
    // do NOT perform weapon condition checks here (ammo checks, mana checks), these go in Weapon
    private void HandleWeaponInputs(GameObject validationObject, InputAction.CallbackContext context)
    {
        if (validationObject != gameObject) return;
        
        foreach (WeaponGroup weaponGroup in weaponGroups)
        {
            if (weaponGroup.WeaponPrefabs.IsNullOrEmpty()) continue;
            
            /*if (weaponGroup.Action)
            {
                if (weaponGroup.Action.ToInputAction() != action) continue;
            }*/

            if (weaponGroup.SwappingWeapon) return;
            if (weaponGroup.WeaponGroupOnCooldown) return;
            
            weaponGroup.CurrentWeapon.ProcessWeaponAction(gameObject, context);
        }
    }

    private void HandleWeaponReloadInputs(GameObject validationObject, InputAction action)
    {
        if (validationObject != gameObject) return;
        
        foreach (WeaponGroup weaponGroup in weaponGroups)
        {
            if (weaponGroup.WeaponPrefabs.IsNullOrEmpty()) continue;

            if (weaponGroup.SwappingWeapon) return;
            if (weaponGroup.WeaponGroupOnCooldown) return;
            
            weaponGroup.CurrentWeapon.ProcessWeaponReloadAction(gameObject, action);
        }
    }

    private async void BeginWeaponSwap(GameObject validationObject, string weaponGroupName, int weaponIndex)
    {
        if (validationObject != gameObject) return;
        // Select the weapon group of interest
        WeaponGroup weaponGroup = FindWeaponGroupByName(weaponGroupName);
        if (weaponGroup == null) return;
        
        weaponGroup.WeaponGroupSwapCTS?.Cancel();
        
        weaponGroup.WeaponGroupSwapCTS = new CancellationTokenSource();
        weaponSwapTask = SwapWeapons(weaponGroup.WeaponGroupSwapCTS.Token, validationObject, weaponGroup, weaponIndex);
    }
    
    private void BeginWeaponCycle(GameObject validationObject, string weaponGroupName, int direction)
    {
        if (validationObject != gameObject) return;
        // Select the weapon group of interest
        WeaponGroup weaponGroup = FindWeaponGroupByName(weaponGroupName);
        if (weaponGroup == null) return;

        int weaponIndex = 0;
        
        switch (direction)
        {
            // Cycle backwards
            case -1:
                // If we find the lower end of the array, wrap around to the top
                // Otherwise, swap to the spell below the current spell
                if (weaponGroup.WeaponPrefabs.IndexOf(weaponGroup.CurrentWeaponPrefab) - 1 < 0)
                {
                    weaponIndex = weaponGroup.WeaponPrefabs.Count - 1;
                }
                else
                {
                    weaponIndex = weaponGroup.WeaponPrefabs.IndexOf(weaponGroup.CurrentWeaponPrefab) - 1;
                }
                break;
            
            // Cycle forwards
            case 1:
                // If we find the upper end of the array, wrap around to the bottom
                // Otherwise, swap to the spell above the current spell
                if (weaponGroup.WeaponPrefabs.IndexOf(weaponGroup.CurrentWeaponPrefab) + 1 >= weaponGroup.WeaponPrefabs.Count)
                {
                    weaponIndex = 0;
                }
                else
                {
                    weaponIndex = weaponGroup.WeaponPrefabs.IndexOf(weaponGroup.CurrentWeaponPrefab) + 1;
                }
                break;
        }
        
        weaponGroup.WeaponGroupSwapCTS?.Cancel();
        
        weaponGroup.WeaponGroupSwapCTS = new CancellationTokenSource();
        weaponSwapTask = SwapWeapons(weaponGroup.WeaponGroupSwapCTS.Token, validationObject, weaponGroup, weaponIndex);
    }

    private async Task SwapWeapons(CancellationToken ct, GameObject validationObject, WeaponGroup weaponGroup, int weaponIndex)
    {
        if (validationObject != gameObject) return;
        if (weaponGroup == null) return;
        if (!weaponGroup.WeaponPrefabs[weaponIndex]) return;
        float oldWeaponUnequipTime = 0;
        
        // Get the old weapon and unregister it from the aiming system
        Weapon oldWeapon = weaponGroup.CurrentWeapon;
        
        if (oldWeapon)
        {
            OnUnregisterWeaponAiming?.Invoke(gameObject, oldWeapon.gameObject);
            weaponGroup.CurrentWeapon.ReleaseTrigger(gameObject);
        }
        
        // Prevent aiming and shooting until we have swapped weapons
        weaponGroup.SwappingWeapon = true;
        OnSetAimingAllowed?.Invoke(false);
        
        // todo save the old weapon!!
        // Remove the old weapon
        if (oldWeapon)
        {
            oldWeapon.gameObject.transform.DOKill();
            Destroy(oldWeapon.gameObject);
            
            foreach (WeaponPart weaponPart in oldWeapon.weaponScriptableObject.WeaponParts)
            {
                OnCleanupTargetLocks?.Invoke(weaponPart);
                
                if (!weaponPart.drawAimpoint || !weaponPart.worldAimpointInstance) continue;
                weaponPart.worldAimpointInstance.transform.DOKill();
                Destroy(weaponPart.worldAimpointInstance);
            }
        
            // remove all target

            oldWeaponUnequipTime = oldWeapon.weaponScriptableObject.weaponUnequipTime;
        }
        
        SetupAndInstantiateWeapon(weaponGroup, weaponGroup.WeaponPrefabs[(int)weaponIndex]);
        
        if (weaponGroup.name == "Spells")
        {
            OnCurrentSpellChange?.Invoke(weaponGroup.CurrentWeapon.weaponScriptableObject);
        }
        
        // Allow aiming and shooting again after a delay
        await MouseTools.AwaitableTimer(oldWeaponUnequipTime + weaponGroup.CurrentWeapon.weaponScriptableObject.weaponEquipTime);
        if (ct.IsCancellationRequested) return;
        
        weaponGroup.SwappingWeapon = false;
        OnSetAimingAllowed?.Invoke(true);
    }
    
    private void BeginGlobalCooldown(GameObject validationObject, float cooldown)
    {
        globalCooldownCTS?.Cancel();
        globalCooldownCTS = new CancellationTokenSource();
        globalCooldownTask = Cooldown(globalCooldownCTS.Token, allWeaponsOnGlobalCooldown, cooldown);
    }
    
    private void BeginWeaponGroupGlobalCooldown(GameObject validationObject, WeaponGroup weaponGroup, float cooldown)
    {
        weaponGroupCooldownCTS?.Cancel();
        weaponGroupCooldownCTS = new CancellationTokenSource();
        weaponGroupCooldownTask = Cooldown(globalCooldownCTS.Token, weaponGroup.WeaponGroupOnCooldown, cooldown);
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

    public WeaponGroup FindWeaponGroupByName(string name)
    {
        foreach (WeaponGroup group in weaponGroups)
        {
            if (group.name != name) continue;
            return group;
        }
        
        return null;
    }
    
    private void AddWeaponToGroup(GameObject validationObject, string weaponGroupName, Weapon weaponToAdd, int index = -1, bool swapToNewWeapon = false)
    {
        if (validationObject != gameObject) return;
        WeaponGroup weaponGroup = FindWeaponGroupByName(weaponGroupName);
        
        if (weaponGroup.WeaponPrefabs.Count >= weaponGroup.MaxWeaponsInGroup) return;
        
        if (index != -1)
        {
            weaponGroup.WeaponPrefabs.Insert(index, weaponToAdd);
            return;
        }
        
        weaponGroup.WeaponPrefabs.Add(weaponToAdd);
        
        if (!swapToNewWeapon) return;
        BeginWeaponSwap(gameObject, weaponGroupName, weaponGroup.WeaponPrefabs.IndexOf(weaponToAdd));
    }
    
    private void RemoveWeaponFromGroup(GameObject validationObject, string weaponGroupName, Weapon targetWeapon, int index = -1)
    {
        if (validationObject != gameObject) return;
        Weapon weaponToRemove = null;
        WeaponGroup weaponGroup = FindWeaponGroupByName(weaponGroupName);

        // if an index is specified
        if (index != -1)
        {
            weaponToRemove = weaponGroup.WeaponPrefabs[index];
            weaponGroup.WeaponPrefabs.RemoveAt(index);

            if (weaponGroup.CurrentWeapon == weaponToRemove)
            {
                BeginWeaponCycle(gameObject, weaponGroupName, 1);
            }
            
            return;
        }
        
        // for now, this removes the first matching weapon - unsure if this works
        foreach (Weapon weapon in weaponGroup.WeaponPrefabs)
        {
            if (weapon.weaponScriptableObject.weaponName != targetWeapon.weaponScriptableObject.weaponName) continue;
            weaponToRemove = weapon;
        }
        
        weaponGroup.WeaponPrefabs.Remove(weaponToRemove);

        if (weaponGroup.CurrentWeapon == weaponToRemove)
        {
            BeginWeaponCycle(gameObject, weaponGroupName, 1);
        }
    }
    
    private void ReplaceTargetWeapon(GameObject validationObject, string weaponGroupName, Weapon replacement, Weapon target)
    {
        /*if (validationObject != gameObject) return;
        WeaponGroup weaponGroup = FindWeaponGroupByName(weaponGroupName);
        Weapon weaponToRemove;
        
        // for now, this removes the first matching weapon - unsure if this works
        foreach (Weapon weapon in weaponGroup.Weapons)
        {
            if (weapon.weaponScriptableObject.weaponComponent.name != target.weaponScriptableObject.weaponComponent.name) continue;
            weaponToRemove = weapon;
        }
        
        weaponToRemove*/
    }
    
    private void ReplaceWeaponInGroup(GameObject validationObject, string weaponGroupName, Weapon replacement, int index)
    {
        if (validationObject != gameObject) return;
        Weapon weaponToRemove = null;
        WeaponGroup weaponGroup = FindWeaponGroupByName(weaponGroupName);

        
        weaponToRemove = weaponGroup.WeaponPrefabs[index];
        weaponGroup.WeaponPrefabs.RemoveAt(index);
        
        weaponGroup.WeaponPrefabs.Insert(index, replacement);
        
        BeginWeaponSwap(gameObject, weaponGroupName, index);
    }
}
