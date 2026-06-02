using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;
using MouseLib;
using MyBox;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// OLD INACCURATE DESCRIPTION
// The Weapon script is the top of the weapon system "stack"
// It contains an instance of a WeaponObject, to serve as the weapon's instance at runtime
// It also contains values only present on the weapon's prefab (which can't be set on the WeaponObject)
// It contains the logic for firing/using the weapon (e.g. handling ammo), and the inputs

// To create a functional weapon, make a gameobject with the Weapon script attached
// Create a FirePoint gameobject (wherever the projectiles should spawn) and add it to the Weapon
// Then create a new WeaponScriptableObject, fill out the weapon's stats, and also add it to the Weapon script
// Then, just hook up inputs to the PullTrigger and ReleaseTrigger methods to use the weapon. do the same for reload, etc
// todo rewrite this because inputs are now handled through weaponmanager to allow for multiple weapons etc
[Serializable]
public class Weapon : MonoBehaviour
{
    public delegate void OnReloadWeapon(WeaponPart weaponPart, GameObject validationObject);
    public static OnReloadWeapon onReloadWeapon;
    
    public static event Action OnWeaponShoot;
    public static event Action<Weapon> OnWeaponRelease;
    public static event Action<WeaponPart, float> OnAttackSpeedModifierChange;
    public static event Action<GameObject, WeaponPart> OnSpellCast;
    public static event Action<GameObject, WeaponPart> OnAttemptSpellCast;
    
    public List<GameObject> prefabFirePoints;
    [DisplayInspector] public WeaponScriptableObject weaponScriptableObject;

    private void OnEnable()
    {
        #if SPELL_SYSTEM
        OnAttackSpeedModifierChange += OnFireRateChange;
        #endif

        onReloadWeapon += StartReload;
    }

    private void OnDisable()
    {
        #if SPELL_SYSTEM
        OnAttackSpeedModifierChange -= OnFireRateChange;
        #endif
        
        onReloadWeapon -= StartReload;
    }

    private void Start()
    {
        // Create an instance of the WeaponObject so we don't affect the base stats
        WeaponScriptableObject newWeaponScriptableObject = Instantiate(weaponScriptableObject);
        weaponScriptableObject = newWeaponScriptableObject;
        
        List<WeaponPart> newWeaponParts = new List<WeaponPart>();
        foreach (WeaponPart weaponPart in weaponScriptableObject.WeaponParts)
        {
            WeaponPart newWeaponPart = Instantiate(weaponPart);
            newWeaponParts.Add(newWeaponPart);

            foreach (WeaponAction weaponAction in weaponScriptableObject.WeaponActions)
            {
                if (weaponAction.weaponPart != weaponPart) continue;
                weaponAction.weaponPart = newWeaponPart;
            }
        }

        weaponScriptableObject.WeaponParts = newWeaponParts;
        
        #if SPELL_SYSTEM
        weaponScriptableObject.weaponOwner.TryGetComponent(out weaponScriptableObject.spellManager);
        #endif

        foreach (WeaponPart weaponPart in weaponScriptableObject.WeaponParts)
        {
            weaponPart.firePoint = prefabFirePoints[weaponScriptableObject.WeaponParts.IndexOf(weaponPart)];
            weaponPart.SetupWeaponPart(weaponScriptableObject);
        }

        foreach (WeaponAction weaponAction in weaponScriptableObject.WeaponActions)
        {
            foreach (WeaponActionCondition weaponActionCondition in weaponAction.ActionConditions)
            {
                if (weaponActionCondition.ConditionType != WeaponActionCondition.WeaponActionConditionType.ChargedForTime) continue;

                InputAction action = weaponAction.InputActionListenedTo.action;
                
                string overrideHoldInteraction = $"Hold(duration={weaponActionCondition.ChargeTime})";

                for (int i = 0; i < action.bindings.Count; i++)
                {
                    action.ApplyBindingOverride(i, new InputBinding { overrideInteractions = overrideHoldInteraction });
                }
            }
        }
    }

    public void ShootWeaponDirectly(GameObject validationObject, bool pressOrRelease)
    {
        if (validationObject != weaponScriptableObject.weaponOwner) return;

        // shoots the first weapon part directly - use for simple weapons for now, expand in future
        WeaponPart currentWeaponPart = weaponScriptableObject.WeaponParts[0];
        if (!currentWeaponPart) return;
        
        if (pressOrRelease)
        {
            currentWeaponPart.isTriggerPulled = true;
            TryFireWeaponLoop(currentWeaponPart);
            // set weapon action to completed
        }
        else
        {
            currentWeaponPart.isTriggerPulled = false;
            OnWeaponRelease?.Invoke(this);
        }
    }

