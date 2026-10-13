using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

public static class CameraFix
{
    private static Camera2DFollow rendering;
    private static Action<Camera2DFollow> updateCamera;

    public static void Install()
    {
        Harmony harmony = new Harmony("rz.high-refresh-fix.camera");
        try
        {
            MethodInfo update = AccessTools.Method(typeof(Camera2DFollow), "Update", null, null);
            harmony.Patch(update, new HarmonyMethod(typeof(CameraFix).GetMethod("AllowUpdate")), null,
                new HarmonyMethod(typeof(CameraFix).GetMethod("RenderMovement")), null, null);
            updateCamera = (Action<Camera2DFollow>)Delegate.CreateDelegate(typeof(Action<Camera2DFollow>), update);
            harmony.Patch(AccessTools.Method(typeof(Camera2DFollow), "Start", null, null), null,
                new HarmonyMethod(typeof(CameraFix).GetMethod("AttachDriver")), null, null, null);
        }
        catch
        {
            harmony.UnpatchSelf();
            throw;
        }
    }

    public static bool AllowUpdate(Camera2DFollow __instance) { return rendering == __instance; }

    public static void AttachDriver(Camera2DFollow __instance, Rigidbody2D ___rb)
    {
        if (___rb != null) ___rb.interpolation = RigidbodyInterpolation2D.None;
        CameraRenderDriver driver = __instance.GetComponent<CameraRenderDriver>();
        if (driver == null) driver = __instance.gameObject.AddComponent<CameraRenderDriver>();
        driver.Follow = __instance;
    }

    public static void Render(Camera2DFollow follow)
    {
        if (follow == null || !follow.isActiveAndEnabled) return;
        Transform target = GameControl.IsIndoor ? follow.IndoorTarget : follow.OutdoorTarget;
        if (target == null) return;
        Camera2DFollow previous = rendering;
        rendering = follow;
        try { updateCamera(follow); }
        finally { rendering = previous; }
    }

    public static void ApplyPosition(Rigidbody2D body, Vector2 position)
    {
        // MovePosition queues movement until the next physics step. A camera must be
        // visible at the new position in this render frame; preserve its original Z.
        body.position = position;
        Vector3 current = body.transform.position;
        body.transform.position = new Vector3(position.x, position.y, current.z);
    }

    public static float LookAheadThreshold(Camera2DFollow follow)
    {
        return follow.lookAheadMoveThreshold * Time.deltaTime * 60f;
    }

    public static float ZoomStep(Camera2DFollow follow)
    {
        return follow.OrthographicSizeSpeed * Time.deltaTime * 60f;
    }

    public static IEnumerable<CodeInstruction> RenderMovement(IEnumerable<CodeInstruction> source)
    {
        List<CodeInstruction> instructions = new List<CodeInstruction>(source);
        MethodInfo move = AccessTools.Method(typeof(Rigidbody2D), "MovePosition", new Type[] { typeof(Vector2) }, null);
        FieldInfo threshold = AccessTools.Field(typeof(Camera2DFollow), "lookAheadMoveThreshold");
        FieldInfo zoom = AccessTools.Field(typeof(Camera2DFollow), "OrthographicSizeSpeed");
        int moves = 0, thresholds = 0, zooms = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(move))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = typeof(CameraFix).GetMethod("ApplyPosition");
                ++moves;
            }
            else if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, threshold))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = typeof(CameraFix).GetMethod("LookAheadThreshold");
                ++thresholds;
            }
            else if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, zoom))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = typeof(CameraFix).GetMethod("ZoomStep");
                ++zooms;
            }
        }
        if (moves != 1 || thresholds != 1 || zooms != 2)
            throw new InvalidOperationException("Unsupported Camera2DFollow IL; refusing partial camera patch");
        return instructions;
    }
}

public sealed class CameraRenderDriver : MonoBehaviour
{
    public Camera2DFollow Follow;
    private void LateUpdate() { CameraFix.Render(Follow); }
}
