using UnityEngine;
using System.Collections.Generic;


public class ObjectAfterImage : MonoBehaviour
{

    public GameObject ghostPrefab;

    public int ghostCount = 8;

    public float delay = 0.05f;


    List<TransformData> history =
        new List<TransformData>();


    GameObject[] ghosts;



    void Start()
    {

        ghosts = new GameObject[ghostCount];


        for(int i=0;i<ghostCount;i++)
        {
            ghosts[i] =
            Instantiate(
                ghostPrefab,
                transform.position,
                transform.rotation
            );


            ghosts[i].GetComponent<Renderer>()
                .material.color =
                new Color(
                    1,
                    1,
                    1,
                    0.15f/(i+1)
                );
        }

    }



    void LateUpdate()
    {

        history.Insert(
            0,
            new TransformData(
                transform.position,
                transform.rotation
            )
        );


        if(history.Count>ghostCount*5)
            history.RemoveAt(history.Count-1);



        for(int i=0;i<ghostCount;i++)
        {

            int index =
                Mathf.Clamp(
                    (i+1)*5,
                    0,
                    history.Count-1
                );


            ghosts[i].transform.position =
                history[index].pos;


            ghosts[i].transform.rotation =
                history[index].rot;

        }

    }



}



public class TransformData
{

    public Vector3 pos;
    public Quaternion rot;


    public TransformData(
        Vector3 p,
        Quaternion r
    )
    {
        pos=p;
        rot=r;
    }

}