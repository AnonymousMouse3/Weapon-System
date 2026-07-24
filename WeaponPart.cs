using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MyBox;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

[Serializable, CreateAssetMenu(fileName = "WeaponPart", menuName = "Weapon System/Weapon Part")]
public class WeaponPart : ScriptableObject
{
    [Separator("Set in Prefab")]
    [ReadOnly] public GameObject firePoint;
    
    [Separator("Runtime States")] // move these to their individual sections at the top
    [ReadOnly] public WeaponScriptableObject parentWeaponScriptableObject;
    [ReadOnly] public bool isTriggerPulled;
    [ReadOnly] public GameObject target;
    [ReadOnly] public float lastCharge;
    
    [ReadOnly] public List<GameObject> spawnedProjectiles;
    
    [ReadOnly, ConditionalField(nameof(hasChamber))] public bool isChamberLoaded;
    [ConditionalField(nameof(hasMagazine))] public int currentMagazineAmmo;
    [ConditionalField(nameof(hasReserveAmmo))] public int currentReserveAmmo;
    
    [ReadOnly] public FireModes currentFireMode;
    
    [ReadOnly] public float chargePercent;
    [ReadOnly] public float fireRateMultiplier = 1;
    [ReadOnly] public float damageMultiplier = 1;
    [ReadOnly] public float burstCounter;
    
    [ReadOnly] public GameObject worldAimpointInstance;
    [ReadOnly] public Image worldAimpointInstanceImage;
    
    
    [ReadOnly] public CycleState cycleState;
    public enum CycleState
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
    
    [ReadOnly] public ChargeState chargeState;
    public enum ChargeState
    {
        Uncharged,
        Charging,
        Charged,
    }

    [FormerlySerializedAs("weaponPartFlags")] [Separator("Data")]
    public WeaponPartTags weaponPartTags;

    [Flags] public enum WeaponPartTags
    {
        PrimaryFire = 1,
        SecondaryFire = 2,
        CanModifyFireRate = 4,
        CanModifyDamage = 8,
    }

    [Separator("UI")]
    public GameObject aimpointIcon;
    public bool drawAimpoint;
    public GameObject targetIcon;
    public bool drawTargetIcon;
    
    [Separator("Aim")]
    public WeaponAimType aimType;
    public enum WeaponAimType
    {
        Crosshair,
        LockOn,
        GroundOnly
    }
    
    [Separator("Projectiles")]
    public HitscanOrProjectile hitscanOrProjectile;
    public enum HitscanOrProjectile
    {
        Projectile,
        Hitscan
    }
    [ConditionalField(nameof(hitscanOrProjectile), false, HitscanOrProjectile.Hitscan)] public float hitscanRange;
    
    public List<WeaponProjectile> projectiles;
    public bool requiresTarget;
    public bool passTargetToProjectile;
    public bool updateTargetContinuously;
    
    [Separator("Effects")]
    public List<PassiveEffectScriptableObject> passiveEffectsAppliedToTarget;
    public List<PassiveEffectScriptableObject> passiveEffectsAppliedToSelf;

    [Separator("Fire Mode")]
    [SerializeField, Tooltip("Rounds/min")] private float fireRate; // cached variable for editor
    [SerializeField, Tooltip("Time (s)")] private float cooldown; // cached variable for editor
    [Tooltip("Whether this WeaponPart will respect the shared weapon cooldown")] public bool hasIndependentCooldown;
    [Tooltip("Time (s) that the weapon (not just the weapon part) will be on cooldown for")] public float weaponCooldown;
    [Tooltip("Time (s) that the entire weapon group will be on cooldown for")] public float weaponGroupCooldown;
    [HideInInspector] public float _fireRate; // real fire rate
    [HideInInspector] public float _cooldown; // real cooldown
    [HideInInspector] public float minimumFireRate = 0.0001f;
    
    public bool canSwitchFireModes;
    
    public FireModes availableFireModes = FireModes.SemiAuto;
    [Flags] public enum FireModes
    {
        SemiAuto = 1,
        FullAuto = 2,
        Burst = 4,
    }
    
    public int burstLength;

    [Separator("Sound")]
    AudioClip placeholder;
    
    [Separator("Knockback")]
    public bool applyUserKnockback;
    [ConditionalField(nameof(applyUserKnockback))] public float knockbackForce;
    [ConditionalField(nameof(applyUserKnockback))] public bool scaleKnockbackWithCharge;
    [ConditionalField(nameof(applyUserKnockback), nameof(scaleKnockbackWithCharge))] public float maxChargeKnockbackForce;
    
    [Separator("Charge")]
    public bool canCharge;
    [ConditionalField(nameof(canCharge))] public bool passChargeToProjectile;
    [ConditionalField(nameof(canCharge))] public float maxChargeTime;
    [ConditionalField(nameof(canCharge))] public bool allowPartialCharge;
    [ConditionalField(nameof(canCharge))] public bool autoRelease;
    [ConditionalField(nameof(canCharge))] public bool allowChargeOnCooldown;
    [ConditionalField(nameof(canCharge))] public bool allowChargeOnWeaponCooldown;
    
    [Separator("Ammo")]
    public bool usesAmmo;
    public int ammoConsumedPerShot = 1;
    
