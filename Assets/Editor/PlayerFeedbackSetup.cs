using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// sets up the player's animations, sounds, burn overlay and first-person body in the open scene, and makes the health pickup prefab
// safe to run again: it only adds what is missing and refills the sound slots.
// save the scene afterwards (Ctrl+S)
public static class PlayerFeedbackSetup
{
    private const string AnimationFolder = "Assets/Animation";
    private const string ControllerPath = "Assets/Animation/Player_Animator.controller";
    private const string ClipFolder = "Assets/PolyOne/Free Stickman/Animation";
    private const string AudioFolder = "Assets/Audio";
    private const string PickupMaterialPath = "Assets/Materials/HealthPickup.mat";
    private const string PickupPrefabPath = "Assets/Prefabs/HealthPickup.prefab";

    // blend tree thresholds: the flat speed (m/s) at which each running animation is fully playing
    private const float WalkSpeed = 6f;
    private const float RunSpeed = 14f;
    private const float RunFastSpeed = 24f;

    private const float TransitionTime = 0.1f;

    [MenuItem("A Quick Sprint Through Hell/Set Up Player Feedback")]
    public static void SetUp()
    {
        CustomPlayerController player = Object.FindFirstObjectByType<CustomPlayerController>();

        if (player == null)
        {
            Debug.LogError("No CustomPlayerController found in the open scene. Open the scene with the Player first.");
            return;
        }

        AnimatorController controller = GetOrBuildAnimatorController();

        if (controller == null)
            return;

        GameObject playerObject = player.gameObject;

        AddIfMissing<PlayerBurn>(playerObject);
        AddIfMissing<BurnOverlay>(playerObject);
        AddIfMissing<FirstPersonBody>(playerObject);
        AddIfMissing<WindAudio>(playerObject);

        PlayerAnimation playerAnimation = AddIfMissing<PlayerAnimation>(playerObject);
        SetReference(playerAnimation, "controller", controller);

        PlayerAudio playerAudio = AddIfMissing<PlayerAudio>(playerObject);
        AssignSounds(playerAudio);

        Camera camera = player.GetComponentInChildren<Camera>();

        if (camera != null)
            AddIfMissing<CameraBob>(camera.gameObject);
        else
            Debug.LogWarning("No Camera found under the Player, so the camera bob was not added.");

        EditorSceneManager.MarkSceneDirty(playerObject.scene);
        Selection.activeObject = playerObject;

        Debug.Log("Player feedback is set up: animations, sounds, burn overlay, first-person body and camera bob. Save the scene (Ctrl+S).");
    }

    // ============================================================
    // ANIMATOR
    // ============================================================

