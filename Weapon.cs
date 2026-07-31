using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;
using MouseLib;
using MyBox;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;
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
[Serializable]
public class Weapon : MonoBehaviour
{
    public delegate void OnReloadWeapon(WeaponPart weaponPart, GameObject target);
    public static OnReloadWeapon onReloadWeapon;

    public static event Action<GameObject, WeaponScriptableObject> OnWeaponInstantiated; // contains the new instance of the weapon
    public static event Action OnWeaponShoot;
    public static event Action<float> OnApplyAimPunch;
    public static event Action<Weapon> OnWeaponRelease;
    public static event Action<WeaponPart, float> OnAttackSpeedModifierChange;
    public static event Action<GameObject, WeaponPart> OnSpellCast;
    public static event Action<GameObject, WeaponPart> OnAttemptSpellCast;

    public bool simpleWeapon;
    
    [ConditionalField(nameof(simpleWeapon), false)] public GameObject simpleFirePoint;
    [ConditionalField(nameof(simpleWeapon), false)] public WeaponPart simpleWeaponPart;
    
    [ConditionalField(nameof(simpleWeapon), false)] public List<GameObject> prefabFirePoints;
    [DisplayInspector, ConditionalField(nameof(simpleWeapon), false)] public WeaponScriptableObject weaponScriptableObject;
    
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
        if (simpleWeapon)
        {
            simpleWeaponPart = Instantiate(simpleWeaponPart);
            if (simpleWeaponPart) simpleWeaponPart.firePoint = simpleFirePoint;
            simpleWeaponPart.SetupWeaponPart();
            return;
        }
        
        // Create an instance of the WeaponObject so we don't affect the base stats
        WeaponScriptableObject newWeaponScriptableObject = Instantiate(weaponScriptableObject);
        weaponScriptableObject = newWeaponScriptableObject;
        weaponScriptableObject.weaponCycleTask = Task.CompletedTask;
        weaponScriptableObject.weaponCycleState = WeaponScriptableObject.WeaponCycleState.ReadyToFire;
        
        List<WeaponPart> newWeaponParts = new List<WeaponPart>();
        foreach (WeaponPart weaponPart in weaponScriptableObject.WeaponParts)
        {
            
            WeaponPart newWeaponPart = Instantiate(weaponPart);
            newWeaponParts.Add(newWeaponPart);

            // replace references
            foreach (WeaponFunction weaponFunction in weaponScriptableObject.WeaponFunctions)
            {
                if (weaponFunction.MainWeaponPart == weaponPart) weaponFunction.MainWeaponPart = newWeaponPart;
                foreach (WeaponFunctionCondition functionCondition in weaponFunction.FunctionConditions)
                {
                    if (functionCondition.WeaponPart != weaponPart) continue;
                    functionCondition.WeaponPart = newWeaponPart;
                }
                
                foreach (WeaponFunctionAction functionAction in weaponFunction.FunctionActions)
                {
                    if (functionAction.functionActionType == WeaponFunctionAction.WeaponFunctionActionType.InvokeMethod)
                    {
                        functionAction.MethodEvent.RemoveAllListeners();
                        
                        // todo test multiple methods
                        for (int i = 0; i < functionAction.MethodEvent.GetPersistentEventCount(); i++)
                        {
                            UnityEngine.Object oldTarget = functionAction.MethodEvent.GetPersistentTarget(i);
                            Component newTarget = gameObject.GetComponent(oldTarget.GetType());
                        
                            string methodName = functionAction.MethodEvent.GetPersistentMethodName(i);
                            var method = newTarget.GetType().GetMethod(methodName);
                            var action = new UnityAction(() => method.Invoke(newTarget, null));
                            
                            functionAction.MethodEvent.AddListener(action);
                            // todo the old persistent listener survives this, kill it
                        }
                    }
                    
                    if (functionAction.WeaponPart != weaponPart) continue;
                    functionAction.WeaponPart = newWeaponPart;
                }
            }
        }

