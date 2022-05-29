#nullable enable

using System.Linq;
using Android.Media;
using Java.IO;
using Java.Lang;

namespace CameraApp.Listeners
{
    public class ImageAvailableListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        public ImageAvailableListener(Camera2BasicFragment fragment, File file)
        {
            _owner = fragment;
            _file = file;
        }

        public void OnImageAvailable(ImageReader? reader)
        {
            if (reader != null)
            {
                var image = reader.AcquireNextImage();
                if (image != null && _owner._backgroundHandler != null)
                {
                    _owner._backgroundHandler.Post(new ImageSaver(image, _file));
                }
            }
        }

        private readonly File _file;
        private readonly Camera2BasicFragment _owner;

        private class ImageSaver : Java.Lang.Object, IRunnable
        {
            public ImageSaver(Image image, File file)
            {
                _image = image;
                _file = file;
            }

            public void Run()
            {
                var buffer = _image.GetPlanes()?.FirstOrDefault()?.Buffer;
                if (buffer == null)
                {
                    return;
                }

                byte[] bytes = new byte[buffer.Remaining()];
                buffer.Get(bytes);
                using (var output = new FileOutputStream(_file))
                {
                    try
                    {
                        output.Write(bytes);
                    }
                    catch (IOException e)
                    {
                        e.PrintStackTrace();
                    }
                    finally
                    {
                        _image.Close();
                    }
                }
            }

            private Image _image;
            private File _file;
        }
    }
}