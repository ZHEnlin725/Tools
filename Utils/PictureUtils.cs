#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public static class PictureUtils
{
    private static readonly ParallelOptions pOptions = new ParallelOptions()
    {
        MaxDegreeOfParallelism = 16
    };

    public static IList<Texture2D> ExtractByCellSize(Texture2D rawAtlas, int cellWidth, int cellHeight)
    {
        var pixels = rawAtlas.GetPixels();
        int width = rawAtlas.width, height = rawAtlas.height;
        int horizontal = Mathf.FloorToInt(width * 1.0f / cellWidth),
            vertical = Mathf.FloorToInt(height * 1.0f / cellHeight);
        var colorsCollection = new List<Color[]>();
        for (var v = 0; v < vertical; v++)
        {
            for (var h = 0; h < horizontal; h++)
            {
                var empty = true;
                var cellPixels = new Color[cellWidth * cellHeight];
                for (var px = 0; px < cellWidth; px++)
                {
                    for (var py = 0; py < cellHeight; py++)
                    {
                        var x = h * cellWidth + px;
                        // var y = v * cellHeight + py;
                        var y = height - (v + 1) * cellHeight + py;
                        var index = x + y * width;
                        if (index < 0 || index >= pixels.Length) continue;
                        var pixel = pixels[index];
                        if (pixel.a > 0.01f) empty = false;
                        cellPixels[px + py * cellWidth] = pixel;
                    }
                }

                if (!empty) colorsCollection.Add(cellPixels);
            }
        }

        var result = new List<Texture2D>(colorsCollection.Count);
        foreach (var colors in colorsCollection)
        {
            var cellTexture = new Texture2D(cellWidth, cellHeight, TextureFormat.ARGB32, false);
            cellTexture.SetPixels(colors);
            result.Add(cellTexture);
        }

        return result;
    }

    public static void PackAtlas(IList<Texture2D> textures, string output)
    {
        const int minSize = 1024, maxSize = 2048;
        var numCells = textures.Count;
        int totalWidth = 0, totalHeight = 0;
        foreach (var texture in textures)
        {
            totalWidth += texture.width;
            totalHeight += texture.height;
        }

        var cellWidth = totalWidth / numCells;
        var cellHeight = totalHeight / numCells;
        var minNum = (minSize / cellWidth) * (minSize / cellHeight);
        var maxNum = (maxSize / cellWidth) * (maxSize / cellHeight);
        if (numCells > maxNum)
        {
            Debug.LogError($"Out Of Range numCells:{numCells}");
            return;
        }

        int width, height;
        if (numCells <= minNum)
        {
            width = minSize;
            height = minSize;
        }
        else
        {
            width = maxSize;
            var col = width / cellWidth;
            var row = Mathf.CeilToInt(numCells * 1f / col);
            height = row * cellHeight <= minSize ? minSize : maxSize;
        }

        var atlas = new Texture2D(width, height, TextureFormat.ARGB32, false);
        var collectionIndex = 0;
        int atlasX = 0, atlasY = height - cellHeight;
        var atlasPixels = new Color[width * height];
        while (collectionIndex < textures.Count)
        {
            if (width - atlasX < cellWidth)
            {
                atlasX = 0;
                atlasY -= cellHeight;
                continue;
            }

            var colors = textures[collectionIndex++].GetPixels();
            Parallel.For(0, cellWidth, cx =>
            {
                Parallel.For(0, cellHeight, cy =>
                {
                    int px = atlasX + cx, py = atlasY + cy;
                    var color = colors[cx + cy * cellWidth];
                    var index = px + py * width;
                    if (index >= 0 && index < atlasPixels.Length) atlasPixels[px + py * width] = color;
                });
            });
            atlasX += cellWidth;
        }

        atlas.SetPixels(atlasPixels);
        var bytes = atlas.EncodeToPNG();
        System.IO.File.WriteAllBytes(output, bytes);
        UnityEditor.AssetDatabase.Refresh();
    }

    public static Texture2D Grayscale(Texture2D image, int channel = 3)
    {
        Color[] colors = PictureUtils.Grayscale(image.GetPixels(), channel);
        Texture2D texture2D = new Texture2D(image.width, image.height, TextureFormat.RGBA32, false);
        texture2D.SetPixels(colors);
        texture2D.Apply();
        return texture2D;
    }

    public static Color[] Grayscale(Color[] pixels, int channel = 3)
    {
        int length = pixels.Length;
        Color[] dest = new Color[length];
        Parallel.For(0, length, PictureUtils.pOptions, (Action<int>) (i =>
        {
            float num = pixels[i][channel];
            dest[i] = new Color()
            {
                r = num,
                g = num,
                b = num,
                a = num
            };
        }));
        return dest;
    }

    public static Color[] Negation(Color[] pixels)
    {
        int length = pixels.Length;
        Color[] result = new Color[length];
        Parallel.For(0, length, PictureUtils.pOptions, (Action<int>) (i =>
        {
            Color pixel = pixels[i];
            result[i] = new Color(1f - pixel.r, 1f - pixel.g, 1f - pixel.b, 1f - pixel.a);
        }));
        return result;
    }

    public static Color[] Shrink(int width, int height, Color[] pixels, float scaleX, float scaleY)
    {
        int newWidth = Mathf.FloorToInt((float) width * scaleX);
        int newHeight = Mathf.FloorToInt((float) height * scaleY);
        float inv_scaleX = 1f / scaleX;
        float inv_scaleY = 1f / scaleY;
        Color[] newPixels = new Color[newWidth * newHeight];
        Parallel.For(0, newWidth, PictureUtils.pOptions,
            (Action<int>) (x => Parallel.For(0, newHeight, PictureUtils.pOptions,
                (Action<int>) (y =>
                    newPixels[x + y * newWidth] =
                        PictureUtils.biCubicInterploator(pixels, width, height, x, y, inv_scaleX, inv_scaleY)))));
        return newPixels;
    }

    public static Texture2D GaussianBlur(Texture2D image, int blurRadial)
    {
        int width = image.width;
        int height = image.height;
        Color[] colors = PictureUtils.GaussianBlur(image.GetPixels(), width, height, blurRadial);
        Texture2D texture2D = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture2D.SetPixels(colors);
        return texture2D;
    }

    public static Color[] GaussianBlur(Color[] source, int width, int height, int blurRadial)
    {
        float[] alpha = new float[width * height];
        float[] red = new float[width * height];
        float[] green = new float[width * height];
        float[] blue = new float[width * height];
        Parallel.For(0, source.Length, PictureUtils.pOptions, (Action<int>) (i =>
        {
            alpha[i] = source[i].a;
            red[i] = source[i].r;
            green[i] = source[i].g;
            blue[i] = source[i].b;
        }));
        float[] newAlpha = new float[width * height];
        float[] newRed = new float[width * height];
        float[] newGreen = new float[width * height];
        float[] newBlue = new float[width * height];
        Color[] dest = new Color[width * height];
        Parallel.Invoke((Action) (() => PictureUtils.gaussBlur_4(alpha, newAlpha, width, height, blurRadial)),
            (Action) (() => PictureUtils.gaussBlur_4(red, newRed, width, height, blurRadial)),
            (Action) (() => PictureUtils.gaussBlur_4(green, newGreen, width, height, blurRadial)),
            (Action) (() => PictureUtils.gaussBlur_4(blue, newBlue, width, height, blurRadial)));
        Parallel.For(0, dest.Length, PictureUtils.pOptions, (Action<int>) (i =>
        {
            if ((double) newAlpha[i] > 1.0)
                newAlpha[i] = 1f;
            if ((double) newRed[i] > 1.0)
                newRed[i] = 1f;
            if ((double) newGreen[i] > 1.0)
                newGreen[i] = 1f;
            if ((double) newBlue[i] > 1.0)
                newBlue[i] = 1f;
            if ((double) newAlpha[i] < 0.0)
                newAlpha[i] = 0.0f;
            if ((double) newRed[i] < 0.0)
                newRed[i] = 0.0f;
            if ((double) newGreen[i] < 0.0)
                newGreen[i] = 0.0f;
            if ((double) newBlue[i] < 0.0)
                newBlue[i] = 0.0f;
            dest[i] = new Color(newRed[i], newGreen[i], newBlue[i], newAlpha[i]);
        }));
        return dest;
    }

    private static void gaussBlur_4(float[] source, float[] dest, int width, int height, int r)
    {
        int[] numArray = PictureUtils.boxesForGauss(r, 3);
        PictureUtils.boxBlur_4(source, dest, width, height, (numArray[0] - 1) / 2);
        PictureUtils.boxBlur_4(dest, source, width, height, (numArray[1] - 1) / 2);
        PictureUtils.boxBlur_4(source, dest, width, height, (numArray[2] - 1) / 2);
    }

    private static int[] boxesForGauss(int sigma, int n)
    {
        int num1 = (int) Math.Floor(Math.Sqrt((double) (12 * sigma * sigma / n + 1)));
        if (num1 % 2 == 0)
            --num1;
        int num2 = num1 + 2;
        double num3 = Math.Round((double) (12 * sigma * sigma - n * num1 * num1 - 4 * n * num1 - 3 * n) /
                                 (double) (-4 * num1 - 4));
        List<int> intList = new List<int>();
        for (int index = 0; index < n; ++index)
            intList.Add((double) index < num3 ? num1 : num2);
        return intList.ToArray();
    }

    private static void boxBlur_4(float[] source, float[] dest, int w, int h, int r)
    {
        for (int index = 0; index < source.Length; ++index)
            dest[index] = source[index];
        PictureUtils.boxBlurH_4(dest, source, w, h, r);
        PictureUtils.boxBlurT_4(source, dest, w, h, r);
    }

    private static void boxBlurH_4(float[] source, float[] dest, int w, int h, int r)
    {
        double iar = 1.0 / (double) (r + r + 1);
        Parallel.For(0, h, PictureUtils.pOptions, (Action<int>) (i =>
        {
            int index1 = i * w;
            int num1 = index1;
            int num2 = index1 + r;
            float num3 = source[index1];
            float num4 = source[index1 + w - 1];
            float num5 = (float) (r + 1) * num3;
            for (int index2 = 0; index2 < r; ++index2)
                num5 += source[index1 + index2];
            for (int index2 = 0; index2 <= r; ++index2)
            {
                num5 += source[num2++] - num3;
                dest[index1++] = num5 * (float) iar;
            }

            for (int index2 = r + 1; index2 < w - r; ++index2)
            {
                num5 += source[num2++] - dest[num1++];
                dest[index1++] = num5 * (float) iar;
            }

            for (int index2 = w - r; index2 < w; ++index2)
            {
                num5 += num4 - source[num1++];
                dest[index1++] = num5 * (float) iar;
            }
        }));
    }

    private static void boxBlurT_4(float[] source, float[] dest, int w, int h, int r)
    {
        double iar = 1.0 / (double) (r + r + 1);
        Parallel.For(0, w, PictureUtils.pOptions, (Action<int>) (i =>
        {
            int index1 = i;
            int index2 = index1;
            int index3 = index1 + r * w;
            float num1 = source[index1];
            float num2 = source[index1 + w * (h - 1)];
            float num3 = (float) (r + 1) * num1;
            for (int index4 = 0; index4 < r; ++index4)
                num3 += source[index1 + index4 * w];
            for (int index4 = 0; index4 <= r; ++index4)
            {
                num3 += source[index3] - num1;
                dest[index1] = num3 * (float) iar;
                index3 += w;
                index1 += w;
            }

            for (int index4 = r + 1; index4 < h - r; ++index4)
            {
                num3 += source[index3] - source[index2];
                dest[index1] = num3 * (float) iar;
                index2 += w;
                index3 += w;
                index1 += w;
            }

            for (int index4 = h - r; index4 < h; ++index4)
            {
                num3 += num2 - source[index2];
                dest[index1] = num3 * (float) iar;
                index2 += w;
                index1 += w;
            }
        }));
    }

    private static double biCubicInterploator(double x)
    {
        if (x < 0.0)
            x = -x;
        double num = 0.0;
        if (x <= 1.0)
            num = (1.5 * x - 2.5) * x * x + 1.0;
        else if (x < 2.0)
            num = ((-0.5 * x + 2.5) * x - 4.0) * x + 2.0;
        return num;
    }

    private static Color biCubicInterploator(Color[] pixels, int width, int height, int px, int py, float xfactor,
        float yfactor)
    {
        Color color = new Color();
        double num1 = (double) px * (double) xfactor - 0.5;
        int num2 = (int) num1;
        double num3 = num1 - (double) num2;
        double num4 = (double) py * (double) yfactor - 0.5;
        int num5 = (int) num4;
        double num6 = num4 - (double) num5;
        for (int index1 = -1; index1 < 3; ++index1)
        {
            double num7 = PictureUtils.biCubicInterploator(num6 - (double) index1);
            int num8 = Mathf.Clamp(num5 + index1, 0, height - 1);
            for (int index2 = -1; index2 < 3; ++index2)
            {
                double num9 = num7 * PictureUtils.biCubicInterploator((double) index2 - num3);
                int num10 = Mathf.Clamp(num2 + index2, 0, width - 1);
                color += (float) num9 * pixels[num10 + num8 * width];
            }
        }

        return color;
    }
}
#endif