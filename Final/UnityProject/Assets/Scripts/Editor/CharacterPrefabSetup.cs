#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility: takes an imported Mixamo FBX character and assembles it
/// into a ready-to-use prefab with RVOAgent + AnimationBridge.
///
/// Menu: Tools > Phase1 > Assemble Character Prefab
///
/// Prerequisites:
///   1. Import a Mixamo character FBX into Assets/Characters/
///   2. Set its Rig to Humanoid in the Inspector
///   3. Run "Create Locomotion Controller" first
///   4. Then run this tool
/// </summary>
public static class CharacterPrefabSetup
{
    [MenuItem("Tools/Phase1/Assemble Character Prefab")]
    public static void AssemblePrefab()
    {
        // Find the locomotion controller
        string controllerPath = "Assets/Animation/LocomotionController.controller";
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

        if (controller == null)
        {
            EditorUtility.DisplayDialog("Missing Controller",
                "Run 'Tools > Phase1 > Create Locomotion Controller' first.", "OK");
            return;
        }

        // Find the character FBX in Assets/Characters/ — must have a SkinnedMeshRenderer
        // (animation-only FBX files have no mesh and are skipped).
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Characters" });
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("No Character Found",
                "Import a Mixamo FBX into Assets/Characters/ first.\n" +
                "Then set Rig → Animation Type → Humanoid in its Import Settings.", "OK");
            return;
        }

        string fbxPath = null;
        GameObject modelPrefab = null;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (candidate != null && candidate.GetComponentInChildren<SkinnedMeshRenderer>() != null)
            {
                fbxPath = path;
                modelPrefab = candidate;
                break;
            }
        }

        if (modelPrefab == null)
        {
            EditorUtility.DisplayDialog("No Character Found",
                "No FBX with a mesh (Skin) found in Assets/Characters/.\n" +
                "Make sure the character FBX (not animation-only) is imported there.", "OK");
            return;
        }

        if (modelPrefab == null)
        {
            EditorUtility.DisplayDialog("Load Failed", $"Could not load {fbxPath}", "OK");
            return;
        }

        // Instantiate in scene
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        instance.name = "ConstrainedRVOCharacter";
        instance.transform.position = Vector3.zero;

        // Add Animator if not present, assign controller
        Animator animator = instance.GetComponent<Animator>();
        if (animator == null)
            animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false; // RVO drives position, not root motion

        // Add RVOAgent
        if (instance.GetComponent<RVOAgent>() == null)
        {
            var agent = instance.AddComponent<RVOAgent>();
            agent.Target = new Vector3(10, 0, 0); // default target
        }

        // Add AnimationBridge
        if (instance.GetComponent<AnimationBridge>() == null)
            instance.AddComponent<AnimationBridge>();

        // Ensure folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        // Save as prefab
        string prefabPath = "Assets/Prefabs/ConstrainedRVOCharacter.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(instance, prefabPath, InteractionMode.UserAction);

        Selection.activeObject = instance;
        EditorGUIUtility.PingObject(instance);

        Debug.Log($"[Phase1] Assembled prefab at {prefabPath} using model from {fbxPath}.\n" +
                  "Next: assign animation clips in the Blend Tree (Animator window → double-click Locomotion state).");
    }
}
#endif
