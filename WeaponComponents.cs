using System;
using System.Collections.Generic;
using System.Reflection;
using MyBox;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Object = System.Object;

// Credit to DeadCows' MyBox for additional editor attributes - https://github.com/Deadcows/MyBox/



// todo look into weaponfunctioncooldown in future, so cyclic weapon functions can have independent/variable cooldowns between attacks without abusing weaponcooldown
[Serializable]
public class WeaponFunction
{
    public InputActionReference InputAction;
    public WeaponPart MainWeaponPart;
    public List<WeaponFunctionCondition> FunctionConditions;
    public List<WeaponFunctionAction> FunctionActions;
}

[Serializable]
public class WeaponFunctionCondition
{
    public string ConditionName;
    [ReadOnly] public bool Fulfilled = true;
    public WeaponFunctionConditionType ConditionType;

    [ConditionalField(nameof(ConditionType), false, WeaponFunctionConditionType.CheckOtherCondition)]
    public string ConditionToMonitor; // string comparison for now - can't justify making every condition into a scriptableobject
    [ConditionalField(nameof(ConditionType), false, WeaponFunctionConditionType.CheckOtherCondition)]
    public bool DesireCompleted;

    [ConditionalField(nameof(ConditionType), false, WeaponFunctionConditionType.AnyProjectileActive, WeaponFunctionCondition.WeaponFunctionConditionType.WeaponPartCharged)]
    public WeaponPart WeaponPart;

    public enum WeaponFunctionConditionType
    {
        Nothing,
        AnyProjectileActive,
        WeaponPartCharged,
        CheckOtherCondition,
    }
}

[Serializable]
public class WeaponFunctionAction
{
    public WeaponFunctionActionType functionActionType;
    [SerializeReference, ConditionalField(nameof(functionActionType), false, WeaponFunctionActionType.UseWeaponPart)]
    public WeaponPart WeaponPart;

    [SerializeField, ConditionalField(nameof(functionActionType), false, WeaponFunctionActionType.InvokeMethod)]
    public UnityEvent MethodEvent;
    
    public enum WeaponFunctionActionType
    {
        UseWeaponPart,
        InvokeMethod
    }
}

[Serializable]
public class WeaponProjectile
{
    public GameObject Projectile;
    
    [Min(1)] public int Count = 1;
    public float SpreadAngle;
}

[Serializable]
public class ParticlesAndLayers
{
    public ParticleSystem particles;
    public LayerMask layers;
}

[Serializable]
public class WeaponParticles
{
    public ParticleSystem particles;
    public bool spawnAsChild;
    public bool scaleWithCharge;

    [ConditionalField(nameof(scaleWithCharge))] public ParticleSystem.MinMaxCurve minChargeStartSpeed;
    [ConditionalField(nameof(scaleWithCharge))] public ParticleSystem.MinMaxCurve maxChargeStartSpeed;
    [ConditionalField(nameof(scaleWithCharge))] public ParticleSystem.MinMaxCurve minChargeBurstSize;
    [ConditionalField(nameof(scaleWithCharge))] public ParticleSystem.MinMaxCurve maxChargeBurstSize;


    public ParticleSystem.MinMaxCurve InterpolateMinMaxCurve(ParticleSystem.MinMaxCurve a, ParticleSystem.MinMaxCurve b, float t)
    {
        float a1 = 0;
        float a2 = 0;
        float b1 = 0;
        float b2 = 0;
        
        switch (a.mode)
        {
            case ParticleSystemCurveMode.Constant:
                a1 = a.constant;
                a2 = a.constant;
                break;
            case ParticleSystemCurveMode.Curve:
                
                break;
            case ParticleSystemCurveMode.TwoCurves:
                
                break;
            case ParticleSystemCurveMode.TwoConstants:
                a1 = a.constantMin;
                a2 = a.constantMax;
                break;
        }

        switch (b.mode)
        {
            case ParticleSystemCurveMode.Constant:
                b1 = b.constant;
                b2 = b.constant;
                break;
            case ParticleSystemCurveMode.Curve:
                
                break;
            case ParticleSystemCurveMode.TwoCurves:
                
                break;
            case ParticleSystemCurveMode.TwoConstants:
                b1 = b.constantMin;
                b2 = b.constantMax;
                break;
        }
        
        return new ParticleSystem.MinMaxCurve(Mathf.Lerp(a1, b1, t), Mathf.Lerp(a2, b2, t));
    }
}

[Serializable]
public class ExplosionComponent
{
    [HideInInspector] public float explosionScalar;
    public bool scaleExplosion;

    public float explosionDelay;
    public LayerMask layersToHit;
    public int maxTargetsChecked;
    public bool destroySelf;
    
    public ExplosionShape explosionShape;
    
    private bool ScaleAndSphere() => explosionShape == ExplosionShape.Sphere && scaleExplosion;
    [ConditionalField(nameof(explosionShape), false, ExplosionShape.Sphere)] public float explosionRadius;
    [ConditionalField(true, nameof(ScaleAndSphere))] public float explosionRadiusMax;
    
    private bool ScaleAndCustom() => explosionShape == ExplosionShape.CustomCollider && scaleExplosion;
    [ConditionalField(nameof(explosionShape), false, ExplosionShape.CustomCollider)] public Collider explosionHitbox;
    [ConditionalField(true, nameof(ScaleAndCustom))] public Collider explosionHitboxMax;
    
    [Separator("Damage")]
    public DamageComponent damageComponent;
    [ReadOnly] public AnimationCurve damageFalloff;
    
    [Separator("Knockback")]
    public bool applyKnockback;
    [ConditionalField(nameof(applyKnockback))] public float knockbackForce;
    [ReadOnly, ConditionalField(nameof(applyKnockback))] public AnimationCurve knockbackFalloff;

    [Separator("Particles")]
    public bool hasParticles;
    [ConditionalField(nameof(hasParticles))] public WeaponParticles explosionParticles;
    
    public enum ExplosionShape
    {
        Sphere,
        CustomCollider
    }
}

[Serializable]
public class DamageComponent
{
    [HideInInspector] public float damageScalar;
    public bool scaleDamage;
    [Separator("Main Settings")]
    public float baseDamage;
    
    [ConditionalField(nameof(scaleDamage))] public float maxDamage;
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
        None,
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
    [ReadOnly] public float projectileCharge;
    
    [Tooltip("The damage component of the current projectile.")]
    public DamageComponent damageComponent;
    [Tooltip("The GameObject(s) that the projectile is able to spawn on impact, during flight, etc.")]
    public List<GameObject> projectileWarheads;
    [FormerlySerializedAs("detonateWarheadsOnImpact")] public bool detonateWarheadsOnDestroy;
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
    public bool destroyInstantly;
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
    [ConditionalField(nameof(createsLiquid), false)] public bool isSplatter;
    [ConditionalField(nameof(createsLiquid), false)] public int radius;
    [ConditionalField(nameof(createsLiquid), false)] public Liquid liquidType;
    #endif
    
    [Separator("Magnetism Settings")]
    public bool isMagnetic;
    [ConditionalField(nameof(isMagnetic), false), Range(1f, 100f)]
    public float magnetAttractionFactor;
}