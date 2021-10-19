using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Video;

namespace WpfNetCore.Controls
{
    /// <summary>
    /// Interaction logic for VideoPlayer.xaml
    /// </summary>
    public partial class VideoPlayer : UserControl, INotifyPropertyChanged
    {
        private const int DPI = 96; // This is just a Default DPI usrd for bitmap metadata.

        private WriteableBitmap _image1 = new WriteableBitmap(1920, 1080, 96, 96, PixelFormats.Bgra32, BitmapPalettes.WebPalette);
        private string _lblPlay = "Play";
        private bool _deInterlace = true;

        private LocalSource _ls;
        private Gst.Device _device;

        public WriteableBitmap ImageSource1
        {
            get { return _image1; }
            set
            {
                _image1 = value;
                OnPropertyChange("ImageSource1");
            }
        }
 
        public ObservableCollection<Gst.Device> Devices { get; set; }
        public ObservableCollection<CapInfo> Formats { get; set; }

        public string LabelPlay
        {
            get { return _lblPlay; }
            set
            {
                _lblPlay = value;
                OnPropertyChange("LabelPlay");
            }
        }

        public bool DeInterlace
        {
            get { return _deInterlace; }
            set
            {
                _deInterlace = value;
                OnPropertyChange("DeInterlace");
            }
        }


        public Gst.Device SelectedDevice
        {
            get { return _device; }
            set
            {
                _device = value;
                Formats = new ObservableCollection<CapInfo>(GetCaps(_device));
                OnPropertyChange("Formats");
                SelectedFormat = Formats[0];
                OnPropertyChange("SelectedFormat");
            }
        }

        public CapInfo SelectedFormat { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public VideoPlayer()
        {
            InitializeComponent();
            DataContext = this;

            _ls = new LocalSource();
            _ls.NewFrame += _ls_NewFrame;
        }

        private async void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _ls.RefreshDevicesAsync();
                Devices = new ObservableCollection<Gst.Device>(_ls.VideoDevices);
                OnPropertyChange("Devices");
                SelectedDevice = Devices[0];
                OnPropertyChange("SelectedDevice");
            }
            catch
            { }
        }

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_ls.PlayState == "Playing")
            {
                await _ls.PauseVideoAsync();
                LabelPlay = "Play";
            }
            else
            {
                if (SelectedFormat.Width > 0 && SelectedFormat.Height > 0)
                {
                    _image1 = new WriteableBitmap(SelectedFormat.Width,
                        SelectedFormat.Height,
                        DPI, DPI, PixelFormats.Bgra32, null);
                }
                _ls.DeInterlace = DeInterlace;
                _ls.Device = SelectedDevice;
                //_ls.DeviceName = SelectedDevice.DisplayName;
                _ls.DeviceIndex = Devices.IndexOf(SelectedDevice);
                _ls.CapInfo = SelectedFormat;
                await _ls.StartVideoAsync();
                LabelPlay = "Pause";
            }
            //_ls2.StartVideo();
        }

        private async void btnStop_Click(object sender, RoutedEventArgs e)
        {
            await _ls.StopVideoAsync();
            LabelPlay = "Play";
        }

        private void _ls_NewFrame(object sender, NewFrameEventArgs e)
        {
            try
            {

                ImageSource1.Dispatcher.Invoke(() =>
                {
                    if (_image1 == null || _image1.Width != e.Width || _image1.Height != e.Height)
                    {
                        _image1 = new WriteableBitmap(e.Width, e.Height, DPI, DPI, PixelFormats.Bgra32, null);
                    }
                    // Get a pointer to the back buffer.
                    ImageSource1.Lock();
                    IntPtr pBackBuffer = ImageSource1.BackBuffer;
                    LocalSource.CopyMemory(pBackBuffer, e.Buffer, (uint)e.Size);

                    // Specify the area of the bitmap that changed.
                    ImageSource1.AddDirtyRect(new Int32Rect(0, 0, (int)_image1.Width, (int)_image1.Height));
                    ImageSource1.Unlock();
                    OnPropertyChange("ImageSource1");
                });
            }
            catch (Exception)
            { }
        }

        private CapInfo[] GetCaps(Gst.Device dev)
        {
            List<CapInfo> ret = new List<CapInfo>();
            foreach (var cap in dev.Caps)
            {
                ret.Add(new CapInfo((Gst.Structure)cap));
            }

            return ret.ToArray();//.Where(c => c.Format.Contains("jpeg")).ToArray();
        }

    }
}