        weaponScriptableObject.WeaponParts = newWeaponParts;
        TryGetComponent(out weaponScriptableObject.weaponRecoilImpulseSource);
        
        #if SPELL_SYSTEM
        weaponScriptableObject.weaponOwner.TryGetComponent(out weaponScriptableObject.spellManager);
        #endif

        foreach (WeaponPart weaponPart in weaponScriptableObject.WeaponParts)
        {
            weaponPart.firePoint = prefabFirePoints[weaponScriptableObject.WeaponParts.IndexOf(weaponPart)];
            weaponPart.SetupWeaponPart(weaponScriptableObject);
        }

        OnWeaponInstantiated?.Invoke(gameObject, weaponScriptableObject);
    }

    public void ShootWeaponDirectly(GameObject target, bool pressOrRelease)
    {
        if (target != weaponScriptableObject.weaponOwner) return;

        // shoots the first weapon part directly - use for simple weapons for now, expand in future
        WeaponPart currentWeaponPart = weaponScriptableObject.WeaponParts[0];
        if (!currentWeaponPart) return;
        
        if (pressOrRelease)
        {
            currentWeaponPart.isTriggerPulled = true;
            WeaponCycleLoop(currentWeaponPart);
            // set weapon action to completed
        }
        else
        {
            currentWeaponPart.isTriggerPulled = false;
            OnWeaponRelease?.Invoke(this);
        }
    }
    
    public void ShootSimpleWeapon()
    {
        if (simpleWeaponPart) TryFireWeapon(simpleWeaponPart);
    }

    public void ShootWeaponPartDirectly(WeaponPart weaponPart, bool bypassCooldown = false, float replacementCooldown = 0)
    {
        WeaponCycleLoop(weaponPart, bypassCooldown, replacementCooldown);
    }

    public async void ProcessWeaponFunction(InputAction.CallbackContext context, bool inputPressed)
    {
        foreach (WeaponFunction weaponFunction in weaponScriptableObject.WeaponFunctions)
        {
            bool inputBuffered = false;
            
            if (!weaponFunction.InputAction) continue;
            if (weaponFunction.InputAction.action != context.action) continue;
            
            /*if (weaponFunction.MainWeaponPart.cycleTask != Task.CompletedTask)
            {
                // buffer an input if it was pressed within 0.25s before the cooldown ends
                if (weaponFunction.MainWeaponPart._cooldown * 1000 - weaponFunction.MainWeaponPart.cycleTimer.ElapsedMilliseconds <= 1000)
                {
                    Debug.Log("cycle buffered!");
                    inputBuffered = true;
                    await weaponFunction.MainWeaponPart.cycleTask;
                }
            }*/
            
            bool canShoot = true;
            bool cancelChargeOnceTriggered = false;
            
            foreach (WeaponFunctionCondition functionCondition in weaponFunction.FunctionConditions)
            {
                functionCondition.Fulfilled = true;
                WeaponPart weaponPart = functionCondition.WeaponPart;
                
                switch (functionCondition.ConditionType)
                {
                    case WeaponFunctionCondition.WeaponFunctionConditionType.Nothing:
                        functionCondition.Fulfilled = inputPressed;
                        break;
                
                    case WeaponFunctionCondition.WeaponFunctionConditionType.WeaponPartCharged:
                        // only start one task at a time todo TEST
                        
                        if (inputPressed && weaponPart.chargePercent > 0) functionCondition.Fulfilled = false;
                        if (!inputPressed && weaponPart.chargePercent <= 0) functionCondition.Fulfilled = false;
                        
                        if (inputPressed && weaponPart.chargePercent <= 0)
                        {
                            if (!weaponPart.allowChargeOnCooldown && weaponPart.cycleState != WeaponPart.CycleState.ReadyToFire) return;
                            
                            // don't shoot, start charge
                            functionCondition.Fulfilled = false;
                            
                            CancelCharge(weaponPart.chargeCTS, weaponPart);
                            weaponPart.chargeCTS = new CancellationTokenSource();
                            weaponPart.chargeTask = WaitForCharge(weaponPart.chargeCTS.Token, context, inputPressed, weaponPart, functionCondition, weaponPart.autoRelease);
                        }

                        if (!inputPressed && weaponPart.chargePercent > 0)
                        {
                            // cancel charge after shooting, shoot if allowed
                            if (!weaponPart.allowPartialCharge && weaponPart.chargePercent < 100) functionCondition.Fulfilled = false;
                            cancelChargeOnceTriggered = true;
                        }
                        break;
                }

                if (!functionCondition.Fulfilled) canShoot = false;
            }
            
            // if all FunctionConditions are satisfied, trigger all FunctionActions
            foreach (WeaponFunctionAction functionAction in weaponFunction.FunctionActions)
            {
                WeaponPart weaponPart = functionAction.WeaponPart;

                switch (functionAction.functionActionType)
                {
                    case WeaponFunctionAction.WeaponFunctionActionType.UseWeaponPart:
                        if (canShoot)
                        {
                            weaponPart.isTriggerPulled = true;
                            WeaponCycleLoop(weaponPart);
                        }
                        else
                        {
                            weaponPart.isTriggerPulled = false;
                            OnWeaponRelease?.Invoke(this);
                        }
                        break;
                    
                    case WeaponFunctionAction.WeaponFunctionActionType.InvokeMethod:
                        if (!canShoot) break;
                        functionAction.MethodEvent?.Invoke();
                        break;
                }
                
                // cancel charges, etc after the weapon has triggered
                if (cancelChargeOnceTriggered) CancelCharge(weaponPart.chargeCTS, weaponPart);
            }
        }
    }


    /*private WeaponPart CheckWeaponFunctionConditions(WeaponFunction weaponFunction)
    {
        foreach (WeaponFunctionCondition functionCondition in weaponFunction.FunctionConditions)
        {
            switch (functionCondition.ConditionType)
            {
                case WeaponFunctionCondition.WeaponFunctionConditionType.Nothing:
                    return weaponFunction.weaponPart;
                
                case WeaponFunctionCondition.WeaponFunctionConditionType.AnyProjectileActive:
                    if (!AnyProjectileActiveCheck(weaponAction)) return null;
                    
                    return weaponFunction.weaponPart;
                
                case WeaponFunctionCondition.WeaponFunctionConditionType.ChargedForTime:
                    
                    return weaponFunction.weaponPart;
                
                case WeaponFunctionCondition.WeaponFunctionConditionType.CheckOtherFunction:
                    if (!ActionCompleteCheck(actionCondition)) return null;
                    
                    return weaponFunction.weaponPart;
            }
        }

        return null;
    }*/

    private async void WeaponCycleLoop(WeaponPart weaponPart, bool bypassCooldown = false, float replacementCooldown = 0)
    {
        if (!weaponPart.isTriggerPulled && weaponPart.currentFireMode != WeaponPart.FireModes.SemiAuto)
        {
            weaponPart.burstCounter = 0;
            return;
        }
        
        switch (weaponPart.currentFireMode)
        {
            case WeaponPart.FireModes.SemiAuto:
                TryFireWeapon(weaponPart, bypassCooldown, replacementCooldown);
                break;
            
            case WeaponPart.FireModes.Burst:
                TryFireWeapon(weaponPart, bypassCooldown, replacementCooldown);
                weaponPart.burstCounter++;

                if (weaponPart.burstCounter < weaponPart.burstLength)
                {
                    WeaponCycleLoop(weaponPart);
                    return;
                }
                
                weaponPart.burstCounter = 0;
                
                break;
            
            case WeaponPart.FireModes.FullAuto:
                TryFireWeapon(weaponPart, bypassCooldown, replacementCooldown);

                if (!weaponPart.isTriggerPulled) break;
                await MouseTools.AwaitableTimer(weaponPart._cooldown);
                
                WeaponCycleLoop(weaponPart);
                break;
        }
    }
    
    private void TryFireWeapon(WeaponPart weaponPart, bool bypassCooldown = false, float replacementCooldown = 0)
    {
        // If the weapon is cycling between shots or reloading, it cannot fire
        if (weaponPart.cycleState == WeaponPart.CycleState.Cycling && !bypassCooldown) { CouldNotFire("weapon part still cycling!"); return; }
    
        if (weaponPart.reloadState == WeaponPart.ReloadState.Reloading) { CouldNotFire("weapon reloading!"); return; }
        
        //if (weaponPart.canCharge && weaponPart.chargeState == WeaponPart.ChargeState.Charging && !weaponPart.allowPartialCharge) { CouldNotFire("weapon not charged!"); return; }
        //if (weaponPart.canCharge && weaponPart.chargeState == WeaponPart.ChargeState.Uncharged && !weaponPart.allowPartialCharge) { CouldNotFire("weapon not charged!"); return; }
        
        if (weaponScriptableObject && weaponScriptableObject.weaponCycleState == WeaponScriptableObject.WeaponCycleState.Cycling && !weaponPart.hasIndependentCooldown && !bypassCooldown) { CouldNotFire("entire weapon still cycling!"); return; }
        
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
        FireWeapon(weaponPart, bypassCooldown, replacementCooldown);
    }

    private void CouldNotFire(string reason, bool warning = false)
    {
        // play empty weapon click, etc.
        if (warning) { if (weaponScriptableObject.debugWeapon) Debug.LogWarning(reason); return; }
        if (weaponScriptableObject.debugWeapon) Debug.Log(reason);
    }
    
    private void FireWeapon(WeaponPart weaponPart, bool bypassCooldown = false, float replacementCooldown = 0)
    {
        if (!weaponPart.firePoint) return;
        
        // handle this through WeaponFunctions instead
        /*Transform selectedFirePoint = firePoints[weaponPart.firePointCounter].transform;

        if (cycleFirePoints) weaponPart.firePointCounter++;
        if (weaponPart.firePointCounter >= firePoints.Count) weaponPart.firePointCounter = 0;*/
        
        switch (weaponPart.hitscanOrProjectile)
        {
            case WeaponPart.HitscanOrProjectile.Hitscan:
                CastHitscan();
                break;
            
            case WeaponPart.HitscanOrProjectile.Projectile:
                foreach (WeaponProjectile projectile in weaponPart.projectiles)
                {
                    for (int i = 0; i < projectile.Count; i++)
                    {
                        SpawnProjectile(weaponPart, projectile, weaponPart.firePoint.transform);
                    }
                }
                break;
        }
        
        // weapon has shot successfully
        OnWeaponShoot?.Invoke();
        
        if (weaponPart.hasParticles)
        {
            ParticleSystem particles;
            
            ParticleSystem.MinMaxCurve minChargeStartSpeed = weaponPart.onShootWeaponParticles.minChargeStartSpeed;
            ParticleSystem.MinMaxCurve maxChargeStartSpeed = weaponPart.onShootWeaponParticles.maxChargeStartSpeed;
            ParticleSystem.MinMaxCurve minChargeBurstSize = weaponPart.onShootWeaponParticles.minChargeBurstSize;
            ParticleSystem.MinMaxCurve maxChargeBurstSize = weaponPart.onShootWeaponParticles.maxChargeBurstSize;

            
            if (!weaponPart.onShootWeaponParticles.spawnAsChild) particles = Instantiate(weaponPart.onShootWeaponParticles.particles, weaponPart.firePoint.transform.position, weaponPart.firePoint.transform.rotation);
            else particles = Instantiate(weaponPart.onShootWeaponParticles.particles, weaponPart.firePoint.transform);


            if (weaponPart.onShootWeaponParticles.scaleWithCharge)
            {
                ParticleSystem.MainModule main = particles.main;
                ParticleSystem.MinMaxCurve startSpeed = weaponPart.onShootWeaponParticles.InterpolateMinMaxCurve(minChargeStartSpeed, maxChargeStartSpeed, weaponPart.lastCharge / 100);
                startSpeed.mode = ParticleSystemCurveMode.TwoConstants;
                main.startSpeed = startSpeed;
            
                ParticleSystem.MinMaxCurve burstSize = weaponPart.onShootWeaponParticles.InterpolateMinMaxCurve(minChargeBurstSize, maxChargeBurstSize, weaponPart.lastCharge / 100);
            
                particles.emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0.0f, burstSize, 1, 0.03f)
                });
            }
        }

        if (weaponPart.applyUserKnockback)
        {
            weaponScriptableObject.weaponOwner.TryGetComponent(out Rigidbody ownerRB);
            
            float knockbackForce = weaponPart.knockbackForce;
            if (weaponPart.scaleKnockbackWithCharge) knockbackForce = Mathf.Lerp(weaponPart.knockbackForce, weaponPart.maxChargeKnockbackForce, weaponPart.lastCharge / 100);
            if (ownerRB) ownerRB.AddForce(-weaponPart.firePoint.transform.forward * knockbackForce, ForceMode.Impulse);
        }

        if (weaponPart.hasRecoil && weaponScriptableObject.weaponRecoilImpulseSource)
        {
            OnApplyAimPunch?.Invoke(weaponPart.aimPunch);
            
            CinemachineImpulseSource impulseSource = weaponScriptableObject.weaponRecoilImpulseSource;
            impulseSource.ImpulseDefinition = weaponPart.impulseDefinition;
            impulseSource.GenerateImpulse(weaponPart.recoil);
        }
        
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
        
        // make into method?
        weaponPart.cycleTimer ??= new Stopwatch();
        weaponPart.cycleTimer.Restart();
        
        weaponPart.cycleCTS ??= new CancellationTokenSource();
        weaponPart.cycleTask = CycleWeaponPart(weaponPart, weaponPart.cycleCTS.Token, bypassCooldown, replacementCooldown);
        weaponPart.cycleTask.ContinueWith(x => { weaponPart.cycleTimer.Stop(); });

        if (weaponScriptableObject && weaponPart.weaponCooldown > 0)
        {
            weaponScriptableObject.weaponCycleTimer ??= new Stopwatch();
            weaponScriptableObject.weaponCycleTimer.Start();
        
            weaponScriptableObject.weaponCycleCTS ??= new CancellationTokenSource();
            weaponScriptableObject.weaponCycleTask = CycleWeapon(weaponScriptableObject, weaponPart, weaponScriptableObject.weaponCycleCTS.Token);
            weaponScriptableObject.weaponCycleTask.ContinueWith(x => { weaponPart.cycleTimer.Stop(); });
            weaponScriptableObject.weaponCooldown = weaponPart.weaponCooldown;
        }
        
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

    private void SpawnProjectile(WeaponPart weaponPart, WeaponProjectile projectileToSpawn, Transform firePoint)
    {
        float randomSpreadAngleX = Random.Range(-projectileToSpawn.SpreadAngle, projectileToSpawn.SpreadAngle);
        float randomSpreadAngleY = Random.Range(-projectileToSpawn.SpreadAngle, projectileToSpawn.SpreadAngle);
        Vector3 spreadVector = new Vector3(randomSpreadAngleX, randomSpreadAngleY, 0);
        Quaternion projectileAngleWithSpread = firePoint.transform.rotation * Quaternion.Euler(spreadVector);
        
        GameObject newProjectile = Instantiate(projectileToSpawn.Projectile, firePoint.transform.position, projectileAngleWithSpread);
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
        if (weaponScriptableObject) newProjectileSystem.projectileOwner = weaponScriptableObject.weaponOwner;
        
        #if SPELL_SYSTEM
        newProjectileSystem.projectileComponent.damageComponent.baseDamage *= 0 + weaponPart.damageMultiplier;
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

        if (weaponPart.passChargeToProjectile)
        {
            newProjectileSystem.SetProjectileCharge(weaponPart.lastCharge / 100);
        }
        
        if (weaponPart.passTargetToProjectile)
        {
            weaponScriptableObject.weaponOwner.TryGetComponent(out AimingSystem playerAimingSystem);
            newProjectileSystem.ChangeTrackingTarget(weaponPart.target);
        }

        newProjectileSystem.InitializeProjectile();
    }
    
    
    // needs redesign
    /*private void SwitchAmmoType(GameObject target, WeaponPart weaponPart, GameObject newAmmoType)
    {
        if (target != gameObject) return;

        foreach (WeaponProjectile projectile in weaponPart.projectiles)
        {
            if (!projectile.Projectile == newAmmoType) continue;
            weaponPart.currentProjectile = newAmmoType;
        }
    }*/
    
    private async Task CycleWeapon(WeaponScriptableObject scriptableObject, WeaponPart weaponPart, CancellationToken ct, bool bypassCooldown = false, float replacementCooldown = 0f)
    {
        scriptableObject.weaponCycleState = WeaponScriptableObject.WeaponCycleState.Cycling;

        if (!bypassCooldown) await MouseTools.AwaitableTimer(weaponPart.weaponCooldown);
        if (bypassCooldown) await MouseTools.AwaitableTimer(replacementCooldown);

        scriptableObject.weaponCycleState = WeaponScriptableObject.WeaponCycleState.ReadyToFire;
    }
    
    private async Task CycleWeaponPart(WeaponPart weaponPart, CancellationToken ct,  bool bypassCooldown = false, float replacementCooldown = 0f)
    {
        weaponPart.cycleState = WeaponPart.CycleState.Cycling;

        if (!bypassCooldown) await MouseTools.AwaitableTimer(weaponPart._cooldown);
        if (bypassCooldown) await MouseTools.AwaitableTimer(replacementCooldown);
        

        weaponPart.cycleState = WeaponPart.CycleState.ReadyToFire;
    }

    private void StartReload(WeaponPart weaponPart, GameObject target)
    {
        if (target != weaponScriptableObject.weaponOwner) return;
        if (!weaponPart.needsReloading) return;

        if (weaponPart.hasMagazine)
        {
            if (weaponPart.currentMagazineAmmo == weaponPart.magazineCapacity) return; // add other checks for other weapons types
        }
        
        if (weaponPart.hasChamber && weaponPart.isChamberLoaded) return;
        
        weaponPart.reloadCTS = new CancellationTokenSource();
        weaponPart.reloadTask = ReloadWeaponPart(weaponPart, weaponPart.reloadCTS.Token);
    }
    
    private async Task ReloadWeaponPart(WeaponPart weaponPart, CancellationToken ct)
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
    
    // check the status of the input action at a fast interval - note that this will not add input lag or affect anything on the player's end
    // for example - charging an example weapon takes 2 seconds
    // the player gives the input to start charging and holds down the input - this happens "instantly"
    // (hardware and processing speed add trivial amounts of delay but that's not relevant)
    //
    // the input begins this loop, which checks the status of that action every 0.05s
    // if the player releases their input at any time, it does not need to wait 0.05s before being processed - it still happens instantly
    // since all this loop does is set a flag - the rest of the code simply checks if the flag is true or false
    //
    // the worst case is that the player has already waited 2 seconds - the weapon should be charged
    // but this could introduce a 0.05s delay to the "charged" flag flipping true - so at most, a 2s charge becomes 2.05s
    // which is trivial - charge times are already tough to mentally predict, so this should have minimal gameplay impact
    // this is opposed to a 0.05s *input delay* - which would have an appreciable effect on gameplay, even if small
    private async Task WaitForCharge(CancellationToken ct, InputAction.CallbackContext context, bool pressOrRelease, WeaponPart weaponPart, WeaponFunctionCondition functionCondition, bool autoRelease)
    {
        if (!weaponPart.allowChargeOnCooldown && weaponPart.cycleState == WeaponPart.CycleState.Cycling) return;
        if (!weaponPart.allowChargeOnWeaponCooldown && weaponPart.parentWeaponScriptableObject.weaponCycleState == WeaponScriptableObject.WeaponCycleState.Cycling) return;
        
        
        for (float f = 0; f < weaponPart.maxChargeTime * 1000; f += 0.05f)
        {
            if (ct.IsCancellationRequested) break;
            
            weaponPart.chargeState = WeaponPart.ChargeState.Charging;
            weaponPart.chargePercent = Mathf.Clamp(f / weaponPart.maxChargeTime * 100, 0, 100);
            weaponPart.lastCharge = weaponPart.chargePercent;
            
            if (weaponScriptableObject.debugWeapon) Debug.Log($"{context.action.name} - {f}");
            if (weaponScriptableObject.debugWeapon) Debug.Log($"{weaponPart.name} Charge - {weaponPart.chargePercent}%");
            if (weaponScriptableObject.debugWeapon) Debug.Log($"{weaponPart.name} Last Charge - {weaponPart.lastCharge}%");
            
            if (f >= weaponPart.maxChargeTime)
            {
                weaponPart.chargeState = WeaponPart.ChargeState.Charged;
                weaponPart.chargePercent = 100; // check
                weaponPart.lastCharge = weaponPart.chargePercent;
                
                // simulate an input with opposite press/release value
                if (autoRelease) ProcessWeaponFunction(context, !pressOrRelease);
                
                break;
            }
            await MouseTools.AwaitableTimer(0.05f);
        }
    }

    public void CancelCharge(CancellationTokenSource cts, WeaponPart weaponPart)
    {
        if (weaponPart.chargeState == WeaponPart.ChargeState.Uncharged || weaponPart.chargePercent == 0) return;
        
        weaponPart.chargeCTS.Cancel();
        weaponPart.chargePercent = 0;
        weaponPart.chargeState = WeaponPart.ChargeState.Uncharged;
    }

    public void ProcessWeaponReloadAction(GameObject target, InputAction action)
    {
        foreach (WeaponPart weaponPart in weaponScriptableObject.WeaponParts)
        {
            if (weaponPart.reloadAction.action != action) continue;
            StartReload(weaponPart, weaponScriptableObject.weaponOwner);
        }
    }

    public void ReleaseTrigger(GameObject target)
    {
        if (target != weaponScriptableObject.weaponOwner) return;
        
        foreach (WeaponPart weaponPart in weaponScriptableObject.WeaponParts)
        {
            weaponPart.isTriggerPulled = false;
            OnWeaponRelease?.Invoke(this);
        }
    }

    // untested!
    private void CycleFireMode(WeaponPart weaponPart)
    {
        foreach (WeaponPart.FireModes fireMode in Enum.GetValues(typeof(WeaponPart.FireModes)))
        {
            if (!weaponPart.availableFireModes.HasFlag(fireMode) || fireMode == weaponPart.currentFireMode) continue;
            weaponPart.currentFireMode = fireMode;
            break;
        }

        if (weaponPart.currentFireMode == 0) weaponPart.currentFireMode = WeaponPart.FireModes.SemiAuto;
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
        weaponPart.damageMultiplier = mod;
    }

    public void ResetModifiers(WeaponPart weaponPart)
    {
        weaponPart.damageMultiplier = 1;
        weaponPart.fireRateMultiplier = 1;
    }
    #endif
}
