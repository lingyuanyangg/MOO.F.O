using UnityEngine;

public class SkyboxRotation : MonoBehaviour
{
    public float rotationSpeed = 5f;

    private Material skyboxMaterial;
    private float rotation;

    void Start()
    {
        skyboxMaterial = RenderSettings.skybox;
    }

    void Update()
    {
        rotation += rotationSpeed * Time.deltaTime;

        skyboxMaterial.SetFloat("_Rotation", rotation);
    }
}