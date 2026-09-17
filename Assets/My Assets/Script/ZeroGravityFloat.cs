using UnityEngine;

public class ZeroGravityFloat : MonoBehaviour
{
    [Header("Small Rotation")]
    public float rotationAmount = 8f;   // 最大旋转角度
    public float rotationSpeed = 0.3f;  // 变化速度

    [Header("Small Position Drift")]
    public float positionAmount = 0.05f; // 最大位置偏移
    public float positionSpeed = 0.3f;


    private Vector3 startPosition;
    private Quaternion startRotation;

    private float randomSeed;


    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        randomSeed = Random.Range(0f, 1000f);
    }


    void Update()
    {
        float t = Time.time;


        // =====================
        // 微小位置漂浮
        // =====================

        float px = Mathf.PerlinNoise(randomSeed, t * positionSpeed) - 0.5f;
        float py = Mathf.PerlinNoise(randomSeed + 20, t * positionSpeed) - 0.5f;
        float pz = Mathf.PerlinNoise(randomSeed + 40, t * positionSpeed) - 0.5f;


        Vector3 positionOffset =
            new Vector3(px, py, pz) * positionAmount;


        transform.position =
            startPosition + positionOffset;



        // =====================
        // 微小旋转
        // =====================

        float rx = Mathf.PerlinNoise(randomSeed, t * rotationSpeed) - 0.5f;
        float ry = Mathf.PerlinNoise(randomSeed + 50, t * rotationSpeed) - 0.5f;
        float rz = Mathf.PerlinNoise(randomSeed + 100, t * rotationSpeed) - 0.5f;


        Vector3 rotationOffset =
            new Vector3(rx, ry, rz) * rotationAmount * 2;


        transform.rotation =
            startRotation *
            Quaternion.Euler(rotationOffset);
    }
}