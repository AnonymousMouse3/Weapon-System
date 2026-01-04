using System;
using System.Collections.Generic;
using MyBox;
using UnityEngine;

// Credit to DeadCows' MyBox for additional editor attributes - https://github.com/Deadcows/MyBox/

[Serializable]
public class DamageComponent
{
    public float baseDamage;
    [ReadOnly] public List<PassiveEffectScriptableObject> passiveEffectsAppliedToTarget;
}

[Serializable]
public class ProjectileComponent
{
    [Tooltip("The prefab of the current projectile.")]
    public GameObject projectilePrefab;
    [Tooltip("The damage component of the current projectile.")]
    public DamageComponent damageComponent;
    [Tooltip("The GameObject(s) that the projectile is able to spawn on impact, during flight, etc.")]
    public List<GameObject> projectileWarheads;
    [Tooltip("The on-hit particle system of the projectile.")]
    public List<ParticleSystem> onHitParticles;
    
    // This is set on-the-fly by the projectile
    [ReadOnly, Tooltip("The team the projectile belongs to. It can damage all teams other than its own")]
    public ProjectileTeam projectileTeam;
    public enum ProjectileTeam
    {
        Neutral,
        Player,
        Legion,
    }
    
    [Tooltip("Time (s)")] public float projectileLifetime;
    public bool destroyOnImpact;
    public bool triggerOnImpact;
    public bool detonateWarheadsOnImpact;
    
    [Tooltip("Velocity (m/s)")] public float initialVelocity;
    [Tooltip("Velocity (m/s). 0 = No limit.")] public float maxVelocity;
    [Tooltip("Mass (g)")] public float projectileMass;

    [Separator("Tracking")]
    public bool isTracking;
    [ReadOnly(nameof(isTracking), true)] public float trackingTurnTime;
    [ReadOnly(nameof(isTracking), true)] public float trackingForce;
    [ReadOnly(nameof(isTracking), true)] public float trackingPValue;
    [ReadOnly(nameof(isTracking), true)] public float trackingStartDelay;
    [ReadOnly(nameof(isTracking), true)] public float trackingActiveDuration;
    [ReadOnly(nameof(isTracking), true)] public bool snapToTargetAfterDuration;
    
    [ReadOnly(nameof(isTracking), true)] public TrackingModes currentTrackingMode;
    public enum TrackingModes
    {
        CannotSwitchTarget,
    }
    
    public bool projectileDrag;
    [ReadOnly(nameof(projectileDrag), true)] public float projectileDragFactor;
    
    public bool projectileGravity;
    [ReadOnly(nameof(projectileGravity), true)] public float projectileGravityFactor;

    [Tooltip("Whether the projectile is powered by a rocket engine or the like, and generates its own velocity after firing.")]
    public bool isPowered;
    [ReadOnly(nameof(isPowered), true)] public float enginePower;
    [ReadOnly(nameof(isPowered), true)] public float engineTime;
    [ReadOnly(nameof(isPowered), true)] public float engineDelay;

    #if LIQUID_SYSTEM
    [Separator("Liquid Settings")]
    public bool createsLiquid;
    [ReadOnly(nameof(createsLiquid), true)] public int radius;
    [ReadOnly(nameof(createsLiquid), true)] public Liquid liquidType;
    #endif

    [Separator("Magnetism Settings")]
    public bool isMagnetic;
    [ReadOnly(nameof(isMagnetic) ,true), Range(1f, 100f)]
    public float magnetAttractionFactor;
}

[Serializable]
public class SubProjectileComponent
{
    [Tooltip("The prefab of the current projectile.")]
    public GameObject projectilePrefab;
    [Tooltip("The damage component of the current projectile.")]
    public DamageComponent damageComponent;
    [Tooltip("The GameObject(s) that the projectile is able to spawn on impact, during flight, etc.")]
    public List<GameObject> projectileWarheads;
    [Tooltip("The on-hit particle system of the projectile.")]
    public List<ParticleSystem> onHitParticles;
    
    [Tooltip("Time (s)")] public float projectileLifetime;
    public bool destroyOnImpact;
    public bool triggerOnImpact;
    
    [Tooltip("Velocity (m/s)")] public float muzzleVelocity;
    [Tooltip("Mass (g)")] public float projectileMass;

    public bool isTracking;
    [ReadOnly(nameof(isTracking), true)] public float trackingTime;
    [ReadOnly(nameof(isTracking), true)] public float trackingDelay;
    [ReadOnly(nameof(isTracking), true)] public float trackingSlowness;
    
    [ReadOnly(nameof(isTracking), true)] public TrackingModes currentTrackingMode;
    public enum TrackingModes
    {
        CannotSwitchTarget,
    }
    
