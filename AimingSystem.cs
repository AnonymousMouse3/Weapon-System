using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using JetBrains.Annotations;
using MouseLib;
using MyBox;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.TextCore.Text;

public class AimingSystem : MonoBehaviour
{
    public delegate void OnSetAIAimTargetingMode(GameObject validationObject, AIAimTargetingMode newAIAimTargetingMode);
    public static OnSetAIAimTargetingMode onSetAIAimTargetingMode;
    public delegate void OnSetAimpoint(GameObject validationObject, GameObject newAimpoint);
    public static OnSetAimpoint onSetAimpoint;
    public delegate void OnMoveAimpoint(GameObject validationObject, Vector3 newPosition);
    public static OnMoveAimpoint onMoveAimpoint;
    public delegate void OnSetTargetGameObject(GameObject validationObject, GameObject newTargetCharacter);
    public static OnSetTargetGameObject onSetTargetGameObject;
    public static event Action OnReportAIAimState;
    public static event Action<GameObject, GameObject> OnTargetLock;
    public static event Action<GameObject, GameObject> OnTargetLost;
    public static event Action<WeaponScriptableObject, Vector3, float> OnPlaceWorldCrosshair;
    
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
        AimAtGameObject,
    }
    
    public List<GameObject> WeaponObjectsToAim => weaponObjectsToAim; // Added this because I needed to access the current equipped weapon for the save system.
    [SerializeField, ReadOnly] private List<GameObject> weaponObjectsToAim;

    public bool AimingAllowed = true;
    
    [SerializeField, ReadOnly] private List<Collider> targetsInCone; // make available to targeting system
    
    [Separator("Settings")]
    [SerializeField] private bool aimWithMainCamera;
    [SerializeField] private bool turnCharacterWithAim;
    [SerializeField] private float characterTurnTime;
    
    public GameObject Aimpoint
    {
        get => aimpoint;
        set => aimpoint = value;
    }
    [SerializeField] private GameObject aimpoint;
    
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
    
    [SerializeField] private bool aimCharacterInsteadOfWeapon; // temporary
    
    // put this on the weapon prefab
    public LineRenderer AimLine;
    
    [Separator("Debug")]
    [SerializeField] private bool debugAim;
    
    private Camera mainCamera;
    private Rigidbody rb;
    private bool displayAimLine;
    
    public bool DisplayAimLine
    {
        get => displayAimLine;
        set => displayAimLine = value;
    }
    
    

    void OnEnable()
    {
        WeaponManager.OnRegisterWeaponAiming += RegisterWeapon;
        WeaponManager.OnUnregisterWeaponAiming += UnregisterWeapon;
        WeaponManager.OnSetAimingAllowed += SetAimingAllowed;
        WeaponManager.OnCleanupTargetLocks += CleanupTargetLocks;
        onSetTargetGameObject += SetTargetGameObject;
        onSetAimpoint += SetAimpoint;
        onSetAIAimTargetingMode += SetAIAimMode;
        onMoveAimpoint += MoveAimpoint;
    }

    void OnDisable()
    {
        WeaponManager.OnRegisterWeaponAiming -= RegisterWeapon;
        WeaponManager.OnUnregisterWeaponAiming -= UnregisterWeapon;
        WeaponManager.OnSetAimingAllowed -= SetAimingAllowed;
        WeaponManager.OnCleanupTargetLocks -= CleanupTargetLocks;
        onSetTargetGameObject -= SetTargetGameObject;
        onSetAimpoint -= SetAimpoint;
        onSetAIAimTargetingMode -= SetAIAimMode;
        onMoveAimpoint -= MoveAimpoint;
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mainCamera = Camera.main;
        targetsInCone = new List<Collider>();
        TryGetComponent(out AimLine);
        TryGetComponent(out rb);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (aimWithMainCamera) PlaceCameraAimpoint();
        
        // todo should select a specific weapon instead of default
        if (turnCharacterWithAim) TurnCharacterWithAim();
        
        if (weaponObjectsToAim.IsNullOrEmpty()) return;
        
        foreach (GameObject weaponObject in weaponObjectsToAim)
        {
            if (!weaponObject) return;
            
            weaponObject.TryGetComponent(out Weapon weapon);
            WeaponScriptableObject weaponScriptableObject = weapon.weaponScriptableObject;
            // if multiple weaponparts use the same firepoint, use only that one, use the first weaponpart's icon
            // if there are multiple firepoints
            
            if (!weapon || !weaponScriptableObject) return;
            
            if (AimingAllowed) AimWeapon(weaponObject, weaponScriptableObject);


            foreach (WeaponPart weaponPart in weaponScriptableObject.WeaponParts)
            {
                if (!weaponPart.firePoint) continue;
                PlaceWorldAimpoint(weaponPart, weaponScriptableObject);
            
                switch (weaponPart.aimType)
                {
                    case WeaponPart.WeaponAimType.Crosshair:
                        break;
            
                    case WeaponPart.WeaponAimType.LockOn:
                        FindTargetsInAimCone();
                        LockOnCrosshairClosestTarget(weaponPart);
                        break;
            
                    case WeaponPart.WeaponAimType.GroundOnly:
                        break;
                }
            }
        }

        /*if (aimCharacterInsteadOfWeapon) return;
        Vector3 weaponVector = unitWeapon.transform.rotation.eulerAngles;
        weaponVector.x = 0f; // Ignore vertical component
            
        transform.DORotate(weaponVector, weaponAimTime);*/
    }

    private void PlaceCameraAimpoint()
    {
        if (!aimpoint) return;
        
        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, maxAimDistance, nonTransparentLayerMask);
        
        aimpoint.transform.position = hit.point;
        
        if (!hit.collider) aimpoint.transform.position = ray.GetPoint(maxAimDistance);
        
        if (!debugAim) return;
        Debug.DrawRay(mainCamera.transform.position, mainCamera.transform.forward * maxAimDistance, Color.blue, 0.01f);
    }

    private void PlaceWorldAimpoint(WeaponPart weaponPart, WeaponScriptableObject weaponScriptableObject)
    {
        Vector3 firePointPos = weaponPart.firePoint.transform.position;
        Vector3 firePointForward = weaponPart.firePoint.transform.forward;
        
        // This aimpoint is used to show the physical aim direction of the weapon, including any obstacles that may be blocking it
        Ray ray = new Ray(firePointPos, firePointForward);
        Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, maxAimDistance, nonTransparentLayerMask);
        
        Vector3 indicatorPos = hit.point;
        
        if (!hit.collider)
        {
            indicatorPos = ray.GetPoint(maxAimDistance);
        }

        if (weaponPart.worldAimpointInstance)
        {
            OnPlaceWorldCrosshair?.Invoke(weaponScriptableObject, mainCamera.WorldToScreenPoint(indicatorPos), 0.025f);
        }

        // set aim line positions to firepoint and aimpoint, if we should display it - set to zero if otherwise
        if (AimLine)
        {
            if (displayAimLine)
            {
                AimLine.enabled = true;
                AimLine.SetPositions(new[] { firePointPos, indicatorPos});
            }
            else AimLine.enabled = false;
        }

        if (!debugAim) return;
        Debug.DrawRay(firePointPos, firePointForward * maxAimDistance, Color.green, 0.01f);
    }
    
    private void AimWeapon(GameObject weaponObject, WeaponScriptableObject weaponScriptableObject)
    {
        GameObject localTarget = null;
        switch (currentAIAimTargetingMode)
        {
            case AIAimTargetingMode.AimAtPoint:
                localTarget = aimpoint;
                break;
            case AIAimTargetingMode.AimAtGameObject:
                localTarget = targetCharacter;
                break;
        }
        
        if (!localTarget) return;
        if (weaponScriptableObject.weaponTween == null)
        {
            // make this an actual calc from ergo and weight etc
            weaponScriptableObject.weaponTween = weaponObject.transform.DOLookAt(localTarget.transform.position, weaponScriptableObject.weaponWeight).SetAutoKill(false); // this is causing memory leak maybe lol
        }
        weaponScriptableObject.weaponTween.Kill();
        weaponScriptableObject.weaponTween = weaponObject.transform.DOLookAt(localTarget.transform.position, weaponScriptableObject.weaponWeight);
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

    private void LockOnCrosshairClosestTarget(WeaponPart weaponPart)
    {
        GameObject previousTarget = weaponPart.target;
        weaponPart.target = null;
        Vector3 cameraForward = mainCamera.transform.forward;
        
        foreach (Collider target in targetsInCone)
        {
            if (!target) continue;
            if (!weaponPart.target)
            {
                weaponPart.target = target.gameObject;
            }
            
            Vector3 directionToTarget = target.transform.position - mainCamera.transform.position;
            Vector3 directionToClosestTarget = weaponPart.target.transform.position - mainCamera.transform.position;
            
            if (Vector3.Angle(cameraForward, directionToTarget) > Vector3.Angle(cameraForward, directionToClosestTarget)) continue;
            weaponPart.target = target.gameObject;
        }
        
        if (weaponPart.target == previousTarget) return;

        TargetableObject.onDisableCanvas?.Invoke(gameObject, previousTarget, weaponPart.targetIcon);
        OnTargetLost?.Invoke(gameObject, previousTarget);
        
        if (!weaponPart.target) return;
        TargetableObject.onEnableCanvas?.Invoke(gameObject, weaponPart.target.gameObject, weaponPart.targetIcon);
        OnTargetLock?.Invoke(gameObject, weaponPart.target.gameObject);
    }

    public void CleanupTargetLocks(WeaponPart weaponPart)
    {
        GameObject previousTarget = weaponPart.target;
        
        TargetableObject.onDisableCanvas?.Invoke(gameObject, previousTarget, weaponPart.targetIcon);
        OnTargetLost?.Invoke(gameObject, previousTarget);
    }
    
    private void CheckAimStatus(WeaponPart weaponPart)
    {
        // check if tween is within X distance to completion
        
        // check line of sight and see if pointing at target (mandatory if direct fire weapon)
        // If the raycast hits our target, we are aimed; if not, we're still aiming

        // (todo this is simple logic for now, follow the other comments for advanced targeting decisions)
        switch (currentAIAimTargetingMode)
        {
            case AIAimTargetingMode.AimAtPoint:
                break;
            case AIAimTargetingMode.AimAtGameObject:
                Ray aimRay = new Ray(weaponPart.firePoint.transform.position, weaponPart.firePoint.transform.forward);
                Physics.Raycast(aimRay, out RaycastHit aimHit, maxAimDistance);
                
                currentAIAimState = aimHit.collider.gameObject == targetCharacter ? AIAimState.Aimed : AIAimState.Aiming;
                
                break;
        }
        
        // wait until weapon is steady, strengthens chance to hit
        
        // check ballistic trajectory and see if lined up on target (mandatory if indirect fire weapon, optional and strengthens chance to hit otherwise)
        // otherwise check for obstacles etc
        // some smart checks such as moving object going to obscure target before its hit, target moving out of sight (reduces chance to hit)
    }

    public async Task<bool> WaitUntilAimed(WeaponPart weaponPart, float maxWaitTime)
    {
        for (float i = 0; i < maxWaitTime * 100; i += maxWaitTime / 100f)
        {
            await MouseTools.AwaitableTimer(maxWaitTime / 100f);
            
            CheckAimStatus(weaponPart);
            if (currentAIAimState == AIAimState.Aimed) return true;
        }
        
        return false;
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

    private void SetAimpoint(GameObject validationObject, GameObject newAimpoint)
    {
        if (validationObject != gameObject) return;
        aimpoint = newAimpoint;
    }

    private void MoveAimpoint(GameObject validationObject, Vector3 newPosition)
    {
        if (validationObject != gameObject) return;
        aimpoint.transform.position = newPosition;
    }
    
    private void SetTargetGameObject(GameObject validationObject, GameObject newCharacter)
    {
        if (validationObject != gameObject) return;
        targetCharacter = newCharacter;
    }
    
    private void TurnCharacterWithAim()
    {
        Vector3 aimDirection = Vector3.zero;
        switch (currentAIAimTargetingMode)
        {
            case AIAimTargetingMode.AimAtPoint:
                aimDirection = aimpoint.transform.position - transform.position;
                break;
            case AIAimTargetingMode.AimAtGameObject:
                aimDirection = targetCharacter.transform.position - transform.position;
                break;
        }
        
        Vector3 flatAimDirection = new Vector3(aimDirection.x, 0f, aimDirection.z);
            
        if (flatAimDirection == Vector3.zero) return;
        Quaternion flatAimQuaternion = Quaternion.LookRotation(flatAimDirection, Vector3.up);
        Tween rotationTween = transform.DORotateQuaternion(flatAimQuaternion, characterTurnTime);
    }
}
