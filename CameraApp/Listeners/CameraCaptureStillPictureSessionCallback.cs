#nullable enable

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
            // If something goes wrong with the save (or the handler isn't even 
            // registered, this code will toast a success message regardless...)
            _owner.ShowToast("Saved: " + _owner.mFile);
            Log.Debug(TAG, _owner.mFile.ToString());
            _owner.UnlockFocus();

            var intent = new Intent();
            intent.PutExtra("file", _owner.mFile.ToString());
            _owner.Activity.SetResult(0, intent);
            _owner.Finish();
        }

        private readonly Camera2BasicFragment _owner;
    }
}
