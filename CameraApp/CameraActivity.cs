﻿#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Core.Content;
using AndroidX.Fragment.App;
using CameraApp.Listeners;
using Java.IO;
using Java.Lang;
using Java.Util;
using Java.Util.Concurrent;
using Boolean = Java.Lang.Boolean;
using Fragment = AndroidX.Fragment.App.Fragment;
using FragmentCompat = AndroidX.Core.App.ActivityCompat;
using Math = Java.Lang.Math;
using Orientation = Android.Content.Res.Orientation;
using Uri = Android.Net.Uri;

namespace CameraApp
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar")]
    public class CameraActivity : FragmentActivity
    {
        protected override void OnCreate(Bundle? bundle)
        {
            base.OnCreate(bundle);
            // ActionBar.Hide();
            SetContentView(Resource.Layout.activity_camera);

            if (bundle == null)
            {
                SupportFragmentManager.BeginTransaction().Replace(Resource.Id.container, Camera2BasicFragment.NewInstance()).Commit();
            }
        }
    }

    public interface IPreviewSizeCallback
    {
        public void PreviewSizeUpdated(Size previewSize);
    }

    public class Camera2BasicFragment : Fragment, View.IOnClickListener, FragmentCompat.IOnRequestPermissionsResultCallback, IPreviewSizeCallback
    {
        private static readonly SparseIntArray ORIENTATIONS = new SparseIntArray();
        public static readonly int REQUEST_CAMERA_PERMISSION = 1;
        // private static readonly string FRAGMENT_DIALOG = "dialog";

        // Tag for the {@link Log}.
        private static readonly string TAG = "Camera2BasicFragment";

        // Camera state: Showing camera preview.
        public const int STATE_PREVIEW = 0;

        // Camera state: Waiting for the focus to be locked.
        public const int STATE_WAITING_LOCK = 1;

        // Camera state: Waiting for the exposure to be precapture state.
        public const int STATE_WAITING_PRECAPTURE = 2;

        //Camera state: Waiting for the exposure state to be something other than precapture.
        public const int STATE_WAITING_NON_PRECAPTURE = 3;

        // Camera state: Picture was taken.
        public const int STATE_PICTURE_TAKEN = 4;

        // Max preview width that is guaranteed by Camera2 API
        private static readonly int MAX_PREVIEW_WIDTH = 1920;

        // Max preview height that is guaranteed by Camera2 API
        private static readonly int MAX_PREVIEW_HEIGHT = 1080;

        // TextureView.ISurfaceTextureListener handles several lifecycle events on a TextureView
        private Camera2BasicSurfaceTextureListener? _surfaceTextureListener;

        // ID of the current {@link CameraDevice}.
        private string _cameraId = string.Empty;

        // A {@link CameraCaptureSession } for camera preview.
        public CameraCaptureSession? _captureSession;

        // A reference to the opened CameraDevice
        public CameraDevice? _cameraDevice;

        // The size of the camera preview
        private Size _previewSize = new Size(0, 0);

        // CameraDevice.StateListener is called when a CameraDevice changes its state
        private CameraStateListener? _stateCallback;

        // An additional thread for running tasks that shouldn't block the UI.
        private HandlerThread? _backgroundThread;

        // A {@link Handler} for running tasks in the background.
        public Handler? _backgroundHandler;

        // An {@link ImageReader} that handles still image capture.
        private ImageReader? _imageReader;

        // This is the output file for our picture.
        public File? _file;

        // This a callback object for the {@link ImageReader}. "onImageAvailable" will be called when a
        // still image is ready to be saved.
        private ImageAvailableListener? _onImageAvailableListener;

        //{@link CaptureRequest.Builder} for the camera preview
        public CaptureRequest.Builder? _previewRequestBuilder;

        // {@link CaptureRequest} generated by {@link #mPreviewRequestBuilder}
        public CaptureRequest? _previewRequest;

        // The current state of camera state for taking pictures.
        public int _state = STATE_PREVIEW;

        // A {@link Semaphore} to prevent the app from exiting before closing the camera.
        public Semaphore _cameraOpenCloseLock = new Semaphore(1);

        // Whether the current camera device supports Flash or not.
        private bool _flashSupported;

        // Orientation of the camera sensor
        private int mSensorOrientation;

        // A {@link CameraCaptureSession.CaptureCallback} that handles events related to JPEG capture.
        public CameraCaptureListener? _captureCallback;

        public IPreviewSizeCallback? _previewSizeCallback;

        // Shows a {@link Toast} on the UI thread.
        public void ShowToast(string text)
        {
            if (Activity != null && Activity.ApplicationContext != null)
            {
                Activity.RunOnUiThread(new ShowToastRunnable(Activity.ApplicationContext, text));
            }
        }

        private class ShowToastRunnable : Java.Lang.Object, IRunnable
        {
            public ShowToastRunnable(Context context, string text)
            {
                this._context = context;
                this._text = text;
            }

            public void Run()
            {
                Toast.MakeText(_context, _text, ToastLength.Short)?.Show();
            }

            private string _text;
            private Context _context;
        }

        private static Size ChooseOptimalSize(Size[] choices, int textureViewWidth, int textureViewHeight, int maxWidth, int maxHeight, Size aspectRatio)
        {
            // Collect the supported resolutions that are at least as big as the preview Surface
            var bigEnough = new List<Size>();
            // Collect the supported resolutions that are smaller than the preview Surface
            var notBigEnough = new List<Size>();
            int w = aspectRatio.Width;
            int h = aspectRatio.Height;

            for (var i = 0; i < choices.Length; i++)
            {
                Size option = choices[i];
                if ((option.Width <= maxWidth) && (option.Height <= maxHeight) && option.Height == option.Width * h / w)
                {
                    if (option.Width >= textureViewWidth &&
                        option.Height >= textureViewHeight)
                    {
                        bigEnough.Add(option);
                    }
                    else
                    {
                        notBigEnough.Add(option);
                    }
                }
            }

            // Pick the smallest of those big enough. If there is no one big enough, pick the
            // largest of those not big enough.
            if (bigEnough.Count > 0)
            {
                return (Size)Collections.Min(bigEnough, new ByAreaComparator())!;
            }
            else if (notBigEnough.Count > 0)
            {
                return (Size)Collections.Max(notBigEnough, new ByAreaComparator())!;
            }
            else
            {
                Log.Error(TAG, "Couldn't find any suitable preview size");
                return choices[0];
            }
        }

        public static Camera2BasicFragment NewInstance()
        {
            return new Camera2BasicFragment();
        }

        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            _stateCallback = new CameraStateListener(this);
            _surfaceTextureListener = new Camera2BasicSurfaceTextureListener(this);

            // fill ORIENTATIONS list
            ORIENTATIONS.Append((int)SurfaceOrientation.Rotation0, 90);
            ORIENTATIONS.Append((int)SurfaceOrientation.Rotation90, 0);
            ORIENTATIONS.Append((int)SurfaceOrientation.Rotation180, 270);
            ORIENTATIONS.Append((int)SurfaceOrientation.Rotation270, 180);

            _previewSizeCallback = this;
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            return inflater.Inflate(Resource.Layout.fragment_camera2_basic, container, false)!;
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            _textureView = view.FindViewById<AutoFitTextureView>(Resource.Id.texture)!;
            view.FindViewById(Resource.Id.closeCamera)?.SetOnClickListener(this);
            view.FindViewById(Resource.Id.info)?.SetOnClickListener(this);

            _previewOverlayView = Activity.FindViewById<PreviewOverlayView>(Resource.Id.previewOverlayView1)!;
            if (_previewOverlayView != null)
            {
                _previewOverlayView.SetOnClickListener(this);
                _previewOverlayView.AvailableSize = _previewSize;
            }
        }

        public override void OnActivityCreated(Bundle savedInstanceState)
        {
            base.OnActivityCreated(savedInstanceState);
            _file = new File(Activity.GetExternalFilesDir(null), $"{Guid.NewGuid()}.jpg");
            _captureCallback = new CameraCaptureListener(this);
            _onImageAvailableListener = new ImageAvailableListener(this, _file);
        }

        public override void OnResume()
        {
            base.OnResume();
            StartBackgroundThread();

            // When the screen is turned off and turned back on, the SurfaceTexture is already
            // available, and "onSurfaceTextureAvailable" will not be called. In that case, we can open
            // a camera and start preview from here (otherwise, we wait until the surface is ready in
            // the SurfaceTextureListener).
            if (_textureView.IsAvailable)
            {
                OpenCamera(_textureView.Width, _textureView.Height);
            }
            else
            {
                _textureView.SurfaceTextureListener = _surfaceTextureListener;
            }
        }

        public override void OnPause()
        {
            CloseCamera();
            StopBackgroundThread();
            base.OnPause();
        }

        private void RequestCameraPermission()
        {
            if (FragmentCompat.ShouldShowRequestPermissionRationale(Activity, Manifest.Permission.Camera))
            {
                System.Diagnostics.Debug.WriteLine("new ConfirmationDialog().Show(ChildFragmentManager, FRAGMENT_DIALOG);");
            }
            else
            {
                FragmentCompat.RequestPermissions(Activity, new[] { Manifest.Permission.Camera }, REQUEST_CAMERA_PERMISSION);
            }
        }

        public void OnRequestPermissionsResult(int requestCode, string[] permissions, int[] grantResults)
        {
            if (requestCode != REQUEST_CAMERA_PERMISSION)
                return;

            if (grantResults.Length != 1 || grantResults[0] != (int)Permission.Granted)
            {
                //ErrorDialog.NewInstance(GetString(Resource.String.request_permission))
                //        .Show(ChildFragmentManager, FRAGMENT_DIALOG);
                System.Diagnostics.Debug.WriteLine("error");
            }
        }

        public void Finish()
        {
            // Activity.SupportFragmentManager.BeginTransaction().Remove(this).Commit();

            // CloseCamera();
            // StopBackgroundThread();
            Activity.Finish();
        }

        public void ProcessOCR()
        {/*
            var orientation = BitmapHelpers.GetOrientation(originpath);

            int height = Resources?.DisplayMetrics?.HeightPixels ?? 0;
            // int width = _imageView.Height;
            var bitmap = BitmapHelpers.LoadAndResizeBitmap(originpath, 0, height, orientation);
            var cropHeight = bitmap.Height / 10; // TODO: no heuristic
            bitmap = Bitmap.CreateBitmap(bitmap, 0, (bitmap.Height - cropHeight) / 2, bitmap.Width, cropHeight);
            if (bitmap != null)
            {
                // _imageView.SetImageBitmap(bitmap);

                var resizedPath = System.IO.Path.ChangeExtension(originpath, ".resized.jpg");
                BitmapHelpers.ExportBitmapAsJpeg(bitmap, resizedPath);

                {
                    var f = new System.IO.FileInfo(resizedPath);
                    System.Diagnostics.Debug.WriteLine($"{bitmap.Width}, {bitmap.Height}, {f.Length}");
                }

                ProcessOCR(resizedPath);
            }*/
            if (_file == null)
            {
                return;
            }
            var originpath = _file.Path;

            var orientation = BitmapHelpers.GetOrientation(originpath);

            // int height = Resources?.DisplayMetrics?.HeightPixels ?? 0;
            // int width = _imageView.Height;
            var bitmap = BitmapHelpers.LoadAndResizeBitmap(originpath, 0, 0, orientation);
            var cropHeight = bitmap.Height / 10; // TODO: no heuristic
            bitmap = Bitmap.CreateBitmap(bitmap, 0, (bitmap.Height - cropHeight) / 2, bitmap.Width, cropHeight);
            if (bitmap != null)
            {
                var resizedPath = System.IO.Path.ChangeExtension(originpath, ".resized.jpg");
                BitmapHelpers.ExportBitmapAsJpeg(bitmap, resizedPath);

                {
                    var f = new System.IO.FileInfo(resizedPath);
                    System.Diagnostics.Debug.WriteLine($"{bitmap.Width}, {bitmap.Height}, {f.Length}");
                }

                Task.Run(async () =>
                {
                    var ocrtext = await new VisionClient(Constants.AzureComputerVisionApiKey, Constants.AzureComputerVisionEndpoint).ProcessFileAsync(resizedPath);

                    var intent = new Intent();
                    intent.PutExtra("file", originpath);
                    intent.PutExtra("ocrtext", ocrtext);
                    Activity.SetResult(0, intent);
                    Activity.RunOnUiThread(() => { ShowToast($"result: {ocrtext}"); });
                });
            }
        }

        // Sets up member variables related to camera.
        private void SetUpCameraOutputs(int width, int height)
        {
            var activity = Activity;
            var manager = (CameraManager)activity.GetSystemService(Context.CameraService)!;
            try
            {
                for (var i = 0; i < manager.GetCameraIdList().Length; i++)
                {
                    var cameraId = manager.GetCameraIdList()[i];
                    CameraCharacteristics characteristics = manager.GetCameraCharacteristics(cameraId);

                    // We don't use a front facing camera in this sample.
                    var facing = (Integer)characteristics.Get(CameraCharacteristics.LensFacing)!;
                    if (facing != null && facing == (Integer.ValueOf((int)LensFacing.Front)))
                    {
                        continue;
                    }

                    var map = (StreamConfigurationMap?)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
                    if (map == null)
                    {
                        continue;
                    }

                    var largest = (Size?)Collections.Max(Arrays.AsList(map.GetOutputSizes((int)ImageFormatType.Jpeg)!), new ByAreaComparator());
                    if (largest == null)
                    {
                        continue;
                    }
                    _imageReader = ImageReader.NewInstance(largest.Width, largest.Height, ImageFormatType.Jpeg, /*maxImages*/2);
                    _imageReader.SetOnImageAvailableListener(_onImageAvailableListener, _backgroundHandler);

                    // Find out if we need to swap dimension to get the preview size relative to sensor
                    // coordinate.
                    var displayRotation = activity.WindowManager?.DefaultDisplay?.Rotation;
                    //noinspection ConstantConditions
                    mSensorOrientation = (int)(characteristics?.Get(CameraCharacteristics.SensorOrientation) ?? 0);
                    bool swappedDimensions = false;
                    switch (displayRotation)
                    {
                        case SurfaceOrientation.Rotation0:
                        case SurfaceOrientation.Rotation180:
                            if (mSensorOrientation == 90 || mSensorOrientation == 270)
                            {
                                swappedDimensions = true;
                            }
                            break;
                        case SurfaceOrientation.Rotation90:
                        case SurfaceOrientation.Rotation270:
                            if (mSensorOrientation == 0 || mSensorOrientation == 180)
                            {
                                swappedDimensions = true;
                            }
                            break;
                        default:
                            Log.Error(TAG, "Display rotation is invalid: " + displayRotation);
                            break;
                    }

                    // var displaySize = new Point();
                    // activity?.WindowManager?.DefaultDisplay?.GetSize(displaySize);
                    var bounds = activity?.WindowManager?.CurrentWindowMetrics.Bounds;
                    var rotatedPreviewWidth = width;
                    var rotatedPreviewHeight = height;
                    var maxPreviewWidth = bounds?.Width() ?? default;
                    var maxPreviewHeight = bounds?.Height() ?? default;

                    if (swappedDimensions)
                    {
                        rotatedPreviewWidth = height;
                        rotatedPreviewHeight = width;
                        maxPreviewWidth = bounds?.Height() ?? default;
                        maxPreviewHeight = bounds?.Width() ?? default;
                    }

                    if (maxPreviewWidth > MAX_PREVIEW_WIDTH)
                    {
                        maxPreviewWidth = MAX_PREVIEW_WIDTH;
                    }

                    if (maxPreviewHeight > MAX_PREVIEW_HEIGHT)
                    {
                        maxPreviewHeight = MAX_PREVIEW_HEIGHT;
                    }

                    // Danger, W.R.! Attempting to use too large a preview size could  exceed the camera
                    // bus' bandwidth limitation, resulting in gorgeous previews but the storage of
                    // garbage capture data.
                    _previewSize = ChooseOptimalSize(map.GetOutputSizes(Class.FromType(typeof(SurfaceTexture)))!,
                        rotatedPreviewWidth, rotatedPreviewHeight, maxPreviewWidth,
                        maxPreviewHeight, largest);

                    // We fit the aspect ratio of TextureView to the size of preview we picked.
                    var orientation = Resources.Configuration?.Orientation;
                    if (orientation == Orientation.Landscape)
                    {
                        _previewSizeCallback?.PreviewSizeUpdated(_previewSize);

                        _textureView.SetAspectRatio(_previewSize.Width, _previewSize.Height);
                    }
                    else
                    {
                        _previewSizeCallback?.PreviewSizeUpdated(new Size(_previewSize.Height, _previewSize.Width));

                        _textureView.SetAspectRatio(_previewSize.Height, _previewSize.Width);
                    }

                    _flashSupported = ((Boolean?)characteristics?.Get(CameraCharacteristics.FlashInfoAvailable))?.BooleanValue() ?? false;
                    _cameraId = cameraId;
                    return;
                }
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
            catch (NullPointerException e)
            {
                // Currently an NPE is thrown when the Camera2API is used but not supported on the
                // device this code runs.
                // ErrorDialog.NewInstance(GetString(Resource.String.camera_error)).Show(ChildFragmentManager, FRAGMENT_DIALOG);
                e.PrintStackTrace();
            }
        }

        // Opens the camera specified by {@link Camera2BasicFragment#mCameraId}.
        public void OpenCamera(int width, int height)
        {
            if (ContextCompat.CheckSelfPermission(Activity, Manifest.Permission.Camera) != Permission.Granted)
            {
                RequestCameraPermission();
                return;
            }

            SetUpCameraOutputs(width, height);
            ConfigureTransform(width, height);
            var activity = Activity;
            var manager = (CameraManager?)activity.GetSystemService(Context.CameraService);

            if (manager == null || _stateCallback == null)
            {
                throw new NotImplementedException();
            }

            try
            {
                if (!_cameraOpenCloseLock.TryAcquire(2500, TimeUnit.Milliseconds))
                {
                    throw new RuntimeException("Time out waiting to lock camera opening.");
                }
                manager.OpenCamera(_cameraId, _stateCallback, _backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
            catch (InterruptedException e)
            {
                throw new RuntimeException("Interrupted while trying to lock camera opening.", e);
            }
        }

        // Closes the current {@link CameraDevice}.
        private void CloseCamera()
        {
            try
            {
                _cameraOpenCloseLock.Acquire();
                if (null != _captureSession)
                {
                    _captureSession.Close();
                    _captureSession = null;
                }
                if (null != _cameraDevice)
                {
                    _cameraDevice.Close();
                    _cameraDevice = null;
                }
                if (null != _imageReader)
                {
                    _imageReader.Close();
                    _imageReader = null;
                }
            }
            catch (InterruptedException e)
            {
                throw new RuntimeException("Interrupted while trying to lock camera closing.", e);
            }
            finally
            {
                _cameraOpenCloseLock.Release();
            }
        }

        // Starts a background thread and its {@link Handler}.
        private void StartBackgroundThread()
        {
            _backgroundThread = new HandlerThread("CameraBackground");
            _backgroundThread.Start();
            if (_backgroundThread != null && _backgroundThread.Looper != null)
            {
                _backgroundHandler = new Handler(_backgroundThread.Looper);
            }
        }

        // Stops the background thread and its {@link Handler}.
        private void StopBackgroundThread()
        {
            _backgroundThread?.QuitSafely();
            try
            {
                _backgroundThread?.Join();
                _backgroundThread = null;
                _backgroundHandler = null;
            }
            catch (InterruptedException e)
            {
                e.PrintStackTrace();
            }
        }

        // Creates a new {@link CameraCaptureSession} for camera preview.
        public void CreateCameraPreviewSession()
        {
            try
            {
                var texture = _textureView.SurfaceTexture;
                if (texture == null || _cameraDevice == null)
                {
                    throw new IllegalStateException("texture is null");
                }

                // We configure the size of default buffer to be the size of camera preview we want.
                texture.SetDefaultBufferSize(_previewSize.Width, _previewSize.Height);

                // This is the output Surface we need to start preview.
                Surface surface = new Surface(texture);

                // We set up a CaptureRequest.Builder with the output Surface.
                _previewRequestBuilder = _cameraDevice.CreateCaptureRequest(CameraTemplate.Preview);
                _previewRequestBuilder.AddTarget(surface);

                // Here, we create a CameraCaptureSession for camera preview.
                List<Surface> surfaces = new List<Surface>();
                surfaces.Add(surface);
                surfaces.Add(_imageReader?.Surface!);
                _cameraDevice.CreateCaptureSession(surfaces, new CameraCaptureSessionCallback(this), null);

            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

#if false
        public static T Cast<T>(Java.Lang.Object obj) where T : class
        {
            var propertyInfo = obj.GetType().GetProperty("Instance");
            return propertyInfo == null ? null : propertyInfo.GetValue(obj, null) as T;
        }
#endif

        // Configures the necessary {@link android.graphics.Matrix}
        // transformation to `mTextureView`.
        // This method should be called after the camera preview size is determined in
        // setUpCameraOutputs and also the size of `mTextureView` is fixed.

        public void ConfigureTransform(int viewWidth, int viewHeight)
        {
            Activity activity = Activity;
            if (_textureView == null || _previewSize == null || activity == null)
            {
                return;
            }
            var rotation = (int)(activity.WindowManager?.DefaultDisplay?.Rotation ?? 0);
            Matrix matrix = new Matrix();
            RectF viewRect = new RectF(0, 0, viewWidth, viewHeight);
            RectF bufferRect = new RectF(0, 0, _previewSize.Height, _previewSize.Width);
            float centerX = viewRect.CenterX();
            float centerY = viewRect.CenterY();
            if ((int)SurfaceOrientation.Rotation90 == rotation || (int)SurfaceOrientation.Rotation270 == rotation)
            {
                bufferRect.Offset(centerX - bufferRect.CenterX(), centerY - bufferRect.CenterY());
                matrix.SetRectToRect(viewRect, bufferRect, Matrix.ScaleToFit.Fill);
                float scale = Math.Max((float)viewHeight / _previewSize.Height, (float)viewWidth / _previewSize.Width);
                matrix.PostScale(scale, scale, centerX, centerY);
                matrix.PostRotate(90 * (rotation - 2), centerX, centerY);
            }
            else if ((int)SurfaceOrientation.Rotation180 == rotation)
            {
                matrix.PostRotate(180, centerX, centerY);
            }
            _textureView.SetTransform(matrix);
        }

        // Initiate a still image capture.
        private void TakePicture()
        {
            LockFocus();
        }

        private void LockFocus()
        {
            if (_previewRequestBuilder == null)
            {
                return;
            }

            try
            {
                _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger!, (int)ControlAFTrigger.Start);
                _state = STATE_WAITING_LOCK;
                _captureSession?.Capture(_previewRequestBuilder.Build(), _captureCallback, _backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        public void RunPrecaptureSequence()
        {
            if (_previewRequestBuilder == null)
            {
                return;
            }

            try
            {
                _previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger!, (int)ControlAEPrecaptureTrigger.Start);
                _state = STATE_WAITING_PRECAPTURE;
                _captureSession?.Capture(_previewRequestBuilder.Build(), _captureCallback, _backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        private CaptureRequest.Builder _stillCaptureBuilder = default!;

        // Capture a still picture. This method should be called when we get a response in
        // {@link #mCaptureCallback} from both {@link #lockFocus()}.
        public void CaptureStillPicture()
        {
            try
            {
                var activity = Activity;
                if (activity == null || _cameraDevice == null)
                {
                    return;
                }
                // This is the CaptureRequest.Builder that we use to take a picture.
                if (_stillCaptureBuilder == null)
                    _stillCaptureBuilder = _cameraDevice.CreateCaptureRequest(CameraTemplate.StillCapture);

                _stillCaptureBuilder.AddTarget(_imageReader?.Surface!);

                // Use the same AE and AF modes as the preview.
                _stillCaptureBuilder.Set(CaptureRequest.ControlAfMode!, (int)ControlAFMode.ContinuousPicture);
                SetAutoFlash(_stillCaptureBuilder);

                // Orientation
                int rotation = (int)(activity.WindowManager?.DefaultDisplay?.Rotation ?? 0);
                _stillCaptureBuilder.Set(CaptureRequest.JpegOrientation!, GetOrientation(rotation));

                _captureSession?.StopRepeating();
                _captureSession?.Capture(_stillCaptureBuilder.Build(), new CameraCaptureStillPictureSessionCallback(this), null);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        // Retrieves the JPEG orientation from the specified screen rotation.
        private int GetOrientation(int rotation)
        {
            // Sensor orientation is 90 for most devices, or 270 for some devices (eg. Nexus 5X)
            // We have to take that into account and rotate JPEG properly.
            // For devices with orientation of 90, we simply return our mapping from ORIENTATIONS.
            // For devices with orientation of 270, we need to rotate the JPEG 180 degrees.
            return (ORIENTATIONS.Get(rotation) + mSensorOrientation + 270) % 360;
        }

        // Unlock the focus. This method should be called when still image capture sequence is
        // finished.
        public void UnlockFocus()
        {
            if (_previewRequestBuilder == null || _previewRequest == null)
            {
                return;
            }

            try
            {
                // Reset the auto-focus trigger
                _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger!, (int)ControlAFTrigger.Cancel);
                SetAutoFlash(_previewRequestBuilder);
                _captureSession?.Capture(_previewRequestBuilder.Build(), _captureCallback, _backgroundHandler);
                // After this, the camera will go back to the normal state of preview.
                _state = STATE_PREVIEW;
                _captureSession?.SetRepeatingRequest(_previewRequest, _captureCallback, _backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        public void OnClick(View? v)
        {
            if (v == null)
            {
                return;
            }

            if (v.Id == Resource.Id.previewOverlayView1)
            {
                TakePicture();
            }
            else if (v.Id == Resource.Id.closeCamera)
            {
                //TakePicture();
                Activity.Finish();
            }
            else if (v.Id == Resource.Id.info)
            {

                EventHandler<DialogClickEventArgs> nullHandler = default!;
                Activity activity = Activity;
                if (activity != null)
                {
                    new AlertDialog.Builder(activity)
                        ?.SetMessage("This sample demonstrates the basic use of the Camera2 API. ...")
                        ?.SetPositiveButton(Android.Resource.String.Ok, nullHandler)
                        ?.Show();
                }
            }
        }

        public void SetAutoFlash(CaptureRequest.Builder requestBuilder)
        {
            if (_flashSupported)
            {
                requestBuilder.Set(CaptureRequest.ControlAeMode!, (int)ControlAEMode.OnAutoFlash);
            }
        }


        public void PreviewSizeUpdated(Size previewSize)
        {
            if (_previewOverlayView != null)
            {
                _previewOverlayView.AvailableSize = previewSize;
            }
        }

        private AutoFitTextureView _textureView = default!;
        private PreviewOverlayView _previewOverlayView = default!;

        public class ByAreaComparator : Java.Lang.Object, IComparator
        {
            public int Compare(Java.Lang.Object? lhs, Java.Lang.Object? rhs)
            {
                var lhsSize = (Size?)lhs ?? new Size(0, 0);
                var rhsSize = (Size?)rhs ?? new Size(0, 0);
                // We cast here to ensure the multiplications won't overflow
                return Long.Signum((long)lhsSize.Width * lhsSize.Height - (long)rhsSize.Width * rhsSize.Height);
            }
        }
    }
}