using UnityEngine;
using UnityEngine.InputSystem;
using InputSystemGyroscope = UnityEngine.InputSystem.Gyroscope;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.InputSystem.Android;
#endif

/// <summary>
/// Controla una cámara monoscópica usando la orientación física del teléfono.
/// No depende de un visor XR y conserva una única imagen a pantalla completa.
/// </summary>
public class MobileGyroscopeCamera : MonoBehaviour
{
    [SerializeField, Range(0f, 30f)] private float smoothing = 18f;
    [SerializeField, Range(15f, 120f)] private float samplingFrequency = 60f;

    private AttitudeSensor attitudeSensor;
    private InputSystemGyroscope gyroscopeSensor;
    private Quaternion initialCameraRotation;
    private Quaternion referenceAttitude;
    private Quaternion integratedGyroscopeRotation = Quaternion.identity;
    private bool attitudeCalibrated;
    private ScreenOrientation observedScreenOrientation;
    private float orientationStableTime;

    private const float CalibrationDelay = 0.25f;

    private void OnEnable()
    {
        // Los sensores informan sus ejes respecto de la orientación física
        // nativa del dispositivo (retrato). Unity debe compensarlos cuando la
        // aplicación está bloqueada en modo horizontal.
        InputSystem.settings.compensateForScreenOrientation = true;

        initialCameraRotation = transform.localRotation;
        observedScreenOrientation = Screen.orientation;
        orientationStableTime = 0f;
        EnableBestAvailableSensor();
    }

    private void EnableBestAvailableSensor()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // GameRotationVector evita depender del norte magnético y suele ser la
        // opción más estable para una experiencia tipo VR con teléfono.
        attitudeSensor = InputSystem.GetDevice<AndroidGameRotationVector>();
        if (attitudeSensor == null)
            attitudeSensor = InputSystem.GetDevice<AndroidRotationVector>();
#else
        attitudeSensor = AttitudeSensor.current;
#endif

        if (attitudeSensor != null)
        {
            InputSystem.EnableDevice(attitudeSensor);
            attitudeSensor.samplingFrequency = samplingFrequency;
            Debug.Log("Seguimiento monoscópico iniciado con el sensor de actitud.");
            return;
        }

        gyroscopeSensor = InputSystemGyroscope.current;
        if (gyroscopeSensor != null)
        {
            InputSystem.EnableDevice(gyroscopeSensor);
            gyroscopeSensor.samplingFrequency = samplingFrequency;
            Debug.LogWarning("Sensor de actitud no disponible; se usa el giroscopio como respaldo.");
            return;
        }

        Debug.LogError("El teléfono no expone un sensor de actitud ni un giroscopio compatible.");
    }

    private void Update()
    {
        Quaternion targetRotation;

        if (attitudeSensor != null && attitudeSensor.enabled)
        {
            // Android puede informar primero una orientación transitoria al
            // cambiar de escena. Esperamos a que se estabilice para que esa
            // transición no quede guardada como un giro fijo de 90 grados.
            if (Screen.orientation != observedScreenOrientation)
            {
                observedScreenOrientation = Screen.orientation;
                orientationStableTime = 0f;
                attitudeCalibrated = false;
                transform.localRotation = initialCameraRotation;
                return;
            }

            orientationStableTime += Time.unscaledDeltaTime;
            Quaternion currentAttitude = ConvertToUnityCoordinates(attitudeSensor.attitude.ReadValue());

            if (!attitudeCalibrated)
            {
                transform.localRotation = initialCameraRotation;
                if (orientationStableTime < CalibrationDelay)
                    return;

                referenceAttitude = currentAttitude;
                attitudeCalibrated = true;
                return;
            }

            // La diferencia debe calcularse en el sistema local inicial del
            // teléfono. El orden inverso la convertiría en una rotación global
            // y, en horizontal, intercambiaría cabeceo y giro lateral.
            Quaternion relativeRotation = Quaternion.Inverse(referenceAttitude) * currentAttitude;

            // Esta experiencia usa el teléfono como una ventana monoscópica,
            // no como un visor sujeto a la cabeza. Conservamos cabeceo y giro
            // lateral, pero bloqueamos el roll para mantener techo, piso y onda
            // con la orientación original de la escena.
            Vector3 relativeEuler = relativeRotation.eulerAngles;
            float pitch = Mathf.DeltaAngle(0f, relativeEuler.x);
            float yaw = Mathf.DeltaAngle(0f, relativeEuler.y);
            targetRotation = initialCameraRotation * Quaternion.Euler(pitch, yaw, 0f);
        }
        else if (gyroscopeSensor != null && gyroscopeSensor.enabled)
        {
            Vector3 radiansPerSecond = gyroscopeSensor.angularVelocity.ReadValue();
            Vector3 degrees = new Vector3(-radiansPerSecond.x, -radiansPerSecond.y, radiansPerSecond.z)
                * Mathf.Rad2Deg * Time.deltaTime;
            integratedGyroscopeRotation *= Quaternion.Euler(degrees);
            Vector3 integratedEuler = integratedGyroscopeRotation.eulerAngles;
            float pitch = Mathf.DeltaAngle(0f, integratedEuler.x);
            float yaw = Mathf.DeltaAngle(0f, integratedEuler.y);
            targetRotation = initialCameraRotation * Quaternion.Euler(pitch, yaw, 0f);
        }
        else
        {
            return;
        }

        float blend = smoothing <= 0f ? 1f : 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, blend);
    }

    /// <summary>
    /// Define la orientación actual del teléfono como la nueva vista frontal.
    /// </summary>
    public void Recenter()
    {
        attitudeCalibrated = false;
        orientationStableTime = 0f;
        integratedGyroscopeRotation = Quaternion.identity;
        transform.localRotation = initialCameraRotation;
    }

    private static Quaternion ConvertToUnityCoordinates(Quaternion attitude)
    {
        // Android y Unity utilizan convenciones de ejes y handedness diferentes.
        return new Quaternion(attitude.x, attitude.y, -attitude.z, -attitude.w);
    }

    private void OnDisable()
    {
        if (attitudeSensor != null && attitudeSensor.enabled)
            InputSystem.DisableDevice(attitudeSensor);

        if (gyroscopeSensor != null && gyroscopeSensor.enabled)
            InputSystem.DisableDevice(gyroscopeSensor);
    }
}
