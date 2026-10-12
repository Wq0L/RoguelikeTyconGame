using System.Collections.Generic;
using UnityEngine;

// Shared original low-poly mesh authoring; editor only.
public sealed class LateGameMeshBuilder
    {
        readonly List<Vector3> vertices=new();readonly List<int> indices=new();readonly List<Color> colors=new();
        void Tri(Vector3 a,Vector3 b,Vector3 c,Color color)
        {int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(n);indices.Add(n+1);indices.Add(n+2);colors.Add(color.linear);colors.Add(color.linear);colors.Add(color.linear);}
        public void Ribbon(Vector3 a,Vector3 b,float width,Color color)
        {
            Vector3 side=Vector3.Cross((a+b).normalized,(b-a).normalized).normalized*width;
            Tri(a-side,a+side,b+side,color);Tri(a-side,b+side,b-side,color);
            Tri(a+side,a-side,b-side,color);Tri(a+side,b-side,b+side,color);
        }
        public void Tube(Vector3[] points,float[] radii,Color color,int sides=6)
        {
            var rings=new Vector3[points.Length,sides];
            for(int j=0;j<points.Length;j++)
            {
                Vector3 axis=(points[Mathf.Min(j+1,points.Length-1)]-points[Mathf.Max(j-1,0)]).normalized;
                Vector3 u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.95f?Vector3.forward:Vector3.up).normalized;
                Vector3 v=Vector3.Cross(axis,u);
                for(int k=0;k<sides;k++){float a=k*Mathf.PI*2/sides;rings[j,k]=points[j]+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*radii[j];}
            }
            for(int j=0;j<points.Length-1;j++)for(int k=0;k<sides;k++)
            {
                int next=(k+1)%sides;Color face=color*(k%3==0?.91f:1f);face.a=1;
                Tri(rings[j,k],rings[j,next],rings[j+1,next],face);Tri(rings[j,k],rings[j+1,next],rings[j+1,k],face);
            }
            for(int k=0;k<sides;k++){Tri(points[0],rings[0,(k+1)%sides],rings[0,k],color);int last=points.Length-1;Tri(points[last],rings[last,k],rings[last,(k+1)%sides],color);}
        }
        public void Leaf(float length, float width, Color face, Color underside)
        {
            Vector3 root=Vector3.zero, left=new Vector3(-width, .10f, length*.46f),
                tip=new Vector3(0,.15f,length), right=new Vector3(width,.10f,length*.46f),
                ridge=new Vector3(0,.23f,length*.42f), back=new Vector3(0,.03f,length*.42f);
            Vector3[] edge={root,left,tip,right};
            for(int i=0;i<4;i++){Tri(edge[i],edge[(i+1)%4],ridge,face);Tri(edge[i],back,edge[(i+1)%4],underside);}
        }
        public void Prism(Vector2[] polygon,float depth,Color color)
        {
            Vector3 V(int i,float z)=>new Vector3(polygon[i].x,polygon[i].y,z);
            for(int i=1;i<polygon.Length-1;i++)
            {Tri(V(0,-depth),V(i+1,-depth),V(i,-depth),color);Tri(V(0,depth),V(i,depth),V(i+1,depth),color);}
            for(int i=0;i<polygon.Length;i++)
            {int n=(i+1)%polygon.Length;Tri(V(i,-depth),V(n,-depth),V(n,depth),color);Tri(V(i,-depth),V(n,depth),V(i,depth),color);}
        }
        public void Append(LateGameMeshBuilder source, Matrix4x4 matrix)
        {
            int offset=vertices.Count;
            foreach(var v in source.vertices)vertices.Add(matrix.MultiplyPoint3x4(v));
            colors.AddRange(source.colors);
            foreach(int i in source.indices)indices.Add(offset+i);
        }
        public Mesh Mesh(string name){var m=new Mesh{name=name};m.SetVertices(vertices);m.SetTriangles(indices,0);m.SetColors(colors);m.RecalculateNormals();m.RecalculateBounds();return m;}
    }

