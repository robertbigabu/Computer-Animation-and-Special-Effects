#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Editor utility: auto-creates an AnimatorController with a 2D Freeform
/// Cartesian Blend Tree pre-configured for the Phase 1 animation bridge.
///
/// Menu: Tools > Phase1 > Create Locomotion Controller
///
/// After running, drag your Mixamo clips into the blend tree motions
/// (double-click the Blend Tree state in the Animator window to edit).
/// </summary>
public static class BlendTreeSetup
{
    [MenuItem("Tools/Phase1/Create Locomotion Controller")]
    public static void CreateController()
    {
        // Save path
        string path = "Assets/Animation/LocomotionController.controller";

        // Ensure folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Animation"))
            AssetDatabase.CreateFolder("Assets", "Animation");

        // Create controller
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        // Add parameters
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        // Get the base layer's state machine
        var rootStateMachine = controller.layers[0].stateMachine;

        // Create a Blend Tree state
        BlendTree blendTree;
        var state = controller.CreateBlendTreeInController("Locomotion", out blendTree, 0);
        state.motion = blendTree;

        // Configure as 2D Freeform Cartesian
        blendTree.blendType = BlendTreeType.FreeformCartesian2D;
        blendTree.blendParameter    = "MoveX";
        blendTree.blendParameterY   = "MoveY";
        blendTree.name = "Locomotion Blend Tree";

        // Add motion slots with positions.
        // User will drag their Mixamo clips into these slots.
        // The 'motion' field is null — user fills it in.
        blendTree.AddChild(null, new Vector2( 0f,  0f));  // [0] Idle
        blendTree.AddChild(null, new Vector2( 0f,  1f));  // [1] Walk Forward
        blendTree.AddChild(null, new Vector2( 0f, -1f));  // [2] Walk Backward
        blendTree.AddChild(null, new Vector2(-1f,  0f));  // [3] Strafe Left
        blendTree.AddChild(null, new Vector2( 1f,  0f));  // [4] Strafe Right
        blendTree.AddChild(null, new Vector2( 0f,  2f));  // [5] Run Forward

        // Make it the default state
        rootStateMachine.defaultState = state;

        // Save
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Select it so user can see it
        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);

        Debug.Log("[Phase1] Created LocomotionController at Assets/Animation/LocomotionController.controller\n" +
                  "Next: double-click the Blend Tree state in the Animator window → drag Mixamo clips into the 6 motion slots:\n" +
                  "  [0] Idle  [1] Walk Fwd  [2] Walk Back  [3] Strafe L  [4] Strafe R  [5] Run Fwd");
    }
}
#endif
