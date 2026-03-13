using System;
using System.Collections.Generic;
using DG.Tweening;
using JetBrains.Annotations;
using MouseLib;
using MyBox;
using Spellslinger.UI;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.TextCore.Text;

public class AimingSystem : MonoBehaviour
{
    public delegate void OnSetAIAimTargetingMode(GameObject validationObject, AIAimTargetingMode newAIAimTargetingMode);
    public static OnSetAIAimTargetingMode onSetAIAimTargetingMode;
    public delegate void OnSetAimType(GameObject validationObject, WeaponComponent.WeaponAimType newAimType);
    public static OnSetAimType onSetAimType;
    public delegate void OnSetTargetAimpoint(GameObject validationObject, GameObject newTargetAimpoint);
    public static OnSetTargetAimpoint onSetTargetAimpoint;
    public delegate void OnSetTargetCharacter(GameObject validationObject, GameObject newTargetCharacter);
    public static OnSetTargetCharacter onSetTargetCharacter;
    public static event Action OnReportAIAimState;
    public static event Action<GameObject, GameObject> OnTargetLock;
    public static event Action<GameObject, GameObject> OnTargetLost;
    public static event Action<Weapon, Vector3, float> OnPlaceWorldCrosshair;
    
    [Separator("Runtime")]
    public AIAimState currentAIAimState;
    public enum AIAimState
    {
        Idle,
        Aiming,
        Aimed,
    }
    
    public AIAimTargetingMode currentAIAimTargetingMode;
    public enum AIAimTargetingMode
    {
        AimAtPoint,
        AimAtTarget,
    }
    
    public List<GameObject> WeaponObjectsToAim => weaponObjectsToAim; // Added this because I needed to access the current equipped weapon for the save system.
    [SerializeField, ReadOnly] private List<GameObject> weaponObjectsToAim;

    public bool AimingAllowed = true;
    
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
    
    [SerializeField] private float maxAimDistance; // PER WEAPON
    [SerializeField] private float maxConeAimDistance; // PER WEAPON
    [SerializeField] private float aimConeWidthDegrees; // PER WEAPON
    
    [SerializeField] LayerMask enemyLayerMask; // RENAME TO TARGETABLE LAYERS, PER WEAPON
    [SerializeField] LayerMask nonTransparentLayerMask; // PER WEAPON
    
    [SerializeField] private Vector3 defaultAimpoint = new (100, 0, 0);
    [SerializeField] private bool aimCharacterInsteadOfWeapon; // temporary
    
    [Separator("Debug")]
    [SerializeField] private bool debugAim;
    
    private Camera mainCamera;

    void OnEnable()
    {
        WeaponManager.OnRegisterWeaponAiming += RegisterWeapon;
        WeaponManager.OnUnregisterWeaponAiming += UnregisterWeapon;
        WeaponManager.OnSetAimingAllowed += SetAimingAllowed;
        WeaponManager.OnCleanupTargetLocks += CleanupTargetLocks;
        onSetTargetCharacter += SetTargetCharacter;
        onSetTargetAimpoint += SetTargetAimpoint;
        onSetAIAimTargetingMode += SetAIAimMode;
    }

