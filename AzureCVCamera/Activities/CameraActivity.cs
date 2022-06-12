using System;
using System.Collections.Generic;
using System.Linq;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Media;
using Android.OS;
using Android.Util;
using Android.Views;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Java.IO;
using Java.Lang;
using Java.Util;
using Java.Util.Concurrent;
using Meuzz.Android.Utils.Camera;
using Math = Java.Lang.Math;
using Orientation = Android.Content.Res.Orientation;

namespace AzureCVCamera
{
    public interface IPreviewSizeCallback
    {
        public void PreviewSizeUpdated(Size previewSize);
    }

    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar")]
    public class CameraActivity : Activity, View.IOnClickListener, ActivityCompat.IOnRequestPermissionsResultCallback, IPreviewSizeCallback
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            // ActionBar.Hide();
            SetContentView(Resource.Layout.activity_camera);

            _stateCallback = new CameraStateListener(this);
            _surfaceTextureListener = new Camera2BasicSurfaceTextureListener(this);

            // fill ORIENTATIONS list
            _orientationAdjustmentTable.Append((int)SurfaceOrientation.Rotation0, 90);
            _orientationAdjustmentTable.Append((int)SurfaceOrientation.Rotation90, 0);
            _orientationAdjustmentTable.Append((int)SurfaceOrientation.Rotation180, 270);
            _orientationAdjustmentTable.Append((int)SurfaceOrientation.Rotation270, 180);

            _previewSizeCallback = this;

            var file = new File(GetExternalFilesDir(null), $"{Guid.NewGuid()}.jpg");
            _captureCallback = new CameraCaptureListener(this, file);
            _onImageAvailableListener = new ImageAvailableListener(this, file);

            _textureView = FindViewById<AutoFitTextureView>(Resource.Id.texture)!;
            FindViewById(Resource.Id.closeCamera)?.SetOnClickListener(this);
            FindViewById(Resource.Id.info)?.SetOnClickListener(this);

