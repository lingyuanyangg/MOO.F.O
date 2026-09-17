using UnityEngine;

public class FloatingObject : MonoBehaviour
{
    [Header("Movement")]
    public float minSpeed = 0.5f;
    public float maxSpeed = 2.5f;

    [Header("Rotation")]
    public float minRotateSpeed = 20f;
    public float maxRotateSpeed = 120f;

    [Header("Scale")]
    public float minScale = 0.5f;
    public float maxScale = 2.0f;

    [Header("Lifetime")]
    public float minLifetime = 5f;
    public float maxLifetime = 10f;

    [Header("Floating Noise")]
    public float noiseStrength = 0.5f;
    public float noiseSpeed = 1.0f;

    private Vector3 direction;
    private Vector3 rotationAxis;
    private float moveSpeed;
    private float rotateSpeed;
    private float lifetime;

    private Vector3 basePosition;
    private float seedX;
    private float seedY;
    private float seedZ;

    void Start()
    {
        // 随机大小
        float scale = Random.Range(minScale, maxScale);
        transform.localScale *= scale;

        // 随机移动方向
        direction = Random.onUnitSphere;

        // 稍微增加向上的趋势
        direction += Vector3.up * 0.5f;
        direction.Normalize();

        // 随机速度
        moveSpeed = Random.Range(minSpeed, maxSpeed);

        // 随机旋转
        rotationAxis = Random.onUnitSphere;
        rotateSpeed = Random.Range(minRotateSpeed, maxRotateSpeed);

        // 生命周期
        lifetime = Random.Range(minLifetime, maxLifetime);
        Destroy(gameObject, lifetime);

        // Noise
        basePosition = transform.position;

        seedX = Random.Range(0f, 100f);
        seedY = Random.Range(0f, 100f);
        seedZ = Random.Range(0f, 100f);
    }

    void Update()
    {
        float t = Time.time * noiseSpeed;

        Vector3 noise = new Vector3(
            Mathf.PerlinNoise(seedX, t) - 0.5f,
            Mathf.PerlinNoise(seedY, t) - 0.5f,
            Mathf.PerlinNoise(seedZ, t) - 0.5f
        );

        transform.position +=
            (direction + noise * noiseStrength) *
            moveSpeed *
            Time.deltaTime;

        transform.Rotate(
            rotationAxis,
            rotateSpeed * Time.deltaTime,
            Space.World
        );
    }
}