using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;
using MouseLib;
using MyBox;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

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
public class Weapon : MonoBehaviour
{
    public delegate void OnReloadWeapon(GameObject validationObject);
    public static OnReloadWeapon onReloadWeapon;
    
    public static event Action OnWeaponShoot;
    public static event Action<Weapon> OnWeaponRelease;
    public static event Action<float> OnAttackSpeedModifierChange;
    public static event Action<GameObject, WeaponComponent> OnSpellCast;
    public static event Action<GameObject, WeaponComponent> OnAttemptSpellCast;

    [SerializeField, ReadOnly] public Tween weaponTween;
    [SerializeField, ReadOnly] public GameObject worldAimpointInstance;
    
    [SerializeField, ReadOnly] public GameObject target;
    [SerializeField, ReadOnly] public GameObject currentProjectile;
    [SerializeField] public List<GameObject> firePoints;
    [SerializeField] public bool cycleFirePoints;
    
    [ReadOnly] public GameObject WeaponOwner 
    {
        get => weaponOwner;
        set => weaponOwner = value;
    }
    [SerializeField, ReadOnly] private GameObject weaponOwner;
    
    //[SerializeField] private TargetingSystem targetingSystem;
    [SerializeField, DisplayInspector] public WeaponScriptableObject weaponScriptableObject; // make property
    
    public float AttackSpeedModifier { get => attackSpeedModifier; private set { attackSpeedModifier = value; OnAttackSpeedModifierChange?.Invoke(attackSpeedModifier); } }
    [SerializeField] private float attackSpeedModifier;
    [SerializeField] private float damageModifier;

    public WeaponComponent WeaponComponent
    {
        get => weaponComponent;
        set => weaponComponent  = value;
    }
    private WeaponComponent weaponComponent;
    
    private Task cycleTask;
    private CancellationTokenSource cycleCTS;
    private Task reloadTask;
    private CancellationTokenSource reloadCTS;
    private float burstCounter;
    private int firePointCounter;
    private float baseFireRate;
    
    #if SPELL_SYSTEM
    [SerializeField] private SpellManager spellManager;
    #endif

    private void OnEnable()
    {
        OnAttackSpeedModifierChange += OnFireRateChange;

        onReloadWeapon += StartReload;
    }

    private void OnDisable()
    {
        OnAttackSpeedModifierChange -= OnFireRateChange;
        
        onReloadWeapon -= StartReload;
    }

    private void Start()
    {
        // Create an instance of the WeaponObject so we don't affect the base stats
        WeaponScriptableObject newWeaponScriptableObject = Instantiate(weaponScriptableObject);
        
        weaponScriptableObject = newWeaponScriptableObject;
        weaponComponent = weaponScriptableObject.weaponComponent;

        cycleTask = Task.CompletedTask;
        reloadTask = Task.CompletedTask;
        
        //replace with an actual check on start
        weaponComponent.firingState = WeaponComponent.FiringState.ReadyToFire;
        weaponComponent.reloadState = WeaponComponent.ReloadState.ReadyToFire;
        weaponComponent.fireInterval = 60 / weaponComponent.fireRate;

        baseFireRate = weaponComponent.fireInterval;

        weaponComponent.isChamberLoaded = true; // change dynamically
        weaponComponent.currentMagazineAmmo = weaponComponent.magazineCapacity;
        weaponComponent.currentReserveAmmo = weaponComponent.maxReserveAmmo; // change dynamically in future, obviously
        
        weaponOwner = transform.parent.transform.parent.gameObject;
        weaponOwner.TryGetComponent(out spellManager);

        if (weaponComponent.projectilePrefabs.IsNullOrEmpty()) { Debug.Log("Weapon has no projectile. Assign one in the inspector."); return;}
        currentProjectile = weaponComponent.projectilePrefabs[0]; // change/remember the default during gameplay? hardcoded for now
    }

    public void PullTrigger(GameObject validationObject)
    {
        if (validationObject != weaponOwner) return;
        
        weaponComponent.isTriggerPulled = true;
        
        TryFireWeaponLoop();
    }

    public void ReleaseTrigger(GameObject validationObject)
    {
        if (validationObject != weaponOwner) return;
        
        weaponComponent.isTriggerPulled = false;
        OnWeaponRelease?.Invoke(this);
    }

    private async void TryFireWeaponLoop()
    {
        if (!cycleTask.IsCompleted) { await cycleTask; }
        if (!reloadTask.IsCompleted) { await reloadTask; }
        
        if (cycleTask.IsCompleted && reloadTask.IsCompleted)
        {
            // Prevent the loop from looping too fast
            await MouseTools.AwaitableTimer(0.001f);
        }

        if (!weaponComponent.isTriggerPulled)
        {
            burstCounter = 0;
            return;
        }
        switch (weaponComponent.currentFireMode)
        {
            case WeaponComponent.FireModes.SemiAuto:
                TryFireWeapon();
                break;
            
            case WeaponComponent.FireModes.Burst:
                TryFireWeapon();
                burstCounter++;

                if (burstCounter < weaponComponent.burstLength)
                {
                    TryFireWeaponLoop();
                    return;
                }
                
                burstCounter = 0;
                
                break;
            
            case WeaponComponent.FireModes.FullAuto:
                TryFireWeapon();
                
                TryFireWeaponLoop();
                break;
        }
    }
    
