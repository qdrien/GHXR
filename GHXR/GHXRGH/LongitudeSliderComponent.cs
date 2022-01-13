using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Rhino.Geometry;

namespace GHXR
{
    public class LongitudeSliderComponent : GH_NumberSlider
    {
        public LongitudeSliderComponent()
        {
            Name = "LongitudeSlider";
            Description = "Slider restricted by default to valid longitude input.";
            NickName = "LongitudeSlider";

            Slider.Type = Grasshopper.GUI.Base.GH_SliderAccuracy.Float;
            Slider.DecimalPlaces = 5;
            //Slider.SnapDistance = 
            Slider.Minimum = -180;
            Slider.Maximum = 180;
            Slider.Value = (decimal)5.941462;
        }


        public override Guid ComponentGuid
        {
            get
            {
                return new Guid("e9b8a0d3-8583-4265-8b96-099d376198cb");
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
                return Resources.longitude;
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            m_attributes = new GH_NumberSliderAttributes(this);
        }
    }
}