    public void ProcessWeaponAction(GameObject validationObject, InputAction action, bool pressOrRelease)
    {
        if (validationObject != weaponScriptableObject.weaponOwner) return;

        WeaponPart currentWeaponPart = null;
        WeaponAction currentWeaponAction = null;

        foreach (WeaponAction weaponAction in weaponScriptableObject.WeaponActions)
        {
            if (!weaponAction.InputActionListenedTo) continue;
            if (weaponAction.InputActionListenedTo.action != action) continue;
            
            currentWeaponPart = CheckWeaponActionConditions(weaponAction);
        }
        
        if (!currentWeaponPart) return;
        
        if (pressOrRelease)
        {
            currentWeaponPart.isTriggerPulled = true;
            TryFireWeaponLoop(currentWeaponPart);
            // set weapon action to completed
        }
        else
        {
            currentWeaponPart.isTriggerPulled = false;
            OnWeaponRelease?.Invoke(this);
        }
    }

    public void ProcessWeaponReloadAction(GameObject validationObject, InputAction action)
    {
        foreach (WeaponPart weaponPart in weaponScriptableObject.WeaponParts)
        {
            if (weaponPart.reloadAction.action != action) continue;
            StartReload(weaponPart, weaponScriptableObject.weaponOwner);
        }
    }

    public void ReleaseTrigger(GameObject validationObject)
    {
        if (validationObject != weaponScriptableObject.weaponOwner) return;
        
        foreach (WeaponPart weaponPart in weaponScriptableObject.WeaponParts)
        {
            weaponPart.isTriggerPulled = false;
            OnWeaponRelease?.Invoke(this);
        }
    }

    private WeaponPart CheckWeaponActionConditions(WeaponAction weaponAction)
    {
        foreach (WeaponActionCondition actionCondition in weaponAction.ActionConditions)
        {
            switch (actionCondition.ConditionType)
            {
                case WeaponActionCondition.WeaponActionConditionType.Nothing:
                    return weaponAction.weaponPart;
                
                case WeaponActionCondition.WeaponActionConditionType.AnyProjectileActive:
                    if (!AnyProjectileActiveCheck(weaponAction)) return null;
                    
                    return weaponAction.weaponPart;
                
                case WeaponActionCondition.WeaponActionConditionType.ChargedForTime:
                    
                    break;
                
                case WeaponActionCondition.WeaponActionConditionType.ActionComplete:
                    if (!ActionCompleteCheck(actionCondition)) return null;
                    
                    return weaponAction.weaponPart;
                
                case WeaponActionCondition.WeaponActionConditionType.ActionIncomplete:
                    if(ActionIncompleteCheck(actionCondition)) return null;
                    
                    return weaponAction.weaponPart;
            }
        }

        return null;
    }

    private bool AnyProjectileActiveCheck(WeaponAction weaponAction)
    {
        if (weaponAction.weaponPart.projectilePrefabs.IsNullOrEmpty()) return false;
        
        return true;
    }

    private void ChargeTimeCheck()
    {
        
    }

    private bool ActionCompleteCheck(WeaponActionCondition actionCondition)
    {
        if (actionCondition.ActionToMonitor.ActionComplete) return true;
        
        return false;
    }
    
    private bool ActionIncompleteCheck(WeaponActionCondition actionCondition)
    {
        if (!actionCondition.ActionToMonitor.ActionComplete) return true;
        
        return false;
    }

    private async void TryFireWeaponLoop(WeaponPart weaponPart)
    {
        if (!weaponPart.cycleTask.IsCompleted) { await weaponPart.cycleTask; }
        if (!weaponPart.reloadTask.IsCompleted) { await weaponPart.reloadTask; }
        
        if (weaponPart.cycleTask.IsCompleted && weaponPart.reloadTask.IsCompleted)
        {
            // Prevent the loop from looping too fast
            await MouseTools.AwaitableTimer(0.001f);
        }

        if (!weaponPart.isTriggerPulled)
        {
            weaponPart.burstCounter = 0;
            return;
        }
        
        switch (weaponPart.currentFireMode)
        {
            case WeaponPart.FireModes.SemiAuto:
                TryFireWeapon(weaponPart);
                break;
            
            case WeaponPart.FireModes.Burst:
                TryFireWeapon(weaponPart);
                weaponPart.burstCounter++;

                if (weaponPart.burstCounter < weaponPart.burstLength)
                {
                    TryFireWeaponLoop(weaponPart);
                    return;
                }
                
                weaponPart.burstCounter = 0;
                
                break;
            
            case WeaponPart.FireModes.FullAuto:
                TryFireWeapon(weaponPart);
                
                TryFireWeaponLoop(weaponPart);
                break;
        }
    }
    
