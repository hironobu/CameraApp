#nullable enable

using Android.Views;
using Android.Graphics;

namespace CameraApp.Listeners
{
    public class Camera2BasicSurfaceTextureListener : Java.Lang.Object, TextureView.ISurfaceTextureListener
    {
        public Camera2BasicSurfaceTextureListener(Camera2BasicFragment owner)
        {
            _owner = owner;
        }

        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
        {
            _owner.OpenCamera(width, height);
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
        {
            return true;
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        {
            _owner.ConfigureTransform(width, height);
        }

        public void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {
        }

        private readonly Camera2BasicFragment _owner;
    }
}