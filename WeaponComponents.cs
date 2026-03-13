using System;
using System.Collections.Generic;
using MyBox;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Credit to DeadCows' MyBox for additional editor attributes - https://github.com/Deadcows/MyBox/

[Serializable]
public class DamageComponent
{
    public float baseDamage;
    public List<PassiveEffectScriptableObject> passiveEffectsAppliedToTarget;
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

    #if SPELL_SYSTEM
    [Separator("Spell Settings")]
    public bool disableManasteal;
    #endif
    
    [Separator("Magnetism Settings")]
    public bool isMagnetic;
    [ReadOnly(nameof(isMagnetic) ,true), Range(1f, 100f)]
    public float magnetAttractionFactor;
}