    private void TryFireWeapon(WeaponPart weaponPart)
    {
        // If the weapon is cycling between shots or reloading, it cannot fire
        if (weaponPart.firingState == WeaponPart.FiringState.Cycling) { CouldNotFire("weapon still cycling!"); return; }
    
        if (weaponPart.reloadState == WeaponPart.ReloadState.Reloading) { CouldNotFire("weapon reloading!"); return; }
        
        if (weaponPart.usesAmmo)
        {
            // If the weapon has a chamber, and it is not loaded, the weapon cannot fire
            if (weaponPart.hasChamber)
            {
                if (!weaponPart.isChamberLoaded) { CouldNotFire("chamber not loaded"); return; }
            }

            // If the weapon does not have a chamber, only a magazine, canister, etc. (e.g. revolvers, flamethrowers)
            // Then the weapon cannot fire if it is empty
            // Note that it must NOT have a chamber for this to be true. If the chamber is just empty the gun needs to be racked
            else if (weaponPart.hasMagazine && !weaponPart.hasChamber)
            {
                if (weaponPart.currentMagazineAmmo <= 0) { CouldNotFire("magazine is empty - weapon has no chamber"); return; }
            }

            else if (weaponPart.drawsFromReserveAmmoDirectly && !weaponPart.hasMagazine && !weaponPart.hasChamber)
            {
                if (weaponPart.currentReserveAmmo <= 0) { CouldNotFire("reserve ammo empty"); return; }
            }

            else if (!weaponPart.drawsFromReserveAmmoDirectly && !weaponPart.hasMagazine && !weaponPart.hasChamber)
            {
                CouldNotFire("weapon has no chamber, magazine, or ability to draw from reserve ammo. check weapon settings", true); return;
            }
        }
        
        if (weaponPart.requiresTarget && !weaponPart.target) { CouldNotFire("no target"); return; }

        
        #if SPELL_SYSTEM
        if (weaponPart.isSpell)
        {
            // use spell manager to run mana checks, etc
            if (!weaponScriptableObject.spellManager.SpellChecks(weaponScriptableObject.weaponOwner, weaponPart)) { CouldNotFire("one or more spell checks failed"); return; }
        }
        #endif
        FireWeapon(weaponPart);
    }

    private void CouldNotFire(string reason, bool warning = false)
    {
        // play empty weapon click, etc.
        if (warning)
        {
            if (weaponScriptableObject.debugWeapon) Debug.LogWarning(reason);
            return;
        }
        
        if (weaponScriptableObject.debugWeapon) Debug.Log(reason);
    }
    
