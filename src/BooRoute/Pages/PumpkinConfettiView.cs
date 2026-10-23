namespace BooRoute.Pages;

public sealed class PumpkinConfettiView : GraphicsView
{
    private const string AnimationName = "PumpkinConfetti";
    private readonly PumpkinDrawable pumpkins = new();

    public PumpkinConfettiView()
    {
        Drawable = pumpkins;
        InputTransparent = true;
        // Keep the overlay laid out so the first burst has valid bounds.
        Opacity = 0;
        AutomationProperties.SetIsInAccessibleTree(this, false);
        Unloaded += (_, _) => Stop();
    }

    public void Burst()
    {
        Stop();
        if (!MotionEnabled || Width <= 0 || Height <= 0)
            return;

        pumpkins.Reset();
        Opacity = 1;
        new Animation(progress =>
        {
            if (!MotionEnabled)
            {
                Stop();
                return;
            }
            pumpkins.Elapsed = (float)progress * 1.6f;
            Invalidate();
        }).Commit(this, AnimationName, rate: 16, length: 1600,
            finished: (_, _) => Opacity = 0);
    }

    public void Stop()
    {
        this.AbortAnimation(AnimationName);
        Opacity = 0;
    }

    private static bool MotionEnabled
    {
        get
        {
#if IOS || MACCATALYST
            return !UIKit.UIAccessibility.IsReduceMotionEnabled;
#elif ANDROID
            return Android.Provider.Settings.Global.GetFloat(
                Android.App.Application.Context.ContentResolver,
                Android.Provider.Settings.Global.AnimatorDurationScale, 1) > 0;
#elif WINDOWS
            return new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
#else
            return false;
#endif
        }
    }

    private sealed class PumpkinDrawable : IDrawable
    {
        private readonly record struct Pumpkin(float Horizontal, float Upward, float Size,
            float Angle, float Spin, float Delay, int Kind, Color Color);

        // Kind 0: plain pumpkin, 1: jack-o'-lantern, 2: small pumpkin with a leaf.
        private static readonly Color[] Palette =
            [Ui.Orange, Color.FromArgb("#FF8A2B"), Color.FromArgb("#F2752A"), Color.FromArgb("#FFC07A")];
        private static readonly Color Rib = Color.FromArgb("#C9561A");
        private static readonly Color Stem = Color.FromArgb("#6B4A2B");
        private static readonly PathF Leaf = CreateLeaf();
        private static readonly PathF Face = CreateFace();
        private readonly Pumpkin[] pumpkins = new Pumpkin[28];
        public float Elapsed { get; set; }

        public void Reset()
        {
            Elapsed = 0;
            for (var i = 0; i < pumpkins.Length; i++)
            {
                pumpkins[i] = new(
                    (Random.Shared.NextSingle() - 0.5f) * 1.2f,
                    0.8f + Random.Shared.NextSingle() * 0.5f,
                    0.8f + Random.Shared.NextSingle() * 0.6f,
                    (Random.Shared.NextSingle() - 0.5f) * 60,
                    (Random.Shared.NextSingle() - 0.5f) * 300,
                    Random.Shared.NextSingle() * 0.08f,
                    i % 3, Palette[i % Palette.Length]);
            }
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            foreach (var pumpkin in pumpkins)
            {
                var time = Elapsed - pumpkin.Delay;
                if (time < 0)
                    continue;
                var x = dirtyRect.Width * (0.5f + pumpkin.Horizontal * time);
                var y = dirtyRect.Height * (0.85f - pumpkin.Upward * time + 0.85f * time * time);
                canvas.SaveState();
                canvas.Alpha = Math.Clamp((1.6f - Elapsed) / 0.35f, 0, 1);
                canvas.Translate(x, y);
                canvas.Rotate(pumpkin.Angle + pumpkin.Spin * time);
                var scale = pumpkin.Kind == 2 ? pumpkin.Size * 0.75f : pumpkin.Size;
                canvas.Scale(scale, scale);
                DrawPumpkin(canvas, pumpkin);
                canvas.RestoreState();
            }
        }

        private static void DrawPumpkin(ICanvas canvas, Pumpkin pumpkin)
        {
            // Stem first so the body overlaps its base.
            canvas.FillColor = Stem;
            canvas.FillRoundedRectangle(-1.75f, -13, 3.5f, 6, 1.5f);

            // Body: two side lobes and a centre lobe.
            canvas.FillColor = pumpkin.Color;
            canvas.FillEllipse(-12, -8, 13, 17);
            canvas.FillEllipse(-1, -8, 13, 17);
            canvas.FillEllipse(-6.5f, -9, 13, 18);

            // Ribs.
            canvas.StrokeColor = Rib;
            canvas.StrokeSize = 1.2f;
            canvas.DrawEllipse(-6.5f, -9, 13, 18);

            if (pumpkin.Kind == 1)
            {
                canvas.FillColor = Ui.Midnight;
                canvas.FillPath(Face);
            }
            else if (pumpkin.Kind == 2)
            {
                canvas.FillColor = Ui.Mint;
                canvas.FillPath(Leaf);
            }
        }

        private static PathF CreateFace()
        {
            var path = new PathF();
            // Eyes.
            path.MoveTo(-6, -2); path.LineTo(-2.5f, -2); path.LineTo(-4.25f, -5.5f); path.Close();
            path.MoveTo(2.5f, -2); path.LineTo(6, -2); path.LineTo(4.25f, -5.5f); path.Close();
            // Grin with one tooth.
            path.MoveTo(-7, 2);
            path.QuadTo(0, 9, 7, 2);
            path.LineTo(2, 3.5f);
            path.LineTo(1, 5.5f);
            path.LineTo(-1, 3.8f);
            path.Close();
            return path;
        }

        private static PathF CreateLeaf()
        {
            var path = new PathF();
            path.MoveTo(1.5f, -11);
            path.QuadTo(6, -15, 10, -12);
            path.QuadTo(6, -8.5f, 1.5f, -11);
            path.Close();
            return path;
        }
    }
}
