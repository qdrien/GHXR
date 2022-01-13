using System.Collections.Generic;
using System.Drawing;

namespace GHXR
{
    abstract class ShareableParameter
    {
        public string Type;
        public string Name;
        public string NickName;
        public string Guid;

        public class ShareableToggle : ShareableParameter
        {
            public bool Value;
        }

        public class ShareableSlider : ShareableParameter
        {
            public float Value;
            public int Accuracy;
            public float Min;
            public float Max;
            public float Epsilon;
            public int DecimalPlaces;
        }

        public class ShareableList : ShareableParameter
        {
            public List<ShareableListItem> Values;
            public int ListMode; //0=checklist; 1=dropdown; 2=sequence; 3=cyclic sequence
        }

        public class ShareableListItem
        {
            public string Expression;
            public bool Selected; //true for the selected item (or for all checked items if mode=0)
            public string Name;
        }

        /*public class ShareableColour : ShareableParameter
        {
            public Color Value;
        }*/

        public class ShareableKnob : ShareableParameter
        {
            public float Value;
            public int Decimals;
            public float Range;
            public bool LimitKnobValue;
            public float Min;
            public float Max;
        }

    }
}
