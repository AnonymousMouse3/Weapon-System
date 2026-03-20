using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyBox;
using UnityEngine;

[Serializable, CreateAssetMenu(fileName = "WeaponPart", menuName = "Weapon Part")]
public class WeaponPart : ScriptableObject
{
    public WeaponScriptableObject parentWeaponScriptableObject;
    
    [Separator("Runtime States")] // move these to their individual sections at the top
    [ReadOnly] public bool isTriggerPulled;
    
    [ReadOnly, ConditionalField(nameof(hasChamber))] public bool isChamberLoaded;
    [ConditionalField(nameof(hasMagazine))] public int currentMagazineAmmo;
    [ConditionalField(nameof(hasReserveAmmo))] public int currentReserveAmmo;
    
    [ReadOnly] public GameObject currentProjectile;
    [ReadOnly] public List<GameObject> spawnedProjectiles;
    
    [ReadOnly] public float burstCounter;
    [ReadOnly] public int firePointCounter;
    [ReadOnly] public float baseFireRate;
    
    public Task cycleTask;
    public CancellationTokenSource cycleCTS;
    public Task reloadTask;
    public CancellationTokenSource reloadCTS;
    
    
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
    
    
    [Separator("Projectile Settings")]
    public HitscanOrProjectile hitscanOrProjectile;
    public enum HitscanOrProjectile
    {
        Projectile,
        Hitscan
    }
    public float hitscanRange;
    
    [DisplayInspector] public List<GameObject> projectilePrefabs;
    public bool requiresTarget;
    public bool passTargetToProjectile;
    public bool updateTargetContinuously;
    
    public bool shootsMultipleProjectiles;
    [ReadOnly(nameof(shootsMultipleProjectiles), true)] public int projectilesPerShot = 1;

    public float projectileSpreadAngle;

    [Separator("Fire Mode Settings")]
    [Tooltip("Rounds/min")] public float fireRate;
    [ReadOnly, Tooltip("Time (s)")] public float fireInterval; // Set at runtime by Weapon
    
    public bool canSwitchFireModes;
    public FireModes currentFireMode;
    public FireModes availableFireModes;
    public enum FireModes
    {
        SemiAuto = 0,
        FullAuto = 1,
        Burst = 2,
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
    [Tooltip("Determines if the weapon will be reloaded if the player attempts to shoot while empty."), ReadOnly(nameof(needsReloading), true)]
    public bool canQuickReload;

    [Separator("Project-Specific Settings")] 
    public bool dummyBool;
    
    [Separator("Advanced AI Settings")]
    public float expectedDamage;
    
    [Separator("Spell Settings")]
    public bool isSpell;
    
    public int manaCost;
    public int healthCost;
    
    [Tooltip("How long until the spell can be re-used")] public float spellCooldownTime;
    [Tooltip("How long the spell prevents any spell from being cast")] public float slotCooldownTime;

    public SpellType spellType;
    public enum SpellType
    {
        Static,
        Projectile,
        AoE,
        Uncastable,
    }
    
    public List<PassiveEffectScriptableObject> passiveEffectsAppliedToTarget;
    public List<PassiveEffectScriptableObject> passiveEffectsAppliedToCaster;
    
    public float attackSpeedModifier;
    public float damageModifier;
}