using Android.Hardware.Camera2;

namespace CameraApp.Listeners
{
    public class CameraCaptureSessionCallback : CameraCaptureSession.StateCallback
    {
        private readonly Camera2BasicFragment owner;

        public CameraCaptureSessionCallback(Camera2BasicFragment owner)
        {
            if (owner == null)
                throw new System.ArgumentNullException("owner");
            this.owner = owner;
        }

        public override void OnConfigureFailed(CameraCaptureSession session)
        {
            owner.ShowToast("Failed");
        }

        public override void OnConfigured(CameraCaptureSession session)
        {
            // The camera is already closed
            if (null == owner._cameraDevice)
            {
                return;
            }

            // When the session is ready, we start displaying the preview.
            owner._captureSession = session;
            try
            {
                // Auto focus should be continuous for camera preview.
                owner._previewRequestBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
                // Flash is automatically enabled when necessary.
                owner.SetAutoFlash(owner._previewRequestBuilder);

                // Finally, we start displaying the camera preview.
                owner.mPreviewRequest = owner._previewRequestBuilder.Build();
                owner._captureSession.SetRepeatingRequest(owner.mPreviewRequest,
                        owner._captureCallback, owner._backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }
    }
}