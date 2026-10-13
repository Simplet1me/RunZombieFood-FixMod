using System;
using System.Reflection;
using BepInEx;
using UnityEngine;

[BepInPlugin("rz.scheduling-probe", "Scheduling Probe (test only)", "1.0.0")]
[BepInDependency("rz.high-refresh-fix", BepInDependency.DependencyFlags.HardDependency)]
public sealed class SchedulingProbe : BaseUnityPlugin
{
    private bool tested;
    private void FixedUpdate()
    {
        if (tested) return;
        tested = true;
        GameObject subject = new GameObject("HighRefreshFix isolated scheduling test");
        try
        {
            Rigidbody2D body = subject.AddComponent<Rigidbody2D>();
            body.isKinematic = true;
            PolyNavAgent agent = subject.AddComponent<PolyNavAgent>();
            agent.enabled = false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = typeof(PolyNavAgent);
            type.GetMethod("Start", flags).Invoke(agent, null);
            if (body.interpolation != RigidbodyInterpolation2D.Interpolate)
                throw new Exception("Actor render interpolation not enabled");
            FieldInfo speed = type.GetField("maxSpeed", flags);
            type.GetField("originalSpeed", flags).SetValue(agent, 7f);
            type.GetField("maxSpeedInNight", flags).SetValue(agent, 7f);
            MethodInfo late = type.GetMethod("LateUpdate", flags);
            MethodInfo physics = type.GetMethod("FixedUpdate", flags);
            foreach (int renderFrames in new int[] { 0, 1, 2, 3, 4, 8, 16 })
            {
                speed.SetValue(agent, -1f);
                for (int i = 0; i < renderFrames; ++i) late.Invoke(agent, null);
                if ((float)speed.GetValue(agent) != -1f) throw new Exception("Render navigation was not suppressed");
                physics.Invoke(agent, null);
                if ((float)speed.GetValue(agent) != 7f) throw new Exception("Physics did not execute original navigation");
                speed.SetValue(agent, -1f);
                late.Invoke(agent, null);
                if ((float)speed.GetValue(agent) != -1f) throw new Exception("Physics guard leaked");
            }
            Debug.Log("[SchedulingProbe] PASS: 0/1/2/3/4/8/16 render calls suppressed, original navigation runs in physics, guard resets. deltaTime=" + Time.deltaTime);
        }
        catch (Exception error) { Debug.LogError("[SchedulingProbe] FAIL: " + error); }
        finally { Destroy(subject); }
    }
}
