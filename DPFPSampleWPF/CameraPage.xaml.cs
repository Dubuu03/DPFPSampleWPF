using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace DPFPSampleWPF
{
    public partial class CameraPage : Page
    {
        private VideoCapture capture;
        private Mat frame;
        private System.Windows.Threading.DispatcherTimer timer;
        private byte[] capturedPhoto;
        public byte[] CapturedPhoto => capturedPhoto;
        private int cameraIndex;

        public CameraPage() : this(0) { }

        public CameraPage(int cameraIndex)
        {
            InitializeComponent();
            this.cameraIndex = cameraIndex;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            try
            {
                // Attempt to open the specified camera index
                capture = new VideoCapture(cameraIndex);
                if (!capture.IsOpened())
                {
                    lblStatus.Text = $"Error: Cannot open camera (index={cameraIndex}).";
                    return;
                }

                // Allocate a Mat and set up the frame-grabbing timer
                frame = new Mat();
                timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(50) // ~20 fps
                };
                timer.Tick += Timer_Tick;
                timer.Start();

                lblStatus.Text = $"Camera index={cameraIndex} opened. Reading frames...";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "OpenCV error: " + ex.Message;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (capture == null || !capture.IsOpened())
            {
                lblStatus.Text = "Camera not opened!";
                return;
            }

            // Read a frame
            bool success = capture.Read(frame);
            if (!success || frame.Empty())
            {
                lblStatus.Text = "No frames from camera!";
                return;
            }

            // Convert Mat -> System.Drawing.Bitmap -> WPF BitmapImage
            using (var bmp = BitmapConverter.ToBitmap(frame))
            {
                picPreview.Source = BitmapToImageSource(bmp);
            }

            lblStatus.Text = "Receiving frames... Press 'Capture' to take a photo.";
        }

        private BitmapImage BitmapToImageSource(System.Drawing.Bitmap bitmap)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                // Save the bitmap as JPEG into memory
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                ms.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = ms;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                return bi;
            }
        }

        private void btnCapture_Click(object sender, RoutedEventArgs e)
        {
            if (picPreview.Source == null)
            {
                lblStatus.Text = "No image to capture!";
                return;
            }

            // Convert the current preview to byte[]
            if (picPreview.Source is BitmapImage bmpImage)
            {
                using (var ms = new MemoryStream())
                {
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bmpImage));
                    encoder.Save(ms);
                    capturedPhoto = ms.ToArray();
                }

                MessageBox.Show("Photo captured!");
                lblStatus.Text = "Photo captured. You may close or capture again.";
            }
            else
            {
                lblStatus.Text = "No valid image in preview!";
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            // We'll close the parent window as a DialogResult = true
            System.Windows.Window parent = System.Windows.Window.GetWindow(this);
            if (parent != null)
            {
                parent.DialogResult = true;
                parent.Close();
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            // Cleanup logic here, since we're not overriding OnUnloaded
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }
            if (capture != null)
            {
                capture.Release();
                capture.Dispose();
                capture = null;
            }
        }
    }
}
