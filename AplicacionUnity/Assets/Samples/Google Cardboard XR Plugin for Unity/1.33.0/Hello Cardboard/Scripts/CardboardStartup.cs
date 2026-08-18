using System.Collections;
using Google.XR.Cardboard;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;

/// <summary>
/// Prepara la escena en el modo solicitado: pantalla completa con sensores o
/// vista estereoscópica de Google Cardboard.
/// </summary>
public class CardboardStartup : MonoBehaviour
{
    private IEnumerator Start()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.brightness = 1.0f;

        XRManagerSettings manager = XRGeneralSettings.Instance != null
            ? XRGeneralSettings.Instance.Manager
            : null;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("No se encontró la cámara principal para configurar la visualización.");
            yield break;
        }

        DisplayModeController displayMode = GetComponent<DisplayModeController>();
        if (displayMode == null)
            displayMode = gameObject.AddComponent<DisplayModeController>();

        if (DisplayModeController.IsVrModeRequested)
        {
            yield return StartCardboard(manager, mainCamera, displayMode);
            yield break;
        }

        yield return StartFlatMode(manager, mainCamera, displayMode);
    }

    private static IEnumerator StartFlatMode(
        XRManagerSettings manager,
        Camera mainCamera,
        DisplayModeController displayMode)
    {
        if (manager != null && manager.activeLoader != null)
        {
            manager.StopSubsystems();
            manager.DeinitializeLoader();
            yield return null;
        }

        mainCamera.stereoTargetEye = StereoTargetEyeMask.None;
        SetUrpXrRendering(mainCamera, true);

        MobileGyroscopeCamera gyroscope = mainCamera.GetComponent<MobileGyroscopeCamera>();
        if (gyroscope == null)
            gyroscope = mainCamera.gameObject.AddComponent<MobileGyroscopeCamera>();
        gyroscope.enabled = true;

        TrackedPoseDriver trackedPose = mainCamera.GetComponent<TrackedPoseDriver>();
        if (trackedPose != null)
            trackedPose.enabled = false;

        displayMode.SetRuntimeVrActive(false);
        Debug.Log("Visualización monoscópica activada.");
    }

    private static IEnumerator StartCardboard(
        XRManagerSettings manager,
        Camera mainCamera,
        DisplayModeController displayMode)
    {
        MobileGyroscopeCamera gyroscope = mainCamera.GetComponent<MobileGyroscopeCamera>();
        if (gyroscope != null)
            gyroscope.enabled = false;

        // URP puede mantener la cámara activa y aun así excluirla del pase XR.
        // En ese caso Cardboard dibuja el divisor, pero ambos ojos quedan negros.
        SetUrpXrRendering(mainCamera, true);
        mainCamera.stereoTargetEye = StereoTargetEyeMask.Both;

        TrackedPoseDriver trackedPose = mainCamera.GetComponent<TrackedPoseDriver>();
        if (trackedPose != null)
            trackedPose.enabled = true;

        if (manager == null)
        {
            Debug.LogError("XR Management no está configurado. Se vuelve al modo plano.");
            DisplayModeController.ForceFlatPreference();
            yield return StartFlatMode(null, mainCamera, displayMode);
            yield break;
        }

        if (manager.activeLoader == null)
        {
            Debug.Log("Inicializando Google Cardboard XR...");
            yield return manager.InitializeLoader();

            if (manager.activeLoader == null)
            {
                Debug.LogError("No fue posible iniciar Google Cardboard. Se vuelve al modo plano.");
                DisplayModeController.ForceFlatPreference();
                yield return StartFlatMode(manager, mainCamera, displayMode);
                yield break;
            }

            manager.StartSubsystems();
        }

        if (Api.HasNewDeviceParams())
            Api.ReloadDeviceParams();
        else if (!Api.HasDeviceParams())
            Api.ScanDeviceParams();

        Api.UpdateScreenParams();
        displayMode.SetRuntimeVrActive(true);
        Debug.Log("Visualización Google Cardboard activada.");
    }

    private static void SetUrpXrRendering(Camera camera, bool enabled)
    {
        UniversalAdditionalCameraData cameraData =
            camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData != null)
            cameraData.allowXRRendering = enabled;
    }
}
