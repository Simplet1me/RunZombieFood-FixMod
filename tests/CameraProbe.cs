using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using UnityEngine;

[BepInPlugin("rz.camera-probe", "Camera Probe (test only)", "1.1.0")]
[BepInDependency("rz.high-refresh-fix", BepInDependency.DependencyFlags.HardDependency)]
public sealed class CameraProbe : BaseUnityPlugin
{
    private IEnumerator Start()
    {
        int savedRate = Time.captureFramerate;
        bool savedIndoor = GameControl.IsIndoor;
        GameObject subject = new GameObject("Isolated camera test");
        GameObject target = new GameObject("Isolated camera target");
        GameObject indoor = new GameObject("Isolated indoor target");
        try
        {
            Camera camera = subject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            Rigidbody2D body = subject.AddComponent<Rigidbody2D>();
            body.isKinematic = true;
            subject.transform.position = new Vector3(0, 0, -10);
            Camera2DFollow follow = subject.AddComponent<Camera2DFollow>();
            follow.OutdoorTarget = target.transform;
            follow.IndoorTarget = indoor.transform;
            follow.lookAheadFactor = 0;
            follow.damping = 0.1f;
            follow.OrthographicSizeRate = 0;
            follow.OrthographicSizeSpeed = 0.01f;
            GameControl.IsIndoor = false;
            yield return null;
            CameraRenderDriver driver = subject.GetComponent<CameraRenderDriver>();
            Require(driver != null, "Start did not attach render driver");
            driver.enabled = false; // Manually invoke the real patched callback to inspect each frame.
            MethodInfo update = typeof(Camera2DFollow).GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (int rate in new int[] { 30, 60, 144, 180, 240 })
            {
                Time.captureFramerate = rate;
                yield return null;
                camera.orthographicSize = 30f;
                for (int frame = 0; frame < rate; ++frame)
                {
                    Vector3 before = subject.transform.position;
                    target.transform.position = new Vector3(before.x + 5f, 0, 0);
                    update.Invoke(follow, null);
                    Require(subject.transform.position == before, "Update moved camera before LateUpdate");
                    CameraFix.Render(follow);
                    Require(subject.transform.position.x > before.x, "Camera waited for physics instead of moving immediately");
                    Require(Mathf.Abs(subject.transform.position.z + 10f) < 0.0001f, "Camera depth changed");
                    Require(Vector2.Distance(Camera2DFollow.CameraPostion, body.position) < 0.0001f, "Shared camera position is stale");
                    yield return null;
                }
                Require(Mathf.Abs(camera.orthographicSize - 29.4f) < 0.003f, "Zoom speed varies with render rate");
                Debug.Log("[CameraProbe] PASS rate=" + rate + ": immediate movement each render, stable Z, current shared position, equal zoom per second");
            }
            follow.enabled = false;
            Vector3 stopped = subject.transform.position;
            CameraFix.Render(follow);
            Require(subject.transform.position == stopped, "Disabled camera still updates");
            follow.enabled = true;
            GameControl.IsIndoor = true;
            follow.IndoorTarget = null;
            CameraFix.Render(follow);
            Require(subject.transform.position == stopped, "Missing target moved camera");
            follow.IndoorTarget = indoor.transform;
            indoor.transform.position = stopped + Vector3.right * 100f;
            CameraFix.Render(follow);
            Require(subject.transform.position.x > stopped.x, "Indoor target switch failed");
            Debug.Log("[CameraProbe] ALL PASS including disabled camera, missing target, indoor switch");
        }
        finally
        {
            Time.captureFramerate = savedRate;
            GameControl.IsIndoor = savedIndoor;
            Destroy(subject);
            Destroy(target);
            Destroy(indoor);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("[CameraProbe] FAIL: " + message);
    }
}
