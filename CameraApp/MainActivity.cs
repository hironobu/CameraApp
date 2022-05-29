#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Media;
using Android.OS;
using Android.Provider;
using Android.Widget;
using Java.IO;
using Java.Lang;
using Environment = Android.OS.Environment;
using Uri = Android.Net.Uri;

namespace CameraApp
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            if (IsThereAnAppToTakePictures())
            {
                CreateDirectoryForPictures();

                var button = FindViewById<Button>(Resource.Id.myButton);
                _imageView = FindViewById<ImageView>(Resource.Id.imageView1);
                _textView = FindViewById<TextView>(Resource.Id.textView1);
                if (button != null)
                {
                    button.Click += OpenCameraActivity;
                }
            }
        }

        private void CreateDirectoryForPictures()
        {
            var dir = new File(GetExternalFilesDir(Environment.DirectoryPictures), "CameraAppDemo");
            if (!dir.Exists())
            {
                dir.Mkdirs();
            }
        }

        private bool IsThereAnAppToTakePictures()
        {
            var intent = new Intent(MediaStore.ActionImageCapture);
            var availableActivities = PackageManager?.QueryIntentActivities(intent, PackageInfoFlags.MatchDefaultOnly);
            return availableActivities != null && availableActivities.Count > 0;
        }

        private void OpenCameraActivity(object sender, EventArgs eventArgs)
        {
#if false
            Context context = Application.Context;

            Intent intent = new Intent(MediaStore.ActionImageCapture);
            App._file = new File(App._dir, string.Format("myPhoto_{0}.jpg", Guid.NewGuid()));
            //intent.PutExtra(MediaStore.ExtraOutput, Uri.FromFile(App._file));
            Uri uri = FileProvider.GetUriForFile(this, $"{context.PackageName}.fileprovider", App._file);

            intent.PutExtra(MediaStore.ExtraOutput, uri);
            // intent.PutExtra(MediaStore.ExtraScreenOrientation, (int)ScreenOrientation.Portrait);

            // intent.SetType("message/rfc822");
            StartActivityForResult(intent, 0);

            Diagnostics();
#endif

            var intent = new Intent(this, typeof(CameraActivity));

            StartActivityForResult(intent, 0);
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (_imageView == null || _textView == null)
            {
                return;
            }

            var ocrtext = data?.GetStringExtra("ocrtext");
            if (ocrtext == null)
            {
                return;
            }
            var file = data?.GetStringExtra("file");
            if (file == null)
            {
                return;
            }

            /*
            // Make it available in the gallery
            var file = data?.GetStringExtra("file");
            if (file == null)
            {
                return;
            }

            var contentUri = Uri.FromFile(new File(file));
            if (contentUri == null || contentUri.Path == null)
            {
                return;
            }

            // 標準ギャラリーにスキャンさせる
            MediaScannerConnection.ScanFile( // API Level 8
                    this, // Context
                    new [] { contentUri.Path },
                    new [] { "image/jpeg" },
                    null);

            // Display in ImageView. We will resize the bitmap to fit the display.
            // Loading the full sized image will consume to much memory
            // and cause the application to crash.
            var orientation = BitmapHelpers.GetOrientation(file);

            int height = Resources?.DisplayMetrics?.HeightPixels ?? 0;
            int width = _imageView.Height;
            var bitmap = BitmapHelpers.LoadAndResizeBitmap(file, width, height, orientation);
            var cropHeight = bitmap.Height / 10; // TODO: no heuristic
            bitmap = Bitmap.CreateBitmap(bitmap, 0, (bitmap.Height - cropHeight) / 2, bitmap.Width, cropHeight);
            if (bitmap != null)
            {
                _imageView.SetImageBitmap(bitmap);

                var resizedPath = System.IO.Path.ChangeExtension(file, ".resized.jpg");
                BitmapHelpers.ExportBitmapAsJpeg(bitmap, resizedPath);

                {
                    var f = new System.IO.FileInfo(resizedPath);
                    System.Diagnostics.Debug.WriteLine($"{bitmap.Width}, {bitmap.Height}, {f.Length}");
                }

                ProcessOCR(resizedPath);
            }*/

            var orientation = BitmapHelpers.GetOrientation(file);

            int height = Resources?.DisplayMetrics?.HeightPixels ?? 0;
            int width = _imageView.Height;
            var bitmap = BitmapHelpers.LoadAndResizeBitmap(file, width, height, orientation);

            _imageView.SetImageBitmap(bitmap);
            _textView.Text = ocrtext;

            // Dispose of the Java side bitmap.
            GC.Collect();
        }

        public void Diagnostics()
        {
            var cameraManager = (CameraManager?)GetSystemService(CameraService);
            if (cameraManager == null)
            {
                return;
            }

            var selectedCameraId = cameraManager.GetCameraIdList().First();
            CameraCharacteristics characteristics = cameraManager.GetCameraCharacteristics(selectedCameraId);
            var sensorOrientation = (Integer?)characteristics.Get(CameraCharacteristics.SensorOrientation);

            System.Diagnostics.Debug.WriteLine(sensorOrientation);

            var rotation = WindowManager?.DefaultDisplay?.Rotation;

            System.Diagnostics.Debug.WriteLine(rotation);

            var orientation = Resources?.Configuration?.Orientation;

            System.Diagnostics.Debug.WriteLine(orientation);
        }

        private ImageView? _imageView;
        private TextView? _textView;
    }
}
