using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class PopArtBurstSymbol : MonoBehaviour
{
    private Material burstMaterial;
    private float expiresAt;
    private void Awake() { expiresAt = Time.unscaledTime + 0.4f; }
    private void Update() { if (Time.unscaledTime >= expiresAt) Destroy(gameObject); }
    private void OnDestroy() { if (burstMaterial != null) Destroy(burstMaterial); }
    public static void Spawn(Vector3 position, Vector3 normal)
    {
        GameObject g=new GameObject("Graphic Milk Burst Star"); g.transform.position=position; g.transform.rotation=normal.sqrMagnitude>0.01f?Quaternion.LookRotation(normal):Quaternion.identity; g.AddComponent<PopArtBurstSymbol>().Build();
    }
    private void Build()
    {
        LineRenderer l=gameObject.AddComponent<LineRenderer>(); l.useWorldSpace=false; l.loop=true; l.positionCount=24; l.alignment=LineAlignment.View; l.startWidth=l.endWidth=0.12f; l.numCornerVertices=2; l.shadowCastingMode=ShadowCastingMode.Off;
        for(int i=0;i<24;i++){float a=i/24f*Mathf.PI*2f;float r=(i%2==0)?1.25f:0.42f;l.SetPosition(i,new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0));}
        Shader s=Shader.Find("Hand2/SpectralEnergyLine");
        if (s == null || !s.isSupported) { Destroy(gameObject); return; }
        Material m=new Material(s); burstMaterial=m; m.SetColor("_BaseColor",new Color(2.0f,1.3f,1.8f,1));l.material=m;StartCoroutine(Fade(l,m));
    }
    private IEnumerator Fade(LineRenderer l,Material m){float d=.22f,e=0;while(e<d){e+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(e/d);transform.localScale=Vector3.one*Mathf.Lerp(.25f,1.1f,t);l.startColor=l.endColor=new Color(1,1,1,1-t);var c=m.GetColor("_BaseColor");c.a=1-t;m.SetColor("_BaseColor",c);yield return null;}Destroy(gameObject);}
}
