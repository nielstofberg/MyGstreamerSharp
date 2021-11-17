using System;

namespace Video
{
    /// <summary>
    /// Inherist  EventArgs and contains information necessary to build a new WriteableBitmap.<br />
    /// Use PixelFormats.Bgra32 and DPI 96<br />
    /// eg. new WriteableBitmap(1920, 1080, 96, 96, PixelFormats.Bgra32, BitmapPalettes.WebPalette);<br/>
    /// Image1Source.Lock();
    /// IntPtr pBackBuffer = Image1Source.BackBuffer;
    /// LocalSource.CopyMemory(pBackBuffer, e.Buffer, (uint) e.Size); 
    /// Image1Source.AddDirtyRect(new Int32Rect(0, 0, (int) _image1.Width, (int) _image1.Height));
    ///                    Image1Source.Unlock();

    /// 
    /// </summary>
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