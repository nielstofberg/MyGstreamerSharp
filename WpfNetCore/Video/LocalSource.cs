using Gst;
using Gst.App;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace Video
{
    /// <summary>
    /// Local Video Source Player using Gstreamer Sharp
    /// Gstreamer V1.18
    /// ToDo:
    /// Implement GstDeviceMonitor
    /// </summary>
    public class LocalSource
    {
        private Gst.Pipeline _pipeline = null;
        private string _srcName = "";
        private string _decName = "";

        public string PlayState
        {
            get
            {
                if (_pipeline != null)
                {
                    _pipeline.GetState(out State s, out State p, 100);
                    if (s == State.Playing)
                    {
                        return "Playing";
                    }
                    else if (s == State.Paused)
                    {
                        return "Paused";
                    }
                }
                return "Stopped";
            }
        }

        public Device Device { get; set; } = null;

        public string DeviceName { get; set; } = "";
        public int DeviceIndex { get; set; } = -1;

        public CapInfo CapInfo { get; set; }

        public bool DeInterlace { get; set; } = false;
        public Device[] VideoDevices { get; set; } = new Device[0];

        public event EventHandler<NewFrameEventArgs> NewFrame;

        public LocalSource()
        {

        }

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        public async Task StartVideoAsync()
        {
            if (_pipeline == null)
            {
                await CrearePipelineAsync();
            }
            else
            { 
                _pipeline.GetState(out State s, out State p, 100);
                if (s == State.Null)
                {
                    await CrearePipelineAsync();
                }
                else if (s == State.Paused)
                {
                    _pipeline.SetState(State.Playing);
                }
            }
        }

        public async Task PauseVideoAsync()
        {
            if (_pipeline != null)
            {
                _pipeline.GetState(out State s, out State p, 100);
                if (s == State.Playing)
                {
                    await Task.Run (() => _pipeline.SetState(State.Paused));
                }
            }
        }

        public async Task StopVideoAsync()
        {
            if (_pipeline != null)
            {
                _pipeline.GetState(out State s, out State p, 100);
                if (s != State.Null)
                {
                    await Task.Run(() => _pipeline.SetState(State.Null));
                    while (s != State.Null)
                    {
                        _pipeline.GetState(out s, out p, 100);
                    }
                }
            }
        }

        /// <summary>
        /// Create the video pipeline from the selected local video device.
        /// </summary>
        private async Task CrearePipelineAsync()
        {
            InitApp();
            _pipeline = new Gst.Pipeline();

            // Create source element
            Gst.Element source = Gst.ElementFactory.Make("ksvideosrc");
            Gst.Element converter = Gst.ElementFactory.Make("videoconvert");
            var vs = ElementFactory.Make("appsink");// as AppSink;

            if (DeviceName.Length > 0)
            {
                source.SetProperty("device-name", new GLib.Value(DeviceName));
            }
            else if (DeviceIndex >= 0)
            {
                source.SetProperty("device-index", new GLib.Value(DeviceIndex));
            }

            _pipeline.Add(source, converter, vs);

            if (DeInterlace)
            {
                Gst.Element di = Gst.ElementFactory.Make("deinterlace");
                di.SetProperty("mode", new GLib.Value(1)); // 1=Force Deinterlacing
                _pipeline.Add(di);
                if (!Gst.Element.Link(converter, di, vs))
                {
                    _pipeline.Dispose();
                    _pipeline = null;
                    return;
                }
                di.Unref();
            }
            else
            {
                if (!Gst.Element.Link(converter, vs))
                {
                    _pipeline.Dispose();
                    _pipeline = null;
                    return;
                }
            }

            if (CapInfo.Format?.Length > 0) // && CapInfo.Width > 0 && CapInfo.Height > 0 && CapInfo.FPS > 0)
            {
                Gst.Caps myCaps = new Gst.Caps(CapInfo.InputType);
                myCaps[0].SetValue("format", new GLib.Value(CapInfo.Format));
                if (CapInfo.Width > 0)
                {
                    myCaps[0].SetValue("width", new GLib.Value(CapInfo.Width));
                }
                if (CapInfo.Height > 0)
                {
                    myCaps[0].SetValue("height", new GLib.Value(CapInfo.Height));
                }
                if (CapInfo.FPS > 0)
                {
                    myCaps[0].SetValue("framerate", new GLib.Value(new Fraction(CapInfo.FPS, 1)));
                }

                if (CapInfo.InputType.Contains("jpeg"))
                {
                    // If this is a image/jpeg source, add a decoder between the source and converter
                    Gst.Element decoder = Gst.ElementFactory.Make("jpegdec");
                    _pipeline.Add(decoder);
                    if (!source.LinkFiltered(decoder, myCaps) ||
                        !decoder.Link(converter))
                    {
                        _pipeline.Dispose();
                        _pipeline = null;
                        return;
                    }
                    decoder.Unref();
                }
                else
                {
                    if (!source.Link(converter))
                    {
                        _pipeline.Dispose();
                        _pipeline = null;
                        return;
                    }

                }
            }


            var videoSink = new AppSink(vs.Handle)
            {
                Caps = Caps.FromString($"video/x-raw,format=BGRA"), // We only accept raw RGBA samples
                Drop = true,
                MaxBuffers = 1,
                EmitSignals = true,
                Sync = true,
                Qos = true
            };
            videoSink.NewSample += VideoSink_NewSample;

            //_srcName = source.Name;
            //_decName = decoder.Name;

            source.Unref();
            converter.Unref();

            // Start playing
            await Task.Run(() => _pipeline.SetState(State.Playing));
        }

        /// <summary>
        /// Extract a frame when a new sample is available in the bus.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="args"></param>
        private void VideoSink_NewSample(object o, NewSampleArgs args)
        {
            var sink = (Gst.App.AppSink)o;

            // Retrieve the buffer
            using (Sample sample = sink.PullSample())
            {
                if (sample != null)
                {
                    using var buffer = sample.Buffer;
                    using Caps caps = sample.Caps;
                    using var cap = caps[0];

                    string format = cap.GetString("format");
                    cap.GetInt("width", out int vWidth);
                    cap.GetInt("height", out int vHheight);
                    cap.GetFraction("framerate", out int fpsNumerator, out int fpsDenominator);

                    MapInfo map;
                    if (buffer.Map(out map, MapFlags.Read))
                    {
                        NewFrame?.Invoke(this, new NewFrameEventArgs(vWidth, vHheight, map.DataPtr, (int)map.Size, (int)map.Size/vHheight));
                        buffer.Unmap(map);
                    }
                }
            }
        }

        public async Task  RefreshDevicesAsync()
        {
            InitApp();
            var devmon = new DeviceMonitor();
            devmon.AddFilter("Video/Source", new Caps("video/x-raw"));
            devmon.AddFilter("Video/Source", new Caps("image/jpeg"));
            //var bus = devmon.Bus;
            //bus.AddWatch(BusWatchEventHandler);

            bool r = await Task.Run(() => devmon.Start());
            if (!r)
            {
                return;
            }
            Console.WriteLine("Video devices count = " + devmon.Devices.Length);
            VideoDevices = devmon.Devices; //.Where(d => d.DeviceClass.Contains("Video")).ToArray();


            //var l = new GLib.MainLoop();
            //l.Run();
            devmon.Stop();
            devmon.Unref();
        }

        private bool BusWatchEventHandler(Bus bus, Message message)
        {
            return true;
        }

        private void InitApp()
        {
            if (!Gst.Application.InitCheck())
            {
                Gst.Application.Init();
                GtkSharp.GstreamerSharp.ObjectManager.Initialize();
            }
        }
    }
}

