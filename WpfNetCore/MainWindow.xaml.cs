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

namespace WpfNetCore
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const int DPI = 96; // This is just a Default DPI usrd for bitmap metadata.

        private WriteableBitmap _image1;// = new WriteableBitmap(1920, 1080, 96, 96, PixelFormats.Bgra32, BitmapPalettes.WebPalette);
        private WriteableBitmap _image2 = new WriteableBitmap(1920, 1080, 96, 96, PixelFormats.Bgra32, BitmapPalettes.WebPalette);
        private LocalSource _ls;
        private LocalSource _ls2;
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
        public WriteableBitmap ImageSource2
        {
            get { return _image2; }
            set
            {
                _image2 = value;
                OnPropertyChange("ImageSource2");
            }
        }

        public ObservableCollection<Gst.Device> Devices { get; set; }
        public ObservableCollection<CapInfo> Formats { get; set; }

        public Gst.Device SelectedDevice
        {
            get { return _device; }
            set 
            { 
                _device = value;
                Formats = new ObservableCollection<CapInfo>(GetCaps(_device));
                OnPropertyChange("Formats");
            }
        }

        public CapInfo SelectedFormat { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        public MainWindow()
        {
            //Environment.SetEnvironmentVariable("PATH", @"C:\gstreamer\1.0\x86\bin");
            Environment.SetEnvironmentVariable("PATH", @"C:\gstreamer_1_18\1.0\mingw_x86_64\bin");
            InitializeComponent();
            DataContext = this;

            _ls = new LocalSource();
            _ls.NewFrame += _ls_NewFrame;

            _ls2 = new LocalSource();
            _ls2.NewFrame += _ls_NewFrame2; ;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await _ls.RefreshDevicesAsync();
            Devices = new ObservableCollection<Gst.Device>(_ls.VideoDevices);
            OnPropertyChange("Devices");
        }

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_ls.PlayState == "Playing")
            {
                await _ls.PauseVideoAsync();
                ((Button)sender).Content = "Play";
            }
            else
            {
                _image1 = new WriteableBitmap(SelectedFormat.Width,
                    SelectedFormat.Height,
                    DPI, DPI, PixelFormats.Bgra32, null);
                _ls.DeviceName = SelectedDevice.DisplayName;
                _ls.CapInfo = SelectedFormat;
                await _ls.StartVideoAsync();
                ((Button)sender).Content = "Pause";
            }
            //_ls2.StartVideo();
        }

        private async void btnStop_Click(object sender, RoutedEventArgs e)
        {
            await _ls.StopVideoAsync();
        }

        private void _ls_NewFrame(object sender, NewFrameEventArgs e)
        {
            try
            {
                ImageSource1.Dispatcher.Invoke(() =>
                {
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
            catch(Exception)
            { }
        }

        private void _ls_NewFrame2(object sender, NewFrameEventArgs e)
        {
            try
            {
                ImageSource2.Dispatcher.Invoke(() =>
                {
                // Get a pointer to the back buffer.
                ImageSource2.Lock();
                    IntPtr pBackBuffer = ImageSource2.BackBuffer;
                    LocalSource.CopyMemory(pBackBuffer, e.Buffer, (uint)e.Size);

                // Specify the area of the bitmap that changed.
                ImageSource2.AddDirtyRect(new Int32Rect(0, 0, 1920, 1080));
                    ImageSource2.Unlock();
                    OnPropertyChange("ImageSource2");
                });
            }
            catch(Exception)
            {

            }
        }

        private CapInfo[] GetCaps(Gst.Device dev)
        {
            List<CapInfo> ret = new List<CapInfo>();
            foreach (var cap in dev.Caps)
            {
                ret.Add(new CapInfo((Gst.Structure) cap));
            }

            return ret.Where(c=> c.Format.Contains("jpeg")).ToArray();
        }

    }
}