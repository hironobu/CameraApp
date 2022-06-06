using System;
using System.Collections.Generic;
using System.Linq;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Util;
using Android.Views;
using Java.Lang;
using Java.Util;
using Boolean = Java.Lang.Boolean;

namespace AzureCVCamera
{
    public partial class CameraActivity
    {
        public class CameraDeviceSpec
        {
            private CameraDeviceSpec(string cameraId, StreamConfigurationMap streamConfigurationMap, Size largestSize, LensFacing lensFacing, int sensorOrientation, bool flashSupported)
            {
                _cameraId = cameraId;
                _streamConfigurationMap = streamConfigurationMap;
                _largestSize = largestSize;
                _lensFacing = lensFacing;
                _sensorOrientation = sensorOrientation;
                _flashSupported = flashSupported;
            }

            public string CameraId => _cameraId;

            public Size LargestSize => _largestSize;

            public LensFacing LensFacing => _lensFacing;

            public int SensorOrientation => _sensorOrientation;

            public bool FlashSupported => _flashSupported;

            public Size ChooseOptimalSize(SurfaceOrientation displayRotation, Rect bounds, int width, int height, int maxWidth, int maxHeight)
            {
                //noinspection ConstantConditions
                bool swappedDimensions = false;
                switch (displayRotation)
                {
                    case SurfaceOrientation.Rotation0:
                    case SurfaceOrientation.Rotation180:
                        if (_sensorOrientation == 90 || _sensorOrientation == 270)
                        {
                            swappedDimensions = true;
                        }
                        break;
                    case SurfaceOrientation.Rotation90:
                    case SurfaceOrientation.Rotation270:
                        if (_sensorOrientation == 0 || _sensorOrientation == 180)
                        {
                            swappedDimensions = true;
                        }
                        break;
                    default:
                        Log.Error(TAG, "Display rotation is invalid: " + displayRotation);
                        break;
                }

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

                maxPreviewWidth = Enumerable.Min<int>(new[] { maxPreviewWidth, maxWidth });
                maxPreviewHeight = Enumerable.Min<int>(new[] { maxPreviewHeight, maxHeight });

                var previewDimension = new PreviewDimension(rotatedPreviewWidth, rotatedPreviewHeight, maxPreviewWidth, maxPreviewHeight);

                var choices = _streamConfigurationMap.GetOutputSizes(Class.FromType(typeof(SurfaceTexture)));
                var aspectRatio = _largestSize;

                if (choices == null)
                {
                    throw new NotImplementedException("choices is null");
                }

                // Collect the supported resolutions that are at least as big as the preview Surface
                var bigEnough = new List<Size>();
                // Collect the supported resolutions that are smaller than the preview Surface
                var notBigEnough = new List<Size>();
                int w = aspectRatio.Width;
                int h = aspectRatio.Height;

                for (var i = 0; i < choices.Length; i++)
                {
                    Size option = choices[i];
                    if ((option.Width <= previewDimension.MaxWidth) && (option.Height <= previewDimension.MaxHeight) && option.Height == option.Width * h / w)
                    {
                        if (option.Width >= previewDimension.Width &&
                            option.Height >= previewDimension.Height)
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

            private string _cameraId;
            private StreamConfigurationMap _streamConfigurationMap;
            private Size _largestSize;
            private LensFacing _lensFacing;
            private int _sensorOrientation;
            private bool _flashSupported;

            public static CameraDeviceSpec LoadSpec(CameraManager cameraManager, string cameraId)
            {
                var characteristics = cameraManager.GetCameraCharacteristics(cameraId);
                if (characteristics == null)
                {
                    throw new NotImplementedException("characteristics is null");
                }

                var facing = (Integer?)characteristics.Get(CameraCharacteristics.LensFacing);
                if (facing == null)
                {
                    throw new NotImplementedException("facing is null");
                }

                var streamConfigurationMap = (StreamConfigurationMap?)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
                if (streamConfigurationMap == null)
                {
                    throw new NotImplementedException("streamConfigurationMap is null");
                }

                var largest = (Size?)Collections.Max(streamConfigurationMap.GetOutputSizes((int)ImageFormatType.Jpeg), new ByAreaComparator());
                if (largest == null)
                {
                    throw new NotImplementedException("largest is null");
                }

                var sensorOrientation = (int)(characteristics?.Get(CameraCharacteristics.SensorOrientation) ?? 0);

                var flashSupported = ((Boolean?)characteristics?.Get(CameraCharacteristics.FlashInfoAvailable))?.BooleanValue() ?? false;

                return new CameraDeviceSpec(cameraId, streamConfigurationMap, largest, (LensFacing)facing.IntValue(), (int)sensorOrientation, flashSupported);
            }
        }
    }
}