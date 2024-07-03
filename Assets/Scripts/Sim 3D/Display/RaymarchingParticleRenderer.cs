using UnityEngine;

public class RaymarchingParticleRenderer : MonoBehaviour
{
    public ComputeShader computeShader;
    public Shader raymarchShader;
    private Material raymarchMaterial;
    private ComputeBuffer particleBuffer;

    private const int numParticles = 100;
    private Particle[] particles = new Particle[numParticles];

    struct Particle
    {
        public Vector3 pos;
        public Vector3 vel;
    }

    void Start()
    {
        // Initialize particle positions and velocities
        for (int i = 0; i < numParticles; i++)
        {
            particles[i] = new Particle
            {
                pos = new Vector3(
                    Random.Range(-5f, 5f),
                    Random.Range(-5f, 5f),
                    Random.Range(-5f, 5f)
                ),
                vel = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f)
                )
            };
        }

        particleBuffer = new ComputeBuffer(numParticles, sizeof(float) * 6);
        particleBuffer.SetData(particles);

        raymarchMaterial = new Material(raymarchShader);
        raymarchMaterial.SetBuffer("particles", particleBuffer);
    }

    void Update()
    {
        Camera camera = Camera.main;
        raymarchMaterial.SetMatrix("_CamToWorld", camera.cameraToWorldMatrix);
        raymarchMaterial.SetMatrix("_CamInvProj", camera.projectionMatrix.inverse);
        raymarchMaterial.SetPass(0);
        RenderTexture rt = RenderTexture.active;
        RenderTexture.active = DEST;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = rt;
        Graphics.Blit(SRC, DEST, raymarchMaterial);
    }
    public RenderTexture SRC;
    public RenderTexture DEST;
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Debug.Log("A");
        Graphics.Blit(src, dest, raymarchMaterial);
    }

    void OnDestroy()
    {
        if (particleBuffer != null)
        {
            particleBuffer.Release();
        }
    }
}
