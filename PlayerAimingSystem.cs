    using System;
using System.Collections.Generic;
using DG.Tweening;
using MouseLib;
using MyBox;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerAimingSystem : MonoBehaviour
{
    public static event Action<GameObject, GameObject> OnTargetLock;
    public static event Action<GameObject, GameObject> OnNoTarget;
    public static event Action<GameObject, Vector3, float> OnPlaceWorldCrosshair;
    
    [Separator("Runtime")]
    public GameObject ClosestTargetToCrosshair => closestTargetToCrosshair;
    [SerializeField, ReadOnly] private GameObject closestTargetToCrosshair;
    [SerializeField, ReadOnly] private List<Collider> targetsInCone;
    private Camera mainCamera;
    
    [Separator("Settings")]
    public List<GameObject> WeaponObjectsToAim => weaponObjectsToAim; // Added this because I needed to access the current equipped weapon for the save system.
    [FormerlySerializedAs("weaponsToAim")] [SerializeField, ReadOnly] private List<GameObject> weaponObjectsToAim;

    public GameObject TargetWeaponAimpoint
    {
        get => targetWeaponAimpoint;
        set => targetWeaponAimpoint = value;
    }

    [SerializeField] private GameObject targetWeaponAimpoint;
    [SerializeField] private float maxAimDistance;
    [SerializeField] private float maxConeAimDistance;
    [SerializeField] private float aimConeWidthDegrees;
    
    [SerializeField] LayerMask enemyLayerMask;
    [SerializeField] LayerMask nonTransparentLayerMask;
    
    [Separator("Debug")]
    [SerializeField] private bool debugAim;

    void OnEnable()
    {
        WeaponManager.OnSetWeapon += SetWeapon;
    }

    void OnDisable()
    {
        WeaponManager.OnSetWeapon -= SetWeapon;
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mainCamera = Camera.main;
        targetsInCone = new List<Collider>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        PlaceCameraAimpoint();

        foreach (GameObject weaponObject in weaponObjectsToAim)
        {
            if (!weaponObject) return;
            
            weaponObject.TryGetComponent(out Weapon weapon);
            WeaponComponent weaponComponent = weapon.weaponScriptableObject.weaponComponent;
            if (!weapon) return;
            if (weaponComponent == null) return;
            
            AimWeapon(weaponObject, weapon, weaponComponent);
            PlaceWorldAimpoint(weaponObject, weapon, weaponComponent);

            switch (weaponComponent.currentAimMode)
            {
                case WeaponComponent.AimModes.Crosshair:
                    break;
            
                case WeaponComponent.AimModes.LockOn:
                    FindTargetsInAimCone();
                    SpellLockOn();
                    break;
            
                case WeaponComponent.AimModes.GroundOnly:
                    break;
            }
        }
    }

    private void PlaceCameraAimpoint()
    {
        if (!targetWeaponAimpoint) return;
        
        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, maxAimDistance, nonTransparentLayerMask);
        
        targetWeaponAimpoint.transform.position = hit.point;
        
        if (!hit.collider)
        {
            targetWeaponAimpoint.transform.position = ray.GetPoint(maxAimDistance);
        }
        
        if (!debugAim) return;
        Debug.DrawRay(mainCamera.transform.position, mainCamera.transform.forward * maxAimDistance, Color.blue, 0.01f);
    }

    private void PlaceWorldAimpoint(GameObject weaponObject, Weapon weapon, WeaponComponent weaponComponent)
    {
        // consider multiple firepoints
        Vector3 firePointPos = weapon.firePoints[0].transform.position;
        Vector3 firePointForward = weapon.firePoints[0].transform.forward;
        
        // This aimpoint is used to show the physical aim direction of the weapon, including any obstacles that may be blocking it
        Ray ray = new Ray(firePointPos, firePointForward);
        Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, maxAimDistance, nonTransparentLayerMask);
        
        Vector3 indicatorPos = hit.point;
        
        if (!hit.collider)
        {
            indicatorPos = ray.GetPoint(maxAimDistance);
        }

        OnPlaceWorldCrosshair?.Invoke(gameObject, mainCamera.WorldToScreenPoint(indicatorPos), 0.01f);
        
        if (!debugAim) return;
        Debug.DrawRay(firePointPos, firePointForward * maxAimDistance, Color.green, 0.01f);
    }
    
    private void AimWeapon(GameObject weaponObject, Weapon weapon, WeaponComponent weaponComponent)
    {
        //if (currentWeapon.weaponTween == null)
        {
            // make this an actual calc from ergo and weight etc
            weapon.weaponTween = weaponObject.transform.DOLookAt(targetWeaponAimpoint.transform.position, weaponComponent.weaponWeight)/*.SetAutoKill(false)*/; // this is causing memory leak lol
        }
        
        weapon.weaponTween = weaponObject.transform.DOLookAt(targetWeaponAimpoint.transform.position, weaponComponent.weaponWeight);
    }

    private void FindTargetsInAimCone()
    {
        if (!gameObject) return;
        targetsInCone.Clear();
        Collider[] targetsInRange = new Collider[9999];
        Physics.OverlapSphereNonAlloc(transform.position, maxConeAimDistance, targetsInRange);
        
        Vector3 cameraPos = mainCamera.transform.position;
        Vector3 cameraForward = mainCamera.transform.forward;
        
        foreach (Collider target in targetsInRange)
        {
            if (!target) continue;
            Vector3 directionToTarget = target.transform.position - mainCamera.transform.position;
            
            if (Vector3.Angle(cameraForward, directionToTarget) > aimConeWidthDegrees) continue;
            if (!MouseTools.IsLayerInLayerMask(target.gameObject.layer, enemyLayerMask ) && !target.CompareTag("EnvironmentObstacle")) continue;
            Physics.Raycast(cameraPos, directionToTarget, out RaycastHit hit, maxAimDistance, nonTransparentLayerMask);

            if (!target || !hit.collider) continue;
            if (hit.collider.gameObject != target.gameObject) continue;
            targetsInCone.Add(target);
        }
    }

    private void SpellLockOn()
    {
        GameObject previousTarget = closestTargetToCrosshair;
        closestTargetToCrosshair = null;
        Vector3 cameraForward = mainCamera.transform.forward;

        foreach (Collider target in targetsInCone)
        {
            if (!target) continue;
            if (!closestTargetToCrosshair)
            {
                closestTargetToCrosshair = target.gameObject;
            }
            
            Vector3 directionToTarget = target.transform.position - mainCamera.transform.position;
            Vector3 directionToClosestTarget = closestTargetToCrosshair.transform.position - mainCamera.transform.position;
            
            if (Vector3.Angle(cameraForward, directionToTarget) > Vector3.Angle(cameraForward, directionToClosestTarget)) continue;
            closestTargetToCrosshair = target.gameObject;
        }

        if (!closestTargetToCrosshair)
        {
            TargetableObject.onDisableCanvas?.Invoke(gameObject, previousTarget);
            OnNoTarget?.Invoke(gameObject, previousTarget);
            return;
        }
        
        TargetableObject.onEnableCanvas?.Invoke(gameObject, closestTargetToCrosshair.gameObject);
        OnTargetLock?.Invoke(gameObject, closestTargetToCrosshair.gameObject);
    }

    private void SetWeapon(GameObject validationObject, Weapon newWeapon, WeaponSlot weaponSlot)
    {
        if (validationObject != gameObject) return;

        foreach (GameObject weaponObject in weaponObjectsToAim)
        {
            if (!weaponSlot.Weapons.Contains(weaponObject)) continue;
            weaponObjectsToAim.Remove(weaponObject);
        }
        
        weaponObjectsToAim.Add(newWeapon.gameObject);
    }
}
