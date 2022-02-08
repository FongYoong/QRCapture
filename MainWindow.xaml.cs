using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NHotkey;
using NHotkey.Wpf;
using BarcodeReader = ZXing.Presentation.BarcodeReader;
using ToastNotifications;
using ToastNotifications.Lifetime;
using ToastNotifications.Position;
using ToastNotifications.Messages;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using System.Windows.Threading;
using System.ComponentModel;

namespace QRCapture
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Notifier notifier;
        private readonly BarcodeReader reader = new BarcodeReader();
        private bool selectInitialPoint = false;
        private Point initialPoint = new Point();
        private bool selectFinalPoint = false;
        private Point finalPoint = new Point();

        public MainWindow()
        {
            InitializeComponent();
            HotkeyManager.Current.AddOrReplace("CaptureScreen", Key.PrintScreen, ModifierKeys.Control, CaptureScreen);
            //HotkeyManager.Current.AddOrReplace("Decrement", Key.Subtract, ModifierKeys.Control | ModifierKeys.Alt, OnDecrement);

            notifier = new Notifier(cfg =>
            {
                cfg.PositionProvider = new WindowPositionProvider(
                    parentWindow: Application.Current.MainWindow,
                    corner: Corner.BottomRight,
                    offsetX: 10,
                    offsetY: 10);

                cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(
                    notificationLifetime: TimeSpan.FromSeconds(2),
                    maximumNotificationCount: MaximumNotificationCount.FromCount(3));

                cfg.Dispatcher = Application.Current.Dispatcher;
            });
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr onj);

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
            base.OnStateChanged(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            base.OnClosing(e);
        }

        private Size GetDpiSafeResolution()
        {
            PresentationSource _presentationSource = PresentationSource.FromVisual(Application.Current.MainWindow);
            Matrix matix = _presentationSource.CompositionTarget.TransformToDevice;
            return new Size(
                SystemParameters.PrimaryScreenWidth * matix.M22,
                SystemParameters.PrimaryScreenHeight * matix.M11
            );
        }

        private void CaptureScreen(object sender, HotkeyEventArgs e = null)
        {
            imageBorder.Visibility = Visibility.Visible;
            Bitmap bitmap;
            Size size = GetDpiSafeResolution();
            bitmap = new Bitmap((int)size.Width, (int)size.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
            }
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = bitmap.GetHbitmap();
                BitmapSource capturedBitmap = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                imageOutput.Source = capturedBitmap;
                selectInitialPoint = true;
                selectFinalPoint = false;
                imageOutput.Cursor = Cursors.Pen;
                instructionText.Text = "→ Click and drag to select region.";
                //SystemCommands.RestoreWindow(this);
                Show();
                WindowState = WindowState.Normal;
                //Activate();
                //Focus();
            }
            catch (Exception)
            {

            }
            finally
            {
                DeleteObject(handle);
            }
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            CaptureScreen(sender);
        }

        private void ImageOutput_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (selectInitialPoint)
            {
                double x = e.GetPosition(imageOutput).X;
                double y = e.GetPosition(imageOutput).Y;
                initialPoint = new Point(x, y);
                recSelection.Margin = new Thickness(initialPoint.X, initialPoint.Y, 0, 0);
                selectInitialPoint = false;
                selectFinalPoint = true;
                instructionText.Text = "→ Release to scan QR code.";
            }
        }

        private void ImageOutput_MouseMove(object sender, MouseEventArgs e)
        {
            if (selectFinalPoint)
            {

                finalPoint = new Point(e.GetPosition(imageOutput).X, e.GetPosition(imageOutput).Y);
                recSelection.Visibility = Visibility.Visible;
                recSelection.Margin = new Thickness(Math.Min(initialPoint.X, finalPoint.X), Math.Min(initialPoint.Y, finalPoint.Y), 0, 0);
                recSelection.Width = Math.Abs(finalPoint.X - initialPoint.X);
                recSelection.Height = Math.Abs(finalPoint.Y - initialPoint.Y);
            }
        }

        private void ImageOutput_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (selectFinalPoint)
            {
                int scaledX = (int)(Math.Min(initialPoint.X, finalPoint.X) / imageOutput.ActualWidth * imageOutput.Source.Width);
                int scaledY = (int)(Math.Min(initialPoint.Y, finalPoint.Y) / imageOutput.ActualHeight * imageOutput.Source.Height);
                int scaledWidth = (int)(Math.Abs(finalPoint.X - initialPoint.X) / imageOutput.ActualWidth * imageOutput.Source.Width);
                int scaledHeight = (int)(Math.Abs(finalPoint.Y - initialPoint.Y) / imageOutput.ActualHeight * imageOutput.Source.Height);
                CroppedBitmap croppedBitmap = new CroppedBitmap((BitmapSource)imageOutput.Source, new Int32Rect(scaledX, scaledY, scaledWidth, scaledHeight));
                imageOutput.Source = croppedBitmap;
                //using (FileStream fileStream = new FileStream("capture.jpg", FileMode.Create))
                //{
                //    BitmapEncoder encoder = new PngBitmapEncoder();
                //    encoder.Frames.Add(BitmapFrame.Create((BitmapSource) imageOutput.Source));
                //    encoder.Save(fileStream);
                //}
                ScanQR(croppedBitmap);
                imageOutput.Cursor = Cursors.Arrow;
                selectFinalPoint = false;
                instructionText.Text = "→ Click Capture button to begin.";
                ClearRectangles();
            }
        }

        private void ScanQR(BitmapSource bitmapSource)
        {
            ZXing.Result result = reader.Decode(bitmapSource);
            if (result != null)
            {
                TextOutput.Text = result.Text;
                notifier.ShowSuccess("QR code found:\n" + result.Text);
            }
            else
            {
                //TextOutput.Text = "No QR code found.";
                notifier.ShowError("No QR code found.");
            }
        }
        private void ClearRectangles()
        {
            recSelection.Visibility = Visibility.Collapsed;
            recSelection.Width = 0;
            recSelection.Height = 0;
        }

        private void ClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(TextOutput.Text);
            notifier.ShowInformation("Copied to clipboard:\n" + TextOutput.Text);
        }

        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        //private Bitmap BitmapFromSource(BitmapSource bitmapsource)
        //{
        //    System.Drawing.Bitmap bitmap;
        //    using (MemoryStream outStream = new MemoryStream())
        //    {
        //        BitmapEncoder enc = new BmpBitmapEncoder();
        //        enc.Frames.Add(BitmapFrame.Create(bitmapsource));
        //        enc.Save(outStream);
        //        bitmap = new System.Drawing.Bitmap(outStream);
        //    }
        //    return bitmap;
        //}

    }
}
