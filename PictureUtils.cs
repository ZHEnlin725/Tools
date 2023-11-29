using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public static class PictureUtils
{
    private static readonly ParallelOptions pOptions = new ParallelOptions {MaxDegreeOfParallelism = 16};

    public static Texture2D Grayscale(Texture2D image, int channel = 3)
    {
        var dest = Grayscale(image.GetPixels(), channel);
        var result = new Texture2D(image.width, image.height, TextureFormat.RGBA32, false);
        result.SetPixels(dest);
        result.Apply();
        return result;
    }

    public static Color[] Grayscale(Color[] pixels, int channel = 3)
    {
        var length = pixels.Length;
        var dest = new Color[length];
        Parallel.For(0, length, pOptions, i =>
        {
            var pixel = pixels[i];
            var f = pixel[channel];
            dest[i] = new Color {r = f, g = f, b = f, a = f};
        });
        return dest;
    }

    public static Color[] Negation(Color[] pixels)
    {
        var length = pixels.Length;
        var result = new Color[length];
        Parallel.For(0, length, pOptions, i =>
        {
            var color = pixels[i];
            result[i] = new Color(1 - color.r, 1 - color.g, 1 - color.b, 1 - color.a);
        });
        return result;
    }

    public static Color[] Shrink(int width, int height, Color[] pixels, float scaleX, float scaleY)
    {
        int newWidth = Mathf.FloorToInt(width * scaleX), newHeight = Mathf.FloorToInt(height * scaleY);
        float inv_scaleX = 1 / scaleX, inv_scaleY = 1 / scaleY;
        var newPixels = new Color[newWidth * newHeight];
        Parallel.For(0, newWidth, pOptions,
            x =>
            {
                Parallel.For(0, newHeight, pOptions,
                    y =>
                    {
                        newPixels[x + y * newWidth] =
                            biCubicInterploator(pixels, width, height, x, y, inv_scaleX, inv_scaleY);
                    });
            });

        return newPixels;
    }

    public static Texture2D GaussianBlur(Texture2D image, int blurRadial)
    {
        var width = image.width;
        var height = image.height;
        var source = image.GetPixels();
        var dest = GaussianBlur(source, width, height, blurRadial);
        var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.SetPixels(dest);
        return result;
    }

    public static Color[] GaussianBlur(Color[] source, int width, int height, int blurRadial)
    {
        var alpha = new float[width * height];
        var red = new float[width * height];
        var green = new float[width * height];
        var blue = new float[width * height];

        Parallel.For(0, source.Length, pOptions, i =>
        {
            alpha[i] = source[i].a;
            red[i] = source[i].r;
            green[i] = source[i].g;
            blue[i] = source[i].b;
        });

        var newAlpha = new float[width * height];
        var newRed = new float[width * height];
        var newGreen = new float[width * height];
        var newBlue = new float[width * height];
        var dest = new Color[width * height];

        Parallel.Invoke(
            () => gaussBlur_4(alpha, newAlpha, width, height, blurRadial),
            () => gaussBlur_4(red, newRed, width, height, blurRadial),
            () => gaussBlur_4(green, newGreen, width, height, blurRadial),
            () => gaussBlur_4(blue, newBlue, width, height, blurRadial));

        Parallel.For(0, dest.Length, pOptions, i =>
        {
            if (newAlpha[i] > 1) newAlpha[i] = 1;
            if (newRed[i] > 1) newRed[i] = 1;
            if (newGreen[i] > 1) newGreen[i] = 1;
            if (newBlue[i] > 1) newBlue[i] = 1;

            if (newAlpha[i] < 0) newAlpha[i] = 0;
            if (newRed[i] < 0) newRed[i] = 0;
            if (newGreen[i] < 0) newGreen[i] = 0;
            if (newBlue[i] < 0) newBlue[i] = 0;

            dest[i] = new Color(newRed[i], newGreen[i], newBlue[i], newAlpha[i]);
        });

        return dest;
    }

    private static void gaussBlur_4(float[] source, float[] dest, int width, int height, int r)
    {
        var bxs = boxesForGauss(r, 3);
        boxBlur_4(source, dest, width, height, (bxs[0] - 1) / 2);
        boxBlur_4(dest, source, width, height, (bxs[1] - 1) / 2);
        boxBlur_4(source, dest, width, height, (bxs[2] - 1) / 2);
    }

    private static int[] boxesForGauss(int sigma, int n)
    {
        var wIdeal = Math.Sqrt((12 * sigma * sigma / n) + 1);
        var wl = (int) Math.Floor(wIdeal);
        if (wl % 2 == 0) wl--;
        var wu = wl + 2;

        var mIdeal = (double) (12 * sigma * sigma - n * wl * wl - 4 * n * wl - 3 * n) / (-4 * wl - 4);
        var m = Math.Round(mIdeal);

        var sizes = new List<int>();
        for (var i = 0; i < n; i++) sizes.Add(i < m ? wl : wu);
        return sizes.ToArray();
    }

    private static void boxBlur_4(float[] source, float[] dest, int w, int h, int r)
    {
        for (var i = 0; i < source.Length; i++) dest[i] = source[i];
        boxBlurH_4(dest, source, w, h, r);
        boxBlurT_4(source, dest, w, h, r);
    }

    private static void boxBlurH_4(float[] source, float[] dest, int w, int h, int r)
    {
        var iar = (double) 1 / (r + r + 1);
        Parallel.For(0, h, pOptions, i =>
        {
            var ti = i * w;
            var li = ti;
            var ri = ti + r;
            var fv = source[ti];
            var lv = source[ti + w - 1];
            var val = (r + 1) * fv;
            for (var j = 0; j < r; j++) val += source[ti + j];
            for (var j = 0; j <= r; j++)
            {
                val += source[ri++] - fv;
                dest[ti++] = (float) (val * iar);
            }

            for (var j = r + 1; j < w - r; j++)
            {
                val += source[ri++] - dest[li++];
                dest[ti++] = (float) (val * iar);
            }

            for (var j = w - r; j < w; j++)
            {
                val += lv - source[li++];
                dest[ti++] = (float) (val * iar);
            }
        });
    }

    private static void boxBlurT_4(float[] source, float[] dest, int w, int h, int r)
    {
        var iar = (double) 1 / (r + r + 1);
        Parallel.For(0, w, pOptions, i =>
        {
            var ti = i;
            var li = ti;
            var ri = ti + r * w;
            var fv = source[ti];
            var lv = source[ti + w * (h - 1)];
            var val = (r + 1) * fv;
            for (var j = 0; j < r; j++) val += source[ti + j * w];
            for (var j = 0; j <= r; j++)
            {
                val += source[ri] - fv;
                dest[ti] = (float) (val * iar);
                ri += w;
                ti += w;
            }

            for (var j = r + 1; j < h - r; j++)
            {
                val += source[ri] - source[li];
                dest[ti] = (float) (val * iar);
                li += w;
                ri += w;
                ti += w;
            }

            for (var j = h - r; j < h; j++)
            {
                val += lv - source[li];
                dest[ti] = (float) (val * iar);
                li += w;
                ti += w;
            }
        });
    }

    private static double biCubicInterploator(double x)
    {
        if (x < 0) x = -x;
        double biCoef = 0;
        if (x <= 1)
        {
            biCoef = (1.5 * x - 2.5) * x * x + 1;
        }
        else if (x < 2)
        {
            biCoef = ((-0.5 * x + 2.5) * x - 4) * x + 2;
        }

        return biCoef;
    }

    private static Color biCubicInterploator(Color[] pixels, int width, int height, int px, int py, float xfactor,
        float yfactor)
    {
        var color = (Color) default;
        var ox = px * xfactor - 0.5;
        var ox1 = (int) ox;
        var dx = ox - ox1;
        var oy = py * yfactor - 0.5;
        var oy1 = (int) oy;
        var dy = oy - oy1;
        for (int n = -1; n < 3; n++)
        {
            var k1 = biCubicInterploator(dy - n);
            var oy2 = Mathf.Clamp(oy1 + n, 0, height - 1);
            for (int m = -1; m < 3; m++)
            {
                var k2 = k1 * biCubicInterploator(m - dx);
                var ox2 = Mathf.Clamp(ox1 + m, 0, width - 1);
                color += (float) k2 * pixels[ox2 + oy2 * width];
            }
        }

        return color;
    }
}