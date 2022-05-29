using Android.App;
using Android.Hardware.Camera2;

namespace CameraApp.Listeners
{
    public class CameraStateListener : CameraDevice.StateCallback
    {
        private readonly Camera2BasicFragment owner;

        public CameraStateListener(Camera2BasicFragment owner)
        {
            if (owner == null)
                throw new System.ArgumentNullException("owner");
            this.owner = owner;
        }

        public override void OnOpened(CameraDevice cameraDevice)
        {
            // This method is called when the camera is opened.  We start camera preview here.
            owner._cameraOpenCloseLock.Release();
            owner._cameraDevice = cameraDevice;
            owner.CreateCameraPreviewSession();
        }

        public override void OnDisconnected(CameraDevice cameraDevice)
        {
            owner._cameraOpenCloseLock.Release();
            cameraDevice.Close();
            owner._cameraDevice = null;
        }

        public override void OnError(CameraDevice cameraDevice, CameraError error)
        {
            owner._cameraOpenCloseLock.Release();
            cameraDevice.Close();
            owner._cameraDevice = null;
            if (owner == null)
                return;
            Activity activity = owner.Activity;
            if (activity != null)
            {
                activity.Finish();
            }
        }
    }
}