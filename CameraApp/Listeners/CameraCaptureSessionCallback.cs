#nullable enable

using Android.Hardware.Camera2;

namespace CameraApp.Listeners
{
    public class CameraCaptureSessionCallback : CameraCaptureSession.StateCallback
    {
        public CameraCaptureSessionCallback(Camera2BasicFragment owner)
        {
            _owner = owner;
        }

        public override void OnConfigureFailed(CameraCaptureSession session)
        {
            _owner.ShowToast("Failed");
        }

        public override void OnConfigured(CameraCaptureSession session)
        {
            // The camera is already closed
            if (_owner._cameraDevice == null)
            {
                return;
            }

            // When the session is ready, we start displaying the preview.
            _owner._captureSession = session;
            try
            {
                // Auto focus should be continuous for camera preview.
                _owner._previewRequestBuilder.Set(CaptureRequest.ControlAfMode!, (int)ControlAFMode.ContinuousPicture);
                // Flash is automatically enabled when necessary.
                _owner.SetAutoFlash(_owner._previewRequestBuilder);

                // Finally, we start displaying the camera preview.
                _owner._previewRequest = _owner._previewRequestBuilder.Build();
                _owner._captureSession.SetRepeatingRequest(_owner._previewRequest, _owner._captureCallback, _owner._backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        private readonly Camera2BasicFragment _owner;
    }
}