    // builds the player's animator controller from the stick figure's clips, unless it already exists (delete it to rebuild)
    private static AnimatorController GetOrBuildAnimatorController()
    {
        AnimationClip idle = LoadClip("Idle");
        AnimationClip walk = LoadClip("Walk");
        AnimationClip run = LoadClip("Run");
        AnimationClip runFast = LoadClip("Run Fast");
        AnimationClip jumpingUp = LoadClip("Jumping Up");
        AnimationClip sitting = LoadClip("Sitting");

        if (idle == null || walk == null || run == null || runFast == null || jumpingUp == null || sitting == null)
        {
            Debug.LogError($"Could not find all the stick figure clips in {ClipFolder} (Idle, Walk, Run, Run Fast, Jumping Up, Sitting).");
            return null;
        }

        // the clips came with Loop Time off, so a run would play once and freeze
        SetLoop(idle, true);
        SetLoop(walk, true);
        SetLoop(run, true);
        SetLoop(runFast, true);
        SetLoop(sitting, true);
        SetLoop(jumpingUp, false);

        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        if (existing != null)
        {
            Debug.Log($"Using the existing animator controller at {ControllerPath}. Delete it and run this again to rebuild it.");
            return existing;
        }

        if (!AssetDatabase.IsValidFolder(AnimationFolder))
            AssetDatabase.CreateFolder("Assets", "Animation");

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Sliding", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Crouching", AnimatorControllerParameterType.Bool);
        controller.AddParameter("WallRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

        // PlaybackSpeed starts at 1, so the cycle plays at normal speed before the script sets it
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "PlaybackSpeed",
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f
        });

        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        // running: one blend tree on Speed, which is also the state the character starts in
        AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, WalkSpeed);
        tree.AddChild(run, RunSpeed);
        tree.AddChild(runFast, RunFastSpeed);

        locomotion.speedParameterActive = true;
        locomotion.speedParameter = "PlaybackSpeed";

        AnimatorState air = AddState(machine, "Air", jumpingUp);
        AnimatorState crouch = AddState(machine, "Crouch", sitting);

        // slide holds the idle pose: SlideVisuals already tilts the whole body, so no animation of its own is needed
        AnimatorState slide = AddState(machine, "Slide", idle);
        AnimatorState wallRun = AddState(machine, "WallRun", runFast);

        // any state: slide, wall run and the jump trigger can happen from wherever we are
        AnimatorStateTransition toSlide = machine.AddAnyStateTransition(slide);
        SetUpTransition(toSlide);
        toSlide.AddCondition(AnimatorConditionMode.If, 0f, "Sliding");

        AnimatorStateTransition toWallRun = machine.AddAnyStateTransition(wallRun);
        SetUpTransition(toWallRun);
        toWallRun.AddCondition(AnimatorConditionMode.If, 0f, "WallRunning");

        // jumps, wall jumps and dashes all fire the Jump trigger.
        // letting it transition to itself restarts the clip each time
        AnimatorStateTransition toJump = machine.AddAnyStateTransition(air);
        SetUpTransition(toJump);
        toJump.canTransitionToSelf = true;
        toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

        // normal running <-> in the air
        AddTransition(locomotion, air, "Grounded", false);
        AddTransition(air, locomotion, "Grounded", true);

        // crouching
        AddTransition(locomotion, crouch, "Crouching", true);
        AddTransition(crouch, locomotion, "Crouching", false);
        AddTransition(crouch, air, "Grounded", false);

        // back to running once a slide or wall run ends.
        // if we are airborne, locomotion hands over to the air state next
        AddTransition(slide, locomotion, "Sliding", false);
        AddTransition(wallRun, locomotion, "WallRunning", false);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        return controller;
    }

    private static AnimationClip LoadClip(string clipName)
    {
        return AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/{clipName}.anim");
    }

    private static void SetLoop(AnimationClip clip, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);

        if (settings.loopTime == loop)
            return;

        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static AnimatorState AddState(AnimatorStateMachine machine, string stateName, AnimationClip clip)
    {
        AnimatorState state = machine.AddState(stateName);
        state.motion = clip;

        return state;
    }

    // a transition that happens as soon as its condition is true, blended over a short time
    private static void SetUpTransition(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = TransitionTime;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, string boolParameter, bool whenTrue)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        SetUpTransition(transition);
        transition.AddCondition(whenTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, boolParameter);
    }

    // ============================================================
    // SOUNDS
    // ============================================================

    private static void AssignSounds(PlayerAudio playerAudio)
    {
        SerializedObject serialized = new SerializedObject(playerAudio);

        SerializedProperty footsteps = serialized.FindProperty("footsteps");
        footsteps.arraySize = 3;

        for (int i = 0; i < 3; i++)
            footsteps.GetArrayElementAtIndex(i).objectReferenceValue = LoadSound($"footstep_{i + 1}");

        SetSound(serialized, "jump", "jump");
        SetSound(serialized, "dash", "dash");
        SetSound(serialized, "wallJump", "wall_jump");
        SetSound(serialized, "lavaTouch", "lava_touch");
        SetSound(serialized, "electricTouch", "electric_touch");
        SetSound(serialized, "wallRunLoop", "wall_run_loop");
        SetSound(serialized, "wallSlideLoop", "wall_slide_loop");
        SetSound(serialized, "slideLoop", "slide_loop");

        serialized.ApplyModifiedProperties();
    }

    private static void SetSound(SerializedObject serialized, string field, string clipName)
    {
        serialized.FindProperty(field).objectReferenceValue = LoadSound(clipName);
    }

    private static AudioClip LoadSound(string clipName)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{clipName}.wav");

        if (clip == null)
            Debug.LogWarning($"Missing sound {AudioFolder}/{clipName}.wav. That slot is left empty.");

        return clip;
    }

    // ============================================================
    // HEALTH PICKUP
    // ============================================================

    [MenuItem("A Quick Sprint Through Hell/Create Health Pickup Prefab")]
    public static void CreateHealthPickupPrefab()
    {
        GameObject root = new GameObject("HealthPickup");

        // the orb you see.
        // it has no collider of its own: the trigger on the root does that job
        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "Orb";
        orb.transform.SetParent(root.transform, false);
        orb.transform.localScale = Vector3.one * 1.2f;
        Object.DestroyImmediate(orb.GetComponent<Collider>());
        orb.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreatePickupMaterial();

        // bigger than the orb so it is easy to run into at speed.
        // is Trigger: the player passes straight through it
        SphereCollider trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.5f;

        HealthPickup pickup = root.AddComponent<HealthPickup>();
        SetReference(pickup, "pickupSound", LoadSound("pickup"));

        string path = AssetDatabase.GenerateUniqueAssetPath(PickupPrefabPath);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);

        Debug.Log($"Health pickup prefab created at {path}. Drag it into a scene or a room prefab.");
    }

    // a glowing green material for the orb
    private static Material GetOrCreatePickupMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(PickupMaterialPath);

        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            Debug.LogWarning("Could not find the URP Lit shader, so the pickup orb uses the default material.");
            return null;
        }

        Color green = new Color(0.2f, 1f, 0.45f, 1f);

        material = new Material(shader) { name = "HealthPickup" };
        material.SetColor("_BaseColor", green);
        material.SetFloat("_Smoothness", 0.8f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", green * 2f);

        AssetDatabase.CreateAsset(material, PickupMaterialPath);

        return material;
    }

    // ============================================================
    // SMALL HELPERS
    // ============================================================

    private static T AddIfMissing<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();

        if (component == null)
            component = Undo.AddComponent<T>(target);

        return component;
    }

    private static void SetReference(Object target, string field, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
    }
}
