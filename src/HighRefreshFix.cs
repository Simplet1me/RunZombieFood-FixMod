using System;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("rz.high-refresh-fix", "High Refresh Movement Fix", "1.1.0")]
public sealed class HighRefreshFix : BaseUnityPlugin
{
    private static bool physicsNavigation;
    private static Action<PolyNavAgent> navigate;
    private void Awake()
    {
        Harmony harmony = new Harmony("rz.high-refresh-fix");
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo late = typeof(PolyNavAgent).GetMethod("LateUpdate", flags);
            MethodInfo fixedUpdate = typeof(PolyNavAgent).GetMethod("FixedUpdate", flags);
            if (late == null || fixedUpdate == null) throw new MissingMethodException("Unsupported PolyNavAgent version");
            // Patch first, then create the delegate so it enters the patched method.
            harmony.Patch(late, new HarmonyMethod(typeof(HighRefreshFix).GetMethod("AllowNavigation")), null, null, null, null);
            navigate = (Action<PolyNavAgent>)Delegate.CreateDelegate(typeof(Action<PolyNavAgent>), late);
            harmony.Patch(fixedUpdate, new HarmonyMethod(typeof(HighRefreshFix).GetMethod("BeforePhysics")), null, null, null, null);
            harmony.Patch(typeof(PolyNavAgent).GetMethod("get_position", flags), new HarmonyMethod(typeof(HighRefreshFix).GetMethod("PhysicsPosition")), null, null, null, null);
            harmony.Patch(typeof(PolyNavAgent).GetMethod("Start", flags), null, new HarmonyMethod(typeof(HighRefreshFix).GetMethod("InterpolateAgent")), null, null, null);
            CameraFix.Install();
            Debug.Log("[HighRefreshFix] READY 1.1.0: fixed-step navigation, interpolated actors, per-render LateUpdate camera; fixedDeltaTime=" + Time.fixedDeltaTime);
        }
        catch (Exception error)
        {
            // Roll back all movement hooks, including FixedUpdate, on setup failure.
            harmony.UnpatchSelf();
            Debug.LogError("[HighRefreshFix] FAILED: " + error);
        }
    }

    public static bool AllowNavigation() { return physicsNavigation; }

    public static void InterpolateAgent(Rigidbody2D ___rb)
    {
        if (___rb != null) ___rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    public static bool PhysicsPosition(Rigidbody2D ___rb, ref Vector2 __result)
    {
        // Rendering uses interpolated transforms; navigation must use the actual physics position.
        if (!physicsNavigation || ___rb == null) return true;
        __result = ___rb.position;
        return false;
    }

    public static void BeforePhysics(PolyNavAgent __instance)
    {
        physicsNavigation = true;
        try { navigate(__instance); }
        finally { physicsNavigation = false; }
    }
}

