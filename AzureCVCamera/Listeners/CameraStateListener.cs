#nullable enable

using Android.App;
using Android.Hardware.Camera2;

namespace AzureCVCamera.Listeners
{
    public class CameraStateListener : CameraDevice.StateCallback
    {
        public CameraStateListener(Camera2BasicFragment owner)
        {
            _owner = owner;
        }

        public override void OnOpened(CameraDevice cameraDevice)
        {
            // This method is called when the camera is opened.  We start camera preview here.
            _owner._cameraOpenCloseLock.Release();
            _owner._cameraDevice = cameraDevice;
            _owner.CreateCameraPreviewSession();
        }

        public override void OnDisconnected(CameraDevice cameraDevice)
        {
            _owner._cameraOpenCloseLock.Release();
            cameraDevice.Close();
            _owner._cameraDevice = null;
        }

        public override void OnError(CameraDevice cameraDevice, CameraError error)
        {
            _owner._cameraOpenCloseLock.Release();
            cameraDevice.Close();
            _owner._cameraDevice = null;
            if (_owner == null)
                return;
            Activity activity = _owner.Activity;
            if (activity != null)
            {
                activity.Finish();
            }
        }

        private readonly Camera2BasicFragment _owner;
    }
}