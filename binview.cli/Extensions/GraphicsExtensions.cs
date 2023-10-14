namespace binview.cli.Extensions
{
    using System.Drawing;

    public static class GraphicsExtensions
    {
        public static void DrawPixel(this Graphics source, int x, int y, Color colour)
        {
            using (var pen = new Pen(colour))
            {
                source.DrawLine(pen, x, y, x + 0.1F, y + 0.1F);
            }
        }

        public static void DrawPixel(this Graphics source, int x, int y, byte red, byte green, byte blue, byte? alpha = default(byte?))
        {
            source.DrawPixel(x, y, Color.FromArgb(alpha ?? 255, red, green, blue));
        }
    }
}
