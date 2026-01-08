using System.Threading.Tasks;
using MouseLib;
using MyBox;
using UnityEngine;

public class Explosion : MonoBehaviour
{
    [Separator("Size")]
    [SerializeField] private float explosionRadius;
    
    [Separator("Damage")]
    [SerializeField] private float explosionDamage;
    [SerializeField] private bool scaleDamageWithDistance;
    
    [Separator("Armour Penetration")]
    [SerializeField] private int explosionArmourPenetration;
    
    [Separator("Settings")]
    [SerializeField] private bool destroySelf;
    [SerializeField] private float explosionDelay;
    [SerializeField] private LayerMask layersToHit;
    [SerializeField] private int maxTargetsChecked;
    
    private Task explosionTask = Task.CompletedTask;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        explosionTask = Explode(explosionDelay);
    }

    private async Task Explode(float delay)
    {
        await MouseTools.AwaitableTimer(delay);
        
        Collider[] hitTargets = new Collider[10];
        Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, hitTargets);

        foreach (Collider collider in hitTargets)
        {
            if (!collider) break;
            
            collider.gameObject.TryGetComponent(out HealthSystem healthSystem);
            if (!healthSystem) continue;
            
            healthSystem.DoDamage(explosionDamage);
        }

        if (!destroySelf) return;
        
        // Preserve trails, projectiles, sound emitters, etc.
        // The effects themselves are under an empty GameObject named "Effects" which is unparented from the projectile
        // as Unity will restart particle systems if their GameObject is unparented directly
        // it's an old bug apparently
        foreach (Transform child in transform)
        {
            child.parent = null;
            
            foreach (Transform subChild in child)
            {
                subChild.gameObject.TryGetComponent(out ParticleSystem childParticleSystem);
                subChild.gameObject.TryGetComponent(out TrailRenderer childTrail);

                if (childParticleSystem)
                {
                    childParticleSystem.Stop();
                    continue;
                }

                if (childTrail)
                {
                    continue;
                }
                
                Destroy(subChild.gameObject); 
            }
        }
        
        Destroy(gameObject);
    }
}
