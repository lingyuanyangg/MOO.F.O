using UnityEngine;
using extOSC;
using System.Collections;


public class OSCSpawner : MonoBehaviour
{
    [Header("OSC")]
    public OSCReceiver receiver;
    public string oscAddress = "/buttonL";


    [Header("Spawn")]
    public GameObject prefab;
    public Camera targetCamera;


    [Header("Spawn Area")]
    public float minDistance = 2f;
    public float maxDistance = 8f;


    [Header("Life")]
    public float objectLifeTime = 3f;


    [Header("Scale Animation")]
    public float appearDuration = 0.5f;
    public float disappearDuration = 0.5f;



    void Start()
    {
        if (receiver == null)
        {
            Debug.LogError("OSC Receiver 未指定！");
            return;
        }


        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }


        receiver.Bind(
            oscAddress,
            OnReceiveButton
        );
    }



    void OnReceiveButton(OSCMessage message)
    {
        if (message.Values.Count == 0)
            return;


        int value = message.Values[0].IntValue;


        if (value == 1)
        {
            SpawnObject();
        }
    }



    void SpawnObject()
    {
        float screenX = Random.Range(0.2f, 0.8f);
        float screenY = Random.Range(0.2f, 0.8f);


        float distance = Random.Range(
            minDistance,
            maxDistance
        );


        Vector3 screenPos = new Vector3(
            screenX * Screen.width,
            screenY * Screen.height,
            distance
        );


        Vector3 worldPos =
            targetCamera.ScreenToWorldPoint(
                screenPos
            );



        GameObject obj = Instantiate(
            prefab,
            worldPos,
            Random.rotation
        );



        // 保存原始scale
        Vector3 targetScale =
            obj.transform.localScale;



        // 初始隐藏
        obj.transform.localScale =
            Vector3.zero;



        StartCoroutine(
            ScaleRoutine(
                obj,
                targetScale
            )
        );
    }




    IEnumerator ScaleRoutine(
        GameObject obj,
        Vector3 targetScale
    )
    {
        float timer = 0f;



        // 出现动画
        while(timer < appearDuration)
        {
            timer += Time.deltaTime;


            float t =
                timer / appearDuration;


            t =
                Mathf.SmoothStep(
                    0,
                    1,
                    t
                );


            obj.transform.localScale =
                Vector3.Lerp(
                    Vector3.zero,
                    targetScale,
                    t
                );


            yield return null;
        }


        obj.transform.localScale =
            targetScale;




        // 保持存在
        yield return new WaitForSeconds(
            objectLifeTime
        );




        // 消失动画
        timer = 0f;


        while(timer < disappearDuration)
        {
            timer += Time.deltaTime;


            float t =
                timer / disappearDuration;


            t =
                Mathf.SmoothStep(
                    0,
                    1,
                    t
                );


            obj.transform.localScale =
                Vector3.Lerp(
                    targetScale,
                    Vector3.zero,
                    t
                );


            yield return null;
        }



        Destroy(obj);
    }
}