#nullable enable

using System;
using System.IO;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Uri = Android.Net.Uri;

namespace CameraApp
{
    public static class BitmapHelpers
    {
        public static Bitmap LoadAndResizeBitmap(string fileName, int width, int height, Orientation orientation = default)
        {
            // First we get the the dimensions of the file on disk
            BitmapFactory.Options options = new BitmapFactory.Options { InJustDecodeBounds = true };
            var _ = BitmapFactory.DecodeFile(fileName, options);

            // Next we calculate the ratio that we need to resize the image by
            // in order to fit the requested dimensions.
            int outHeight = options.OutHeight;
            int outWidth = options.OutWidth;
            int inSampleSize = 1;

#if false
            if (outHeight > height || outWidth > width)
            {
                inSampleSize = outWidth > outHeight
                                   ? outHeight / height
                                   : outWidth / width;
            }
#endif

            // Now we will load the image and have BitmapFactory resize it for us.
            options.InSampleSize = inSampleSize;
            options.InJustDecodeBounds = false;
            var bitmap = BitmapFactory.DecodeFile(fileName, options);
            if (bitmap == null)
            {
                throw new NotSupportedException("BitmapFactory.DecodeFile() failed");
            }

            var resizedBitmap = ScaleBitmap(bitmap, width, height);

            var rotatedBitmap = RotateBitmap(resizedBitmap, OrientationToDegree(orientation));

            return rotatedBitmap;
        }

        /*public static Bitmap ResolveOrientation()
        {
            try
            {
                Bitmap cameraBmp = MediaStore.Images.Media.GetBitmap(
                        State.MainActivity.GetContentResolver(),
                        Uri.FromFile(Utils.TempFileForAnImage()));

                cameraBmp = ThumbnailUtils.ExtractThumbnail(cameraBmp, 320, 320);
                // NOTE incredibly useful trick for cropping/resizing square
                // http://stackoverflow.com/a/17733530/294884

                Matrix m = new Matrix();
                m.PostRotate(Utils.NeededRotation(Utils.TempFileForAnImage()));

                cameraBmp = Bitmap.CreateBitmap(cameraBmp,
                        0, 0, cameraBmp.Width, cameraBmp.Height,
                        m, true);
                return cameraBmp;
            }
            catch (Exception)
            {
                throw;
            }

            return;
        }*/

        public static Bitmap CreateBitmapFromUri(Context context, Uri uri)
        {
            var contentResolver = context.ContentResolver;
            BitmapFactory.Options imageOptions;
            Bitmap? imageBitmap;

            try
            {
                var inputStream = contentResolver?.OpenInputStream(uri);
                imageOptions = new BitmapFactory.Options();
                imageOptions.InJustDecodeBounds = true;
                BitmapFactory.DecodeStream(inputStream, null, imageOptions);
                inputStream?.Close();

                inputStream = contentResolver?.OpenInputStream(uri);
                if (imageOptions.OutWidth > 2048 && imageOptions.OutHeight > 2048)
                {
                    imageOptions = new BitmapFactory.Options();
                    imageOptions.InSampleSize = 2;
                    imageBitmap = BitmapFactory.DecodeStream(inputStream, null, imageOptions);
                }
                else
                {
                    imageBitmap = BitmapFactory.DecodeStream(inputStream, null, null);
                }
                inputStream?.Close();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.StackTrace);

                throw e;
            }

            if (imageBitmap == null)
            {
                throw new NotImplementedException();
            }

            return imageBitmap;
        }

        public static Orientation GetOrientation(Uri uri)
        {
            if (uri.Path == null)
            {
                throw new ArgumentException("uri.Path invalid");
            }

            try
            {
                var exifInterface = new ExifInterface(uri.Path);
                return (Orientation)exifInterface.GetAttributeInt(ExifInterface.TagOrientation, -1);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.StackTrace);
                return default(Orientation);
            }
        }

        public static Bitmap RotateBitmapIfRequired(Context context, Java.IO.File file)
        {
            var uri = Uri.FromFile(file);
            var bitmap = BitmapFactory.DecodeFile(file.Path, new BitmapFactory.Options { });

            if (uri == null || bitmap == null)
            {
                throw new NotSupportedException();
            }

            var parcelFileDescriptor = context.ContentResolver?.OpenFileDescriptor(uri, "r");

            var orientation = GetOrientation(uri);

            parcelFileDescriptor?.Close();

            switch (orientation)
            {
                case Orientation.Rotate90:
                    return RotateBitmap(bitmap, 90);
                case Orientation.Rotate180:
                    return RotateBitmap(bitmap, 180);
                case Orientation.Rotate270:
                    return RotateBitmap(bitmap, 270);
                default:
                    return bitmap;
            }
        }

        private static Bitmap RotateBitmap(Bitmap bitmap, int degree)
        {
            var matrix = new Matrix();
            matrix.PostRotate(degree);
            var rotatedImg = Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true);
            if (rotatedImg == null)
            {
                throw new NotImplementedException();
            }
            //bitmap.Recycle();
            return rotatedImg;
        }

        private static Bitmap ScaleBitmap(Bitmap bitmap, int width, int height)
        {
            double factor;

            if (bitmap.Width >= bitmap.Height)
            {
                factor = (double)width / bitmap.Width;
            }
            else
            {
                factor = (double)height / bitmap.Height;
            }

            var scaledBitmap = Bitmap.CreateScaledBitmap(bitmap, (int)(bitmap.Width * factor), (int)(bitmap.Height * factor), true);
            if (scaledBitmap == null)
            {
                throw new NotImplementedException();
            }

            return scaledBitmap;
        }

        private static int OrientationToDegree(Orientation orientation)
        {
            switch (orientation)
            {
                case Orientation.Rotate90:
                    return 90;
                case Orientation.Rotate180:
                    return 180;
                case Orientation.Rotate270:
                    return 270;
                default:
                    return 0;
            }
        }

        public static void ExportBitmapAsJpeg(Bitmap bitmap, string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Create);
            bitmap.Compress(Bitmap.CompressFormat.Jpeg, 70, stream);
            stream.Close();
        }
    }
}