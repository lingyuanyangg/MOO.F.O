using UnityEngine;
using extOSC;


public class IMUBoneDeformer : MonoBehaviour
{

    [Header("OSC")]
    public OSCReceiver receiver;


    // 可以在 Inspector 自定义
    public string accelAddress = "/imu/accel";
    public string gyroAddress = "/imu/gyro";



    [Header("Bone")]
    public Transform targetBone;



    [Header("Position Deformation")]
    public float positionScale = 0.05f;
    public float maxPositionOffset = 0.15f;



    [Header("Rotation")]
    public float rotationScale = 30f;
    public float maxRotation = 45f;



    [Header("Smooth")]
    public float smoothSpeed = 8f;



    private Vector3 acceleration;
    private Vector3 angularVelocity;


    private Vector3 originalPosition;
    private Quaternion originalRotation;


    private Vector3 currentOffset;
    private Quaternion currentRotation;



    void Start()
    {

        if(targetBone == null)
            targetBone = transform;


        originalPosition =
            targetBone.localPosition;


        originalRotation =
            targetBone.localRotation;



        if(receiver != null)
        {

            receiver.Bind(
                accelAddress,
                ReceiveAcceleration
            );


            receiver.Bind(
                gyroAddress,
                ReceiveGyro
            );

        }
        else
        {
            Debug.LogError(
                "OSC Receiver missing!"
            );
        }

    }



    void Update()
    {


        /*
         * ACCEL -> POSITION
         */


        Vector3 targetOffset =
            acceleration *
            positionScale;



        targetOffset =
            Vector3.ClampMagnitude(
                targetOffset,
                maxPositionOffset
            );



        currentOffset =
            Vector3.Lerp(
                currentOffset,
                targetOffset,
                Time.deltaTime * smoothSpeed
            );



        targetBone.localPosition =
            originalPosition +
            currentOffset;




        /*
         * GYRO -> ROTATION
         */


        Vector3 targetEuler =
            angularVelocity *
            rotationScale;



        targetEuler =
            Vector3.ClampMagnitude(
                targetEuler,
                maxRotation
            );



        Quaternion targetRot =
            originalRotation *
            Quaternion.Euler(
                targetEuler
            );



        currentRotation =
            Quaternion.Slerp(
                currentRotation,
                targetRot,
                Time.deltaTime * smoothSpeed
            );



        targetBone.localRotation =
            currentRotation;


    }





    void ReceiveAcceleration(OSCMessage message)
    {

        acceleration =
            new Vector3(

                message.Values[0].FloatValue,

                message.Values[1].FloatValue,

                message.Values[2].FloatValue

            );

    }




    void ReceiveGyro(OSCMessage message)
    {

        angularVelocity =
            new Vector3(

                message.Values[0].FloatValue,

                message.Values[1].FloatValue,

                message.Values[2].FloatValue

            );

    }

}