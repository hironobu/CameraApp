#nullable enable

using System;
using Android.Content;
using Android.Hardware.Camera2;
using Android.Util;

namespace CameraApp.Listeners
{
    public class CameraCaptureStillPictureSessionCallback : CameraCaptureSession.CaptureCallback
    {
        private static readonly string TAG = "CameraCaptureStillPictureSessionCallback";

        public CameraCaptureStillPictureSessionCallback(Camera2BasicFragment owner)
        {
            _owner = owner;
        }

        public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result)
        {
            if (_owner._file == null)
            {
                throw new NotImplementedException();
            }

            _owner.ShowToast("Saved: " + _owner._file);
            Log.Debug(TAG, _owner._file.ToString());
            _owner.UnlockFocus();

            var intent = new Intent();
            intent.PutExtra("file", _owner._file.ToString());
            _owner.Activity.SetResult(0, intent);
            _owner.Finish();
        }

        private readonly Camera2BasicFragment _owner;
    }
}
