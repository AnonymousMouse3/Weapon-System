using System.Threading.Tasks;
using MouseLib;
using MyBox;
using UnityEngine;

public class Explosion : MonoBehaviour
{
    [SerializeField] private ExplosionComponent explosionComponent;
    
    public Task explosionTask = Task.CompletedTask;

    public async Task Explode(float delay, float explosionScalar = 0)
    {
        explosionComponent.explosionScalar = explosionScalar;
        explosionComponent.damageComponent.damageScalar = explosionScalar;
        
        await MouseTools.AwaitableTimer(delay);
        
        Collider[] hitTargets = new Collider[10];

        // add shapes
        
        switch (explosionComponent.explosionShape)
        {
            case ExplosionComponent.ExplosionShape.Sphere:
                float radius = Mathf.Lerp(explosionComponent.explosionRadius, explosionComponent.explosionRadiusMax, explosionComponent.explosionScalar);
                
                Physics.OverlapSphereNonAlloc(transform.position, radius, hitTargets, explosionComponent.layersToHit);
                break;
            
            case ExplosionComponent.ExplosionShape.CustomCollider:
                
                break;
        }

        foreach (Collider collider in hitTargets)
        {
            if (!collider) break;
            
            collider.gameObject.TryGetComponent(out HealthSystem healthSystem);
            if (!healthSystem) continue;
            
            healthSystem.DoDamage(explosionComponent.damageComponent);
            
            if (!explosionComponent.applyKnockback) continue;
            collider.gameObject.TryGetComponent(out Rigidbody rb);
            
            Vector3 force = (transform.position - rb.position) * (explosionComponent.knockbackForce * explosionComponent.explosionScalar);
            
            switch (explosionComponent.explosionShape)
            {
                case ExplosionComponent.ExplosionShape.Sphere:
                    float distance = Vector3.Distance(transform.position, rb.position);
                    // todo test
                    force *= explosionComponent.knockbackFalloff.Evaluate(explosionComponent.explosionRadius - distance);
                    break;
                case ExplosionComponent.ExplosionShape.CustomCollider:
                    
                    break;
            }
            
            if (rb) rb.AddForce(-force, ForceMode.Impulse);
        }
        
        if (explosionComponent.hasParticles)
        {
            ParticleSystem particles;
            
            ParticleSystem.MinMaxCurve minChargeStartSpeed = explosionComponent.explosionParticles.minChargeStartSpeed;
            ParticleSystem.MinMaxCurve maxChargeStartSpeed = explosionComponent.explosionParticles.maxChargeStartSpeed;
            ParticleSystem.MinMaxCurve minChargeBurstSize = explosionComponent.explosionParticles.minChargeBurstSize;
            ParticleSystem.MinMaxCurve maxChargeBurstSize = explosionComponent.explosionParticles.maxChargeBurstSize;

            
            if (!explosionComponent.explosionParticles.spawnAsChild) particles = Instantiate(explosionComponent.explosionParticles.particles, transform.position, transform.rotation);
            else particles = Instantiate(explosionComponent.explosionParticles.particles, transform);
            
            
            if (!explosionComponent.explosionParticles.scaleWithCharge) return;
            
            ParticleSystem.MainModule main = particles.main;
            ParticleSystem.MinMaxCurve startSpeed = explosionComponent.explosionParticles.InterpolateMinMaxCurve(minChargeStartSpeed, maxChargeStartSpeed, explosionComponent.explosionScalar);
            startSpeed.mode = ParticleSystemCurveMode.TwoConstants;
            main.startSpeed = startSpeed;
            
            ParticleSystem.MinMaxCurve burstSize = explosionComponent.explosionParticles.InterpolateMinMaxCurve(minChargeBurstSize, maxChargeBurstSize, explosionComponent.explosionScalar);
            
            particles.emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0.0f, burstSize, 1, 0.03f)
            });

        }

        if (!explosionComponent.destroySelf) return;
        
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
