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
    public static event Action<Vector3, float> OnMoveWeaponCrosshair;
    public static event Action<Vector3, float> OnMoveSpellCrosshair;

    public Weapon CurrentEquippedWeapon => currentWeaponComponent; // Added this because I needed to access the current equipped weapon for the save system.
    
    [Separator("Runtime")]
    public GameObject ClosestTargetToCrosshair => closestTargetToCrosshair;
    [SerializeField, ReadOnly] private GameObject closestTargetToCrosshair;
    [SerializeField, ReadOnly] private List<Collider> targetsInCone;
    private Camera mainCamera;
    private Tween weaponTween;
    private Tween spellTween;

    public SpellAimMode CurrentSpellAimMode
    {
        get => currentSpellAimMode;
        set => currentSpellAimMode = value;
    }
    [SerializeField] private SpellAimMode currentSpellAimMode;
    public enum SpellAimMode
    {
        Crosshair,
        LockOn,
        GroundOnly
    }
    
    [Separator("Settings")]
    [SerializeField] private GameObject currentWeapon;
    [SerializeField] private Weapon currentWeaponComponent;
    [SerializeField] private GameObject spellHolder;
    [SerializeField] private GameObject targetWeaponAimpoint;
    [SerializeField] private GameObject spellAimpoint;
    [SerializeField] private float weaponAimTime; // maybe decide this per-spell and per-weapon
    [SerializeField] private float spellAimTime;
    [SerializeField] private float maxAimDistance;
    [SerializeField] private float maxConeAimDistance;
    [FormerlySerializedAs("selectionConeWidthDegrees")] [SerializeField] private float aimConeWidthDegrees;
    
    [SerializeField] LayerMask enemyLayerMask;
    [SerializeField] LayerMask nonTransparentLayerMask;
    [SerializeField] private SpellAimMode currentAimMode1;
    
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

        weaponTween = currentWeapon.transform.DOLookAt(targetWeaponAimpoint.transform.position, 0.1f);
        spellTween = spellHolder.transform.DOLookAt(targetWeaponAimpoint.transform.position, 0.1f);

        currentWeapon.transform.GetChild(0).TryGetComponent(out currentWeaponComponent);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        PlaceCameraAimpoint();
        AimWeapon();
        PlaceWeaponAimpoint();
        PlaceSpellAimpoint();

        switch (currentSpellAimMode)
        {
            case SpellAimMode.Crosshair:
                break;
            
            case SpellAimMode.LockOn:
                FindTargetsInAimCone();
                SpellLockOn();
                break;
            
            case SpellAimMode.GroundOnly:
                break;
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

    private void PlaceWeaponAimpoint()
    {
        if (!currentWeapon) return;
        if (!currentWeaponComponent) return;
        
        // consider multiple firepoints
        Vector3 firePointPos = currentWeaponComponent.firePoints[0].transform.position;
        Vector3 firePointForward = currentWeaponComponent.firePoints[0].transform.forward;
        
        // This aimpoint is used to show the physical aim direction of the weapon, including any obstacles that may be blocking it
        Ray ray = new Ray(firePointPos, firePointForward);
        Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, maxAimDistance, nonTransparentLayerMask);
        
        Vector3 indicatorPos = hit.point;
        
        if (!hit.collider)
        {
            indicatorPos = ray.GetPoint(maxAimDistance);
        }

        OnMoveWeaponCrosshair?.Invoke(mainCamera.WorldToScreenPoint(indicatorPos), weaponAimTime);
        
        if (!debugAim) return;
        Debug.DrawRay(firePointPos, firePointForward * maxAimDistance, Color.green, 0.01f);
    }
    
    private void PlaceSpellAimpoint()
    {
        Vector3 spellHolderPos = spellHolder.transform.position;
        Vector3 spellHolderForward = spellHolder.transform.forward;
        
        Ray ray = new Ray(spellHolderPos, spellHolderForward);
        Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, maxAimDistance, nonTransparentLayerMask);
        
        Vector3 indicatorPos = hit.point;
        
        if (!hit.collider)
        {
            indicatorPos = ray.GetPoint(maxAimDistance);
        }

        OnMoveSpellCrosshair?.Invoke(mainCamera.WorldToScreenPoint(indicatorPos), spellAimTime);
        
        if (!debugAim) return;
        Debug.DrawRay(spellHolderPos, spellHolderForward * maxAimDistance, Color.blueViolet, 0.01f);
    }
    
    private void AimWeapon()
    {
        if (!currentWeapon) return;
        
        weaponTween.Kill();
        //weaponTween = currentWeapon.transform.DOLookAt(targetWeaponAimpoint.transform.position, weaponAimTime);
        
        spellTween = spellHolder.transform.DOLookAt(targetWeaponAimpoint.transform.position, spellAimTime); // temp, move to separate method for spells
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
        
        currentWeapon = newWeapon.gameObject;
        currentWeaponComponent = newWeapon;
    }
}