    private void FireWeapon(WeaponPart weaponPart)
    {
        if (!weaponPart.firePoint) return;
        
        // handle this through WeaponActions instead
        /*Transform selectedFirePoint = firePoints[weaponPart.firePointCounter].transform;

        if (cycleFirePoints) weaponPart.firePointCounter++;
        if (weaponPart.firePointCounter >= firePoints.Count) weaponPart.firePointCounter = 0;*/
        
        switch (weaponPart.hitscanOrProjectile)
        {
            case WeaponPart.HitscanOrProjectile.Hitscan:
                CastHitscan();
                break;
            
            case WeaponPart.HitscanOrProjectile.Projectile:
                if (weaponPart.shootsMultipleProjectiles)
                {
                    for (int i = 0; i < weaponPart.projectilesPerShot; i++)
                    {
                        SpawnProjectile(weaponPart, weaponPart.currentProjectile, weaponPart.firePoint.transform);
                    }

                    break;
                }
                
                SpawnProjectile(weaponPart, weaponPart.currentProjectile, weaponPart.firePoint.transform);
                break;
        }
        
        // weapon has shot successfully
        OnWeaponShoot?.Invoke();
        
        #if SPELL_SYSTEM
        if (weaponPart.isSpell)
        {
            OnSpellCast?.Invoke(weaponScriptableObject.weaponOwner, weaponPart);
            
            #if SPELL_SYSTEM
            weaponScriptableObject.spellManager.ConsumeSpellResources(weaponPart);
            #endif
            // tell spell manager to consume mana, etc
        }
        #endif
        
        weaponPart.cycleCTS = new CancellationTokenSource();
        weaponPart.cycleTask = CycleWeapon(weaponPart, weaponPart.cycleCTS.Token);
        
        // Remove ammo from the weapon (if it uses ammo)
        if (weaponPart.usesAmmo)
        {
            // If this weapon has a chamber, and either does not have a magazine, or that magazine is empty, empty the chamber
            if (weaponPart.hasChamber && (weaponPart.currentMagazineAmmo <= 0 || !weaponPart.hasMagazine))
            {
                weaponPart.isChamberLoaded = false;

                weaponPart.reloadState = WeaponPart.ReloadState.RequiresReload;
                
                return;
            }
            
            // If the weapon has a magazine, subtract the amount of ammo consumed
            if (weaponPart.hasMagazine)
            {
                if (weaponPart.currentMagazineAmmo > 0)
                {
                    weaponPart.currentMagazineAmmo -= weaponPart.ammoConsumedPerShot;
                }

                if (weaponPart.currentMagazineAmmo <= 0 && !weaponPart.hasChamber)
                {
                    weaponPart.reloadState = WeaponPart.ReloadState.RequiresReload;
                }

                if (weaponPart.currentMagazineAmmo <= 0 && weaponPart.hasChamber && !weaponPart.isChamberLoaded)
                {
                    weaponPart.reloadState = WeaponPart.ReloadState.RequiresReload;
                }

                // Clamp magazine ammo just in case
                weaponPart.currentMagazineAmmo = Mathf.Clamp(weaponPart.currentMagazineAmmo, 0, weaponPart.magazineCapacity);
                return;
            }

            if (weaponPart.drawsFromReserveAmmoDirectly && !weaponPart.hasMagazine && !weaponPart.hasChamber)
            {
                weaponPart.currentReserveAmmo -= weaponPart.ammoConsumedPerShot;
                return;
            }
            
            // ???
            /*if (weaponComponent.hasChamber)
            {
                weaponComponent.totalAmmoInWeapon = weaponComponent.magazineCurrentAmmo + 1;
            }
            else
            {
                weaponComponent.totalAmmoInWeapon = weaponComponent.magazineCurrentAmmo;
            }*/
        }
    }
    
    private void CastHitscan()
    {
        /*// Perform our hitscan with a raycast
        Ray raycast = new Ray(weaponComponent.firePoint.transform.position, weaponComponent.firePoint.transform.position);
        Physics.Raycast(raycast, out RaycastHit hit,  weaponComponent.hitscanRange);
            
        Debug.DrawRay(weaponComponent.firePoint.transform.position, weaponComponent.firePoint.transform.position * weaponComponent.hitscanRange, Color.cyan, 0.1f);
            
        // Check if we hit anything damageable
        if (hit.collider == null) return;
                
            
        //hit.collider.gameObject.GetComponent<HealthSystem>().DoDamage(selectedWeapon.damageTable);*/
    }

    private void SpawnProjectile(WeaponPart weaponPart, GameObject projectileToSpawn, Transform selectedFirePoint)
    {
        float randomSpreadAngleX = UnityEngine.Random.Range(-weaponPart.projectileSpreadAngle, weaponPart.projectileSpreadAngle);
        float randomSpreadAngleY = UnityEngine.Random.Range(-weaponPart.projectileSpreadAngle, weaponPart.projectileSpreadAngle);
        Vector3 spreadVector = new Vector3(randomSpreadAngleX, randomSpreadAngleY, 0);
        Quaternion projectileAngleWithSpread = selectedFirePoint.transform.rotation * Quaternion.Euler(spreadVector);
        
        GameObject newProjectile = Instantiate(projectileToSpawn, selectedFirePoint.transform.position, projectileAngleWithSpread);
        OnWeaponShoot?.Invoke();
        
        newProjectile.TryGetComponent(out ProjectileSystem newProjectileSystem);
        if (!newProjectileSystem)
        {
            newProjectileSystem = newProjectile.GetComponentInChildren<ProjectileSystem>();
        }
        
        if (!newProjectileSystem) return;

        newProjectileSystem.weaponPartFiredFrom = weaponPart;
        newProjectileSystem.weaponPartFiredFrom.spawnedProjectiles.Add(newProjectile);
        newProjectileSystem.weaponFiredFrom = this;
        newProjectileSystem.projectileOwner = weaponScriptableObject.weaponOwner;
        
        #if SPELL_SYSTEM
        newProjectileSystem.projectileComponent.damageComponent.baseDamage *= 1 + weaponPart.damageModifier;
        #endif
        
        if (newProjectileSystem.TryGetComponent(out Rigidbody projectileRB))
        {
            newProjectileSystem.ApplyVelocityToProjectile();
        }

        if (newProjectileSystem.projectileComponent.spawnAsChildOfWeapon)
        {
            newProjectileSystem.ParentProjectile(gameObject);
        }
        
        //newProjectileSystem.SetProjectileTeam(LayerMask.LayerToName(weaponOwner.gameObject.layer));
                
        if (!weaponPart.passTargetToProjectile) return;
        weaponScriptableObject.weaponOwner.TryGetComponent(out AimingSystem playerAimingSystem);
        newProjectileSystem.ChangeTrackingTarget(weaponPart.target);
    }
    
