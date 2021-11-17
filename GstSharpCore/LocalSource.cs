using GLib;
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
        private string _ext = "avi";
        private Gst.Pipeline _pipeline = null;
        private string _recordBinName;
        private string _dispQueueName;
        private string _recQueueName;
        private string _teeName;

        public string PlayState
        {
            get
            {
                if (_pipeline != null)
                {
                    _pipeline.GetState(out State s, out _, 100);
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

        public RecordOptions RecordingOptions { get; set; } = RecordOptions.NoRecord;

        public bool Recording { get; private set; } = false;

        /// <summary>
        /// Name of recording file.
        /// </summary>
        public string FileName { get; set; } = "Video";

        /// <summary>
        /// Number of seconds to record per file
        /// Default is 10 minutes
        /// </summary>
        public int FileLength { get; set; } = 600;

        /// <summary>
        /// Use date in filename
        /// The filename will be FileName_yyyymmddThhmmss_[index].[ext]
        /// </summary>
        public string DateFormat { get; set; } = "";

        public Device[] VideoDevices { get; set; } = System.Array.Empty<Device>();

        /// <summary>
        /// Event Raised when new frame is received from the source
        /// </summary>
        public event EventHandler<NewFrameEventArgs> NewFrame;

        public LocalSource()
        {

        }

        /// <summary>
        /// This function copies data from one IntPtr to another. Use this function to copy
        /// the frame data received from the camera to a new area to be used in the application.<br/>
        /// Interop function that Implements RtlMoveMemory in kernel32.dll.
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="src"></param>
        /// <param name="count"></param>
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        private static extern void RtlMoveMemory(IntPtr dest, IntPtr src, uint count);

        /// <summary>
        /// This function copies data from one IntPtr to another. Use this function to copy
        /// the frame data received from the camera to a new area to be used in the application.<br/>
        /// Interop function that Implements RtlMoveMemory in kernel32.dll.
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="src"></param>
        /// <param name="count"></param>
        public static void CopyMemory(IntPtr dest, IntPtr src, uint count)
        {
            RtlMoveMemory(dest, src, count);
        }

        /// <summary>
        /// Start playing the video. If the video was paused, it will just resume, otherwise it will apply all the current settings 
        /// </summary>
        /// <returns></returns>
        public async Task StartVideoAsync()
        {
            if (_pipeline == null)
            {
                await CreatePipelineAsync();
            }
            else
            { 
                _pipeline.GetState(out State s, out _, 100);
                if (s == State.Null)
                {
                    await CreatePipelineAsync();
                }
                else if (s == State.Paused)
                {
                    _pipeline.SetState(State.Playing);
                }
            }
        }

        /// <summary>
        /// Pause the video
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Stop the video
        /// </summary>
        /// <returns></returns>
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
                    _pipeline.Dispose();
                    _pipeline = null;
                }
            }
        }

        /// <summary>
        /// Start recording. Pipeline must be in play state for this to have an effect
        /// </summary>
        public async Task<bool> StartRecordingAsync()
        {
            bool ret = false;
            if (_pipeline != null)
            {
                if (_recQueueName.Length == 0)
                {
                    await Task.Run(() => CreateRecordingBin());
                }

                _pipeline.GetState(out State s, out State p, 100);
                if (s == State.Playing)
                {
                    await Task.Run(() => _pipeline.SetState(State.Paused));
                    while (s != State.Paused)
                    {
                        _pipeline.GetState(out s, out p, 100);
                    }

                    var recQueue = _pipeline.GetByName(_recQueueName);
                    var tee = _pipeline.GetByName(_teeName);

                    if (tee.Link(recQueue))
                    {
                        Recording = true;
                        ret = true;
                    }

                    recQueue.Unref();
                    tee.Unref();

                    await Task.Run(() => _pipeline.SetState(State.Playing));
                }
            }
            return ret;
        }

        /// <summary>
        /// **** This is not working ****
        /// Stop recording. Pipeline must be in play state for this to have an effect
        /// </summary>
        public async Task StopRecordingAsync()
        {
            if (_pipeline != null && _recQueueName.Length > 0)
            {
                _pipeline.GetState(out State s, out State p, 100);
                if (s == State.Playing)
                {
                    var recBin = _pipeline.GetByName(_recordBinName);
                    var recQueue = _pipeline.GetByName(_recQueueName);
                    var tee = _pipeline.GetByName(_teeName);



                    _pipeline.SetState(State.Paused);
                    while (s != State.Paused)
                    {
                        _pipeline.GetState(out s, out _, 100);
                    }

                    Element.Unlink(new Element[] { tee, recQueue });

                    await Task.Run(() => recBin.SendEvent(Gst.Event.NewEos()));

                    Recording = false;
                    await Task.Run(() => recBin.Unref());
                    recQueue.Unref();
                    tee.Unref();

                    _pipeline.SetState(State.Playing);

                }
            }
        }

        private void Sinkpad_Unlinked(object o, UnlinkedArgs args)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Create the video pipeline from the selected local video device.
        /// </summary>
        private async Task CreatePipelineAsync()
        {
            InitApp();
            _recQueueName = string.Empty;
            _pipeline = new Gst.Pipeline();

            // Create source element
            Gst.Element source = Gst.ElementFactory.Make("ksvideosrc");
            Gst.Element converter = Gst.ElementFactory.Make("videoconvert");
            var tee = Gst.ElementFactory.Make("tee");
            var displayQueue = Gst.ElementFactory.Make("queue");

            var vs = ElementFactory.Make("appsink");// as AppSink;

            if (DeviceName.Length > 0)
            {
                source.SetProperty("device-name", new GLib.Value(DeviceName));
            }
            else if (DeviceIndex >= 0)
            {
                source.SetProperty("device-index", new GLib.Value(DeviceIndex));
            }

            _pipeline.Add(source, tee, displayQueue, converter, vs);

            #region Deinterlacing
            if (DeInterlace)
            {
                // If the video is interlaced, we must add a deinterlace element to the pipeline
                Gst.Element di = Gst.ElementFactory.Make("deinterlace");
                di.SetProperty("mode", new GLib.Value(1)); // 1=Force Deinterlacing
                _pipeline.Add(di);
                if (!Gst.Element.Link(tee, displayQueue, converter, di, vs))
                {
                    _pipeline.Dispose();
                    _pipeline = null;
                    return;
                }
                di.Unref();
            }
            else
            {
                if (!Gst.Element.Link(tee, displayQueue, converter, vs))
                {
                    _pipeline.Dispose();
                    _pipeline = null;
                    return;
                }
            }
            #endregion

            #region specify User selected CAPS
            if (CapInfo.InputType == null || CapInfo.InputType.Length == 0)
            {
                // Just Auto link if there is no CAP specified
                if (!source.Link(tee))
                {
                    _pipeline.Dispose();
                    _pipeline = null;
                    return;
                }
            }
            else if (CapInfo.InputType?.Length > 0)
            {
                // If caps are specified, 
                Gst.Caps myCaps = new(CapInfo.InputType);
                if (CapInfo.Format.Length > 0)
                {
                    myCaps[0].SetValue("format", new GLib.Value(CapInfo.Format));
                }
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
                        !decoder.Link(tee))
                    {
                        _pipeline.Dispose();
                        _pipeline = null;
                        return;
                    }

                    decoder.Unref();
                }
                else
                {
                    if (!source.LinkFiltered(tee, myCaps))
                    {
                        _pipeline.Dispose();
                        _pipeline = null;
                        return;
                    }
                }
            }
            #endregion

            // Set up appsink to emit each received frame
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

            _dispQueueName = displayQueue.Name;
            _teeName = tee.Name;

            source.Unref();
            converter.Unref();
            tee.Unref();
            displayQueue.Unref();

            // Start playing
            await Task.Run(() => _pipeline.SetState(State.Playing));
        }

        /// <summary>
        /// Create a recording bin.
        /// </summary>
        private void CreateRecordingBin()
        {
            #region Recording
            if (RecordingOptions != RecordOptions.NoRecord)
            {
                Gst.Bin recordBin = new();
                var displayQueue = _pipeline.GetByName(_dispQueueName);

                string enc = ((RecordingOptions & RecordOptions.MJPeg) == RecordOptions.MJPeg) ?
                    "jpegenc" : "x264enc";
                var queuesave = Gst.ElementFactory.Make("queue");
                var encoder = Gst.ElementFactory.Make(enc);
                string muxFactory;
                if ((RecordingOptions & RecordOptions.TSFile) == RecordOptions.TSFile)
                {
                    muxFactory = "mpegtsmux";
                    _ext = "ts";
                }
                else if ((RecordingOptions & RecordOptions.MJPeg) == RecordOptions.MJPeg)
                {
                    muxFactory = "avimux";
                    _ext = "avi";
                }
                else
                {
                    muxFactory = "mp4mux";
                    _ext = "mp4";
                }


                if ((RecordingOptions & RecordOptions.Multifile) == RecordOptions.Multifile)
                {
                    var filesink = Gst.ElementFactory.Make("splitmuxsink");
                    filesink.SetProperty("async-finalize", new GLib.Value(true));
                    filesink.SetProperty("muxer-factory", new GLib.Value(muxFactory));

                    filesink.SetProperty("location", new GLib.Value($"{FileName}_%0002d.{_ext}"));
                    filesink.SetProperty("max-size-time", new GLib.Value(FileLength * 1e9));

                    recordBin.Add(queuesave, encoder, filesink);

                    if (!Gst.Element.Link(queuesave, encoder, filesink))
                    {
                        recordBin.Dispose();
                        return;
                    }

                    filesink.Connect("format-location-full", SplitFileCreated);

                    filesink.Unref();
                }
                else
                {
                    var mux = Gst.ElementFactory.Make(muxFactory);
                    var filesink = Gst.ElementFactory.Make("filesink");
                    filesink.SetProperty("location", new GLib.Value($"video.{_ext}"));

                    recordBin.Add(queuesave, encoder, mux, filesink);
                    if (!Gst.Element.Link(queuesave, encoder, mux, filesink))
                    {
                        recordBin.Dispose();
                        return;
                    }

                    mux.Unref();
                    filesink.Unref();
                }

                // Add the Recordbin to the pipeline and link the queue to the tee.
                _pipeline.Add(recordBin);

                // Add loads of buffer space to display queue otherwise the pipeline freezes up.
                displayQueue.SetProperty("max-size-bytes", new GLib.Value(1000 * (1024 * 1024)));
                displayQueue.SetProperty("max-size-time", new GLib.Value(5000000000));

                _recQueueName = queuesave.Name;
                _recordBinName = encoder.Name;

                displayQueue.Unref();
                queuesave.Unref();
                encoder.Unref();
                recordBin.Unref();
            }
            #endregion
        }

        /// <summary>
        /// Called by the splitmuxsink whenever a new file is being created.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="args"></param>
        private void SplitFileCreated(object o, SignalArgs args)
        {
            if (Recording)
            {
                string fn = (FileName.Length > 0) ? $"{FileName}_" : string.Empty;
                fn += (DateFormat.Length > 0) ? System.DateTime.Now.ToString(DateFormat) : string.Empty;
                if (fn.Length == 0)
                {
                    fn = "Video";
                }

                fn += $".{_ext}";
                args.RetVal = fn;
            }
            else
            {
            }
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
            using Sample sample = sink.PullSample();
            if (sample != null)
            {
                using var buffer = sample.Buffer;
                using Caps caps = sample.Caps;
                using var cap = caps[0];

                string format = cap.GetString("format");
                cap.GetInt("width", out int vWidth);
                cap.GetInt("height", out int vHheight);
                cap.GetFraction("framerate", out int fpsNumerator, out int fpsDenominator);

                if (buffer.Map(out MapInfo map, MapFlags.Read))
                {
                    NewFrame?.Invoke(this, new NewFrameEventArgs(vWidth, vHheight, map.DataPtr, (int)map.Size, (int)map.Size / vHheight));
                    buffer.Unmap(map);
                }
            }
        }


        public async Task RefreshDevicesAsync()
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

        private static void InitApp()
        {
            if (!Gst.Application.InitCheck())
            {
                Gst.Application.Init();
                GtkSharp.GstreamerSharp.ObjectManager.Initialize();
            }
        }
    }
}

