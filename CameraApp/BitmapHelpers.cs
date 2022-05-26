using System;
using System.IO;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.OS;
using Android.Views;
using Java.IO;
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

            if (outHeight > height || outWidth > width)
            {
                inSampleSize = outWidth > outHeight
                                   ? outHeight / height
                                   : outWidth / width;
            }

            // Now we will load the image and have BitmapFactory resize it for us.
            options.InSampleSize = inSampleSize;
            options.InJustDecodeBounds = false;
            Bitmap resizedBitmap = BitmapFactory.DecodeFile(fileName, options);

            var rotatedBitmap = RotateImage(resizedBitmap, OrientationToDegree(orientation));

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
            ContentResolver contentResolver = context.ContentResolver;
            BitmapFactory.Options imageOptions;
            Bitmap imageBitmap = null;

            // メモリ上に画像を読み込まず、画像サイズ情報のみを取得する
            try
            {
                var inputStream = contentResolver.OpenInputStream(uri);
                imageOptions = new BitmapFactory.Options();
                imageOptions.InJustDecodeBounds = true;
                BitmapFactory.DecodeStream(inputStream, null, imageOptions);
                inputStream.Close();
                // もし読み込む画像が大きかったら縮小して読み込む
                inputStream = contentResolver.OpenInputStream(uri);
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
                inputStream.Close();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.StackTrace);
            }
            return imageBitmap;
        }

        public static Orientation GetOrientation(Uri uri)
        {
            ExifInterface exifInterface;

            try
            {
                exifInterface = new ExifInterface(uri.Path);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.StackTrace);
                return default(Orientation);
            }

            return (Orientation)exifInterface.GetAttributeInt(ExifInterface.TagOrientation, -1);
        }

        public static Bitmap RotateImageIfRequired(Context context, Java.IO.File file)
        {
            var uri = Uri.FromFile(file);
            var bitmap = BitmapFactory.DecodeFile(file.Path, new BitmapFactory.Options { });

            ParcelFileDescriptor parcelFileDescriptor = context.ContentResolver.OpenFileDescriptor(uri, "r");
            var fileDescriptor = parcelFileDescriptor.FileDescriptor;

            ExifInterface ei = new ExifInterface(fileDescriptor);
            var orientation = GetOrientation(uri);

            parcelFileDescriptor.Close();

            switch (orientation)
            {
                case Orientation.Rotate90:
                    return RotateImage(bitmap, 90);
                case Orientation.Rotate180:
                    return RotateImage(bitmap, 180);
                case Orientation.Rotate270:
                    return RotateImage(bitmap, 270);
                default:
                    return bitmap;
            }
        }

        private static Bitmap RotateImage(Bitmap bitmap, int degree)
        {
            Matrix matrix = new Matrix();
            matrix.PostRotate(degree);
            Bitmap rotatedImg = Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true);
            //bitmap.Recycle();
            return rotatedImg;
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