    private void SwitchAmmoType(GameObject validationObject, WeaponPart weaponPart, GameObject newAmmoType)
    {
        if (validationObject != gameObject) return;
        
        if (!weaponPart.projectilePrefabs.Contains(newAmmoType)) return;
        weaponPart.currentProjectile = newAmmoType;
    }
    
    private async Task CycleWeapon(WeaponPart weaponPart, CancellationToken ct)
    {
        weaponPart.firingState = WeaponPart.FiringState.Cycling;

        await MouseTools.AwaitableTimer(weaponPart.fireInterval);

        weaponPart.firingState = WeaponPart.FiringState.ReadyToFire;
    }

    private void StartReload(WeaponPart weaponPart, GameObject validationObject)
    {
        if (validationObject != weaponScriptableObject.weaponOwner) return;
        if (!weaponPart.needsReloading) return;

        if (weaponPart.hasMagazine)
        {
            if (weaponPart.currentMagazineAmmo == weaponPart.magazineCapacity) return; // add other checks for other weapons types
        }
        
        if (weaponPart.hasChamber && weaponPart.isChamberLoaded) return;
        
        weaponPart.reloadCTS = new CancellationTokenSource();
        weaponPart.reloadTask = ReloadWeapon(weaponPart, weaponPart.reloadCTS.Token);
    }
    
    private async Task ReloadWeapon(WeaponPart weaponPart, CancellationToken ct)
    {
        // todo reload states/progressive reloading per-weapon
        weaponPart.reloadState = WeaponPart.ReloadState.Reloading;
        
        await MouseTools.AwaitableTimer(weaponPart.reloadTime);
        
        if (weaponPart.reloadsRoundsIndividually)
        {
            return;
        }
        
        if (weaponPart.hasMagazine)
        {
            // This is the helldivers style of reloading, the entire magazine is dropped and the spare ammo is lost
            weaponPart.currentReserveAmmo -= weaponPart.magazineCapacity;
            weaponPart.currentMagazineAmmo = weaponPart.magazineCapacity;
        }

        if (weaponPart.hasChamber && !weaponPart.isChamberLoaded)
        {
            if (weaponPart.hasMagazine)
            {
                //weaponPart.currentMagazineAmmo -= weaponPart.chamberCapacity;
                weaponPart.currentMagazineAmmo -= 1;
            }

            
            weaponPart.isChamberLoaded = true;
        }
        
        weaponPart.reloadState = WeaponPart.ReloadState.ReadyToFire;
    }
    
    /*private void RackWeapon()
    {
        // We cannot rack a weapon with no chamber
        if (!selectedWeapon.hasChamber) return;
        
        if (selectedWeapon.isChamberLoaded)
        {
            selectedWeapon.isChamberLoaded = false;
            // eject a round
        }

        if (selectedWeapon.hasMagazine && selectedWeapon.currentMagazineAmmo > 0)
        {
            selectedWeapon.isChamberLoaded = true;
            selectedWeapon.currentMagazineAmmo -= 1;
        }
    }*/

    public void ApplyOnHit(HealthSystem target)
    {
        //On hit checks will go in here and allow subscribing to a on hit event
    }

    #if SPELL_SYSTEM
    public void ModifyAttackSpeed(WeaponPart weaponPart, float mod)
    {
        weaponPart.fireRateMultiplier = mod;
        OnAttackSpeedModifierChange?.Invoke(weaponPart, mod);
    }

    public void OnFireRateChange(WeaponPart weaponPart, float a)
    {
        if (!weaponScriptableObject) return;
        weaponPart.fireRateMultiplier += a; 
        //Debug.Log(weaponComponent.fireInterval + " | " + attackSpeedModifier);
    }

    public void ModifyDamage(WeaponPart weaponPart, float mod)
    {
        weaponPart.damageModifier = mod;
    }

    public void ResetModifiers(WeaponPart weaponPart)
    {
        weaponPart.damageModifier = 0;
        weaponPart.fireRateMultiplier = 1;
    }
    #endif
}
