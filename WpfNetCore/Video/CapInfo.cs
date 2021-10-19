using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Video
{
    public struct CapInfo
    {
        private Gst.Structure Cap;
        public string InputType { get; private set; }
        public string Format { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int FPS { get; private set; }

        public string Description {
            get 
            {
                return $"{Format}, {Width}x{Height}, {FPS}fps";
            } 
        }

        public CapInfo(Gst.Structure cap)
        {
            Cap = cap;
            InputType = Cap.Name;

            Format = cap.GetString("format");
            var w = cap.GetValue("width");
            if (w.Val is Gst.IntRange)
            {
                Width = ((Gst.IntRange)w.Val).Max;
            }
            else
            {
                Width = (int)w.Val;
            }
            var h = cap.GetValue("height");
            if (h.Val is Gst.IntRange)
            {
                Height = ((Gst.IntRange)h.Val).Max;
            }
            else
            {
                Height = (int)h.Val;
            }
            var fps = cap.GetValue("framerate");
            if (fps.Val is Gst.FractionRange)
            {
                FPS = ((Gst.FractionRange)fps.Val).Max.Numerator;
            }
            else
            {
                FPS = ((Gst.Fraction)fps.Val).Numerator;
            }
        }

        public override string ToString()
        {
            return Cap.ToString();
        }
    }
}
