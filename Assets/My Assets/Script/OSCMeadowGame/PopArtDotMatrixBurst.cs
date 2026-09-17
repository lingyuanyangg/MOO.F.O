using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PopArtDotMatrixBurst : MonoBehaviour
{
    private readonly List<Transform> dots = new List<Transform>();
    private readonly List<Material> materials = new List<Material>();

    public static void Spawn(Vector3 position, float radius)
    {
        GameObject root = new GameObject("Poop Brown Acid Dot Matrix");
        root.transform.position = position + Vector3.up * 0.12f;
        root.AddComponent<PopArtDotMatrixBurst>().Build(radius);
    }

    private void Build(float radius)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        Color brown = new Color(0.48f,0.055f,0.012f,1f);
        Color acid = new Color(1.6f,3.2f,0.015f,1f);
        for(int x=-2;x<=2;x++) for(int z=-2;z<=2;z++)
        {
            if((x+z)%2!=0) continue;
            GameObject dot=GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(dot.GetComponent<Collider>());
            dot.transform.SetParent(transform,false);
            dot.transform.localPosition=new Vector3(x,z==0?0.03f:0f,z)*radius*0.12f;
            dot.transform.localScale=Vector3.one*radius*0.105f;
            Material material=new Material(shader);
            material.SetColor("_BaseColor", ((x+z)&2)==0?brown:acid);
            dot.GetComponent<Renderer>().material=material;
            materials.Add(material); dots.Add(dot.transform);
        }
        StartCoroutine(Animate(radius));
    }

    private IEnumerator Animate(float radius)
    {
        float elapsed=0f,duration=.48f;
        while(elapsed<duration)
        {
            elapsed+=Time.deltaTime; float t=Mathf.Clamp01(elapsed/duration);
            transform.localScale=Vector3.one*Mathf.Lerp(.25f,2.1f,1f-Mathf.Pow(1f-t,3f));
            transform.Rotate(0,85f*Time.deltaTime,0);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy(){foreach(Material m in materials)if(m!=null)Destroy(m);}
}
