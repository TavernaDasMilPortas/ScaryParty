using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using StarterAssets;

public class WeaponSystemSetup
{
    private const string PrefabsPath = "Assets/Prefabs/Weapons";
    private const string AnimatorControllerPath = "Assets/StarterAssets/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";
    private const string ClipsPath = "Assets/Animations/Weapons/Generated";
    private const string MaskPath = "Assets/Animations/Weapons/WeaponsUpperBodyMask.mask";

    [MenuItem("Tools/Weapons/Setup Entire System (Phase 2)")]
    public static void SetupSystem()
    {
        CreateDirectories();
        CreatePlaceholderPrefabs();
        CreateAvatarMask();
        SetupAnimatorController();
        SetupPlayerPrefab();
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ Sistema de Armas configurado com sucesso!");
    }

    private static void CreateDirectories()
    {
        if (!AssetDatabase.IsValidFolder(PrefabsPath))
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "../", PrefabsPath));
        }
    }

    private static void CreatePlaceholderPrefabs()
    {
        // Material
        string matPath = PrefabsPath + "/WeaponMetalMaterial.mat";
        Material metalMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (metalMat == null)
        {
            metalMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            metalMat.color = Color.gray;
            metalMat.SetFloat("_Smoothness", 0.7f);
            AssetDatabase.CreateAsset(metalMat, matPath);
        }

        // Pistol
        string pistolPath = PrefabsPath + "/Pistol_Placeholder.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(pistolPath) == null)
        {
            GameObject pistol = new GameObject("Pistol_Placeholder");
            pistol.AddComponent<PistolWeapon>();
            
            GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grip.name = "Grip";
            grip.transform.SetParent(pistol.transform);
            grip.transform.localPosition = new Vector3(0, -0.05f, 0);
            grip.transform.localScale = new Vector3(0.03f, 0.10f, 0.04f);
            grip.GetComponent<MeshRenderer>().sharedMaterial = metalMat;
            Object.DestroyImmediate(grip.GetComponent<BoxCollider>());

            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = "Barrel";
            barrel.transform.SetParent(pistol.transform);
            barrel.transform.localPosition = new Vector3(0, 0.02f, 0.08f);
            barrel.transform.localScale = new Vector3(0.035f, 0.035f, 0.16f);
            barrel.GetComponent<MeshRenderer>().sharedMaterial = metalMat;
            Object.DestroyImmediate(barrel.GetComponent<BoxCollider>());
            
            PrefabUtility.SaveAsPrefabAsset(pistol, pistolPath);
            Object.DestroyImmediate(pistol);
        }

        // Shotgun
        string shotgunPath = PrefabsPath + "/Shotgun_Placeholder.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(shotgunPath) == null)
        {
            GameObject shotgun = new GameObject("Shotgun_Placeholder");
            shotgun.AddComponent<ShotgunWeapon>();
            
            GameObject stock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stock.name = "Stock";
            stock.transform.SetParent(shotgun.transform);
            stock.transform.localPosition = new Vector3(0, -0.05f, -0.1f);
            stock.transform.localScale = new Vector3(0.04f, 0.12f, 0.25f);
            stock.GetComponent<MeshRenderer>().sharedMaterial = metalMat;
            Object.DestroyImmediate(stock.GetComponent<BoxCollider>());

            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = "Barrel";
            barrel.transform.SetParent(shotgun.transform);
            barrel.transform.localPosition = new Vector3(0, 0.02f, 0.2f);
            barrel.transform.localScale = new Vector3(0.025f, 0.025f, 0.6f);
            barrel.GetComponent<MeshRenderer>().sharedMaterial = metalMat;
            Object.DestroyImmediate(barrel.GetComponent<BoxCollider>());
            
            PrefabUtility.SaveAsPrefabAsset(shotgun, shotgunPath);
            Object.DestroyImmediate(shotgun);
        }
    }

    private static void CreateAvatarMask()
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        bool isNew = false;
        if (mask == null)
        {
            mask = new AvatarMask();
            mask.name = "WeaponsUpperBodyMask";
            isNew = true;
        }
        
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        }
        
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);

        if (isNew)
        {
            AssetDatabase.CreateAsset(mask, MaskPath);
        }
        else
        {
            EditorUtility.SetDirty(mask);
        }
    }

    private static void SetupAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
        if (controller == null)
        {
            Debug.LogError("Animator Controller not found!");
            return;
        }

        // Add parameters
        AddParameterIfNotExists(controller, "WeaponMode", AnimatorControllerParameterType.Int);
        AddParameterIfNotExists(controller, "IsAiming", AnimatorControllerParameterType.Bool);
        AddParameterIfNotExists(controller, "Fire", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists(controller, "IsMoving", AnimatorControllerParameterType.Bool);
        AddParameterIfNotExists(controller, "IsSprinting", AnimatorControllerParameterType.Bool);

        // Remove old layer if exists to rebuild
        int layerIndex = -1;
        for (int i = 0; i < controller.layers.Length; i++)
        {
            if (controller.layers[i].name == "Weapon Upper Body")
            {
                layerIndex = i;
                break;
            }
        }
        if (layerIndex != -1)
        {
            controller.RemoveLayer(layerIndex);
        }

        // Add Layer
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        AnimatorControllerLayer weaponLayer = new AnimatorControllerLayer
        {
            name = "Weapon Upper Body",
            defaultWeight = 1.0f,
            avatarMask = mask,
            stateMachine = new AnimatorStateMachine
            {
                name = "Weapon Upper Body",
                hideFlags = HideFlags.HideInHierarchy
            }
        };
        
        AssetDatabase.AddObjectToAsset(weaponLayer.stateMachine, controller);
        controller.AddLayer(weaponLayer);

        AnimatorStateMachine rootSm = weaponLayer.stateMachine;

        AnimatorState unarmed = rootSm.AddState("Unarmed");
        rootSm.defaultState = unarmed;

        // Load Clips
        var pIdle = LoadClip("Pistol_Idle");
        var pWalk = LoadClip("Pistol_Walk");
        var pRun = LoadClip("Pistol_Run");
        var pAimIdle = LoadClip("Pistol_AimIdle");
        var pAimWalk = LoadClip("Pistol_AimWalk");
        var pFire = LoadClip("Pistol_Fire");
        var pAimFire = LoadClip("Pistol_AimFire");
        var pEquip = LoadClip("Pistol_Equip");
        var pUnequip = LoadClip("Pistol_Unequip");

        var sIdle = LoadClip("Shotgun_Idle");
        var sWalk = LoadClip("Shotgun_Walk");
        var sRun = LoadClip("Shotgun_Run");
        var sAimIdle = LoadClip("Shotgun_AimIdle");
        var sAimWalk = LoadClip("Shotgun_AimWalk");
        var sFire = LoadClip("Shotgun_Fire");
        var sAimFire = LoadClip("Shotgun_AimFire");
        var sEquip = LoadClip("Shotgun_Equip");
        var sUnequip = LoadClip("Shotgun_Unequip");

        AnimatorStateMachine smPistol = rootSm.AddStateMachine("SM_Pistol");
        AnimatorStateMachine smShotgun = rootSm.AddStateMachine("SM_Shotgun");

        SetupWeaponStateMachine(smPistol, 1, pIdle, pWalk, pRun, pAimIdle, pAimWalk, pFire, pAimFire, pEquip, pUnequip, unarmed, rootSm);
        SetupWeaponStateMachine(smShotgun, 2, sIdle, sWalk, sRun, sAimIdle, sAimWalk, sFire, sAimFire, sEquip, sUnequip, unarmed, rootSm);

        // Entry Transitions to SubStates
        var t1 = rootSm.AddAnyStateTransition(smPistol);
        t1.AddCondition(AnimatorConditionMode.Equals, 1, "WeaponMode");
        
        var t2 = rootSm.AddAnyStateTransition(smShotgun);
        t2.AddCondition(AnimatorConditionMode.Equals, 2, "WeaponMode");
        
        var t3 = rootSm.AddAnyStateTransition(unarmed);
        t3.AddCondition(AnimatorConditionMode.Equals, 0, "WeaponMode");
    }

    private static void SetupWeaponStateMachine(AnimatorStateMachine sm, int modeId, 
        AnimationClip idle, AnimationClip walk, AnimationClip run, 
        AnimationClip aimIdle, AnimationClip aimWalk, 
        AnimationClip fire, AnimationClip aimFire, 
        AnimationClip equip, AnimationClip unequip,
        AnimatorState unarmedState, AnimatorStateMachine rootSm)
    {
        var stEquip = sm.AddState("Equip"); stEquip.motion = equip;
        var stIdle = sm.AddState("Idle"); stIdle.motion = idle;
        var stWalk = sm.AddState("Walk"); stWalk.motion = walk;
        var stRun = sm.AddState("Run"); stRun.motion = run;
        var stAimIdle = sm.AddState("AimIdle"); stAimIdle.motion = aimIdle;
        var stAimWalk = sm.AddState("AimWalk"); stAimWalk.motion = aimWalk;
        var stFire = sm.AddState("Fire"); stFire.motion = fire;
        var stAimFire = sm.AddState("AimFire"); stAimFire.motion = aimFire;
        var stUnequip = sm.AddState("Unequip"); stUnequip.motion = unequip;

        sm.defaultState = stEquip;

        // Equip -> Idle
        var t1 = stEquip.AddTransition(stIdle);
        t1.hasExitTime = true;
        t1.exitTime = 0.9f;

        // Idle <-> Walk
        var t2 = stIdle.AddTransition(stWalk);
        t2.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        var t3 = stWalk.AddTransition(stIdle);
        t3.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");

        // Walk <-> Run
        var t4 = stWalk.AddTransition(stRun);
        t4.AddCondition(AnimatorConditionMode.If, 0, "IsSprinting");
        var t5 = stRun.AddTransition(stWalk);
        t5.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSprinting");

        // Run -> Idle
        var t6 = stRun.AddTransition(stIdle);
        t6.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");

        // Idle <-> AimIdle
        var t7 = stIdle.AddTransition(stAimIdle);
        t7.AddCondition(AnimatorConditionMode.If, 0, "IsAiming");
        var t8 = stAimIdle.AddTransition(stIdle);
        t8.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAiming");

        // AimIdle <-> AimWalk
        var t9 = stAimIdle.AddTransition(stAimWalk);
        t9.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        var t10 = stAimWalk.AddTransition(stAimIdle);
        t10.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");

        // Walk <-> AimWalk
        var t11 = stWalk.AddTransition(stAimWalk);
        t11.AddCondition(AnimatorConditionMode.If, 0, "IsAiming");
        var t12 = stAimWalk.AddTransition(stWalk);
        t12.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAiming");

        // Fire
        var t13 = sm.AddAnyStateTransition(stFire);
        t13.AddCondition(AnimatorConditionMode.If, 0, "Fire");
        t13.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAiming");
        t13.AddCondition(AnimatorConditionMode.Equals, modeId, "WeaponMode");
        var t14 = stFire.AddTransition(stIdle);
        t14.hasExitTime = true;
        t14.exitTime = 0.9f;

        // Aim Fire
        var t15 = sm.AddAnyStateTransition(stAimFire);
        t15.AddCondition(AnimatorConditionMode.If, 0, "Fire");
        t15.AddCondition(AnimatorConditionMode.If, 0, "IsAiming");
        t15.AddCondition(AnimatorConditionMode.Equals, modeId, "WeaponMode");
        var t16 = stAimFire.AddTransition(stAimIdle);
        t16.hasExitTime = true;
        t16.exitTime = 0.9f;

        var t17 = sm.AddAnyStateTransition(stUnequip);
        t17.AddCondition(AnimatorConditionMode.NotEqual, modeId, "WeaponMode");
        var t18 = stUnequip.AddExitTransition();
        t18.hasExitTime = true;
        t18.exitTime = 0.9f;
    }

    private static AnimationClip LoadClip(string name)
    {
        return AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipsPath}/{name}.anim");
    }

    private static void AddParameterIfNotExists(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == name) return;
        }
        controller.AddParameter(name, type);
    }

    private static void SetupPlayerPrefab()
    {
        string path = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        
        if (prefab != null)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            
            if (instance.GetComponent<WeaponController>() == null)
                instance.AddComponent<WeaponController>();
                
            if (instance.GetComponent<WeaponInputHandler>() == null)
                instance.AddComponent<WeaponInputHandler>();

            WeaponController wc = instance.GetComponent<WeaponController>();
            wc.pistolPrefab = AssetDatabase.LoadAssetAtPath<WeaponBase>(PrefabsPath + "/Pistol_Placeholder.prefab");
            wc.shotgunPrefab = AssetDatabase.LoadAssetAtPath<WeaponBase>(PrefabsPath + "/Shotgun_Placeholder.prefab");

            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
        }
    }
}
