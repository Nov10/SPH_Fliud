using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Master : MonoBehaviour {
    public ComputeShader raymarching;

    RenderTexture target;
    Camera cam;
    Light lightSource;
    List<ComputeBuffer> buffersToDispose;

    void Init () {
        cam = Camera.current;
        lightSource = FindObjectOfType<Light> ();
    }


    void OnRenderImage (RenderTexture source, RenderTexture destination) {
        Init ();
        buffersToDispose = new List<ComputeBuffer> ();

        InitRenderTexture ();
        CreateScene ();
        SetParameters ();

        raymarching.SetTexture (0, "Source", source);
        raymarching.SetTexture (0, "Destination", target);

        int threadGroupsX = Mathf.CeilToInt (cam.pixelWidth / 8.0f);
        int threadGroupsY = Mathf.CeilToInt (cam.pixelHeight / 8.0f);
        raymarching.Dispatch (0, threadGroupsX, threadGroupsY, 1);

        Graphics.Blit (target, destination);

        foreach (var buffer in buffersToDispose) {
            buffer.Dispose ();
        }
    }
    ComputeBuffer BUF_Pos;
    ShapeData[] shapeData;
    Vector3[] ps;
    ComputeBuffer shapeBuffer;
    public void SetData(ComputeBuffer positions)
    {
        Debug.Log("A");
        BUF_Pos = positions;
        int c = BUF_Pos.count;
        //ps = new Vector3[BUF_Pos.count];
        //BUF_Pos.GetData(ps);
        shapeData = new ShapeData[c];

        //for (int i = 0; i < c; i++)
        //{
        //    shapeData[i] = new ShapeData()
        //    {
        //        position = ps[i],
        //        scale = Vector3.one * 0.2f,
        //        colour = new Vector3(127, 255, 255) / 255f,
        //        shapeType = (int)0,
        //        operation = (int)1,
        //        blendStrength = 0.5f,
        //        numChildren = 0
        //    };
        //}
        shapeBuffer = new ComputeBuffer(shapeData.Length, ShapeData.GetSize());
        raymarching.SetBuffer(0, "shapes", shapeBuffer);
        raymarching.SetBuffer(0, "pos", BUF_Pos);
        raymarching.SetInt("numShapes", shapeData.Length);
    }
    void CreateScene () {
        //List<Shape> allShapes = new List<Shape>();
        //for(int i = 0; i < allShapes.Count; i++)
        //{
        //    allShapes.Add(new Shape(Shape.ShapeType.Sphere, Shape.Operation.Blend, Color.white, 0.5f));
        //}
        //allShapes.Sort((a, b) => a.operation.CompareTo(b.operation));

        //List<Shape> orderedShapes = new List<Shape>();

        //for (int i = 0; i < allShapes.Count; i++)
        //{
        //    // Add top-level shapes (those without a parent)
        //    if (allShapes[i].transform.parent == null)
        //    {

        //        Transform parentShape = allShapes[i].transform;
        //        orderedShapes.Add(allShapes[i]);
        //        allShapes[i].numChildren = parentShape.childCount;
        //        // Add all children of the shape (nested children not supported currently)
        //        for (int j = 0; j < parentShape.childCount; j++)
        //        {
        //            if (parentShape.GetChild(j).GetComponent<Shape>() != null)
        //            {
        //                orderedShapes.Add(parentShape.GetChild(j).GetComponent<Shape>());
        //                orderedShapes[orderedShapes.Count - 1].numChildren = 0;
        //            }
        //        }
        //    }

        //}
        //if (BUF_Pos == null)
        //    return;
        //int c = BUF_Pos.count;
        // BUF_Pos.GetData(ps);
        //for (int i = 0; i < c; i++) {
        //    shapeData[i].position = ps[i];
        //}

        //shapeBuffer.SetData (shapeData);

        //buffersToDispose.Add (shapeBuffer);
    }

    void SetParameters () {
        bool lightIsDirectional = lightSource.type == LightType.Directional;
        raymarching.SetMatrix ("_CameraToWorld", cam.cameraToWorldMatrix);
        raymarching.SetMatrix ("_CameraInverseProjection", cam.projectionMatrix.inverse);
        raymarching.SetVector ("_Light", (lightIsDirectional) ? lightSource.transform.forward : lightSource.transform.position);
        raymarching.SetBool ("positionLight", !lightIsDirectional);
    }

    void InitRenderTexture () {
        if (target == null || target.width != cam.pixelWidth || target.height != cam.pixelHeight) {
            if (target != null) {
                target.Release ();
            }
            target = new RenderTexture (cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.enableRandomWrite = true;
            target.Create ();
        }
    }

    struct ShapeData {
        public Vector3 position;
        public Vector3 scale;
        public Vector3 colour;
        public int shapeType;
        public int operation;
        public float blendStrength;
        public int numChildren;

        public static int GetSize () {
            return sizeof (float) * 10 + sizeof (int) * 3;
        }
    }
}
