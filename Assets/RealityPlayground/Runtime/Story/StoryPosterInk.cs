using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RealityPlayground.Story
{

    public sealed class StoryPosterInk
    {
        sealed class PrintedMesh
        {
            public MeshFilter source;
            public Mesh mesh;
            public Vector3[] vertices, coordinates, normals;
        }
        sealed class PrintedLine
        {
            public LineRenderer source;
            public Vector3[] vertices, coordinates;
        }
        readonly List<PrintedMesh> meshes=new List<PrintedMesh>();
        readonly List<PrintedLine> lines=new List<PrintedLine>();
        readonly Transform paper;
        readonly int columns,rows;
        readonly Vector3 minimum,maximum;
        readonly Vector3[] normals;
        readonly float depthScale;
        public int GlyphMeshCount { get; private set; }
        public int GlyphVertexCount { get; private set; }

        public StoryPosterInk(MeshFilter surface,Transform graphics,Vector3[] rest)
        {
            paper=surface.transform;minimum=surface.sharedMesh.bounds.min;maximum=surface.sharedMesh.bounds.max;
            int width=1;while(width<rest.Length && Mathf.Abs(rest[width].y-rest[0].y)<.0001f)width++;
            columns=width-1;rows=rest.Length/width-1;normals=new Vector3[columns*rows];
            depthScale=paper.TransformVector(Vector3.forward).magnitude;

            foreach(var label in graphics.GetComponentsInChildren<TextMesh>(true))
            {
                var bridge=label.GetComponent<StoryWorldText>();
                if((!bridge || !bridge.UsesDistanceField) && TMP_Settings.defaultFontAsset)
                {
                    if(!bridge)bridge=label.gameObject.AddComponent<StoryWorldText>();
                    bridge.SetDistanceFieldFont(TMP_Settings.defaultFontAsset);
                }
                if(bridge)bridge.enabled=false;
            }
            foreach(var text in graphics.GetComponentsInChildren<TextMeshPro>(true))
            {
                bool visible=text.renderer.enabled;text.ForceMeshUpdate(true,true);Mesh generated=text.mesh;
                text.enabled=false;

                if(generated && text.textInfo.meshInfo.Length>0)
                {
                    var info=text.textInfo.meshInfo[0];int count=info.vertexCount;
                    generated.Clear();generated.SetVertices(info.vertices,0,count);
                    generated.SetNormals(info.normals,0,count);generated.SetTangents(info.tangents,0,count);
                    generated.SetUVs(0,info.uvs0,0,count);generated.SetUVs(1,info.uvs2,0,count);
                    generated.SetColors(info.colors32,0,count);generated.SetTriangles(info.triangles,0,count/4*6,0);
                    generated.RecalculateBounds();
                    SubdivideGlyphs(generated,info);
                }
                text.GetComponent<MeshFilter>().sharedMesh=generated;text.renderer.enabled=visible;
            }
            Matrix4x4 toPaper=paper.worldToLocalMatrix;
            foreach(var source in graphics.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=source.GetComponent<Renderer>();
                if(!source.sharedMesh || !renderer || !renderer.enabled)continue;
                var data=new PrintedMesh { source=source,mesh=Object.Instantiate(source.sharedMesh) };
                data.mesh.name=source.sharedMesh.name+" Ink";data.mesh.MarkDynamic();
                data.vertices=data.mesh.vertices;data.coordinates=new Vector3[data.vertices.Length];
                Matrix4x4 mapping=toPaper*source.transform.localToWorldMatrix;
                var low=new Vector3(float.PositiveInfinity,float.PositiveInfinity,0);
                var high=new Vector3(float.NegativeInfinity,float.NegativeInfinity,0);
                for(int i=0;i<data.vertices.Length;i++)
                {
                    Vector3 point=mapping.MultiplyPoint3x4(data.vertices[i]);data.coordinates[i]=point;
                    low.x=Mathf.Min(low.x,point.x);low.y=Mathf.Min(low.y,point.y);high.x=Mathf.Max(high.x,point.x);high.y=Mathf.Max(high.y,point.y);
                }
                if(source.GetComponent<TextMeshPro>())
                {
                    FitText(data.coordinates,low,high);data.normals=new Vector3[data.vertices.Length];
                    if(data.vertices.Length>0){GlyphMeshCount++;GlyphVertexCount+=data.vertices.Length;}
                }
                for(int i=0;i<data.coordinates.Length;i++)data.coordinates[i]=Coordinates(data.coordinates[i]);
                source.sharedMesh=data.mesh;meshes.Add(data);
            }
            foreach(var source in graphics.GetComponentsInChildren<LineRenderer>(true))
            {
                var data=new PrintedLine { source=source,vertices=new Vector3[source.positionCount],coordinates=new Vector3[source.positionCount] };
                source.GetPositions(data.vertices);
                for(int i=0;i<data.vertices.Length;i++)
                {
                    Vector3 world=source.useWorldSpace?data.vertices[i]:source.transform.TransformPoint(data.vertices[i]);
                    data.coordinates[i]=Coordinates(toPaper.MultiplyPoint3x4(world));
                }
                lines.Add(data);
            }
            Update(rest);
        }

        static void SubdivideGlyphs(Mesh mesh,TMP_MeshInfo info)
        {

            const int across=2,up=4,stride=across+1,perGlyph=stride*(up+1);
            int glyphs=info.vertexCount/4;
            var vertices=new Vector3[glyphs*perGlyph];var normals=new Vector3[vertices.Length];
            var tangents=new Vector4[vertices.Length];var uv0=new Vector4[vertices.Length];
            var uv1=new Vector2[vertices.Length];var colors=new Color32[vertices.Length];
            var triangles=new int[glyphs*across*up*6];int triangle=0;
            for(int glyph=0;glyph<glyphs;glyph++)
            {
                int original=glyph*4,first=glyph*perGlyph;
                for(int y=0;y<=up;y++)for(int x=0;x<=across;x++)
                {
                    float u=(float)x/across,v=(float)y/up;int index=first+y*stride+x;
                    vertices[index]=Vector3.Lerp(Vector3.Lerp(info.vertices[original],info.vertices[original+3],u),Vector3.Lerp(info.vertices[original+1],info.vertices[original+2],u),v);
                    uv0[index]=Vector4.Lerp(Vector4.Lerp(info.uvs0[original],info.uvs0[original+3],u),Vector4.Lerp(info.uvs0[original+1],info.uvs0[original+2],u),v);
                    uv1[index]=Vector2.Lerp(Vector2.Lerp(info.uvs2[original],info.uvs2[original+3],u),Vector2.Lerp(info.uvs2[original+1],info.uvs2[original+2],u),v);
                    colors[index]=Color32.Lerp(Color32.Lerp(info.colors32[original],info.colors32[original+3],u),Color32.Lerp(info.colors32[original+1],info.colors32[original+2],u),v);
                    normals[index]=info.normals[original];tangents[index]=info.tangents[original];
                    if(x==across || y==up)continue;
                    triangles[triangle++]=index;triangles[triangle++]=index+stride;triangles[triangle++]=index+stride+1;
                    triangles[triangle++]=index+stride+1;triangles[triangle++]=index+1;triangles[triangle++]=index;
                }
            }
            mesh.Clear();mesh.vertices=vertices;mesh.normals=normals;mesh.tangents=tangents;
            mesh.SetUVs(0,uv0);mesh.uv2=uv1;mesh.colors32=colors;mesh.triangles=triangles;mesh.RecalculateBounds();
        }

        void FitText(Vector3[] points,Vector3 low,Vector3 high)
        {
            float allowedWidth=(maximum.x-minimum.x)*.9f,allowedHeight=(maximum.y-minimum.y)*.32f;
            float fit=Mathf.Min(1,allowedWidth/Mathf.Max(.001f,high.x-low.x),allowedHeight/Mathf.Max(.001f,high.y-low.y));
            Vector3 center=(low+high)*.5f;Vector3 half=(high-low)*(.5f*fit);
            float marginX=(maximum.x-minimum.x)*.045f,marginY=(maximum.y-minimum.y)*.045f;
            Vector3 fitted=center;
            fitted.x=Mathf.Clamp(center.x,minimum.x+marginX+half.x,maximum.x-marginX-half.x);
            fitted.y=Mathf.Clamp(center.y,minimum.y+marginY+half.y,maximum.y-marginY-half.y);
            for(int i=0;i<points.Length;i++)
            {
                Vector3 p=points[i];p.x=fitted.x+(p.x-center.x)*fit;p.y=fitted.y+(p.y-center.y)*fit;points[i]=p;
            }
        }

        Vector3 Coordinates(Vector3 point)
        {
            return new Vector3(Mathf.Clamp(Mathf.InverseLerp(minimum.x,maximum.x,point.x),.01f,.99f),
                Mathf.Clamp(Mathf.InverseLerp(minimum.y,maximum.y,point.y),.01f,.99f),Mathf.Min(-.012f,point.z)*depthScale);
        }

        public bool TryCoordinates(MeshFilter source,out Vector3[] coordinates)
        {
            for(int i=0;i<meshes.Count;i++)if(meshes[i].source==source){coordinates=meshes[i].coordinates;return true;}
            coordinates=null;return false;
        }
        public bool TryCoordinates(LineRenderer source,out Vector3[] coordinates)
        {
            for(int i=0;i<lines.Count;i++)if(lines[i].source==source){coordinates=lines[i].coordinates;return true;}
            coordinates=null;return false;
        }

        public void Update(Vector3[] deformed)
        {
            Matrix4x4 world=paper.localToWorldMatrix;
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
            {
                int a=y*(columns+1)+x,b=a+columns+1;
                Vector3 right=world.MultiplyVector(deformed[a+1]-deformed[a]+deformed[b+1]-deformed[b]);
                Vector3 up=world.MultiplyVector(deformed[b]-deformed[a]+deformed[b+1]-deformed[a+1]);
                normals[y*columns+x]=Vector3.Cross(right,up).normalized;
            }
            for(int m=0;m<meshes.Count;m++)
            {
                var data=meshes[m];Matrix4x4 inverse=data.source.transform.worldToLocalMatrix;
                Matrix4x4 normalToLocal=data.source.transform.localToWorldMatrix.transpose;
                for(int i=0;i<data.vertices.Length;i++)
                {
                    data.vertices[i]=inverse.MultiplyPoint3x4(WorldPoint(data.coordinates[i],deformed,world));
                    if(data.normals!=null)data.normals[i]=normalToLocal.MultiplyVector(FrontNormal(data.coordinates[i])).normalized;
                }
                data.mesh.vertices=data.vertices;if(data.normals!=null)data.mesh.normals=data.normals;data.mesh.RecalculateBounds();
            }
            for(int m=0;m<lines.Count;m++)
            {
                var data=lines[m];Matrix4x4 inverse=data.source.transform.worldToLocalMatrix;
                for(int i=0;i<data.vertices.Length;i++)
                {
                    Vector3 point=WorldPoint(data.coordinates[i],deformed,world);
                    data.vertices[i]=data.source.useWorldSpace?point:inverse.MultiplyPoint3x4(point);
                }
                data.source.SetPositions(data.vertices);
            }
        }

        Vector3 WorldPoint(Vector3 uv,Vector3[] deformed,Matrix4x4 world)
        {
            float x=uv.x*columns,y=uv.y*rows;int ix=Mathf.Min(columns-1,(int)x),iy=Mathf.Min(rows-1,(int)y);
            int a=iy*(columns+1)+ix,b=a+columns+1;
            Vector3 point=Vector3.Lerp(Vector3.Lerp(deformed[a],deformed[a+1],x-ix),Vector3.Lerp(deformed[b],deformed[b+1],x-ix),y-iy);
            return world.MultiplyPoint3x4(point)+normals[iy*columns+ix]*uv.z;
        }

        Vector3 FrontNormal(Vector3 uv)
        {
            int ix=Mathf.Min(columns-1,(int)(uv.x*columns)),iy=Mathf.Min(rows-1,(int)(uv.y*rows));
            return -normals[iy*columns+ix];
        }

        public void Dispose(){for(int i=0;i<meshes.Count;i++)if(meshes[i].mesh)Object.Destroy(meshes[i].mesh);}
    }
}
