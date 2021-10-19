//#define CAPS

using Gst;
using GLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Gst.Video;
using Gst.App;

namespace GstreamerSharp_1_18
{
    public partial class Form1 : Form
    {

        protected Gst.Pipeline mPipeline = null;
        IntPtr _windowHandle;


        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, System.EventArgs e)
        {
            _windowHandle = panel1.Handle;
            StartVideo();
        }

        void StartVideo()
        {
            if (!Gst.Application.InitCheck())
            {
                GtkSharp.GstreamerSharp.ObjectManager.Initialize();
                Gst.Application.Init();
            }

            mPipeline = new Gst.Pipeline();

            // Create source element
            Gst.Element source = Gst.ElementFactory.Make("ksvideosrc");
            source.SetProperty("device-index", new GLib.Value(7));

            //Gst.Element decoder = Gst.ElementFactory.Make("jpegdec");
            Gst.Element converter = Gst.ElementFactory.Make("videoconvert");
            //Gst.Element videoSink = Gst.ElementFactory.Make("autovideosink");
            Gst.Element videoSink = Gst.ElementFactory.Make("appsink");

            mPipeline.Add(source, converter, videoSink);

            Gst.Caps myCaps = new Gst.Caps("video/x-raw");
            //myCaps[0].SetValue("width", new GLib.Value(720));
            //myCaps[0].SetValue("height", new GLib.Value(576));
            //myCaps[0].SetValue("framerate", new GLib.Value(new Fraction(25, 1)));
            if (!source.LinkFiltered(converter, myCaps))
            {
                mPipeline.Dispose();
                mPipeline = null;
                return;
            }

            if (!Gst.Element.Link(converter, videoSink))
            {
                //Diagnostic.Print("Elements could not be linked");
                mPipeline.Dispose();
                mPipeline = null;
                return;
            }

            var vs = new AppSink(videoSink.Handle)
            {
                Caps = Caps.FromString("video/x-raw,format=RGB16"), // We only accept raw RGBA samples
                Drop = true,
                MaxBuffers = 1,
                EmitSignals = true,
                Sync = true,
                Qos = true
            };
            vs.NewSample += VideoSink_NewSample;

            source.Unref();
            converter.Unref();
            //videoSink.Unref();

            //subscribe to bussync msgs
            //Gst.Bus bus = mPipeline.Bus;
            //bus.EnableSyncMessageEmission();
            //bus.SyncMessage += new Gst.SyncMessageHandler(BusSyncHandler);

            // Start playing
            mPipeline.SetState(Gst.State.Playing);

        }

        private void VideoSink_NewSample(object sender, Gst.App.NewSampleArgs args)
        {
            var sink = (Gst.App.AppSink)sender;

            // Retrieve the buffer
            using (var sample = sink.PullSample())
            {
                if (sample != null)
                {
                    Caps caps = sample.Caps;
                    var cap = caps[0];
                    using (var buff = sample.Buffer)
                    {

                        string format;
                        int fpsNumerator = 0;
                        int fpsDenominator = 1;

                        format = cap.GetString("format");
                        cap.GetInt("width", out int lWidth);
                        cap.GetInt("height", out int lHeight);
                        cap.GetFraction("framerate", out fpsNumerator, out fpsDenominator);

                        MapInfo map;
                        if (buff.Map(out map, MapFlags.Read))
                        {
                            Image img = new Bitmap(lWidth, lHeight, (int) map.Size/lHeight, System.Drawing.Imaging.PixelFormat.Format16bppRgb565, map.DataPtr);
                            pictureBox1.Image = img;
                            // TODO: work with your RGBA frame in map.Data or map DataPtr or use map.CopyTo(IntPtr, long) to copy raw memory
                            buff.Unmap(map);
                        }
                    }
                }
            }
        }

        private void BusSyncHandler(object o, SyncMessageArgs args)
        {
            //Convenience function to check if the given message is a "prepare-window-handle" message from a GstVideoOverlay.
            if (Gst.Video.Global.IsVideoOverlayPrepareWindowHandleMessage(args.Message))
            {
                Gst.Element src = (Gst.Element)args.Message.Src;

                //Diagnostic.Print("Message'prepare-window-handle' received by: " + src.Name + " " + src.ToString());

                if (src != null)
                {
                    //    Try to set Aspect Ratio
                    try
                    {
                        src["force-aspect-ratio"] = true;
                    }
                    catch (Gst.PropertyNotFoundException)
                    { }

                    //    Try to set Overlay
                    try
                    {
                        Gst.Video.VideoOverlayAdapter overlay_ = new Gst.Video.VideoOverlayAdapter(src.Handle);
                        overlay_.WindowHandle = _windowHandle;
                        overlay_.HandleEvents(true);
                    }
                    catch (Exception ex)
                    {
                        //Diagnostic.Print("Exception thrown: " + ex.Message);
                    }
                }
            }
        }

        Gst.DeviceMonitor mon;
        private void button2_Click(object sender, System.EventArgs e)
        {
            mon = new Gst.DeviceMonitor();
            //mon.Bus.AddWatch(jimmy);
            Gst.Caps myCaps = new Gst.Caps("image/jpeg");
            mon.AddFilter("Video/Source", myCaps);
            mon.Start();
            
        }
    }
}
