using System;
using System.Collections.Generic;
using UnityEngine;
using MyBox;
using Unity.VisualScripting;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;


[Serializable, CreateAssetMenu(fileName = "WeaponScriptableObject", menuName = "Weapon Scriptable Object")]
// A WeaponObject is a ScriptableObject containing a WeaponComponent (the data structure)
// It exists to provide a WeaponComponent as a save-able template to create stats for individual guns
// It is used by a Weapon script to create a copy of itself on the weapon's prefab, to set runtime data such as FirePoint
public class WeaponScriptableObject : ScriptableObject
{
    // ReadOnly attributes will disable editing of the variable depending on a condition
    // We use these here to disable certain gun stats that are only relevant if another is true
    // e.g. a gun's chamber can only be loaded if it has one
    // Thus setting hasChamber to false will set chamberLoaded to read only

    public string name;
    [TextArea] public string desc;
    [TextArea] public string altDesc;
    
    [Separator("Unsorted Settings")]
    public GameObject weaponCrosshairImage;
    public GameObject weaponAimpointIcon;
    public GameObject weaponLockOnIcon;
    
    [Separator("Technical Settings")]
    public List<WeaponAction> WeaponActions;
    
    [Separator("Handling Settings")]
    public float weaponErgonomics;
    public float weaponWeight;
    public float weaponSway;
    public float weaponEquipTime;
    public float weaponUnequipTime;

    public List<WeaponPart> WeaponParts;
        
    public WeaponComponent weaponComponent;
    
}

[Serializable]
public class WeaponPart
{
    [Separator("Runtime States")] // move these to their individual sections at the top
    [ReadOnly] public bool isTriggerPulled;
    
    [ReadOnly, ConditionalField(nameof(hasChamber))] public bool isChamberLoaded;
    [ConditionalField(nameof(hasMagazine))] public int currentMagazineAmmo;
    [ConditionalField(nameof(hasReserveAmmo))] public int currentReserveAmmo;
    
    
    
    [ReadOnly] public WeaponComponent.FiringState firingState;
    [ReadOnly] public WeaponComponent.ReloadState reloadState;
    [ReadOnly] public WeaponComponent.WeaponAimType currentWeaponAimType;
    
    
    
    [Separator("Projectile Settings")]
    public HitscanOrProjectile hitscanOrProjectile;
    public enum HitscanOrProjectile
    {
        Projectile,
        Hitscan
    }
    public float hitscanRange;
    
    [DisplayInspector] public List<GameObject> projectilePrefabs;
    [FormerlySerializedAs("weaponRequiresTarget")] public bool requiresTarget;
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
    public SpellSchool spellSchool;
    public enum SpellType
    {
        Static,
        Projectile,
        AoE,
        Uncastable,
    }

    public enum SpellSchool
    {
        None,
        Healing,
        Pyromancy,
        Ferromancy,
        Lunar,
        Cryomancy,
        Hydromancy,
        Aero
    }
    
    public Texture2D spellIcon;
    public Texture[] spellImages;
    
    [ConditionalField(nameof(spellSchool), false, SpellSchool.Pyromancy)] public int embersCost;
    [ConditionalField(nameof(spellSchool), false, SpellSchool.Ferromancy)] public int swordCost;
    [ConditionalField(nameof(spellSchool), false, SpellSchool.Lunar)] public int astralAmmoCost;
    [ConditionalField(nameof(spellSchool), false, SpellSchool.Hydromancy)] public int waterLevelCost;
    
    public List<PassiveEffectScriptableObject> passiveEffectsAppliedToTarget;
    public List<PassiveEffectScriptableObject> passiveEffectsAppliedToCaster;
}

[Serializable]
public class WeaponAction : ScriptableObject
{
    public string Name;
    [ReadOnly] public bool ActionComplete;
    public InputActionReference InputActionListenedTo;
    public List<WeaponActionConditions> ActionConditions;
    public List<WeaponActionsToTake> ActionsToTake;
    public WeaponPart weaponPart;
    
    public enum WeaponActionsToTake
    {
        ShootWeaponPart
    }
    
    
}

[Serializable]
public class WeaponActionConditions
{
    [ReadOnly] public bool Fulfilled;
    public WeaponActionConditionType ConditionType;
    [ReadOnly(nameof(ConditionType), true, WeaponActionConditionType.ActionComplete, WeaponActionConditionType.ActionIncomplete)]
    [SerializeReference] public string ActionToMonitor; // TEMP - this will be cleaner in future, remake with UI toolkit
    
    public enum WeaponActionConditionType
    {
        Nothing,
        ProjectileActive,
        ChargedForTime,
        ActionComplete,
        ActionIncomplete,
    }
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

    public string name;
    [TextArea] public string desc;
    [TextArea] public string altDesc;
    
    [Separator("Unsorted Settings")]
    public GameObject weaponCrosshairImage;
    public GameObject weaponAimpointIcon;
    public GameObject weaponLockOnIcon;
    
    [Separator("Technical Settings")]
    [SerializeReference] List<WeaponAction> WeaponActions;
    
    [Separator("Handling Settings")]
    public float weaponErgonomics;
    public float weaponWeight;
    public float weaponSway;
    public float weaponEquipTime;
    public float weaponUnequipTime;

    public List<WeaponPart> WeaponParts;
    
    [Separator("Runtime States")] // move these to their individual sections at the top
    [ReadOnly] public bool isTriggerPulled;
    
    [ReadOnly, ConditionalField(nameof(hasChamber))] public bool isChamberLoaded;
    [ConditionalField(nameof(hasMagazine))] public int currentMagazineAmmo;
    [ConditionalField(nameof(hasReserveAmmo))] public int currentReserveAmmo;
    
    
    
    [ReadOnly] public FiringState firingState;
    public enum FiringState
    {
        Cycling,
        ReadyToFire,
    }
    
    public WeaponAimType currentWeaponAimType;
    public enum WeaponAimType
    {
        Crosshair,
        LockOn,
        GroundOnly
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
        Hitscan = 0, // stop making hitscan the default lol make a new weapon actually usable out of box
        Projectile = 1
    }
    public float hitscanRange;
    
    [DisplayInspector] public List<GameObject> projectilePrefabs;
    [FormerlySerializedAs("weaponRequiresTarget")] public bool requiresTarget;
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
    public SpellSchool spellSchool;
    public enum SpellType
    {
        Static,
        Projectile,
        AoE,
        Uncastable,
    }

    public enum SpellSchool
    {
        None,
        Healing,
        Pyromancy,
        Ferromancy,
        Lunar,
        Cryomancy,
        Hydromancy,
        Aero
    }
    
    public Texture2D spellIcon;
    public Texture[] spellImages;
    
    [ConditionalField(nameof(spellSchool), false, SpellSchool.Pyromancy)] public int embersCost;
    [ConditionalField(nameof(spellSchool), false, SpellSchool.Ferromancy)] public int swordCost;
    [ConditionalField(nameof(spellSchool), false, SpellSchool.Lunar)] public int astralAmmoCost;
    [ConditionalField(nameof(spellSchool), false, SpellSchool.Hydromancy)] public int waterLevelCost;
    
    public List<PassiveEffectScriptableObject> passiveEffectsAppliedToTarget;
    public List<PassiveEffectScriptableObject> passiveEffectsAppliedToCaster;
}