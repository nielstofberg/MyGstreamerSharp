using System;

namespace Video
{
    public class NewFrameEventArgs : EventArgs
    {
        public int Width;
        public int Height;
        public IntPtr Buffer;
        public int Size;
        public int Stride;

        public NewFrameEventArgs(int width, int height, IntPtr buff, int size, int stride)
        {
            Width = width;
            Height = height;
            Buffer = buff;
            Size = size;
            Stride = stride;
        }
    }
}