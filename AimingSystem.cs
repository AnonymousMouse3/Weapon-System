using System;
using System.Collections.Generic;
using DG.Tweening;
using MouseLib;
using MyBox;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.TextCore.Text;

public class AimingSystem : MonoBehaviour
{
    public delegate void OnSetAimMode(GameObject validationObject, AimMode newAimMode);
    public static OnSetAimMode onSetAimMode;
    public delegate void OnSetTargetAimpoint(GameObject validationObject, GameObject newTargetAimpoint);
    public static OnSetTargetAimpoint onSetTargetAimpoint;
    public delegate void OnSetTargetCharacter(GameObject validationObject, GameObject newTargetCharacter);
    public static OnSetTargetCharacter onSetTargetCharacter;
    public static event Action OnReportAimState;
    public static event Action<GameObject, GameObject> OnTargetLock;
    public static event Action<GameObject, GameObject> OnNoTarget;
    public static event Action<GameObject, Vector3, float> OnPlaceWorldCrosshair;
    
    [Separator("Runtime")]
    public AimState currentAimState;
    public enum AimState
    {
        Idle,
        Aiming,
        Aimed,
    }
    
    public AimMode currentAimMode;
    public enum AimMode
    {
        AimAtPoint,
        AimAtTarget,
    }
    
    public List<GameObject> WeaponObjectsToAim => weaponObjectsToAim; // Added this because I needed to access the current equipped weapon for the save system.
    [SerializeField, ReadOnly] private List<GameObject> weaponObjectsToAim;

    public GameObject LockOnTarget => lockOnTarget;
    [SerializeField, ReadOnly] private GameObject lockOnTarget;
    [SerializeField, ReadOnly] private List<Collider> targetsInCone; // make available to targeting system
    
    [Separator("Settings")]
    [SerializeField] private bool aimWithMainCamera;
    
    public GameObject TargetAimpoint
    {
        get => targetAimpoint;
        set => targetAimpoint = value;
    }
    [SerializeField] private GameObject targetAimpoint;
    
    public GameObject TargetCharacter
    {
        get => targetCharacter;
        set => targetCharacter = value;
    }
    [SerializeField] private GameObject targetCharacter;
    
    [SerializeField] private float maxAimDistance;
    [SerializeField] private float maxConeAimDistance;
    [SerializeField] private float aimConeWidthDegrees;
    
    [SerializeField] LayerMask enemyLayerMask;
    [SerializeField] LayerMask nonTransparentLayerMask;
    
    [SerializeField] private Vector3 defaultAimpoint = new (100, 0, 0);
    [SerializeField] private bool aimCharacterInsteadOfWeapon; // temporary
    
    [Separator("Debug")]
    [SerializeField] private bool debugAim;
    
    private Camera mainCamera;

    void OnEnable()
    {
        WeaponManager.OnSetWeapon += SetWeapon;
        onSetTargetCharacter += SetTargetCharacter;
        onSetTargetAimpoint += SetTargetAimpoint;
        onSetAimMode += SetAimMode;
    }

    void OnDisable()
    {
        WeaponManager.OnSetWeapon -= SetWeapon;
        onSetTargetCharacter -= SetTargetCharacter;
        onSetTargetAimpoint -= SetTargetAimpoint;
        onSetAimMode -= SetAimMode;
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
        if (aimWithMainCamera)
        {
            PlaceCameraAimpoint();
        }

        foreach (GameObject weaponObject in weaponObjectsToAim)
        {
            if (!weaponObject) return;
            
            weaponObject.TryGetComponent(out Weapon weapon);
            WeaponComponent weaponComponent = weapon.weaponScriptableObject.weaponComponent;
            if (!weapon) return;
            if (weaponComponent == null) return;
            
            AimWeapon(weaponObject, weapon, weaponComponent);
            PlaceWorldAimpoint(weaponObject, weapon, weaponComponent);

            switch (weaponComponent.currentWeaponAimType)
            {
                case WeaponComponent.WeaponAimType.Crosshair:
                    break;
            
                case WeaponComponent.WeaponAimType.LockOn:
                    FindTargetsInAimCone();
                    SpellLockOn();
                    break;
            
                case WeaponComponent.WeaponAimType.GroundOnly:
                    break;
            }
        }

        /*if (aimCharacterInsteadOfWeapon) return;
        Vector3 weaponVector = unitWeapon.transform.rotation.eulerAngles;
        weaponVector.x = 0f; // Ignore vertical component
            
        transform.DORotate(weaponVector, weaponAimTime);*/
    }

