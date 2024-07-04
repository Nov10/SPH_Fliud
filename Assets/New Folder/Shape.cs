using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shape
{

    public enum ShapeType {Sphere,Cube,Torus};
    public enum Operation {None, Blend, Cut,Mask}

    public ShapeType shapeType;
    public Operation operation;
    public Color colour = Color.white;
    [Range(0,1)]
    public float blendStrength;
    public Vector3 pos;
    public Shape(ShapeType shapeType, Operation operation, Color colour, float blendStrength)
    {
        this.shapeType = shapeType;
        this.operation = operation;
        this.colour = colour;
        this.blendStrength = blendStrength;
    }
}
