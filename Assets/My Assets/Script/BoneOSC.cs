using UnityEngine;
using extOSC;
using System.Collections.Generic;


public class HandBoneController : MonoBehaviour
{

    [Header("OSC Receiver")]
    public OSCReceiver receiver;



    [System.Serializable]
    public class Bone
    {

        [Header("OSC Address")]
        public string address;


        [Header("Unity Bone Transform")]
        public Transform bone;


        [HideInInspector]
        public Vector3 rotation;

    }



    [Header("Bones")]
    public List<Bone> bones = new List<Bone>();




    void Start()
    {

        if(receiver == null)
        {
            Debug.LogError(
                "OSC Receiver 未指定!"
            );

            return;
        }



        foreach(Bone b in bones)
        {

            if(b.bone == null)
            {
                Debug.LogWarning(
                    "Bone Transform 未指定: "
                    + b.address
                );

                continue;
            }



            string rotAddress =
                b.address + "/rot";



            receiver.Bind(
                rotAddress,
                msg =>
                {

                    // 检查OSC数据数量
                    if(msg.Values.Count < 3)
                    {

                        Debug.LogWarning(
                            rotAddress +
                            " 数据不足，只收到 "
                            + msg.Values.Count
                            + " 个值"
                        );

                        return;
                    }



                    float x =
                        msg.Values[0].FloatValue;


                    float y =
                        msg.Values[1].FloatValue;


                    float z =
                        msg.Values[2].FloatValue;



                    b.rotation =
                        new Vector3(
                            x,
                            y,
                            z
                        );



                    // Euler -> Quaternion
                    b.bone.localRotation =
                        Quaternion.Euler(
                            b.rotation
                        );


                }
            );

        }

    }

}