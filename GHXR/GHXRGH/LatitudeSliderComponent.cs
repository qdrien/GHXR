using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Rhino.Geometry;

namespace GHXR
{
    public class LatitudeSliderComponent : GH_NumberSlider
    {
        public LatitudeSliderComponent()
        {
            Name = "LatitudeSlider";
            Description = "Slider restricted by default to valid latitude input.";
            NickName = "LatitudeSlider";

            Slider.Type = Grasshopper.GUI.Base.GH_SliderAccuracy.Float;
            Slider.DecimalPlaces = 5;
            //Slider.SnapDistance = 
            Slider.Minimum = -85; //-180 -> 180
            Slider.Maximum = 85;

            Slider.Value = (decimal) 49.502249;
        }


        public override Guid ComponentGuid
        {
            get
            {
                return new Guid("25a00ec1-dd45-423e-9430-a7d58f20a116");
            }
        }



        public override string Category
        {
            get
            {
                return "GHXR";
            }
        }
        public override string SubCategory
        {

            get
            {
                return "Input";
            }
        }


        protected override Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Resources.latitude;
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            m_attributes = new GH_NumberSliderAttributes(this);
        }

        /*public override string Name { get => base.Name; set => base.Name = value; }
        public override string Description { get => base.Description; set => base.Description = value; }

        public override string TypeName => base.TypeName;

        public override Guid ComponentGuid => base.ComponentGuid;

        protected override Bitmap Internal_Icon_24x24 => base.Internal_Icon_24x24;

        protected override Bitmap Icon => base.Icon;

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return base.ToString();
        }

        protected override void ValuesChanged()
        {
            base.ValuesChanged();
        }*/
    }
}