    private void TryFireWeapon()
    {
        // If the weapon is cycling between shots or reloading, it cannot fire
        if (weaponComponent.firingState == WeaponComponent.FiringState.Cycling) { CouldNotFire("weapon still cycling!"); return; }
    
        if (weaponComponent.reloadState == WeaponComponent.ReloadState.Reloading) { CouldNotFire("weapon reloading!"); return; }
        
        if (weaponComponent.usesAmmo)
        {
            // If the weapon has a chamber, and it is not loaded, the weapon cannot fire
            if (weaponComponent.hasChamber)
            {
                if (!weaponComponent.isChamberLoaded) { CouldNotFire("chamber not loaded"); return; }
            }

            // If the weapon does not have a chamber, only a magazine, canister, etc. (e.g. revolvers, flamethrowers)
            // Then the weapon cannot fire if it is empty
            // Note that it must NOT have a chamber for this to be true. If the chamber is just empty the gun needs to be racked
            else if (weaponComponent.hasMagazine && !weaponComponent.hasChamber)
            {
                if (weaponComponent.currentMagazineAmmo <= 0) { CouldNotFire("magazine is empty - weapon has no chamber"); return; }
            }

            else if (weaponComponent.drawsFromReserveAmmoDirectly && !weaponComponent.hasMagazine && !weaponComponent.hasChamber)
            {
                if (weaponComponent.currentReserveAmmo <= 0) { CouldNotFire("reserve ammo empty"); return; }
            }

            else if (!weaponComponent.drawsFromReserveAmmoDirectly && !weaponComponent.hasMagazine && !weaponComponent.hasChamber)
            {
                CouldNotFire("weapon has no chamber, magazine, or ability to draw from reserve ammo. check weapon settings"); return;
            }
        }
        
        if (weaponComponent.requiresTarget && !target) { CouldNotFire("no target"); return; }

        if (weaponComponent.isSpell)
        {
            // use spell manager to run mana checks, etc
            
            #if SPELL_SYSTEM
            if (!spellManager.SpellChecks(weaponOwner, weaponComponent)) { CouldNotFire("one or more spell checks failed"); return; }
            #endif
        }
        
        FireWeapon();
    }

    private void CouldNotFire(string reason)
    {
        // play empty weapon click, etc.
        //if (weaponComponent.debugWeapon)
        Debug.Log(reason);
    }
    
