using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MyBox;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

[Serializable, CreateAssetMenu(fileName = "WeaponPart", menuName = "Weapon Part")]
public class WeaponPart : ScriptableObject
{
    [Separator("Set in Prefab")]
    [ReadOnly] public GameObject firePoint;
    
    [Separator("Runtime States")] // move these to their individual sections at the top
    [ReadOnly] public WeaponScriptableObject parentWeaponScriptableObject;
    [ReadOnly] public bool isTriggerPulled;
    [ReadOnly] public GameObject target;
    
    [ReadOnly] public GameObject currentProjectile;
    [ReadOnly] public List<GameObject> spawnedProjectiles;
    
    [ReadOnly, ConditionalField(nameof(hasChamber))] public bool isChamberLoaded;
    [ConditionalField(nameof(hasMagazine))] public int currentMagazineAmmo;
    [ConditionalField(nameof(hasReserveAmmo))] public int currentReserveAmmo;
    
    [ReadOnly] public float fireRateMultiplier = 1;
    [ReadOnly] public float damageMultiplier = 1;
    [ReadOnly] public float burstCounter;
    
    [ReadOnly] public GameObject worldAimpointInstance;
    [ReadOnly] public Image worldAimpointInstanceImage;
    
    
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
    public WeaponPartFlags weaponPartFlags;

    [Flags]
    public enum WeaponPartFlags
    {
        PrimaryFire,
        SecondaryFire,
    }

    [Separator("UI Settings")]
    public GameObject aimpointIcon;
    public bool drawAimpoint;
    public GameObject targetIcon;
    public bool drawTargetIcon;
    
    [Separator("Aim Settings")]
    public WeaponAimType aimType;
    public enum WeaponAimType
    {
        Crosshair,
        LockOn,
        GroundOnly
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
    
    [Separator("Effects")]
    public List<PassiveEffectScriptableObject> passiveEffectsAppliedToTarget;
    public List<PassiveEffectScriptableObject> passiveEffectsAppliedToSelf;

    [Separator("Fire Mode Settings")]
    [Tooltip("Rounds/min")] public float fireRate; // editor script to link these two
    [ReadOnly, Tooltip("Time (s)")] public float cooldown;
    [Tooltip("Time before end of cooldown, input queues a shot soon as cooldown ends")] public float inputBufferTime;
    
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
    
    [Separator("Ammo")]
    public bool usesAmmo;
    
    [Separator("Multi-Shot")]
    public bool consumesMultipleAmmo;
    public int ammoConsumedPerShot = 1;
    public bool shootsMultipleProjectiles;
    public int projectilesPerShot = 1;
    public float projectileSpreadAngle;
    
    [Separator("Chambers")]
    public bool hasChamber;
    [ConditionalField(nameof(hasChamber), false)]public List<bool> chambers;
    
    [Separator("Magazines")]
    public bool hasMagazine;
    [ConditionalField(nameof(hasMagazine), false)] public int magazineCapacity;
    [ConditionalField(nameof(hasMagazine), false)] public bool magazineIsObject;
    //[ReadOnly(nameof(magazineIsObject), true)] public InventoryItem magazineItem;
    
    [Separator("Reserve Ammo")]
    public bool hasReserveAmmo;
    [ConditionalField(nameof(hasReserveAmmo), false)] public bool drawsFromReserveAmmoDirectly;
    [ConditionalField(nameof(hasReserveAmmo), false)] public int maxReserveAmmo;
    [ConditionalField] public int totalAmmoInWeapon;
    
    [Separator("Reload")]
    public bool needsReloading;
    [ConditionalField(nameof(needsReloading), false)] public InputActionReference reloadAction;
    [ConditionalField(nameof(needsReloading), false), Tooltip("Time (s)")] public float reloadTime;
    [ConditionalField(nameof(needsReloading), false)] public bool reloadsRoundsIndividually;
    [Tooltip("Determines if the weapon will be reloaded if the player attempts to shoot while empty.")]
    [ConditionalField(nameof(needsReloading), false)] public bool canQuickReload;
    
    #if SQUADS
    [Separator("Advanced AI")]
    public float expectedDamage;
    
    [FormerlySerializedAs("squadWeaponTags")] [Separator("Squad Settings")]
    
    // these tags are not meant to replace weapon attributes (things which would be shown on a stats screen)
    // these are simply to be used to determine how weapons are selected under situations like direct control
    public SquadWeaponTags squadWeaponTag;
    public enum SquadWeaponTags
    {
        RapidFire,
        SingleShot,
        ShortRange,
        Heavy,
        Vehicle,
    }
    #endif
    
    #if SPELL_SYSTEM
    [Separator("Spells")]
    public bool isSpell;
    
    [ConditionalField(nameof(isSpell), false)] public int manaCost;
    [ConditionalField(nameof(isSpell), false)] public int healthCost;
    
    [ConditionalField(nameof(isSpell), false), Tooltip("How long until the spell can be re-used")] public float spellCooldownTime;
    [ConditionalField(nameof(isSpell), false), Tooltip("How long the spell prevents any spell from being cast")] public float weaponGroupCooldownTime;

    [ConditionalField(nameof(isSpell), false)] public SpellType spellType;
    public enum SpellType
    {
        Static,
        Projectile,
        AoE,
        Uncastable,
    }
    
    [ConditionalField(nameof(isSpell), false)] public SpellSchool spellSchool;
    
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
    
    [ConditionalField(nameof(spellSchool), false, SpellSchool.Pyromancy)] public int embersCost;
    [ConditionalField(nameof(spellSchool), false, SpellSchool.Ferromancy)] public int swordCost;
    [ConditionalField(nameof(spellSchool), false, SpellSchool.Lunar)] public int astralAmmoCost;
    [ConditionalField(nameof(spellSchool), false, SpellSchool.Hydromancy)] public int waterLevelCost;
    #endif
    
    public Task cycleTask;
    public Stopwatch cycleTimer;
    public CancellationTokenSource cycleCTS;
    public Task reloadTask;
    public Stopwatch reloadTimer;
    public CancellationTokenSource reloadCTS;

    public void SetupWeaponPart(WeaponScriptableObject weaponScriptableObject)
    {
        parentWeaponScriptableObject = weaponScriptableObject;
            
        //replace with an actual check on start
        firingState = WeaponPart.FiringState.ReadyToFire;
        reloadState = WeaponPart.ReloadState.ReadyToFire;
            
        cycleTask = Task.CompletedTask;
        reloadTask = Task.CompletedTask;
            
        cooldown = 60 / fireRate;
        fireRateMultiplier = 1;

        isChamberLoaded = true; // change dynamically
        currentMagazineAmmo = magazineCapacity;
        currentReserveAmmo = maxReserveAmmo; // change dynamically in future, obviously
            
        if (projectilePrefabs.IsNullOrEmpty()) { Debug.Log("Weapon part has no projectile. Assign one in the inspector."); return; }
        currentProjectile = projectilePrefabs[0]; // change/remember the default during gameplay? hardcoded for now
    }
}