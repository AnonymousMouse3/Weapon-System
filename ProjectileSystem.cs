using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using MouseLib;
using MyBox;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

// todo use movement related values from movementsystem if present
public class ProjectileSystem : MonoBehaviour
{
    public static event Action<GameObject, float, float, Vector3> OnImpact;
    public static event Action<GameObject, GameObject, ProjectileComponent> OnHit; // this projectile, target hit, damage
    
    [FormerlySerializedAs("lingeringEffectsOnDestroy")] [SerializeField] public List<GameObject> dontDestroyLingeringEffects;
    [SerializeField] public Rigidbody rb;
    [SerializeField, ReadOnly] public GameObject trackingTarget;
    [SerializeField] public ProjectileComponent projectileComponent;
    
    [SerializeField] public GameObject projectileOwner; // the character who owns the projectile
    [SerializeField] public Weapon weaponFiredFrom; // the weapon that fired it
    [SerializeField] public WeaponPart weaponPartFiredFrom; // the weaponpart that fired it

    private bool trackingAllowed;
    private bool trackingDelayTimers;
    
    private bool accuracyIncreaseAllowed;
    private bool accuracyDelayTimer;
    
    private bool engineAllowed;
    private bool beingDestroyed;

    private void OnEnable()
    {
        Weapon.OnWeaponRelease += WeaponReleaseCheck;
    }

    private void OnDisable()
    {
        Weapon.OnWeaponRelease -= WeaponReleaseCheck;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        if (projectileComponent.projectileLifetime > 0f)
        {
            DestroyProjectile(projectileComponent.projectileLifetime);
        }

        if (gameObject.TryGetComponent(out Rigidbody rb))
        {
            rb.useGravity = projectileComponent.projectileGravity;
        }
        
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        EngineTimers();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        
        if (projectileComponent.maxVelocity <= 0) return;
        rb.maxLinearVelocity = projectileComponent.maxVelocity;
    }

    void FixedUpdate()
    {
        if (projectileComponent.projectileGravity)
        {
            ApplyGravity();
        }
        
        if (projectileComponent.projectileDrag)
        {
            ApplyDrag();
        }
        
        if (projectileComponent.isTracking)
        {
            HandleTracking();
        }
        
        if (projectileComponent.isPowered && engineAllowed)
        {
            // Apply engine force until engine fuel runs out
            rb.AddForce(transform.forward * projectileComponent.enginePower, ForceMode.Acceleration);
        }
    }

    public void ApplyVelocityToProjectile()
    {
        if (!rb) return;
        rb.AddForce(transform.forward * projectileComponent.initialVelocity, ForceMode.Impulse);
    }

    public void RotateProjectile(Quaternion rotation)
    {
        transform.rotation = rotation;
    }

    public void ParentProjectile(GameObject newParent)
    {
        gameObject.transform.parent = newParent.transform;
    }

    private void HandleTracking()
    {
        if (!projectileComponent.isTracking) return;
        if (!trackingTarget) return;
        
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        HandleTrackingTimers();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        
        // stop tracking if target is outside a vision cone
        /*if (Vector3.Dot(trackingTarget.transform.position - transform.position, transform.forward) <= 0.8f)
        {
            trackingAllowed = false;
        }*/
        
        if (!trackingAllowed) return;
        
        
        ApplyProportionalNavigationLineOfSight();
    }

    private async Task HandleTrackingTimers()
    {
        if (!trackingDelayTimers)
        {
            trackingDelayTimers = true;
            await MouseTools.AwaitableTimer(projectileComponent.trackingStartDelay);
            trackingAllowed = true;
        
            if (projectileComponent.trackingActiveDuration == 0f) return;
        
            await MouseTools.AwaitableTimer(projectileComponent.trackingActiveDuration);
            trackingAllowed = false;
            
            if (!projectileComponent.snapToTargetAfterDuration || !rb) return;
            rb.linearVelocity = Vector3.zero;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            MouseTools.AwaitableTimer(0.1f);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            rb.AddForce(-(transform.position - trackingTarget.transform.position) * (projectileComponent.trackingForce * 50), ForceMode.Impulse);
            
            Debug.DrawRay(transform.position, transform.forward * 10, Color.green, 2f);
        }
    }

    private async Task EngineTimers()
    {
        if (projectileComponent.isPowered)
        {
            await MouseTools.AwaitableTimer(projectileComponent.engineDelay);
            engineAllowed = true;
            
            await MouseTools.AwaitableTimer(projectileComponent.engineTime);
            engineAllowed = false;
        }
    }

    public void ChangeTrackingTarget(GameObject target)
    {
        trackingTarget = target;
    }

    /*public void SetProjectileTeam(string newTeam)
    {
        gameObject.layer = LayerMask.NameToLayer($"{newTeam} Projectile");
        
        foreach (var team in Enum.GetValues(typeof(ProjectileComponent.ProjectileTeam)))
        {
            if (team.ToString() != newTeam) continue;
            projectileComponent.projectileTeam = (ProjectileComponent.ProjectileTeam)team;
        }
    }*/

    private void ApplyGravity()
    {
        rb.AddForce(Vector3.down * (projectileComponent.projectileGravityFactor * 10), ForceMode.Force);
    }

