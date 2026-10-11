using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealityPlayground.Story
{

    public static class StoryVentGeometry
    {
        public static void Refine(Transform mount,Transform rotor,Transform guard)
        {
            var filter=rotor.GetComponent<MeshFilter>();var renderer=rotor.GetComponent<Renderer>();
            if(!filter || !renderer)return;

            var hits=Physics.RaycastAll(mount.position-mount.forward*.35f,mount.forward,2,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            float nearest=float.PositiveInfinity;
            foreach(var hit in hits)
            {
                if(hit.collider.transform.IsChildOf(mount) || Vector3.Dot(hit.normal,-mount.forward)<.85f || hit.distance>=nearest)continue;
                if(!ObjectNames.StartsWith(hit.collider.name, "Housing monolith "))continue;
                nearest=hit.distance;mount.position=hit.point+hit.normal*.115f;
                mount.rotation=Quaternion.LookRotation(-hit.normal,Vector3.up);

                float offset=Vector3.Dot(mount.position-hit.collider.bounds.center,mount.right);
                if(Mathf.Abs(offset)>.43f && Mathf.Abs(offset)<2.98f)
                {
                    float slot=Mathf.Abs(offset)<1.525f?0:Mathf.Sign(offset)*3.05f;
                    mount.position+=mount.right*(slot-offset);
                }
            }
            rotor.localPosition=Vector3.zero;rotor.localRotation=Quaternion.identity;rotor.localScale=Vector3.one;
            if(!filter.sharedMesh || filter.sharedMesh.name!="Fan Rotor")filter.sharedMesh=Rotor();

            if(guard && guard.parent!=mount)
            {
                foreach(var old in guard.GetComponentsInChildren<Renderer>(true))old.enabled=false;
                guard.name="OldFanGuard";guard=null;
            }
            if(!guard)guard=StoryVignetteGeometry.Group("Fan safety hoop",mount,Vector3.zero);
            guard.localPosition=Vector3.zero;guard.localRotation=Quaternion.identity;guard.localScale=Vector3.one;
            var line=guard.GetComponent<LineRenderer>();if(line)Object.DestroyImmediate(line);
            var guardFilter=guard.GetComponent<MeshFilter>();if(!guardFilter)guardFilter=guard.gameObject.AddComponent<MeshFilter>();
            var guardRenderer=guard.GetComponent<MeshRenderer>();if(!guardRenderer)guardRenderer=guard.gameObject.AddComponent<MeshRenderer>();
            if(!guardFilter.sharedMesh || guardFilter.sharedMesh.name!="Fan Guard")guardFilter.sharedMesh=Guard();
            guardRenderer.sharedMaterial=renderer.sharedMaterial;
            guardRenderer.shadowCastingMode=ShadowCastingMode.Off;guardRenderer.receiveShadows=true;
        }

        static Mesh Rotor()
        {
            var mesh=new Builder();
            mesh.Torus(.068f,.016f,0,24,8);
            mesh.Hub(.066f,-.029f,.012f,24);
            for(int blade=0;blade<5;blade++)
            for(int step=0;step<7;step++)
            {
                float u=step/7f,v=(step+1)/7f;
                Vector3 Point(float t,float side)
                {
                    float radius=Mathf.Lerp(.047f,.285f,t);
                    float angle=blade*Mathf.PI*.4f+.38f*t+side*Mathf.Lerp(.17f,.235f,t);
                    return new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,side*.015f*Mathf.Sin(t*Mathf.PI));
                }
                var a=Point(u,-1);var b=Point(u,1);var c=Point(v,1);var d=Point(v,-1);
                mesh.Quad(a,b,c,d);mesh.Quad(a+Vector3.forward*.007f,d+Vector3.forward*.007f,c+Vector3.forward*.007f,b+Vector3.forward*.007f);
                mesh.Quad(a,d,d+Vector3.forward*.007f,a+Vector3.forward*.007f);
                mesh.Quad(b,b+Vector3.forward*.007f,c+Vector3.forward*.007f,c);
            }
            return mesh.Finish("Fan Rotor");
        }
        static Mesh Guard()
        {
            var mesh=new Builder();
            mesh.Torus(.343f,.021f,-.044f,48,8);
            mesh.Torus(.347f,.019f,.096f,48,8);
            mesh.Torus(.227f,.006f,-.063f,40,6);
            mesh.Torus(.112f,.006f,-.068f,32,6);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f;var direction=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                mesh.Rod(direction*.039f-Vector3.forward*.075f,direction*.34f-Vector3.forward*.046f,.0055f);
                mesh.Rod(direction*.343f-Vector3.forward*.044f,direction*.347f+Vector3.forward*.11f,.009f);
            }
            return mesh.Finish("Fan Guard");
        }
        sealed class Builder
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> indices=new List<int>();
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                indices.Add(n);indices.Add(n+1);indices.Add(n+2);indices.Add(n);indices.Add(n+2);indices.Add(n+3);
            }
            public void Torus(float radius,float thickness,float z,int segments,int sides)
            {
                Vector3 Point(int ring,int side)
                {
                    float a=ring*Mathf.PI*2/segments,b=side*Mathf.PI*2/sides;
                    return new Vector3(Mathf.Cos(a)*(radius+Mathf.Cos(b)*thickness),Mathf.Sin(a)*(radius+Mathf.Cos(b)*thickness),z+Mathf.Sin(b)*thickness);
                }
                for(int i=0;i<segments;i++)for(int j=0;j<sides;j++)Quad(Point(i,j),Point(i+1,j),Point(i+1,j+1),Point(i,j+1));
            }
            public void Hub(float radius,float front,float back,int sides)
            {
                for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                    var one=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,front);
                    var two=new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,front);
                    var depth=Vector3.forward*(back-front);
                    Triangle(Vector3.forward*front,two,one);
                    Triangle(Vector3.forward*back,one+depth,two+depth);
                    Quad(one,two,two+depth,one+depth);
                }
            }
            void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(n);indices.Add(n+1);indices.Add(n+2);}
            public void Rod(Vector3 from,Vector3 to,float radius)
            {
                var rotation=Quaternion.FromToRotation(Vector3.forward,(to-from).normalized);
                for(int side=0;side<6;side++)
                {
                    float a=side*Mathf.PI/3,b=(side+1)*Mathf.PI/3;
                    Vector3 one=rotation*new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0),two=rotation*new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,0);
                    Quad(from+one,to+one,to+two,from+two);
                }
            }
            public Mesh Finish(string name)
            {
                var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
