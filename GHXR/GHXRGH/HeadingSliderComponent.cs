using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Rhino.Geometry;

namespace GHXR
{
    public class HeadingSliderComponent : GH_NumberSlider
    {
        public HeadingSliderComponent()
        {
            Name = "HeadingSlider";
            Description = "Slider restricted by default to valid heading input.";
            NickName = "HeadingSlider";

            Slider.Type = Grasshopper.GUI.Base.GH_SliderAccuracy.Integer;
            Slider.Minimum = 0;
            Slider.Maximum = 359;
        }


        public override Guid ComponentGuid
        {
            get
            {
                return new Guid("7b67f4f7-bd3b-462e-a4a1-7bafb66b9f28");
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
                return Resources.heading;
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            m_attributes = new GH_NumberSliderAttributes(this);
        }
    }
}