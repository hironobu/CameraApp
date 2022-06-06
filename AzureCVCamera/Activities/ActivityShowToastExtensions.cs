using Android.App;
using Android.Content;
using Android.Widget;
using Java.Lang;

namespace AzureCVCamera
{
    public static class ActivityShowToastExtensions
    {
        public static void ShowToast(this Activity self, string text)
        {
            if (self != null && self.ApplicationContext != null)
            {
                self.RunOnUiThread(new ShowToastRunnable(self.ApplicationContext, text));
            }
        }

        private class ShowToastRunnable : Java.Lang.Object, IRunnable
        {
            public ShowToastRunnable(Context context, string text)
            {
                _context = context;
                _text = text;
            }

            public void Run()
            {
                Toast.MakeText(_context, _text, ToastLength.Short)?.Show();
            }

            private string _text;
            private Context _context;
        }
    }
}