    [Separator("Chambers")]
    public bool hasChamber;
    [ConditionalField(nameof(hasChamber))]public List<bool> chambers;
    
    [Separator("Magazines")]
    public bool hasMagazine;
    [ConditionalField(nameof(hasMagazine))] public int magazineCapacity;
    [ConditionalField(nameof(hasMagazine))] public bool magazineIsObject;
    //[ReadOnly(nameof(magazineIsObject), true)] public InventoryItem magazineItem;
    
    [Separator("Reserve Ammo")]
    public bool hasReserveAmmo;
    [ConditionalField(nameof(hasReserveAmmo))] public bool drawsFromReserveAmmoDirectly;
    [ConditionalField(nameof(hasReserveAmmo))] public int maxReserveAmmo;
    [ConditionalField] public int totalAmmoInWeapon;
    
    [Separator("Reload")]
    public bool needsReloading;
    [ConditionalField(nameof(needsReloading))] public InputActionReference reloadAction;
    [ConditionalField(nameof(needsReloading)), Tooltip("Time (s)")] public float reloadTime;
    [ConditionalField(nameof(needsReloading))] public bool reloadsRoundsIndividually;
    [Tooltip("Determines if the weapon will be reloaded if the player attempts to shoot while empty.")]
    [ConditionalField(nameof(needsReloading))] public bool canQuickReload;
    
    [Separator("Recoil")]
    public bool hasRecoil;
    [ConditionalField(nameof(hasRecoil))] public Vector3 recoil;
    [ConditionalField(nameof(hasRecoil))] public float aimPunch;
    [ConditionalField(nameof(hasRecoil))] public CinemachineImpulseDefinition impulseDefinition;
    
    [Separator("Particles")]
    public bool hasParticles;
    [ConditionalField(nameof(hasParticles))] public WeaponParticles onShootWeaponParticles;
    
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
    
    [FormerlySerializedAs("manaCost")] [ConditionalField(nameof(isSpell), false)] public int aetherCost;
    [ConditionalField(nameof(isSpell))] public int healthCost;
    
    [ConditionalField(nameof(isSpell))] public SpellSchool spellSchool;
    
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
    public Task chargeTask;
    public Stopwatch chargeTimer;
    public CancellationTokenSource chargeCTS;
    
    
    [Separator("Deprecated")]
    // deprecated
    [FormerlySerializedAs("projectilePrefabs")] [DisplayInspector] public List<GameObject> oldProjectilePrefabs;
    // deprecated
    public bool shootsMultipleProjectiles;
    [ConditionalField(nameof(shootsMultipleProjectiles))] public int projectilesPerShot = 1;
    public float projectileSpreadAngle;

    void OnValidate()
    {
        // if the cached value is not equal to the real value, then that is the value that was changed
        if (!Mathf.Approximately(cooldown, _cooldown) && cooldown > 0)
        {
            _fireRate = 60 / cooldown;
            
            fireRate = _fireRate;
            _cooldown = cooldown;
            return;
        }

        if (!Mathf.Approximately(fireRate, _fireRate) && fireRate > 0)
        {
            _cooldown = 60 / fireRate;
        
            cooldown = _cooldown;
            _fireRate = fireRate;
            return;
        }
        
        if (cooldown <= 0)
        {
            cooldown = minimumFireRate;
            
            _cooldown = 60 / fireRate;
            cooldown = _cooldown;
            _fireRate = fireRate;
        }
        
        if (fireRate <= 0)
        {
            fireRate = minimumFireRate;
            _fireRate = 60 / cooldown;
            
            fireRate = _fireRate;
            _cooldown = cooldown;
        }
    }

    public void SetupWeaponPart(WeaponScriptableObject weaponScriptableObject = null)
    {
        if (weaponScriptableObject) parentWeaponScriptableObject = weaponScriptableObject;
            
        //replace with an actual check on start
        cycleState = CycleState.ReadyToFire;
        reloadState = ReloadState.ReadyToFire;
        chargeState = ChargeState.Uncharged;
        
        cycleCTS = new CancellationTokenSource();
        reloadCTS = new CancellationTokenSource();
        chargeCTS = new CancellationTokenSource();
            
        cycleTask = Task.CompletedTask;
        reloadTask = Task.CompletedTask;
        chargeTask = Task.CompletedTask;

        cycleTimer = Stopwatch.StartNew();
        cycleTimer.Stop();
        reloadTimer = Stopwatch.StartNew();
        reloadTimer.Stop();
        chargeTimer = Stopwatch.StartNew();
        chargeTimer.Stop();
        
        // for now, default to the first available fire mode
        foreach (FireModes fireMode in Enum.GetValues(typeof(FireModes)))
        {
            if (!availableFireModes.HasFlag(fireMode)) continue;
            currentFireMode = fireMode;
            break;
        }

        if (currentFireMode == 0) currentFireMode = FireModes.SemiAuto;
        
        fireRateMultiplier = 1;

        isChamberLoaded = true; // change dynamically
        currentMagazineAmmo = magazineCapacity;
        currentReserveAmmo = maxReserveAmmo; // change dynamically in future, obviously
            
        if (projectiles.IsNullOrEmpty()) { Debug.Log("Weapon part has no projectile. Assign one in the inspector."); return; }
    }
}