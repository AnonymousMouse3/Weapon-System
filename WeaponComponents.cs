using System;
using System.Collections.Generic;
using MyBox;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Credit to DeadCows' MyBox for additional editor attributes - https://github.com/Deadcows/MyBox/

[Serializable]
public class WeaponAction
{
    [ReadOnly] public bool ActionComplete;
    public string ActionName;
    public InputActionReference InputActionListenedTo;
    public List<WeaponActionCondition> ActionConditions;
    public List<WeaponActionsToTake> ActionsToTake;
    [SerializeReference] public WeaponPart weaponPart;
    
    public enum WeaponActionsToTake
    {
        ShootWeaponPart
    }
}

[Serializable]
public class WeaponActionCondition
{
    [ReadOnly] public bool Fulfilled;
    public WeaponActionConditionType ConditionType;

    [ConditionalField(nameof(ConditionType), false, WeaponActionConditionType.ActionComplete,
        WeaponActionConditionType.ActionIncomplete)]
    public WeaponAction ActionToMonitor; // TEMP - this will be cleaner in future, remake with UI toolkit

    [ConditionalField(nameof(ConditionType), false, WeaponActionConditionType.AnyProjectileActive)]
    public WeaponPart weaponPart;

    [ConditionalField(nameof(ConditionType), false, WeaponActionConditionType.ChargedForTime)]
    public float ChargeTime;

    public enum WeaponActionConditionType
    {
        Nothing,
        AnyProjectileActive,
        ChargedForTime,
        ActionComplete,
        ActionIncomplete,
    }
}

[Serializable]
public class DamageComponent
{
    [Separator("Main Settings")]
    public float baseDamage;
    public List<PassiveEffectScriptableObject> passiveEffectsAppliedToTarget;
    
    [Separator("Explosion Settings")]
    [SerializeField] public bool isExplosive;
    [SerializeField] public float explosionRadius;
    [SerializeField] public bool scaleDamageWithDistance;
    
    [SerializeField] public bool destroySelf;
    [SerializeField] public float explosionDelay;
    [SerializeField] public LayerMask layersToHit;
    [SerializeField] public int maxTargetsChecked;
    
    [Separator("Armour Penetration")]
    [SerializeField] public int explosionArmourPenetration;
    
    [Separator("Status Effect Settings")]
    public bool appliesDamageOverTime;
    [ReadOnly(nameof(appliesDamageOverTime), true)] public float damagePerTick;
    [ReadOnly(nameof(appliesDamageOverTime), true), Min(0.1f)] public float damageTickDuration;
    
    #if SPELL_SYSTEM
    [Separator("Spell Settings")]
    public bool enableManasteal;
    public float manastealMultiplier = 1;
    
    public bool enableLifesteal;
    public float lifestealMultiplier = 1;
    #endif
}

[Serializable]
public class ParticlesAndLayers
{
    public ParticleSystem particles;
    public LayerMask layers;
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
    public List<ParticlesAndLayers> onHitParticles;
    
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
    public bool spawnAsChildOfWeapon;
    public bool linkedToWeapon;
    [ReadOnly(nameof(linkedToWeapon), true)] public bool destroyWhenWeaponReleases;
    
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