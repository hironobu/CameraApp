#nullable enable

using Android.Hardware.Camera2;
using Java.Lang;

namespace CameraApp.Listeners
{
    public class CameraCaptureListener : CameraCaptureSession.CaptureCallback
    {
        public CameraCaptureListener(Camera2BasicFragment owner)
        {
            this._owner = owner;
        }

        public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result)
        {
            Process(result);
        }

        public override void OnCaptureProgressed(CameraCaptureSession session, CaptureRequest request, CaptureResult partialResult)
        {
            Process(partialResult);
        }

        private void Process(CaptureResult result)
        {
            switch (_owner._state)
            {
                case Camera2BasicFragment.STATE_WAITING_LOCK:
                    {
                        var afState = ((Integer?)result.Get(CaptureResult.ControlAfState))?.IntValue();
                        if (afState == null)
                        {
                            _owner._state = Camera2BasicFragment.STATE_PICTURE_TAKEN; // avoids multiple picture callbacks
                            _owner.CaptureStillPicture();
                        }
                        else if (afState == (int)ControlAFState.FocusedLocked || afState == (int)ControlAFState.NotFocusedLocked)
                        {
                            // ControlAeState can be null on some devices
                            var aeState = (Integer?)result.Get(CaptureResult.ControlAeState);
                            if (aeState == null || aeState.IntValue() == ((int)ControlAEState.Converged))
                            {
                                _owner._state = Camera2BasicFragment.STATE_PICTURE_TAKEN;
                                _owner.CaptureStillPicture();
                            }
                            else
                            {
                                _owner.RunPrecaptureSequence();
                            }
                        }
                        break;
                    }
                case Camera2BasicFragment.STATE_WAITING_PRECAPTURE:
                    {
                        // ControlAeState can be null on some devices
                        var aeState = ((Integer?)result.Get(CaptureResult.ControlAeState))?.IntValue();
                        if (aeState == null ||
                            aeState == ((int)ControlAEState.Precapture) ||
                            aeState == ((int)ControlAEState.FlashRequired))
                        {
                            _owner._state = Camera2BasicFragment.STATE_WAITING_NON_PRECAPTURE;
                        }
                        break;
                    }
                case Camera2BasicFragment.STATE_WAITING_NON_PRECAPTURE:
                    {
                        // ControlAeState can be null on some devices
                        var aeState = ((Integer?)result.Get(CaptureResult.ControlAeState))?.IntValue();
                        if (aeState == null || aeState != ((int)ControlAEState.Precapture))
                        {
                            _owner._state = Camera2BasicFragment.STATE_PICTURE_TAKEN;
                            _owner.CaptureStillPicture();
                        }
                        break;
                    }

                case Camera2BasicFragment.STATE_PICTURE_TAKEN:
                    break;
            }
        }

        private readonly Camera2BasicFragment _owner;
    }
}