    private void PlaceCameraAimpoint()
    {
        if (!targetAimpoint) return;
        
        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, maxAimDistance, nonTransparentLayerMask);
        
        targetAimpoint.transform.position = hit.point;
        
        if (!hit.collider)
        {
            targetAimpoint.transform.position = ray.GetPoint(maxAimDistance);
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
        GameObject localTarget = null;
        switch (currentAimMode)
        {
            case AimMode.AimAtPoint:
                localTarget = targetAimpoint;
                break;
            
            case AimMode.AimAtTarget:
                localTarget = targetCharacter;
                break;
        }
        
        if (!localTarget) return;
        if (weapon.weaponTween == null)
        {
            // make this an actual calc from ergo and weight etc
            weapon.weaponTween = weaponObject.transform.DOLookAt(localTarget.transform.position, weaponComponent.weaponWeight).SetAutoKill(false); // this is causing memory leak maybe lol
        }
        
        weapon.weaponTween = weaponObject.transform.DOLookAt(localTarget.transform.position, weaponComponent.weaponWeight);
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
        GameObject previousTarget = lockOnTarget;
        lockOnTarget = null;
        Vector3 cameraForward = mainCamera.transform.forward;

        foreach (Collider target in targetsInCone)
        {
            if (!target) continue;
            if (!lockOnTarget)
            {
                lockOnTarget = target.gameObject;
            }
            
            Vector3 directionToTarget = target.transform.position - mainCamera.transform.position;
            Vector3 directionToClosestTarget = lockOnTarget.transform.position - mainCamera.transform.position;
            
            if (Vector3.Angle(cameraForward, directionToTarget) > Vector3.Angle(cameraForward, directionToClosestTarget)) continue;
            lockOnTarget = target.gameObject;
        }

        if (!lockOnTarget)
        {
            TargetableObject.onDisableCanvas?.Invoke(gameObject, previousTarget);
            OnNoTarget?.Invoke(gameObject, previousTarget);
            return;
        }
        
        TargetableObject.onEnableCanvas?.Invoke(gameObject, lockOnTarget.gameObject);
        OnTargetLock?.Invoke(gameObject, lockOnTarget.gameObject);
    }

    private void CheckAimStatus()
    {
        // check if tween is within X distance to completion
        
        // check line of sight and see if pointing at target (mandatory if direct fire weapon)
        // If the raycast hits our target, we are aimed; if not, we're still aiming
        //currentAimState = hit.collider.gameObject == target ? AimState.Aimed : AimState.Aiming;
        
        // check ballistic trajectory and see if lined up on target (mandatory if indirect fire weapon, optional and strengthens chance to hit otherwise)
        // otherwise check for obstacles etc
        // some smart checks such as moving object going to obscure target before its hit, target moving out of sight (reduces chance to hit)
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

    private void SetAimMode(GameObject validationObject, AimMode newAimMode)
    {
        if (validationObject != gameObject) return;
        
        currentAimMode = newAimMode;
    }

    private void SetTargetAimpoint(GameObject validationObject, GameObject newAimpoint)
    {
        if (validationObject != gameObject) return;
        targetAimpoint  = newAimpoint;
    }
    
    private void SetTargetCharacter(GameObject validationObject, GameObject newCharacter)
    {
        if (validationObject != gameObject) return;
        targetCharacter  = newCharacter;
    }
}