    public bool projectileDrag;
    [ReadOnly(nameof(projectileDrag), true)] public float projectileDragFactor;
    
    public bool projectileGravity;
    [ReadOnly(nameof(projectileGravity), true)] public float projectileGravityFactor;

    [Tooltip("Whether the projectile is powered by a rocket engine or the like, and generates its own velocity after firing.")]
    public bool isPowered;
    [ReadOnly(nameof(isPowered), true)] public float enginePower;
    [ReadOnly(nameof(isPowered), true)] public float engineTime;
    [ReadOnly(nameof(isPowered), true)] public float engineDelay;

    [Separator("Magnetism Settings")]
    public bool isMagnetic;
    [ReadOnly(nameof(isMagnetic) ,true), Range(1f, 100f)]
    public float magnetAttractionFactor;
}

// The WeaponComponent class serves as the data structure/template and the base of the weapon system
// It is designed to be cloned and saved with WeaponObjects for individual weapons' stat blocks
// Which are further instanced by Weapon scripts for use at runtime
[Serializable]
public class WeaponComponent
{
    // ReadOnly attributes will disable editing of the variable depending on a condition
    // We use these here to disable certain gun stats that are only relevant if another is true
    // e.g. a gun's chamber can only be loaded if it has one
    // Thus setting hasChamber to false will set chamberLoaded to read only

    [Separator("Runtime States")]
    [ReadOnly] public bool isTriggerPulled;
    
    [ReadOnly, ConditionalField(nameof(hasChamber))] public bool isChamberLoaded;
    [ConditionalField(nameof(hasMagazine))] public int currentMagazineAmmo;
    [ConditionalField(nameof(hasReserveAmmo))] public int currentReserveAmmo;
    
    public FireModes currentFireMode;
    public enum FireModes
    {
        SemiAuto = 0,
        FullAuto = 1,
        Burst = 2,
    }
    
    [ReadOnly] public FiringState firingState;
    public enum FiringState
    {
        Cycling,
        ReadyToFire,
    }
    
    [ReadOnly] public ReloadState reloadState;
    public enum ReloadState
    {
        DoesNotReload,
        RequiresReload,
        Reloading,
        ReadyToFire,
    }
    
    [Separator("Technical Settings")]
    public float hitscanRange;
    public HitscanOrProjectile hitscanOrProjectile;
    public enum HitscanOrProjectile
    {
        Hitscan = 0,
        Projectile = 1
    }

    [Separator("AI Settings")]
    public float expectedDamage;
    
    [Separator("Projectile Settings")]
    public GameObject projectilePrefab;
    public bool passTargetToProjectile;
    
    public bool shootsMultipleProjectiles;
    [ReadOnly(nameof(shootsMultipleProjectiles), true)] public int projectilesPerShot = 1;

    public float projectileSpreadAngle;

    [Separator("Fire Mode Settings")]
    [Tooltip("Rounds/min")] public float fireRate;
    [ReadOnly, Tooltip("Time (s)")] public float fireInterval; // Set at runtime by Weapon
    
    public bool canSwitchFireModes;
    
    public AvailableFireModes availableFireModes;
    [Flags] public enum AvailableFireModes
    {
        SemiAuto = 0x1,
        FullAuto = 0x2,
        Burst = 0x4,
    }
    
    public int burstLength;
    
    [Separator("Ammo Settings")]
    public bool usesAmmo;
    
    public bool consumesMultipleAmmo;
    [ReadOnly(nameof(consumesMultipleAmmo), true)] public int ammoConsumedPerShot = 1;
    
    public bool hasChamber;
    public int chamberCapacity;
    
    public bool hasMagazine;
    [ReadOnly(nameof(hasMagazine), true)] public int magazineCapacity;
    [ReadOnly(nameof(hasMagazine), true)] public bool magazineIsObject;
    //[ReadOnly(nameof(magazineIsObject), true)] public InventoryItem magazineItem;
    
    public bool hasReserveAmmo;
    [ReadOnly(nameof(hasReserveAmmo), true)] public bool drawsFromReserveAmmoDirectly;
    [ReadOnly(nameof(hasReserveAmmo), true)] public int maxReserveAmmo;
    [ReadOnly] public int totalAmmoInWeapon;
    
    [Separator("Reload Settings")]
    public bool needsReloading;
    [ReadOnly(nameof(needsReloading), true), Tooltip("Time (s)")] public float reloadTime;
    [ReadOnly(nameof(needsReloading), true)] public bool reloadsRoundsIndividually;
    [Tooltip("Determines if the weapon will be reloaded if the player attempts to use it while it's empty."), ReadOnly(nameof(needsReloading), true)]
    public bool canQuickReload;
}