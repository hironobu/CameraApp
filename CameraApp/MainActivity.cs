using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Media;
using Android.OS;
using Android.Provider;
using Android.Util;
using Android.Widget;
using AndroidX.Core.Content;
using AndroidX.Fragment.App;
using Java.IO;
using Java.Lang;
using Uri = Android.Net.Uri;

namespace CameraApp
{
    public static class App
    {
        public static File _file;
        public static File _dir;
        public static Bitmap bitmap;
    }

    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : Activity
    {
        private ImageView _imageView;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            if (IsThereAnAppToTakePictures())
            {
                CreateDirectoryForPictures();

                Button button = FindViewById<Button>(Resource.Id.myButton);
                _imageView = FindViewById<ImageView>(Resource.Id.imageView1);
                button.Click += TakeAPicture;
            }

        }

        private void CreateDirectoryForPictures()
        {
            App._dir = new File(
                Android.OS.Environment.GetExternalStoragePublicDirectory(
                    Android.OS.Environment.DirectoryPictures), "CameraAppDemo");
            if (!App._dir.Exists())
            {
                App._dir.Mkdirs();
            }
        }

        private bool IsThereAnAppToTakePictures()
        {
            Intent intent = new Intent(MediaStore.ActionImageCapture);
            IList<ResolveInfo> availableActivities =
                PackageManager.QueryIntentActivities(intent, PackageInfoFlags.MatchDefaultOnly);
            return availableActivities != null && availableActivities.Count > 0;
        }

        private void TakeAPicture(object sender, EventArgs eventArgs)
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

        private void ProcessOCR(string path)
        {
            VisionClient.ProcessFile(path);
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            // Make it available in the gallery
            var file = data.GetStringExtra("file");
            if (file == null)
            {
                return;
            }

            Uri contentUri = Uri.FromFile(new File(file));

            // 標準ギャラリーにスキャンさせる
            MediaScannerConnection.ScanFile( // API Level 8
                    this, // Context
                    new string[] { contentUri.Path },
                    new string[] { "image/jpeg" },
                    null);

            // Display in ImageView. We will resize the bitmap to fit the display.
            // Loading the full sized image will consume to much memory
            // and cause the application to crash.
            var orientation = BitmapHelpers.GetOrientation(contentUri);

            int height = Resources.DisplayMetrics.HeightPixels;
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
            }

            // Dispose of the Java side bitmap.
            GC.Collect();
        }

        public void Diagnostics()
        {
            CameraManager cameraManager = (CameraManager)GetSystemService(CameraService);
            var selectedCameraId = cameraManager.GetCameraIdList().First();
            CameraCharacteristics characteristics = cameraManager.GetCameraCharacteristics(selectedCameraId);
            Integer sensorOrientation = (Integer)characteristics.Get(CameraCharacteristics.SensorOrientation);

            System.Diagnostics.Debug.WriteLine(sensorOrientation);

            var rotation = WindowManager.DefaultDisplay.Rotation;

            System.Diagnostics.Debug.WriteLine(rotation);

            var orientation = Resources.Configuration.Orientation;

            System.Diagnostics.Debug.WriteLine(orientation);
        }
    }

    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar")]
    public class CameraActivity : FragmentActivity
    {
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            // ActionBar.Hide();
            SetContentView(Resource.Layout.activity_camera);

            if (bundle == null)
            {
                SupportFragmentManager.BeginTransaction().Replace(Resource.Id.container, Camera2BasicFragment.NewInstance()).Commit();
            }
        }
    }
}
