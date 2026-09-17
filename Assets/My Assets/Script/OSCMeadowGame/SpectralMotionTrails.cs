using UnityEngine;
using UnityEngine.Rendering;

public class SpectralMotionTrails : MonoBehaviour
{
    private Rigidbody body;
    private readonly TrailRenderer[] trails = new TrailRenderer[3];
    private readonly Material[] materials = new Material[3];
    private static readonly Color[] Colors = { new Color(3f,0.02f,0.08f), new Color(0.02f,3f,0.25f), new Color(0.03f,0.4f,3.2f) };
    private void Start()
    {
        body=GetComponent<Rigidbody>();
        Shader shader=Shader.Find("Universal Render Pipeline/Unlit");
        for(int i=0;i<3;i++)
        {
            GameObject g=new GameObject("RGB Ghost "+i); g.transform.SetParent(transform,false); g.transform.localPosition=new Vector3((i-1)*0.12f,0.75f,(1-i)*0.045f);
            TrailRenderer tr=g.AddComponent<TrailRenderer>(); tr.time=0.48f; tr.minVertexDistance=0.018f; tr.startWidth=0.38f; tr.endWidth=0f; tr.numCornerVertices=4; tr.shadowCastingMode=ShadowCastingMode.Off; tr.receiveShadows=false;
            Gradient gr=new Gradient(); gr.SetKeys(new[]{new GradientColorKey(Colors[i],0),new GradientColorKey(Colors[(i+1)%3],1)},new[]{new GradientAlphaKey(0.64f,0),new GradientAlphaKey(0.28f,0.62f),new GradientAlphaKey(0,1)}); tr.colorGradient=gr;
            materials[i]=new Material(shader); materials[i].SetColor("_BaseColor",Colors[i]); tr.material=materials[i]; trails[i]=tr;
        }
    }
    private void Update(){float speed=body==null?0:body.linearVelocity.magnitude; float a=Mathf.InverseLerp(0.25f,7f,speed); for(int i=0;i<3;i++){trails[i].time=Mathf.Lerp(0.12f,0.88f,a); trails[i].startWidth=Mathf.Lerp(0.07f,0.58f,a);}}
    private void OnDestroy(){foreach(Material m in materials)if(m!=null)Destroy(m);}
}