    private void ApplyProportionalNavigationLineOfSight()
    {
        trackingTarget.TryGetComponent(out Rigidbody targetRb);
        if (!targetRb) return;
        Vector3 targetPos = trackingTarget.transform.position;

        float navigationTime = (targetPos - transform.position).magnitude / projectileComponent.maxVelocity;
        Vector3 lineOfSight = (targetPos + targetRb.linearVelocity * navigationTime) - transform.position;

        float angle = Vector3.Angle(rb.linearVelocity, lineOfSight);
        Vector3 adjustment = projectileComponent.trackingPValue * angle * lineOfSight.normalized;

        rb.linearVelocity = rb.linearVelocity.normalized * projectileComponent.maxVelocity;
        if (adjustment.magnitude > Mathf.Epsilon)
        {
            var targetRotation = Quaternion.LookRotation(adjustment);
            
            transform.DORotateQuaternion(targetRotation, projectileComponent.trackingTurnTime);
        }

        rb.AddForce(transform.forward * projectileComponent.trackingForce, ForceMode.Impulse);
    }

    private void ApplyDrag()
    {
        rb.AddForce(-rb.linearVelocity * projectileComponent.projectileDragFactor, ForceMode.Force);
    }

    private void WeaponReleaseCheck(Weapon weapon)
    {
        if (!projectileComponent.destroyWhenWeaponReleases) return;
        if (weaponFiredFrom != weapon) return;
        
        DestroyProjectile();
    }

    private void OnCollisionEnter(Collision other)
    {
        other.gameObject.TryGetComponent(out HealthSystem healthSystem);
        
        if (healthSystem)
        {
            // Friendly fire check
            if (other.gameObject.layer != LayerMask.NameToLayer(projectileComponent.projectileTeam.ToString()))
            {
                healthSystem.DoDamage(projectileComponent.damageComponent);
                OnHit?.Invoke(projectileOwner, other.gameObject, projectileComponent);
                ApplyPassiveEffectsToTarget(other.gameObject);

                if (weaponFiredFrom) // spells don't have a weapon they're fired from
                {
                    weaponFiredFrom.ApplyOnHit(healthSystem);
                } 
            }
        }
        
        // Determine what particles to spawn depending on the hit object
        switch (other.gameObject.layer)
        {
            // Default
            case 0:
                if (projectileComponent.onHitParticles.Count <= 0) break;
                Instantiate(projectileComponent.onHitParticles[0], other.contacts[0].point, Quaternion.identity);
                break;
            
            // Enemy
            case 7:
                if (projectileComponent.onHitParticles.Count <= 0) break;
                Instantiate(projectileComponent.onHitParticles[1], other.contacts[0].point, Quaternion.identity);
                break;
        }

        if (projectileComponent.detonateWarheadsOnImpact)
        {
            foreach (GameObject warhead in projectileComponent.projectileWarheads)
            {
                GameObject newWarhead = Instantiate(warhead, transform.position, Quaternion.identity);
            }
        }

        if (projectileComponent.triggerOnImpact)
        {
            float projectileVelocityAtImpact = rb.linearVelocity.magnitude;
            float velocityOfImpact = other.relativeVelocity.magnitude;
            Vector3 impactPoint = transform.position;

            OnImpact?.Invoke(gameObject, projectileVelocityAtImpact, velocityOfImpact, impactPoint);
        }

        #if LIQUID_SYSTEM
        if (projectileComponent.createsLiquid)
        {
            if (other.gameObject.tag == "Liquid")
            {
                LiquidV2Manager liquidManager = FindFirstObjectByType<LiquidV2Manager>();

                liquidManager.SpawnLiquid(transform.position, projectileComponent.liquidType, projectileComponent.radius);

                projectileComponent.createsLiquid = false;
            }
            else if (other.gameObject.tag == "Floor")
            {
                LiquidV2Manager liquidManager = FindFirstObjectByType<LiquidV2Manager>();

                liquidManager.SpawnLiquid(transform.position, projectileComponent.liquidType, projectileComponent.radius); 

                projectileComponent.createsLiquid = false;               
            }
        }
        #endif
        
        if (!projectileComponent.destroyOnImpact) return;

        transform.DOComplete(this);
        DestroyProjectile();
    }

    private void ApplyPassiveEffectsToTarget(GameObject target)
    {
        foreach (PassiveEffectScriptableObject effect in projectileComponent.damageComponent.passiveEffectsAppliedToTarget)
        {
            PassiveEffectManager.onAddPassiveEffect?.Invoke(effect, target);
        }
    }

    private void RemoteDetonate(GameObject validationObject)
    {
        if (validationObject != gameObject) return;
        
        DestroyProjectile();
    }

    private async void DestroyProjectile(float delay = 0f)
    {
        await MouseTools.AwaitableTimer(delay);
        
        if (beingDestroyed) return;
        beingDestroyed = true;
        
        transform.DOKill(this);
        
        weaponPartFiredFrom.spawnedProjectiles.Remove(gameObject);
        
        // Preserve any effects within dontDestroyLingeringEffects, such as trails, audio, particles
        // Anything not in this list will be destroyed with the projectile
        
        // Particles must be placed under a Particle Parent empty gameobject
        // as Unity will restart particle systems if their GameObject is unparented directly
        // it's an old bug apparently
        if (!dontDestroyLingeringEffects.IsNullOrEmpty())
        {
            foreach (GameObject child in dontDestroyLingeringEffects)
            {
                child.transform.parent = null;
            }
        }
        
        Destroy(gameObject);
    }
}
