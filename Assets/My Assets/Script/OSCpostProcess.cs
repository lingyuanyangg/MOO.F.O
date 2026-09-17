using UnityEngine;
using extOSC;
using System.Collections.Generic;


public class GlitchManager : MonoBehaviour
{

    public OSCReceiver receiver;


    [System.Serializable]
    public class GlitchParameter
    {

        public string oscAddress;


        public Material material;


        public string shaderParameter;


        public float minValue = 0;

        public float maxValue = 1;


        public bool invert = false;


    }



    public List<GlitchParameter> parameters =
        new List<GlitchParameter>();



    void Start()
    {

        foreach(var g in parameters)
        {

            receiver.Bind(
                g.oscAddress,
                message =>
                {

                    float value =
                    message.Values[0].FloatValue;


                    if(g.invert)
                    {
                        value = 1 - value;
                    }


                    value = Mathf.Lerp(
                        g.minValue,
                        g.maxValue,
                        value
                    );


                    g.material.SetFloat(
                        g.shaderParameter,
                        value
                    );


                }
            );


        }

    }

}