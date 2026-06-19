using System;
using System.Collections.Generic;
using MyBox;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Credit to DeadCows' MyBox for additional editor attributes - https://github.com/Deadcows/MyBox/

[Serializable]
public class WeaponFunction
{
    [ReadOnly] public bool FunctionComplete;
    public InputActionReference InputAction;
    public List<WeaponFunctionCondition> FunctionConditions;
    public List<WeaponFunctionAction> FunctionActions;
}

[Serializable]
public class WeaponFunctionCondition
{
    public string ConditionName;
    [ReadOnly] public bool Fulfilled = false;
    public WeaponFunctionConditionType ConditionType;

    [ConditionalField(nameof(ConditionType), false, WeaponFunctionConditionType.CheckOtherCondition)]
    public string ConditionToMonitor; // string comparison for now - can't justify making every condition into a scriptableobject
    [ConditionalField(nameof(ConditionType), false, WeaponFunctionConditionType.CheckOtherCondition)]
    public bool DesireCompleted;

    [ConditionalField(nameof(ConditionType), false, WeaponFunctionConditionType.AnyProjectileActive)]
    public WeaponPart WeaponPart;

    [ConditionalField(nameof(ConditionType), false, WeaponFunctionConditionType.ChargedForTime)]
    public float ChargeTime;
    [ConditionalField(nameof(ConditionType), false, WeaponFunctionConditionType.ChargedForTime)]
    public bool AllowPartialCharge;

    [ConditionalField(nameof(ConditionType), false, WeaponFunctionConditionType.ChargedForTime)]
    public bool AutoRelease;

    public enum WeaponFunctionConditionType
    {
        Nothing,
        AnyProjectileActive,
        ChargedForTime,
        CheckOtherCondition,
    }
}

[Serializable]
public class WeaponFunctionAction
{
    public WeaponFunctionActions FunctionActions;
    [SerializeReference, ConditionalField(nameof(FunctionActions), false, WeaponFunctionActions.UseWeaponPart)]
    public WeaponPart WeaponPart;
    
    public enum WeaponFunctionActions
    {
        UseWeaponPart
    }
}


[Serializable]
public class ParticlesAndLayers
{
    public ParticleSystem particles;
    public LayerMask layers;
}

[Serializable]
public class ExplosionComponent
{
    public float explosionRadius;
    public bool scaleDamageWithDistance;
    
    public int explosionArmourPenetration;
    
    public bool destroySelf;
    public float explosionDelay;
    public LayerMask layersToHit;
    public int maxTargetsChecked;
}

[Serializable]
public class DamageComponent
{
    [Separator("Main Settings")]
    public float baseDamage;
    [FormerlySerializedAs("damageType")] public DamageTags damageTags;
    public DamageElement damageElement;
    public List<PassiveEffectScriptableObject> passiveEffectsAppliedToTarget;
    
    [Separator("Armour Penetration")]
    [SerializeField] public int armourPenetration;

    [Flags] public enum DamageTags
    {
        WeaponDamage = 1,
        SpellDamage = 2,
    }
    
    public enum DamageElement
    {
        Fire,
        Water,
        Ice,
        Air,
        Iron,
        Lunar,
        Cosmic,
        Life,
        Blood,
    }
    
    #if SPELL_SYSTEM
    [Separator("Spell Settings")]
    public bool enableAethersteal;
    public float aetherstealMultiplier = 1;
    
    public bool enableLifesteal;
    public float lifestealMultiplier = 1;
    #endif
}

[Serializable]
public class ProjectileComponent
{
    [ReadOnly] public long projectileActiveTime;
    
    [Tooltip("The damage component of the current projectile.")]
    public DamageComponent damageComponent;
    [Tooltip("The GameObject(s) that the projectile is able to spawn on impact, during flight, etc.")]
    public List<GameObject> projectileWarheads;
    public bool detonateWarheadsOnImpact;
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
    public bool spawnAsChildOfWeapon;
    public bool linkedToWeapon;
    [ConditionalField(nameof(linkedToWeapon), false)] public bool destroyWhenWeaponReleases;
    
    [Tooltip("Velocity (m/s)")] public float initialVelocity;
    [Tooltip("Velocity (m/s). 0 = No limit.")] public float maxVelocity;
    [Tooltip("Mass (g)")] public float projectileMass;

    [Separator("Tracking Settings")]
    public bool isTracking;
    [ConditionalField(nameof(isTracking), false)] public float trackingTurnTime;
    [ConditionalField(nameof(isTracking), false)] public float trackingForce;
    [ConditionalField(nameof(isTracking), false)] public float trackingPValue;
    [ConditionalField(nameof(isTracking), false)] public float trackingStartDelay;
    [ConditionalField(nameof(isTracking), false)] public float trackingActiveDuration;
    [ConditionalField(nameof(isTracking), false)] public bool snapToTargetAfterDuration;
    
    [ConditionalField(nameof(isTracking), false)] public TrackingModes currentTrackingMode;
    public enum TrackingModes
    {
        CannotSwitchTarget,
    }
    
    [Separator("Drag Settings")]
    public bool projectileDrag;
    [ConditionalField(nameof(projectileDrag), false)] public float projectileDragFactor;
    
    [Separator("Gravity Settings")]
    public bool projectileGravity;
    [ConditionalField(nameof(projectileGravity), false)] public float projectileGravityFactor;

    [Separator("Engine Settings")]
    [Tooltip("Whether the projectile is powered by a rocket engine or the like, and generates its own velocity after firing.")]
    public bool isPowered;
    [ConditionalField(nameof(isPowered), false)] public float enginePower;
    [ConditionalField(nameof(isPowered), false)] public float engineTime;
    [ConditionalField(nameof(isPowered), false)] public float engineDelay;

    #if LIQUID_SYSTEM
    [Separator("Liquid Settings")]
    public bool createsLiquid;
    [ConditionalField(nameof(createsLiquid), false)] public int radius;
    [ConditionalField(nameof(createsLiquid), false)] public Liquid liquidType;
    #endif
    
    [Separator("Magnetism Settings")]
    public bool isMagnetic;
    [ConditionalField(nameof(isMagnetic), false), Range(1f, 100f)]
    public float magnetAttractionFactor;
}