using JoyconBaseball.Phase1.Gameplay;
using JoyconBaseball.Phase1.UI;
using UnityEngine;

namespace JoyconBaseball.Phase1.Core
{
    public static class RuntimeSceneFactory
    {
        public static void CreateLightingIfMissing()
        {
            if (Object.FindFirstObjectByType<Light>() != null)
            {
                return;
            }

            var directionalLight = new GameObject("Directional Light");
            var light = directionalLight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            directionalLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        public static BoxCollider GetOrCreateStrikeZone(Phase1SceneReferences sceneReferences)
        {
            if (sceneReferences != null && sceneReferences.StrikeZoneCollider != null)
            {
                return sceneReferences.StrikeZoneCollider;
            }

            var zoneRoot = new GameObject("StrikeZone");
            zoneRoot.transform.position = new Vector3(0f, 1.1f, 0.25f);

            var zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone.name = "StrikeZoneTrigger";
            zone.transform.SetParent(zoneRoot.transform, false);
            zone.transform.localPosition = Vector3.zero;
            zone.transform.localScale = new Vector3(0.7f, 0.9f, 0.1f);

            var collider = zone.GetComponent<BoxCollider>();
            collider.isTrigger = true;

            var triggerRenderer = zone.GetComponent<Renderer>();
            triggerRenderer.enabled = false;

            CreateStrikeZoneLine(zoneRoot.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.72f, 0.015f, 0.015f));
            CreateStrikeZoneLine(zoneRoot.transform, new Vector3(0f, -0.45f, 0f), new Vector3(0.72f, 0.015f, 0.015f));
            CreateStrikeZoneLine(zoneRoot.transform, new Vector3(-0.35f, 0f, 0f), new Vector3(0.015f, 0.92f, 0.015f));
            CreateStrikeZoneLine(zoneRoot.transform, new Vector3(0.35f, 0f, 0f), new Vector3(0.015f, 0.92f, 0.015f));
            CreateStrikeZoneLine(zoneRoot.transform, new Vector3(-0.117f, 0f, 0f), new Vector3(0.012f, 0.9f, 0.012f));
            CreateStrikeZoneLine(zoneRoot.transform, new Vector3(0.117f, 0f, 0f), new Vector3(0.012f, 0.9f, 0.012f));
            CreateStrikeZoneLine(zoneRoot.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.7f, 0.012f, 0.012f));
            CreateStrikeZoneLine(zoneRoot.transform, new Vector3(0f, -0.15f, 0f), new Vector3(0.7f, 0.012f, 0.012f));

            return collider;
        }

        public static BatController GetOrCreateBat(
            Phase1SceneReferences sceneReferences,
            Transform cameraTransform,
            Phase1GameController controller,
            float reach)
        {
            if (sceneReferences != null && sceneReferences.BatController != null)
            {
                sceneReferences.BatController.Initialize(controller, sceneReferences.BatPivot);
                sceneReferences.BatController.ConfigureInput(
                    sceneReferences.UseJoyconGyroBatControl,
                    sceneReferences.JoyconSwingThreshold);
                return sceneReferences.BatController;
            }

            var pivot = new GameObject("BatPivot");
            pivot.transform.SetParent(cameraTransform);
            pivot.transform.localPosition = new Vector3(0.35f, -0.25f, reach);
            pivot.transform.localRotation = Quaternion.identity;

            var bat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bat.name = "Bat";
            bat.transform.SetParent(pivot.transform);
            bat.transform.localPosition = Vector3.zero;
            bat.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            bat.transform.localScale = new Vector3(0.06f, 0.55f, 0.06f);
            bat.GetComponent<Renderer>().material.color = new Color(0.75f, 0.58f, 0.32f);

            var oldCollider = bat.GetComponent<CapsuleCollider>();
            oldCollider.enabled = false;
            var collider = bat.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.9f, 0.12f, 0.12f);

            var batController = bat.AddComponent<BatController>();
            batController.Initialize(controller, pivot.transform);
            batController.ConfigureInput(
                sceneReferences != null && sceneReferences.UseJoyconGyroBatControl,
                sceneReferences != null ? sceneReferences.JoyconSwingThreshold : 1.35f);
            return batController;
        }

        public static PitchingMachine GetOrCreatePitchingMachine(Phase1SceneReferences sceneReferences, float pitchDistance)
        {
            if (sceneReferences != null && sceneReferences.PitchingMachine != null)
            {
                return sceneReferences.PitchingMachine;
            }

            var machine = new GameObject("PitchingMachine");
            machine.transform.position = new Vector3(0f, 1.1f, pitchDistance);

            var pitchingMachine = machine.AddComponent<PitchingMachine>();
            return pitchingMachine;
        }

        public static void CreateCatcher(Vector3 pitchingMachinePosition, BoxCollider strikeZoneCollider)
        {
            if (strikeZoneCollider == null)
            {
                return;
            }

            var strikeZoneCenter = strikeZoneCollider.transform.TransformPoint(strikeZoneCollider.center);
            var throwDir = (strikeZoneCenter - pitchingMachinePosition).normalized;

            // キャッチャー壁をストライクゾーンの4ユニット後方に配置
            var catcherPosition = strikeZoneCenter + throwDir * 2.5f;

            var catcher = GameObject.CreatePrimitive(PrimitiveType.Cube);
            catcher.name = "Catcher";
            catcher.transform.position = catcherPosition;
            catcher.transform.rotation = Quaternion.LookRotation(throwDir);
            catcher.transform.localScale = new Vector3(6f, 6f, 0.3f);

            var col = catcher.GetComponent<BoxCollider>();
            col.isTrigger = true;

            var renderer = catcher.GetComponent<Renderer>();
            renderer.material.color = new Color(0.15f, 0.15f, 0.15f);
        }

        public static Phase1UIController GetUi(Phase1SceneReferences sceneReferences)
        {
            if (sceneReferences != null && sceneReferences.UiController != null)
            {
                sceneReferences.UiController.Initialize();
                return sceneReferences.UiController;
            }

            var existingUiController = Object.FindFirstObjectByType<Phase1UIController>();
            if (existingUiController != null)
            {
                existingUiController.Initialize();
                return existingUiController;
            }

            Debug.LogError("Phase1UIController was not found in the scene. Create the UI in the scene and assign it via Phase1SceneReferences.");
            return null;
        }

        private static void CreateStrikeZoneLine(Transform parent, Vector3 localPosition, Vector3 localScale)
        {
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "Line";
            line.transform.SetParent(parent, false);
            line.transform.localPosition = localPosition;
            line.transform.localScale = localScale;

            var collider = line.GetComponent<Collider>();
            collider.enabled = false;

            var renderer = line.GetComponent<Renderer>();
            renderer.material.color = new Color(1f, 1f, 1f, 0.5f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
