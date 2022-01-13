using Grasshopper.Kernel;
using Rhino.Geometry;

namespace GHXR
{
    class LocalisedMesh
    {
        public LocalisedMesh(Mesh mesh, float latitude, float longitude, float heading)
        {
            Mesh = mesh;
            Latitude = latitude;
            Longitude = longitude;
            Heading = heading;
        }

        public Mesh Mesh { get; }
        public float Latitude { get; }
        public float Longitude { get; }
        public float Heading { get; }
    }
}