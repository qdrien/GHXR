using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GHXR
{
    class ShareableLocalisedMesh
    {
        public float Latitude;
        public float Longitude;
        public float Heading; 
        public ShareableMesh Mesh;

        public ShareableLocalisedMesh(Mesh mesh, float latitude, float longitude, float heading)
        {
            Mesh = new ShareableMesh(mesh);
            Latitude = latitude;
            Longitude = longitude;
            Heading = heading;
        }

        public ShareableLocalisedMesh(LocalisedMesh localisedMesh) : 
            this(localisedMesh.Mesh, localisedMesh.Latitude, localisedMesh.Longitude, localisedMesh.Heading) { }
    }

}
