#nullable enable

using System;
using Android.Content;
using Android.Hardware.Camera2;
using Android.Util;

namespace AzureCVCamera.Listeners
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

            _owner.ProcessOCR();
        }

        private readonly Camera2BasicFragment _owner;
    }
}
