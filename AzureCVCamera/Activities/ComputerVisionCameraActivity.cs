using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Java.IO;
using Java.Lang;
using Java.Util;
using Java.Util.Concurrent;
using Boolean = Java.Lang.Boolean;
using Math = Java.Lang.Math;
using Orientation = Android.Content.Res.Orientation;

namespace AzureCVCamera
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar")]
    public class ComputerVisionCameraActivity : CameraActivity
	{
		public ComputerVisionCameraActivity()
		{
		}

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            var apikey = Intent?.GetStringExtra("AzureComputerVisionApiKey");
            var endpoint = Intent?.GetStringExtra("AzureComputerVisionSubscriptionEndpoint");

            _azureComputerVisionApiKey = apikey;
            _azureComputerVisionEndpoint = endpoint;
        }

        public override void OnCaptureCompleted(string filename)
        {
            base.OnCaptureCompleted(filename);

            if (_azureComputerVisionApiKey == null || _azureComputerVisionEndpoint == null)
            {
                return;
            }

            var orientation = BitmapHelpers.GetOrientation(filename);

            // int height = Resources?.DisplayMetrics?.HeightPixels ?? 0;
            // int width = _imageView.Height;
            var bitmap = BitmapHelpers.LoadAndResizeBitmap(filename, 0, 0, orientation);
            var cropHeight = bitmap.Height / 10; // TODO: no heuristic
            bitmap = Bitmap.CreateBitmap(bitmap, 0, (bitmap.Height - cropHeight) / 2, bitmap.Width, cropHeight);
            if (bitmap != null)
            {
                var resizedPath = System.IO.Path.ChangeExtension(filename, ".resized.jpg");
                BitmapHelpers.ExportBitmapAsJpeg(bitmap, resizedPath);

                Task.Run(async () =>
                {
                    var ocrtext = await new AzureCVClient(_azureComputerVisionApiKey, _azureComputerVisionEndpoint).ProcessFileAsync(resizedPath);

                    var intent = new Intent();
                    intent.PutExtra("file", filename);
                    intent.PutExtra("ocrtext", ocrtext);
                    SetResult(0, intent);

                    this.ShowToast($"result: {ocrtext}");
                });
            }
        }

        private string? _azureComputerVisionApiKey;
        private string? _azureComputerVisionEndpoint;
    }
}