    private void FireWeapon()
    {
        if (firePoints.IsNullOrEmpty()) return;
        Transform selectedFirePoint = firePoints[firePointCounter].transform;

        if (cycleFirePoints) firePointCounter++;
        if (firePointCounter >= firePoints.Count) firePointCounter = 0;
        
        switch (weaponComponent.hitscanOrProjectile)
        {
            case WeaponComponent.HitscanOrProjectile.Hitscan:
                CastHitscan();
                break;
            
            case WeaponComponent.HitscanOrProjectile.Projectile:
                if (weaponComponent.shootsMultipleProjectiles)
                {
                    for (int i = 0; i < weaponComponent.projectilesPerShot; i++)
                    {
                        SpawnProjectile(currentProjectile, selectedFirePoint);
                    }

                    break;
                }
                
                SpawnProjectile(currentProjectile, selectedFirePoint);
                break;
        }
        
        // weapon has shot successfully
        OnWeaponShoot?.Invoke();

        if (weaponComponent.isSpell)
        {
            OnSpellCast?.Invoke(weaponOwner, weaponComponent);
            
            #if SPELL_SYSTEM
            spellManager.ConsumeSpellResources(weaponComponent);
            #endif
            // tell spell manager to consume mana, etc
        }
        
        cycleCTS = new CancellationTokenSource();
        cycleTask = CycleWeapon(cycleCTS.Token);
        
        // Remove ammo from the weapon (if it uses ammo)
        if (weaponComponent.usesAmmo)
        {
            // If this weapon has a chamber, and either does not have a magazine, or that magazine is empty, empty the chamber
            if (weaponComponent.hasChamber && (weaponComponent.currentMagazineAmmo <= 0 || !weaponComponent.hasMagazine))
            {
                weaponComponent.isChamberLoaded = false;

                weaponComponent.reloadState = WeaponComponent.ReloadState.RequiresReload;
                
                return;
            }
            
            // If the weapon has a magazine, subtract the amount of ammo consumed
            if (weaponComponent.hasMagazine)
            {
                if (weaponComponent.currentMagazineAmmo > 0)
                {
                    weaponComponent.currentMagazineAmmo -= weaponComponent.ammoConsumedPerShot;
                }

                if (weaponComponent.currentMagazineAmmo <= 0 && !weaponComponent.hasChamber)
                {
                    weaponComponent.reloadState = WeaponComponent.ReloadState.RequiresReload;
                }

                if (weaponComponent.currentMagazineAmmo <= 0 && weaponComponent.hasChamber && !weaponComponent.isChamberLoaded)
                {
                    weaponComponent.reloadState = WeaponComponent.ReloadState.RequiresReload;
                }

                // Clamp magazine ammo just in case
                weaponComponent.currentMagazineAmmo = Mathf.Clamp(weaponComponent.currentMagazineAmmo, 0, weaponComponent.magazineCapacity);
                return;
            }

            if (weaponComponent.drawsFromReserveAmmoDirectly && !weaponComponent.hasMagazine && !weaponComponent.hasChamber)
            {
                weaponComponent.currentReserveAmmo -= weaponComponent.ammoConsumedPerShot;
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

    private void SpawnProjectile(GameObject projectileToSpawn, Transform selectedFirePoint)
    {
        float randomSpreadAngleX = Random.Range(-weaponComponent.projectileSpreadAngle, weaponComponent.projectileSpreadAngle);
        float randomSpreadAngleY = Random.Range(-weaponComponent.projectileSpreadAngle, weaponComponent.projectileSpreadAngle);
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

        newProjectileSystem.weaponFiredFrom = this;
        newProjectileSystem.projectileOwner = weaponOwner;
        newProjectileSystem.projectileComponent.damageComponent.baseDamage = newProjectileSystem.projectileComponent.damageComponent.baseDamage * (1 + damageModifier);
        
        if (newProjectileSystem.TryGetComponent(out Rigidbody projectileRB))
        {
            newProjectileSystem.ApplyVelocityToProjectile();
        }

        if (newProjectileSystem.projectileComponent.spawnAsChildOfWeapon)
        {
            newProjectileSystem.ParentProjectile(gameObject);
        }
        
        //newProjectileSystem.SetProjectileTeam(LayerMask.LayerToName(weaponOwner.gameObject.layer));
                
        if (!weaponComponent.passTargetToProjectile) return;
        weaponOwner.TryGetComponent(out AimingSystem playerAimingSystem);
        newProjectileSystem.ChangeTrackingTarget(target);
    }
    
    private async Task CycleWeapon(CancellationToken ct)
    {
        weaponComponent.firingState = WeaponComponent.FiringState.Cycling;

        await MouseTools.AwaitableTimer(weaponComponent.fireInterval);

        weaponComponent.firingState = WeaponComponent.FiringState.ReadyToFire;
    }

    private void StartReload(GameObject validationObject)
    {
        if (validationObject != weaponOwner) return;
        if (!weaponComponent.needsReloading) return;

        if (weaponComponent.hasMagazine)
        {
            if (weaponComponent.currentMagazineAmmo == weaponComponent.magazineCapacity) return; // add other checks for other weapons types
        }
        
        if (weaponComponent.hasChamber && weaponComponent.isChamberLoaded) return;
        
        reloadCTS = new CancellationTokenSource();
        reloadTask = ReloadWeapon(reloadCTS.Token);
    }
    
    private async Task ReloadWeapon(CancellationToken ct)
    {
        // todo reload states/progressive reloading per-weapon
        weaponComponent.reloadState = WeaponComponent.ReloadState.Reloading;
        
        await MouseTools.AwaitableTimer(weaponComponent.reloadTime);
        
        if (weaponComponent.reloadsRoundsIndividually)
        {
            return;
        }
        
        if (weaponComponent.hasMagazine)
        {
            // This is the helldivers style of reloading, the entire magazine is dropped and the spare ammo is lost
            weaponComponent.currentReserveAmmo -= weaponComponent.magazineCapacity;
            weaponComponent.currentMagazineAmmo = weaponComponent.magazineCapacity;
        }

        if (weaponComponent.hasChamber && !weaponComponent.isChamberLoaded)
        {
            if (weaponComponent.hasMagazine)
            {
                weaponComponent.currentMagazineAmmo -= weaponComponent.chamberCapacity;
            }

            
            weaponComponent.isChamberLoaded = true;
        }
        
        weaponComponent.reloadState = WeaponComponent.ReloadState.ReadyToFire;
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

    public void ModifyAttackSpeed(float mod)
    {
        attackSpeedModifier = mod;
        OnAttackSpeedModifierChange?.Invoke(mod);
    }

    public void OnFireRateChange(float a)
    {
        if (weaponComponent == null) return;
        weaponComponent.fireInterval = baseFireRate / (1 + attackSpeedModifier); 
        //Debug.Log(weaponComponent.fireInterval + " | " + attackSpeedModifier);
    }

    public void ModifyDamage(float mod)
    {
        damageModifier = mod;
    }

    public void ResetModifiers()
    {
        damageModifier = 0;
        attackSpeedModifier = 0;
    }
}
