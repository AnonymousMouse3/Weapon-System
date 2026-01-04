using System;
using UnityEngine;


[Serializable, CreateAssetMenu(fileName = "WeaponScriptableObject", menuName = "Weapon Scriptable Object")]
// A WeaponObject is a ScriptableObject containing a WeaponComponent (the data structure)
// It exists to provide a WeaponComponent as a save-able template to create stats for individual guns
// It is used by a Weapon script to create a copy of itself on the weapon's prefab, to set runtime data such as FirePoint
public class WeaponScriptableObject : ScriptableObject
{
    public WeaponComponent weaponComponent;
}