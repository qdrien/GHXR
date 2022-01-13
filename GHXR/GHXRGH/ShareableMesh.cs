using Rhino.Geometry;
using System.Collections.Generic;

namespace GHXR
{
    class ShareableMesh
    {
        public List<ShareableVertex> Vertices;
        public List<ShareableUV> Uvs;
        public List<ShareableNormal> Normals;
        public List<ShareableFace> Faces;

        public ShareableMesh(Mesh mesh)
        {
            mesh.Normals.ComputeNormals();

            Vertices = new List<ShareableVertex>();
            Uvs = new List<ShareableUV>();
            Normals = new List<ShareableNormal>();
            Faces = new List<ShareableFace>();

            foreach (Point3f vertex in mesh.Vertices)
            {
                Vertices.Add(new ShareableVertex(vertex.X, vertex.Z, vertex.Y));
            }
            foreach (Point2f uv in mesh.TextureCoordinates)
            {
                Uvs.Add(new ShareableUV(uv.X, uv.Y));
            }
            foreach (Vector3f normal in mesh.Normals)
            {
                Normals.Add(new ShareableNormal(normal.X, normal.X, normal.Y));
            }
            foreach (MeshFace face in mesh.Faces)
            {
                Faces.Add(new ShareableFace(face.IsQuad, face.A, face.B, face.C, face.D));
            }
        }

        public class ShareableVertex
        {
            public float X;
            public float Y;
            public float Z;

            public ShareableVertex(float x, float y, float z)
            {
                this.X = x;
                this.Y = y;
                this.Z = z;
            }
        }

        public class ShareableUV
        {
            public float X;
            public float Y;

            public ShareableUV(float x, float y)
            {
                this.X = x;
                this.Y = y;
            }
        }

        public class ShareableNormal
        {
            public float X;
            public float Y;
            public float Z;

            public ShareableNormal(float x, float y, float z)
            {
                this.X = x;
                this.Y = y;
                this.Z = z;
            }
        }

        public class ShareableFace
        {
            public bool IsQuad;
            public int A;
            public int B;
            public int C;
            public int D;

            public ShareableFace(bool isQuad, int a, int b, int c, int d)
            {
                this.IsQuad = isQuad;
                this.A = a;
                this.B = b;
                this.C = c;
                this.D = d;
            }
        }
    }

}
