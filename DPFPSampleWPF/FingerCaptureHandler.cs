using System;
using System.Drawing;
using System.IO;
using System.Windows.Controls;
using DPFP;
using DPFP.Capture;
using DPFP.Processing;
using System.Windows.Media.Imaging;
using Image = System.Windows.Controls.Image;

namespace DPFPSampleWPF
{
    internal class FingerCaptureHandler : DPFP.Capture.EventHandler
    {
        private Enrollment _enroller;
        private Image _imgPreview; // WPF image
        private Action<byte[]> _onEnrollmentFinished;
        private Action<bool> _onFinishLabelCallback;

        public FingerCaptureHandler(
            Enrollment enroller,
            Image imgPreview,
            Action<byte[]> onFinished,
            Action<bool> onFinishLabelCallback = null)
        {
            _enroller = enroller;
            _imgPreview = imgPreview;
            _onEnrollmentFinished = onFinished;
            _onFinishLabelCallback = onFinishLabelCallback;
        }

        public void OnComplete(object Capture, string ReaderSerialNumber, Sample Sample)
        {
            Bitmap bmp = ConvertSampleToBitmap(Sample);
            if (bmp != null)
            {
                // Show in WPF Image
                _imgPreview.Dispatcher.Invoke(() =>
                {
                    _imgPreview.Source = BitmapToImageSource(bmp);
                });
            }

            FeatureSet features = ExtractFeatures(Sample, DataPurpose.Enrollment);
            if (features != null)
            {
                try
                {
                    _enroller.AddFeatures(features);
                }
                catch { }
                finally
                {
                    if (_enroller.TemplateStatus == Enrollment.Status.Ready)
                    {
                        if (Capture is DPFP.Capture.Capture c) c.StopCapture();
                        Template template = _enroller.Template;
                        using (var ms = new MemoryStream())
                        {
                            template.Serialize(ms);
                            _onEnrollmentFinished(ms.ToArray());
                        }
                        _onFinishLabelCallback?.Invoke(true);
                    }
                    else if (_enroller.TemplateStatus == Enrollment.Status.Failed)
                    {
                        _enroller.Clear();
                        if (Capture is DPFP.Capture.Capture c) c.StopCapture();
                        _onFinishLabelCallback?.Invoke(false);
                    }
                }
            }
        }

        public void OnFingerGone(object Capture, string ReaderSerialNumber) { }
        public void OnFingerTouch(object Capture, string ReaderSerialNumber) { }
        public void OnReaderConnect(object Capture, string ReaderSerialNumber) { }
        public void OnReaderDisconnect(object Capture, string ReaderSerialNumber) { }
        public void OnSampleQuality(object Capture, string ReaderSerialNumber, CaptureFeedback CaptureFeedback) { }

        private FeatureSet ExtractFeatures(Sample sample, DataPurpose purpose)
        {
            FeatureExtraction extractor = new FeatureExtraction();
            CaptureFeedback feedback = CaptureFeedback.None;
            FeatureSet features = new FeatureSet();
            extractor.CreateFeatureSet(sample, purpose, ref feedback, ref features);
            return (feedback == CaptureFeedback.Good) ? features : null;
        }

        private Bitmap ConvertSampleToBitmap(Sample sample)
        {
            SampleConversion convertor = new SampleConversion();
            Bitmap bitmap = null;
            convertor.ConvertToPicture(sample, ref bitmap);
            return bitmap;
        }

        private BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = ms;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                return bi;
            }
        }
    }
}
