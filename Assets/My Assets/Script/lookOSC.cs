using UnityEngine;
using Unity.Cinemachine;
using extOSC;


public class OSC_IMU_CinemachineControl : MonoBehaviour
{

    [Header("OSC")]
    public OSCReceiver receiver;


    [Header("Cinemachine")]
    public CinemachineOrbitalFollow orbitalFollow;
    public CinemachineCamera cinemachineCamera;



    [Header("IMU Filter")]
    [Range(0.01f,1f)]
    public float lowPassStrength = 0.15f;


    [Range(0f,5f)]
    public float deadZone = 0.5f;



    [Header("Camera Smooth")]
    public float rotationSmoothTime = 0.15f;

    public float fovSmoothTime = 0.2f;



    private float imuHorizontal;
    private float imuVertical;


    private float filteredHorizontal;
    private float filteredVertical;


    private float targetHorizontal;
    private float targetVertical;


    private float horizontalVelocity;
    private float verticalVelocity;



    private float targetFOV;
    private float fovVelocity;



    void Start()
    {

        filteredHorizontal =
            orbitalFollow.HorizontalAxis.Value;

        filteredVertical =
            orbitalFollow.VerticalAxis.Value;


        targetFOV =
            cinemachineCamera.Lens.FieldOfView;



        receiver.Bind(
            "/camera/horizontal",
            ReceiveHorizontal
        );


        receiver.Bind(
            "/camera/vertical",
            ReceiveVertical
        );


        receiver.Bind(
            "/camera/fov",
            ReceiveFOV
        );

    }



    void Update()
    {


        /*
         * 1. Low Pass Filter
         *
         * 消除IMU 高频抖动
         */

        filteredHorizontal =
            Mathf.Lerp(
                filteredHorizontal,
                imuHorizontal,
                lowPassStrength
            );


        filteredVertical =
            Mathf.Lerp(
                filteredVertical,
                imuVertical,
                lowPassStrength
            );



        /*
         * 2. Dead Zone
         *
         * 防止手静止时微动
         */


        if(
            Mathf.Abs(
                filteredHorizontal-targetHorizontal
            )
            >
            deadZone
        )
        {
            targetHorizontal =
                filteredHorizontal;
        }



        if(
            Mathf.Abs(
                filteredVertical-targetVertical
            )
            >
            deadZone
        )
        {
            targetVertical =
                filteredVertical;
        }




        /*
         * 3. SmoothDamp
         *
         * 摄像机惯性
         */


        orbitalFollow.HorizontalAxis.Value =
            Mathf.SmoothDamp(
                orbitalFollow.HorizontalAxis.Value,
                targetHorizontal,
                ref horizontalVelocity,
                rotationSmoothTime
            );



        orbitalFollow.VerticalAxis.Value =
            Mathf.SmoothDamp(
                orbitalFollow.VerticalAxis.Value,
                targetVertical,
                ref verticalVelocity,
                rotationSmoothTime
            );



        cinemachineCamera.Lens.FieldOfView =
            Mathf.SmoothDamp(
                cinemachineCamera.Lens.FieldOfView,
                targetFOV,
                ref fovVelocity,
                fovSmoothTime
            );

    }




    void ReceiveHorizontal(OSCMessage message)
    {

        imuHorizontal =
            message.Values[0].FloatValue;

    }



    void ReceiveVertical(OSCMessage message)
    {

        imuVertical =
            message.Values[0].FloatValue;

    }



    void ReceiveFOV(OSCMessage message)
    {

        targetFOV =
            message.Values[0].FloatValue;

    }

}