using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using MyBox;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

[Serializable, CreateAssetMenu(fileName = "WeaponScriptableObject", menuName = "Weapon System/Weapon Scriptable Object")]
// A WeaponObject is a ScriptableObject containing a WeaponComponent (the data structure)
// It exists to provide a WeaponComponent as a save-able template to create stats for individual guns
// It is used by a Weapon script to create a copy of itself on the weapon's prefab, to set runtime data such as FirePoint
public class WeaponScriptableObject : ScriptableObject
{
    // ReadOnly attributes will disable editing of the variable depending on a condition
    // We use these here to disable certain gun stats that are only relevant if another is true
    // e.g. a gun's chamber can only be loaded if it has one
    // Thus setting hasChamber to false will set chamberLoaded to read only

    [Separator("Runtime")]
    [ReadOnly] public GameObject weaponOwner;
    
    [Separator("Info")]
    [FormerlySerializedAs("name")] public string weaponName;
    [TextArea] public string desc;
    [TextArea] public string altDesc;
    
    [Separator("Technical Settings")]
    [SerializeReference] public List<WeaponPart> WeaponParts;
    public List<WeaponFunction> WeaponFunctions;
    
    #if SPELL_SYSTEM
    [ReadOnly] public SpellManager spellManager;
    #endif
    
    [Separator("UI Settings")]
    public GameObject crosshairImage;
    
    #if SPELL_SYSTEM
    public Texture2D spellIcon;
    public Texture[] spellImages;
    #endif
    
    
    
    [Separator("Handling Settings")]
    public float weaponErgonomics;
    public float weaponWeight;
    public float weaponSway;
    public float weaponEquipTime;
    public float weaponUnequipTime;
    
    [Separator("Debug")]
    [SerializeField] public bool debugWeapon;
    
    public Tween weaponTween;
    
    public Task weaponCycleTask;
    public Stopwatch weaponCycleTimer;
    public CancellationTokenSource weaponCycleCTS;
    
    [ReadOnly] public WeaponCycleState weaponCycleState;
    public enum WeaponCycleState
    {
        Cycling,
        ReadyToFire,
    }

    public WeaponFunctionCondition GetFunctionConditionByName(string conditionName)
    {
        foreach (WeaponFunction function in WeaponFunctions)
        {
            foreach (WeaponFunctionCondition condition in function.FunctionConditions)
            {
                // return the first match - don't dupe names!
                if (condition.ConditionName == conditionName) return condition;
            }
        }
        
        return null;
    }
}


// The WeaponComponent class serves as the data structure/template and the base of the weapon system
// It is designed to be cloned and saved with WeaponObjects for individual weapons' stat blocks
// Which are further instanced by Weapon scripts for use at runtime