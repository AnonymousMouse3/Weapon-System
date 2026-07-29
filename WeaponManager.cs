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
    [FormerlySerializedAs("name")] public string weaponGroupName;
    [ReadOnly] public Weapon CurrentWeapon;
    //[ReadOnly] public Weapon CurrentWeaponPrefab;
    [ReadOnly] public bool WeaponGroupOnCooldown;
    [ReadOnly] public bool SwappingWeapon;
    [FormerlySerializedAs("WeaponPrefabs")] public List<Weapon> Weapons;
    public int MaxWeaponsInGroup;
    public GameObject WeaponGroupObject;
    public bool AimWithAimingSystem;
    public CancellationTokenSource WeaponGroupSwapCTS;
}

public class WeaponManager : MonoBehaviour
{
    public delegate void OnHandleWeaponInputs(GameObject validationObject, InputAction.CallbackContext context);
    public static OnHandleWeaponInputs onHandleWeaponInputs;
    public delegate void OnAddWeaponToGroup(GameObject validationObject, string weaponGroupName, Weapon weapon, int index = -1, bool swapToNewWeapon = false);
    public static OnAddWeaponToGroup onAddWeaponToGroup;
    public delegate void OnRemoveWeaponFromGroup(GameObject validationObject, string weaponGroupName, Weapon weapon, int index);
    public static OnRemoveWeaponFromGroup onRemoveWeaponFromGroup;
    public delegate void OnReplaceTargetWeapon(GameObject validationObject, string weaponGroupName,  Weapon replacement, Weapon target);
    public static OnReplaceTargetWeapon onReplaceTargetWeapon;
    public delegate void OnReplaceWeaponInGroup(GameObject validationObject, string weaponGroupName, Weapon replacement, int index, bool swapToNewWeapon = false);
    public static OnReplaceWeaponInGroup onReplaceWeaponInGroup;
    public delegate void OnHandleWeaponReloadInputs(GameObject validationObject, InputAction action = null);
    public static OnHandleWeaponReloadInputs onHandleWeaponReloadInputs;
    public delegate void OnSwapWeapons(GameObject validationObject, string weaponGroupName, int weaponIndex);
    public static OnSwapWeapons onSwapWeapons;
    public delegate void OnCycleWeapons(GameObject validationObject, string weaponGroupName, int direction, int overrideIndex = 0);
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
        onReplaceWeaponInGroup += ReplaceWeapon;
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
        onReplaceWeaponInGroup -= ReplaceWeapon;
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
            CreateAllWeaponsInGroup(weaponGroup);
        }
    }

    void Start()
    {
        foreach (WeaponGroup weaponGroup in weaponGroups)
        {
            if (weaponGroup.Weapons.IsNullOrEmpty()) continue;
            if (weaponGroup.Weapons[0] == null) continue;

            BeginWeaponSwap(gameObject, weaponGroup.weaponGroupName, 0); // swap to last saved weapon
        }
    }

    // for instantiating a weapon
    public Weapon CreateWeapon(WeaponGroup weaponGroup, Weapon weaponPrefab, bool addToGroup = true, int index = -1)
    {
        // Create the new weapon ingame
        GameObject newWeaponGameObject = Instantiate(weaponPrefab.gameObject, weaponGroup.WeaponGroupObject.transform);
        newWeaponGameObject.SetActive(false);
        newWeaponGameObject.TryGetComponent(out Weapon weapon);

        if (!addToGroup) return weapon;
        
        if (weaponGroup.Weapons.Contains(weaponPrefab)) weaponGroup.Weapons[weaponGroup.Weapons.IndexOf(weaponPrefab)] = weapon;
        else weaponGroup.Weapons.Add(weapon);
        
        return weapon;
    }

    // for destroying an instantiated weapon -- use with caution
    public void DestroyWeapon(WeaponGroup weaponGroup, Weapon weapon, int index = -1)
    {
        // todo save the old weapon!!
        DisableWeapon(weaponGroup, weapon);
        weaponGroup.Weapons.Remove(weapon);
        Destroy(weapon.gameObject);
    }

    // for destroying an instantiated weapon and replacing it with a new instantiated weapon
    public void ReplaceWeapon(GameObject validationObject, string weaponGroupName, Weapon replacement, int index, bool swapToNewWeapon = false)
    {
        if (validationObject != gameObject) return;
        WeaponGroup weaponGroup = FindWeaponGroupByName(weaponGroupName);
        Weapon original = weaponGroup.Weapons[index];
        
        DisableWeapon(weaponGroup, original);
        
        // Create the new weapon ingame
        GameObject newWeaponGameObject = Instantiate(replacement.gameObject, weaponGroup.WeaponGroupObject.transform);
        newWeaponGameObject.SetActive(false);
        newWeaponGameObject.TryGetComponent(out Weapon weapon);

        weaponGroup.Weapons[index] = weapon;
        
        if (swapToNewWeapon || original == weaponGroup.CurrentWeapon) BeginWeaponSwap(gameObject, weaponGroup.weaponGroupName, index);

        Destroy(original.gameObject);
    }

    // for swapping weapons, or enabling a weapon that is already instantiated. does not create a new gameobject
    private void EnableWeapon(WeaponGroup weaponGroup, Weapon weapon)
    {
        // Get the relevant components and set relevant data in the weapon group
        weapon.gameObject.SetActive(true);
        weapon.transform.SetAsFirstSibling();
        weaponGroup.CurrentWeapon = weapon;
        //weaponGroup.CurrentWeaponPrefab = weapon.gameObject;

        weaponGroup.CurrentWeapon.weaponScriptableObject.weaponOwner = gameObject;
        
        foreach (WeaponPart weaponPart in weapon.weaponScriptableObject.WeaponParts)
        {
            if (!weaponPart.drawAimpoint || !weaponPart.aimpointIcon) continue;
            
            weaponPart.worldAimpointInstance = Instantiate(weaponPart.aimpointIcon);
            weaponPart.worldAimpointInstance.TryGetComponent(out weaponPart.worldAimpointInstanceImage);
        }
            
        if (!weaponGroup.AimWithAimingSystem) return;
        
        // Register the new weapon with the aiming system
        OnRegisterWeaponAiming?.Invoke(gameObject, weapon.gameObject);
        
        if (weaponGroup.weaponGroupName == "Spells")
        {
            OnCurrentSpellChange?.Invoke(weaponGroup.CurrentWeapon.weaponScriptableObject);
        }
    }

    // for deactivating an instantiated weapon, for weapon swapping, etc. does not unload/destroy the weapon
    public void DisableWeapon(WeaponGroup weaponGroup, Weapon weapon)
    {
        OnUnregisterWeaponAiming?.Invoke(gameObject, weapon.gameObject);
        
        weaponGroup.CurrentWeapon.ReleaseTrigger(gameObject);
        weapon.gameObject.transform.DOKill();
        weapon.gameObject.SetActive(false);
            
        foreach (WeaponPart weaponPart in weapon.weaponScriptableObject.WeaponParts)
        {
            OnCleanupTargetLocks?.Invoke(weaponPart);

            if (!weaponPart.drawAimpoint || !weaponPart.worldAimpointInstance) continue;
            weaponPart.worldAimpointInstance.transform.DOKill();
            Destroy(weaponPart.worldAimpointInstance);
        }
        
        if (weaponGroup.weaponGroupName == "Spells")
        {
            OnCurrentSpellChange?.Invoke(weaponGroup.CurrentWeapon.weaponScriptableObject);
        }
    }

    public void CreateAllWeaponsInGroup(WeaponGroup weaponGroup)
    {
        List<Weapon> instantiatedWeapons = new List<Weapon>();

        foreach (Weapon weapon in weaponGroup.Weapons)
        {
            Weapon newWeapon = CreateWeapon(weaponGroup, weapon, false);
            instantiatedWeapons.Add(newWeapon);
        }
        
        weaponGroup.Weapons = instantiatedWeapons;
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
    
    private void BeginWeaponCycle(GameObject validationObject, string weaponGroupName, int direction, int overrideIndex = 0)
    {
        if (validationObject != gameObject) return;
        // Select the weapon group of interest
        WeaponGroup weaponGroup = FindWeaponGroupByName(weaponGroupName);
        if (weaponGroup == null) return;

        int weaponIndex = 0;
        int currentIndex = weaponGroup.Weapons.IndexOf(weaponGroup.CurrentWeapon);
        int groupCount = weaponGroup.Weapons.Count;
        
        if (overrideIndex != 0) currentIndex = overrideIndex;

        direction = Mathf.Clamp(direction, -1, 1);
        
        switch (direction)
        {
            // Cycle backwards
            case -1:
                // If we find the lower end of the array, wrap around to the top
                // Otherwise, swap to the spell below the current spell
                if (currentIndex == 0) { weaponIndex = groupCount - 1; break; }
                weaponIndex = currentIndex - 1;
                break;
            
            // Cycle forwards
            case 1:
                // If we find the upper end of the array, wrap around to the bottom
                // Otherwise, swap to the spell above the current spell
                if (currentIndex == groupCount - 1) { weaponIndex = 0; break; }
                weaponIndex = currentIndex + 1;
                break;
        }
        
        // if a weapon cannot be selected, skip it and cycle again
        if (weaponGroup.Weapons[weaponIndex].weaponScriptableObject.cannotBeSelected)
        {
            int count = 0;
            foreach (Weapon weapon in weaponGroup.Weapons)
            {
                if (!weapon.weaponScriptableObject.cannotBeSelected) count++;
            }
            // if one weapon or less can be selected, do not cycle as it will cause a loop
            if (count <= 1) return;
            
            BeginWeaponCycle(validationObject, weaponGroupName, direction, weaponIndex);
            return;
        }
        
        weaponGroup.WeaponGroupSwapCTS?.Cancel();
        
        weaponGroup.WeaponGroupSwapCTS = new CancellationTokenSource();
        weaponSwapTask = SwapWeapons(weaponGroup.WeaponGroupSwapCTS.Token, validationObject, weaponGroup, weaponIndex);
    }

    private async Task SwapWeapons(CancellationToken ct, GameObject validationObject, WeaponGroup weaponGroup, int weaponIndex)
    {
        if (validationObject != gameObject || weaponGroup == null || weaponIndex < 0) return;
        if (weaponGroup.Weapons[weaponIndex].weaponScriptableObject.cannotBeSelected) return;
        if (weaponGroup.CurrentWeapon == weaponGroup.Weapons[weaponIndex]) return;
        
        //float oldWeaponUnequipTime = 0;
        
        // Get the old weapon and disable it
        Weapon oldWeapon = weaponGroup.CurrentWeapon;
        if (oldWeapon) DisableWeapon(weaponGroup, oldWeapon);
        
        // Prevent aiming and shooting until we have swapped weapons
        weaponGroup.SwappingWeapon = true;
        OnSetAimingAllowed?.Invoke(false);
        
        EnableWeapon(weaponGroup, weaponGroup.Weapons[weaponIndex]);
        
        // Allow aiming and shooting again after a delay
        await MouseTools.AwaitableTimer(/*oldWeaponUnequipTime +*/weaponGroup.CurrentWeapon.weaponScriptableObject.weaponEquipTime);
        if (ct.IsCancellationRequested) return;
        
        weaponGroup.SwappingWeapon = false;
        OnSetAimingAllowed?.Invoke(true);
        
        #if SPELL_SYSTEM
        OnCurrentSpellChange?.Invoke(weaponGroup.Weapons[weaponIndex].weaponScriptableObject);
        #endif
    }

    // feed inputs to the weapon and feed only the right type of input (press or release, etc)
    // do NOT perform weapon condition checks here (ammo checks, mana checks), these go in Weapon
    private void HandleWeaponInputs(GameObject validationObject, InputAction.CallbackContext context)
    {
        if (validationObject != gameObject) return;
        
        foreach (WeaponGroup weaponGroup in weaponGroups)
        {
            if (weaponGroup.Weapons.IsNullOrEmpty()) continue;
            
            /*if (weaponGroup.Action)
            {
                if (weaponGroup.Action.ToInputAction() != action) continue;
            }*/

            if (weaponGroup.SwappingWeapon) return;
            if (weaponGroup.WeaponGroupOnCooldown) return;

            weaponGroup.CurrentWeapon.ProcessWeaponFunction(context, context.performed);
        }
    }

    private void HandleWeaponReloadInputs(GameObject validationObject, InputAction action)
    {
        if (validationObject != gameObject) return;
        
        foreach (WeaponGroup weaponGroup in weaponGroups)
        {
            if (weaponGroup.Weapons.IsNullOrEmpty()) continue;

            if (weaponGroup.SwappingWeapon) return;
            if (weaponGroup.WeaponGroupOnCooldown) return;
            
            weaponGroup.CurrentWeapon.ProcessWeaponReloadAction(gameObject, action);
        }
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
            if (group.weaponGroupName != name) continue;
            return group;
        }
        
        return null;
    }
    
    private void AddWeaponToGroup(GameObject validationObject, string weaponGroupName, Weapon weaponToAdd, int index = -1, bool swapToNewWeapon = false)
    {
        if (validationObject != gameObject) return;
        WeaponGroup weaponGroup = FindWeaponGroupByName(weaponGroupName);
        
        if (weaponGroup.Weapons.Count >= weaponGroup.MaxWeaponsInGroup) return;
        
        if (index != -1)
        {
            CreateWeapon(weaponGroup, weaponToAdd, true, index);
        }

        CreateWeapon(weaponGroup, weaponToAdd);
        
        if (!swapToNewWeapon) return;
        BeginWeaponSwap(gameObject, weaponGroupName, weaponGroup.Weapons.IndexOf(weaponToAdd));
    }
    
    private void RemoveWeaponFromGroup(GameObject validationObject, string weaponGroupName, Weapon targetWeapon, int index = -1)
    {
        if (validationObject != gameObject) return;
        Weapon weaponToRemove = null;
        WeaponGroup weaponGroup = FindWeaponGroupByName(weaponGroupName);

        // if an index is specified
        if (index != -1)
        {
            weaponToRemove = weaponGroup.Weapons[index];
            weaponGroup.Weapons.RemoveAt(index);

            if (weaponGroup.CurrentWeapon == weaponToRemove)
            {
                BeginWeaponCycle(gameObject, weaponGroupName, 1);
            }
            
            return;
        }
        
        // for now, this removes the first matching weapon - unsure if this works
        foreach (Weapon weapon in weaponGroup.Weapons)
        {
            if (weapon.weaponScriptableObject.weaponName != targetWeapon.weaponScriptableObject.weaponName) continue;
            weaponToRemove = weapon;
        }
        
        weaponGroup.Weapons.Remove(weaponToRemove);

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
}
