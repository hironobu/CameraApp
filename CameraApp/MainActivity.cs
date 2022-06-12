#nullable enable

using System;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Camera2;
using Android.OS;
using Android.Provider;
using Android.Widget;
using Java.IO;
using Java.Lang;
using Environment = Android.OS.Environment;

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
            var intent = new Intent(this, typeof(AzureCVCamera.ComputerVisionCameraActivity));

            intent.PutExtra("AzureComputerVisionApiKey", "74d7f4aec4ce4b97a32bac9636d85717");
            intent.PutExtra("AzureComputerVisionSubscriptionEndpoint", "https://meuzz-cameraapp.cognitiveservices.azure.com/");

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