            _previewOverlayView = FindViewById<PreviewOverlayView>(Resource.Id.previewOverlayView1)!;
            if (_previewOverlayView != null)
            {
                _previewOverlayView.SetOnClickListener(this);
                _previewOverlayView.AvailableSize = _previewSize;
            }
        }

        protected override void OnResume()
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

        protected override void OnPause()
        {
            CloseCamera();
            StopBackgroundThread();
            base.OnPause();
        }

        private void RequestCameraPermission()
        {
            if (ActivityCompat.ShouldShowRequestPermissionRationale(this, Manifest.Permission.Camera))
            {
                Log.Debug(TAG, "new ConfirmationDialog().Show(ChildFragmentManager, FRAGMENT_DIALOG);");
            }
            else
            {
                ActivityCompat.RequestPermissions(this, new[] { Manifest.Permission.Camera }, REQUEST_CAMERA_PERMISSION);
            }
        }

        public void OnRequestPermissionsResult(int requestCode, string[] permissions, int[] grantResults)
        {
            if (requestCode != REQUEST_CAMERA_PERMISSION)
            {
                return;
            }

            if (grantResults.Length != 1 || grantResults[0] != (int)Permission.Granted)
            {
                //ErrorDialog.NewInstance(GetString(Resource.String.request_permission))
                //        .Show(ChildFragmentManager, FRAGMENT_DIALOG);
                Log.Debug(TAG, "error");
            }
        }

        private void SetUpCaptureSession(CameraCaptureSession session)
        {
            // The camera is already closed
            if (_cameraDevice == null || _previewRequestBuilder == null)
            {
                return;
            }

            // When the session is ready, we start displaying the preview.
            _captureSession = session;
            try
            {
                // Auto focus should be continuous for camera preview.
                _previewRequestBuilder.Set(CaptureRequest.ControlAfMode!, (int)ControlAFMode.ContinuousPicture);
                // Flash is automatically enabled when necessary.
                SetAutoFlash(_previewRequestBuilder);

                // Finally, we start displaying the camera preview.
                _previewRequest = _previewRequestBuilder.Build();
                _captureSession.SetRepeatingRequest(_previewRequest, _captureCallback, _backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        // Sets up member variables related to camera.
        private void SetUpCameraOutputs(int width, int height)
        {
            var manager = (CameraManager?)GetSystemService(CameraService);
            if (manager == null)
            {
                throw new NotImplementedException("CameraService is null");
            }
            try
            {
                var cameraIdList = manager.GetCameraIdList();
                if (cameraIdList == null)
                {
                    throw new NotImplementedException("CameraManager.GetCameraIdList() is null");
                }

                foreach (var cameraId in cameraIdList)
                {
                    var cameraDeviceSpec = CameraDeviceSpec.LoadSpec(manager, cameraId);

                    if (cameraDeviceSpec.LensFacing == LensFacing.Front)
                    {
                        continue;
                    }

                    _imageReader = ImageReader.NewInstance(cameraDeviceSpec.LargestSize.Width, cameraDeviceSpec.LargestSize.Height, ImageFormatType.Jpeg, /*maxImages*/2);
                    _imageReader?.SetOnImageAvailableListener(_onImageAvailableListener, _backgroundHandler);

#if true
                    var displaySize = new Point();
                    WindowManager?.DefaultDisplay?.GetSize(displaySize);
                    var bounds = new Rect(0, 0, displaySize.X, displaySize.Y);
#else
                    var bounds = activity?.WindowManager?.CurrentWindowMetrics.Bounds;
#endif

                    // Find out if we need to swap dimension to get the preview size relative to sensor
                    // coordinate.
                    var displayRotation = WindowManager?.DefaultDisplay?.Rotation ?? default;

                    // Danger, W.R.! Attempting to use too large a preview size could  exceed the camera
                    // bus' bandwidth limitation, resulting in gorgeous previews but the storage of
                    // garbage capture data.
                    _previewSize = cameraDeviceSpec.ChooseOptimalSize(displayRotation, bounds, width, height, Camera2Const.MAX_PREVIEW_WIDTH, Camera2Const.MAX_PREVIEW_HEIGHT);

                    // We fit the aspect ratio of TextureView to the size of preview we picked.
                    var orientation = Resources?.Configuration?.Orientation;
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

                    _cameraDeviceSpec = cameraDeviceSpec;
                    break;
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
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.Camera) != Permission.Granted)
            {
                RequestCameraPermission();
                return;
            }

            SetUpCameraOutputs(width, height);
            ConfigureTransform(width, height);
            var manager = (CameraManager?)GetSystemService(CameraService);

            if (manager == null || _stateCallback == null || _cameraDeviceSpec == null)
            {
                throw new NotImplementedException();
            }

            try
            {
                if (!_cameraOpenCloseLock.TryAcquire(2500, TimeUnit.Milliseconds))
                {
                    throw new RuntimeException("Time out waiting to lock camera opening.");
                }
                manager.OpenCamera(_cameraDeviceSpec.CameraId, _stateCallback, _backgroundHandler);
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
                _previewRequestBuilder?.AddTarget(surface);

                // Here, we create a CameraCaptureSession for camera preview.
                var surfaces = new List<Surface>();
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
            if (_textureView == null || _previewSize == null)
            {
                return;
            }
            var rotation = (int)(WindowManager?.DefaultDisplay?.Rotation ?? 0);
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
            _previewOverlayView.Color = Color.Red;
            _previewOverlayView.Invalidate();

            _previewOverlayView.Bitmap = _textureView.Bitmap;

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
                _state = Camera2Const.STATE_WAITING_LOCK;
                _captureSession?.Capture(_previewRequestBuilder.Build(), _captureCallback, _backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        public void RunPrecaptureSequence()
        {
            if (_previewRequestBuilder == null || _captureSession == null)
            {
                return;
            }

            try
            {
                _previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger!, (int)ControlAEPrecaptureTrigger.Start);
                _state = Camera2Const.STATE_WAITING_PRECAPTURE;
                _captureSession.Capture(_previewRequestBuilder.Build(), _captureCallback, _backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        private CaptureRequest.Builder _stillCaptureBuilder = default!;

        // Capture a still picture. This method should be called when we get a response in
        // {@link #mCaptureCallback} from both {@link #lockFocus()}.
        public void CaptureStillPicture(File file)
        {
            try
            {
                if (_cameraDevice == null || _captureSession == null)
                {
                    return;
                }
                // This is the CaptureRequest.Builder that we use to take a picture.
                if (_stillCaptureBuilder == null)
                {
                    var stillCaptureBuilder = _cameraDevice.CreateCaptureRequest(CameraTemplate.StillCapture);
                    if (stillCaptureBuilder == null)
                    {
                        throw new NotImplementedException("CreateCaptureRequest() failed");
                    }
                    _stillCaptureBuilder = stillCaptureBuilder;
                }

                _stillCaptureBuilder.AddTarget(_imageReader?.Surface!);

                // Use the same AE and AF modes as the preview.
                _stillCaptureBuilder.Set(CaptureRequest.ControlAfMode!, (int)ControlAFMode.ContinuousPicture);
                SetAutoFlash(_stillCaptureBuilder);

                // Orientation
                int rotation = (int)(WindowManager?.DefaultDisplay?.Rotation ?? 0);
                _stillCaptureBuilder.Set(CaptureRequest.JpegOrientation!, GetOrientation(rotation));

                _captureSession.StopRepeating();
                _captureSession.Capture(_stillCaptureBuilder.Build(), new CameraCaptureStillPictureSessionCallback(this, file), null);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        // Retrieves the JPEG orientation from the specified screen rotation.
        private int GetOrientation(int rotation)
        {
            if (_cameraDeviceSpec == null)
            {
                throw new NotImplementedException("_cameraDeviceSpec is null");
            }

            // Sensor orientation is 90 for most devices, or 270 for some devices (eg. Nexus 5X)
            // We have to take that into account and rotate JPEG properly.
            // For devices with orientation of 90, we simply return our mapping from ORIENTATIONS.
            // For devices with orientation of 270, we need to rotate the JPEG 180 degrees.
            return (_orientationAdjustmentTable.Get(rotation) + _cameraDeviceSpec.SensorOrientation + 270) % 360;
        }

        // Unlock the focus. This method should be called when still image capture sequence is
        // finished.
        public void UnlockFocus()
        {
            if (_previewRequestBuilder == null || _previewRequest == null || _captureSession == null)
            {
                return;
            }

            try
            {
                // Reset the auto-focus trigger
                _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger!, (int)ControlAFTrigger.Cancel);
                SetAutoFlash(_previewRequestBuilder);
                _captureSession.Capture(_previewRequestBuilder.Build(), _captureCallback, _backgroundHandler);
                // After this, the camera will go back to the normal state of preview.
                _state = Camera2Const.STATE_PREVIEW;
                _captureSession.SetRepeatingRequest(_previewRequest, _captureCallback, _backgroundHandler);
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
                this.Finish();
            }
            else if (v.Id == Resource.Id.info)
            {

                EventHandler<DialogClickEventArgs> nullHandler = default!;

                new AlertDialog.Builder(this)
                    ?.SetMessage("This sample demonstrates the basic use of the Camera2 API. ...")
                    ?.SetPositiveButton(Android.Resource.String.Ok, nullHandler)
                    ?.Show();
            }
        }

        public virtual void OnCaptureCompleted(string filename)
        {
            _previewOverlayView.Bitmap = null;
            _previewOverlayView.Color = Color.White;
            _previewOverlayView.Invalidate();
        }

        private void SetAutoFlash(CaptureRequest.Builder requestBuilder)
        {
            if (_cameraDeviceSpec != null && _cameraDeviceSpec.FlashSupported)
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

        private static readonly SparseIntArray _orientationAdjustmentTable = new SparseIntArray();
        public static readonly int REQUEST_CAMERA_PERMISSION = 1;
        // private static readonly string FRAGMENT_DIALOG = "dialog";

        // Tag for the {@link Log}.
        private static readonly string TAG = "CameraActivity";

        // TextureView.ISurfaceTextureListener handles several lifecycle events on a TextureView
        private Camera2BasicSurfaceTextureListener? _surfaceTextureListener;

        // A {@link CameraCaptureSession } for camera preview.
        private CameraCaptureSession? _captureSession;

        // A reference to the opened CameraDevice
        private CameraDevice? _cameraDevice;

        // The size of the camera preview
        private Size _previewSize = new Size(0, 0);

        // CameraDevice.StateListener is called when a CameraDevice changes its state
        private CameraStateListener? _stateCallback;

        // An additional thread for running tasks that shouldn't block the UI.
        private HandlerThread? _backgroundThread;

        // A {@link Handler} for running tasks in the background.
        private Handler? _backgroundHandler;

        // An {@link ImageReader} that handles still image capture.
        private ImageReader? _imageReader;

        // This is the output file for our picture.
        // private File? _file;

        // This a callback object for the {@link ImageReader}. "onImageAvailable" will be called when a
        // still image is ready to be saved.
        private ImageAvailableListener? _onImageAvailableListener;

        //{@link CaptureRequest.Builder} for the camera preview
        private CaptureRequest.Builder? _previewRequestBuilder;

        // {@link CaptureRequest} generated by {@link #mPreviewRequestBuilder}
        private CaptureRequest? _previewRequest;

        // The current state of camera state for taking pictures.
        private int _state = Camera2Const.STATE_PREVIEW;

        // A {@link Semaphore} to prevent the app from exiting before closing the camera.
        private Semaphore _cameraOpenCloseLock = new Semaphore(1);

        private CameraDeviceSpec? _cameraDeviceSpec;

        // A {@link CameraCaptureSession.CaptureCallback} that handles events related to JPEG capture.
        private CameraCaptureListener? _captureCallback;

        private IPreviewSizeCallback? _previewSizeCallback;

        class Camera2BasicSurfaceTextureListener : Java.Lang.Object, TextureView.ISurfaceTextureListener
        {
            public Camera2BasicSurfaceTextureListener(CameraActivity owner)
            {
                _owner = owner;
            }

            public void OnSurfaceTextureAvailable(SurfaceTexture? surface, int width, int height)
            {
                _owner.OpenCamera(width, height);
            }

            public bool OnSurfaceTextureDestroyed(SurfaceTexture? surface)
            {
                return true;
            }

            public void OnSurfaceTextureSizeChanged(SurfaceTexture? surface, int width, int height)
            {
                _owner.ConfigureTransform(width, height);
            }

            public void OnSurfaceTextureUpdated(SurfaceTexture? surface)
            {
            }

            private readonly CameraActivity _owner;
        }

        class CameraCaptureListener : CameraCaptureSession.CaptureCallback
        {
            public CameraCaptureListener(CameraActivity owner, File file)
            {
                _owner = owner;
                _file = file;
            }

            public override void OnCaptureCompleted(CameraCaptureSession? session, CaptureRequest? request, TotalCaptureResult? result)
            {
                Process(result);
            }

            public override void OnCaptureProgressed(CameraCaptureSession? session, CaptureRequest? request, CaptureResult? partialResult)
            {
                Process(partialResult);
            }

            private void Process(CaptureResult? result)
            {
                switch (_owner._state)
                {
                    case Camera2Const.STATE_WAITING_LOCK:
                        {
                            var afState = ((Integer?)result?.Get(CaptureResult.ControlAfState))?.IntValue();
                            if (afState == null)
                            {
                                _owner._state = Camera2Const.STATE_PICTURE_TAKEN; // avoids multiple picture callbacks
                                _owner.CaptureStillPicture(_file);
                            }
                            else if (afState == (int)ControlAFState.FocusedLocked || afState == (int)ControlAFState.NotFocusedLocked)
                            {
                                // ControlAeState can be null on some devices
                                var aeState = (Integer?)result?.Get(CaptureResult.ControlAeState);
                                if (aeState == null || aeState.IntValue() == ((int)ControlAEState.Converged))
                                {
                                    _owner._state = Camera2Const.STATE_PICTURE_TAKEN;
                                    _owner.CaptureStillPicture(_file);
                                }
                                else
                                {
                                    _owner.RunPrecaptureSequence();
                                }
                            }
                            break;
                        }
                    case Camera2Const.STATE_WAITING_PRECAPTURE:
                        {
                            // ControlAeState can be null on some devices
                            var aeState = ((Integer?)result?.Get(CaptureResult.ControlAeState))?.IntValue();
                            if (aeState == null ||
                                aeState == ((int)ControlAEState.Precapture) ||
                                aeState == ((int)ControlAEState.FlashRequired))
                            {
                                _owner._state = Camera2Const.STATE_WAITING_NON_PRECAPTURE;
                            }
                            break;
                        }
                    case Camera2Const.STATE_WAITING_NON_PRECAPTURE:
                        {
                            // ControlAeState can be null on some devices
                            var aeState = ((Integer?)result?.Get(CaptureResult.ControlAeState))?.IntValue();
                            if (aeState == null || aeState != ((int)ControlAEState.Precapture))
                            {
                                _owner._state = Camera2Const.STATE_PICTURE_TAKEN;
                                _owner.CaptureStillPicture(_file);
                            }
                            break;
                        }

                    case Camera2Const.STATE_PICTURE_TAKEN:
                        break;
                }
            }

            private readonly CameraActivity _owner;
            private File _file;
        }

        class CameraCaptureSessionCallback : CameraCaptureSession.StateCallback
        {
            public CameraCaptureSessionCallback(CameraActivity owner)
            {
                _owner = owner;
            }

            public override void OnConfigureFailed(CameraCaptureSession? session)
            {
                _owner.ShowToast("Failed");
            }

            public override void OnConfigured(CameraCaptureSession? session)
            {
                if (session != null)
                {
                    _owner.SetUpCaptureSession(session);
                }
            }

            private readonly CameraActivity _owner;
        }

        class CameraCaptureStillPictureSessionCallback : CameraCaptureSession.CaptureCallback
        {
            private static readonly string TAG = "CameraCaptureStillPictureSessionCallback";

            public CameraCaptureStillPictureSessionCallback(CameraActivity owner, File file)
            {
                _owner = owner;
                _file = file;
            }

            public override void OnCaptureCompleted(CameraCaptureSession? session, CaptureRequest? request, TotalCaptureResult? result)
            {
                _owner.ShowToast("Saved: " + _file);
                Log.Debug(TAG, _file.ToString());
                _owner.UnlockFocus();

                // _owner.ProcessOCR(_file);
                if (_file.Path != null)
                {
                    _owner.OnCaptureCompleted(_file.Path);
                }
            }

            private readonly CameraActivity _owner;
            private File _file;
        }

        class CameraStateListener : CameraDevice.StateCallback
        {
            public CameraStateListener(CameraActivity owner)
            {
                _owner = owner;
            }

            public override void OnOpened(CameraDevice? cameraDevice)
            {
                // This method is called when the camera is opened.  We start camera preview here.
                _owner._cameraOpenCloseLock.Release();
                _owner._cameraDevice = cameraDevice;
                _owner.CreateCameraPreviewSession();
            }

            public override void OnDisconnected(CameraDevice? cameraDevice)
            {
                _owner._cameraOpenCloseLock.Release();
                cameraDevice?.Close();
                _owner._cameraDevice = null;
            }

            public override void OnError(CameraDevice? cameraDevice, CameraError error)
            {
                _owner._cameraOpenCloseLock.Release();
                cameraDevice?.Close();
                _owner._cameraDevice = null;
                if (_owner == null)
                    return;
                Activity activity = _owner;
                if (activity != null)
                {
                    activity.Finish();
                }
            }

            private readonly CameraActivity _owner;
        }

        class ImageAvailableListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
        {
            public ImageAvailableListener(CameraActivity fragment, File file)
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
            private readonly CameraActivity _owner;

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

    public class Camera2Const
    {
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
        public static readonly int MAX_PREVIEW_WIDTH = 1920;

        // Max preview height that is guaranteed by Camera2 API
        public static readonly int MAX_PREVIEW_HEIGHT = 1080;
    }
}