    void OnDisable()
    {
        WeaponManager.OnRegisterWeaponAiming -= RegisterWeapon;
        WeaponManager.OnUnregisterWeaponAiming -= UnregisterWeapon;
        WeaponManager.OnSetAimingAllowed -= SetAimingAllowed;
        WeaponManager.OnCleanupTargetLocks -= CleanupTargetLocks;
        onSetTargetCharacter -= SetTargetCharacter;
        onSetTargetAimpoint -= SetTargetAimpoint;
        onSetAIAimTargetingMode -= SetAIAimMode;
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

        if (weaponObjectsToAim.IsNullOrEmpty()) return;
        
        foreach (GameObject weaponObject in weaponObjectsToAim)
        {
            if (!weaponObject) return;
            
            weaponObject.TryGetComponent(out Weapon weapon);
            WeaponComponent weaponComponent = weapon.weaponScriptableObject.weaponComponent;
            
            if (!weapon) return;
            if (weaponComponent == null) return;

            if (AimingAllowed)
            {
                AimWeapon(weaponObject, weapon, weaponComponent);
            }
            
            PlaceWorldAimpoint(weaponObject, weapon, weaponComponent);
            
            switch (weaponComponent.currentWeaponAimType)
            {
                case WeaponComponent.WeaponAimType.Crosshair:
                    break;
            
                case WeaponComponent.WeaponAimType.LockOn:
                    FindTargetsInAimCone();
                    LockOnCrosshairClosestTarget(weapon);
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
        if (weapon.firePoints.IsNullOrEmpty())
        {
            Debug.Log("Weapon has no fire points. Assign one or multiple in the inspector and determine if each should use a separate crosshair");
            return;
        }
        
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

        if (weapon.worldAimpointInstance)
        {
            OnPlaceWorldCrosshair?.Invoke(weapon, mainCamera.WorldToScreenPoint(indicatorPos), 0.025f);
        }
        
        if (!debugAim) return;
        Debug.DrawRay(firePointPos, firePointForward * maxAimDistance, Color.green, 0.01f);
    }
    
    private void AimWeapon(GameObject weaponObject, Weapon weapon, WeaponComponent weaponComponent)
    {
        GameObject localTarget = null;
        switch (currentAIAimTargetingMode)
        {
            case AIAimTargetingMode.AimAtPoint:
                localTarget = targetAimpoint;
                break;
            
            case AIAimTargetingMode.AimAtTarget:
                localTarget = targetCharacter;
                break;
        }

        if (!localTarget) return;
        if (weapon.weaponTween == null)
        {
            // make this an actual calc from ergo and weight etc
            weapon.weaponTween = weaponObject.transform.DOLookAt(localTarget.transform.position, weaponComponent.weaponWeight).SetAutoKill(false); // this is causing memory leak maybe lol
        }
        weapon.weaponTween.Kill();
        weapon.weaponTween = weaponObject.transform.DOLookAt(localTarget.transform.position, weaponComponent.weaponWeight);
    }

    private void FindTargetsInAimCone()
    {
        if (aimConeWidthDegrees <= 0) { Debug.Log("aim cone width is zero, assign a value in the inspector"); return; }
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

    private void LockOnCrosshairClosestTarget(Weapon weapon)
    {
        GameObject previousTarget = weapon.target;
        weapon.target = null;
        Vector3 cameraForward = mainCamera.transform.forward;
        
        foreach (Collider target in targetsInCone)
        {
            if (!target) continue;
            if (!weapon.target)
            {
                weapon.target = target.gameObject;
            }
            
            Vector3 directionToTarget = target.transform.position - mainCamera.transform.position;
            Vector3 directionToClosestTarget = weapon.target.transform.position - mainCamera.transform.position;
            
            if (Vector3.Angle(cameraForward, directionToTarget) > Vector3.Angle(cameraForward, directionToClosestTarget)) continue;
            weapon.target = target.gameObject;
        }
        
        if (weapon.target == previousTarget) return;

        TargetableObject.onDisableCanvas?.Invoke(gameObject, previousTarget, weapon.weaponScriptableObject.weaponComponent.weaponLockOnIcon);
        OnTargetLost?.Invoke(gameObject, previousTarget);
        
        if (!weapon.target) return;
        TargetableObject.onEnableCanvas?.Invoke(gameObject, weapon.target.gameObject, weapon.weaponScriptableObject.weaponComponent.weaponLockOnIcon);
        OnTargetLock?.Invoke(gameObject, weapon.target.gameObject);
    }

    public void CleanupTargetLocks(Weapon weapon)
    {
        GameObject previousTarget = weapon.target;
        
        TargetableObject.onDisableCanvas?.Invoke(gameObject, previousTarget, weapon.weaponScriptableObject.weaponComponent.weaponLockOnIcon);
        OnTargetLost?.Invoke(gameObject, previousTarget);
    }
    
    private void CheckAimStatus()
    {
        // check if tween is within X distance to completion
        
        // check line of sight and see if pointing at target (mandatory if direct fire weapon)
        // If the raycast hits our target, we are aimed; if not, we're still aiming
        //currentAimState = hit.collider.gameObject == target ? AimState.Aimed : AimState.Aiming;
        
        // wait until weapon is steady, strengthens chance to hit
        
        // check ballistic trajectory and see if lined up on target (mandatory if indirect fire weapon, optional and strengthens chance to hit otherwise)
        // otherwise check for obstacles etc
        // some smart checks such as moving object going to obscure target before its hit, target moving out of sight (reduces chance to hit)
    }
    
    private void SetAimingAllowed(bool value)
    {
        AimingAllowed = value;
    }

    private void RegisterWeapon(GameObject validationObject, GameObject newWeaponInstance)
    {
        if (validationObject != gameObject) return;
        
        weaponObjectsToAim.Add(newWeaponInstance);
        Weapon weapon = newWeaponInstance.GetComponent<Weapon>();
    }

    private void UnregisterWeapon(GameObject validationObject, GameObject oldWeaponInstance)
    {
        if (validationObject != gameObject) return;
        
        oldWeaponInstance.transform.DOKill();
        weaponObjectsToAim.Remove(oldWeaponInstance);
    }

    private void SetAIAimMode(GameObject validationObject, AIAimTargetingMode newAIAimTargetingMode)
    {
        if (validationObject != gameObject) return;
        
        currentAIAimTargetingMode = newAIAimTargetingMode;
    }

    private void SetTargetAimpoint(GameObject validationObject, GameObject newAimpoint)
    {
        if (validationObject != gameObject) return;
        targetAimpoint = newAimpoint;
    }
    
    private void SetTargetCharacter(GameObject validationObject, GameObject newCharacter)
    {
        if (validationObject != gameObject) return;
        targetCharacter = newCharacter;
    }
}
