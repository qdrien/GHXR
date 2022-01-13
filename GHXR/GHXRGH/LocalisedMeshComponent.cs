using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GHXR
{
    public class LocalisedMeshComponent: GH_Component
    {
        public LocalisedMeshComponent()
          : base("GPSMesh", "LocalisedMesh",
              "Mesh with a GPS position and a heading",
              "GHXR", "Input")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {

            pManager.AddMeshParameter("Mesh", "Mesh", "Mesh to be shared.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Latitude of the Mesh", "Latitude",
                "The latitude associated with the mesh, in EPSG:4326 format.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Longitude of the Mesh", "Longitude",
                "The longitude associated with the mesh, in EPSG:4326 format.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Heading of the Mesh", "Heading",
                "The heading associated with the mesh (in degrees; 0=North, 45=East). " +
                "Determines how the client application will rotate the received mesh.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("LocalisedMesh", "LocalisedMesh", "Localised mesh", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Mesh> meshes = new List<Mesh>();
            double latitude = double.NaN;
            double longitude = double.NaN;
            double heading = double.NaN;


            if (!DA.GetDataList(0, meshes)) return;
            if (!DA.GetData(1, ref latitude)) return;
            if (!DA.GetData(2, ref longitude)) return;
            if (!DA.GetData(3, ref heading)) return;

            if (!Rhino.RhinoMath.IsValidDouble(latitude) 
                || !Rhino.RhinoMath.IsValidDouble(longitude) 
                || !Rhino.RhinoMath.IsValidDouble(heading)) { return; }

            List<LocalisedMesh> localisedMeshes = new List<LocalisedMesh>();

            foreach (Mesh mesh in meshes)
            {
                localisedMeshes.Add(new LocalisedMesh(mesh, (float)latitude, (float)longitude, (float)heading));
            }


            DA.SetDataList(0, localisedMeshes);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);

            if (this.Params.Input[1].SourceCount <= 0)
            {
                LatitudeSliderComponent latitudeSlider = new LatitudeSliderComponent();
                latitudeSlider.Attributes = new GH_NumberSliderAttributes(latitudeSlider);
                latitudeSlider.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 300, this.Attributes.Pivot.Y - 22);

                document.AddObject(latitudeSlider, true);
                this.Params.Input[1].AddSource(latitudeSlider);
            }

            if (this.Params.Input[2].SourceCount <= 0)
            {
                LongitudeSliderComponent longitudeSlider = new LongitudeSliderComponent();
                longitudeSlider.Attributes = new GH_NumberSliderAttributes(longitudeSlider);
                longitudeSlider.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 300, this.Attributes.Pivot.Y + 4);

                document.AddObject(longitudeSlider, true);
                this.Params.Input[2].AddSource(longitudeSlider);
            }

            if (this.Params.Input[3].SourceCount <= 0)
            {
                HeadingSliderComponent headingSlider = new HeadingSliderComponent();
                headingSlider.Attributes = new GH_NumberSliderAttributes(headingSlider);
                headingSlider.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 300, this.Attributes.Pivot.Y + 30);

                document.AddObject(headingSlider, true);
                this.Params.Input[3].AddSource(headingSlider);
            }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Resources.localisedmesh;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("7bae128c-a1b5-43bd-8f57-51debb98b300"); }
        }
    }
}