#nullable enable

using System;
using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;

namespace CameraApp
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

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
            int width = MeasureSpec.GetSize(widthMeasureSpec);
            int height = MeasureSpec.GetSize(heightMeasureSpec);
            if (0 == mRatioWidth || 0 == mRatioHeight)
            {
                SetMeasuredDimension(width, height);
            }
            else
            {
                if (width < (float)height * mRatioWidth / (float)mRatioHeight)
                {
                    SetMeasuredDimension(width, width * mRatioHeight / mRatioWidth);
                }
                else
                {
                    SetMeasuredDimension(height * mRatioWidth / mRatioHeight, height);
                }
            }
        }

        protected override void OnDraw(Canvas? canvas)
        {
            base.OnDraw(canvas);

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

        private int mRatioWidth = 0;
        private int mRatioHeight = 0;

        private Paint _paint = new Paint();
    }
}

