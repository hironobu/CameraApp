#nullable enable

using System;
using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;

namespace AzureCVCamera
{
	public class PreviewOverlayView : View
	{
        public PreviewOverlayView(Context context) : base(context)
        {
        }

        public PreviewOverlayView(Context context, IAttributeSet? attributeSet) : base(context, attributeSet)
        {
        }

        public PreviewOverlayView(Context context, IAttributeSet? attributeSet, int defStyle) : base(context, attributeSet, defStyle)
        {
        }

        public Size AvailableSize { get; set; } = new Size(0, 0);

        public int StrokeWidth { get; set; } = 10;

        public int Span { get; set; } = 50;

        public Color Color { get; set; } = Color.White;

        public Bitmap? Bitmap { get; set; } = null;

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
            int width = MeasureSpec.GetSize(widthMeasureSpec);
            int height = MeasureSpec.GetSize(heightMeasureSpec);
            if (_ratioWidth == 0 || _ratioHeight == 0)
            {
                SetMeasuredDimension(width, height);
            }
            else
            {
                if (width < (float)height * _ratioWidth / (float)_ratioHeight)
                {
                    SetMeasuredDimension(width, width * _ratioHeight / _ratioWidth);
                }
                else
                {
                    SetMeasuredDimension(height * _ratioWidth / _ratioHeight, height);
                }
            }
        }

        protected override void OnDraw(Canvas? canvas)
        {
            base.OnDraw(canvas);

            if (AvailableSize.Width == 0 && AvailableSize.Height == 0)
            {
                return;
            }

            if (Bitmap != null)
            {
                canvas?.DrawBitmap(Bitmap, 0, 0, null);
            }

            var r = new Rect();
            GetDrawingRect(r);

            var h = AvailableSize.Height * r.Width() / AvailableSize.Width;
            var rect = new Rect(StrokeWidth / 2, h / 2 - Span, Width - StrokeWidth / 2, h / 2 + Span);

            _paint.StrokeWidth = StrokeWidth;
            _paint.StrokeCap = Paint.Cap.Square;
            _paint.Color = this.Color;
            _paint.SetStyle(Paint.Style.Stroke);

            canvas?.DrawRect(rect, _paint);
        }

        private int _ratioWidth = 0;
        private int _ratioHeight = 0;

        private Paint _paint = new Paint();
    }
}

