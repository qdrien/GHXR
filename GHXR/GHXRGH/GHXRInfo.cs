using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace GHXR
{
    public class GHXRInfo : GH_AssemblyInfo
    {
        public override string Name
        {
            get
            {
                return "GHXR";
            }
        }
        public override Bitmap Icon
        {
            get
            {
                //Return a 24x24 pixel bitmap to represent this GHA library.
                return Resources.logo; //Made with PhotoFiltre 7, using pictograms (grasshopper, goggles) from FreePik
            }
        }
        public override string Description
        {
            get
            {
                //Return a short string describing the purpose of this GHA library.
                return "Connects Grasshopper to GHXR modules. Sends meshes and parameters; also receives value updates.";
            }
        }
        public override Guid Id
        {
            get
            {
                return new Guid("82747b56-ab61-4ca9-b164-92c8986b9364");
            }
        }

        public override string AuthorName
        {
            get
            {
                //Return a string identifying you or your company.
                return "Adrien Coppens";
            }
        }
        public override string AuthorContact
        {
            get
            {
                //Return a string representing your preferred contact details.
                return "adrien.coppens@umons.ac.be";
            }
        }
    }
}
