#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TilemapUtils
{
    public class HexagonalPointedTopTilemap
    {
        private static readonly Vector3[] HexagonalPointedTopVertices =
        {
            new Vector3(0, 0.5f),
            new Vector3(-0.5f, 0.25f), 
            new Vector3(-0.5f, -0.25f), 
            new Vector3(0, -0.5f),
            new Vector3(0.5f, -0.25f), 
            new Vector3(0.5f, 0.25f),
        };

        public static Texture2D GenerateTexture(Tilemap tilemap, int pixelsPerUnit = 100, float tilescale = -1f,
            float textureExtents = 0f)
        {
            var cellSize = tilemap.cellSize;
            var cellBounds = tilemap.cellBounds;
            var tilemapScale = tilemap.transform.localScale;
            var cellScale = new Vector3(cellSize.x * tilemapScale.x, cellSize.y * tilemapScale.y,
                cellSize.z * tilemapScale.z);
            if (tilescale > 0) cellScale *= tilescale;
            var boundsInWorld = GetTilemapBoundsInWorld(tilemap, tilescale, textureExtents);
            var boundsSize = boundsInWorld.size;
            var imageWidth = LargerPowerOf2(Mathf.CeilToInt(boundsSize.x * pixelsPerUnit));
            var imageHeight = LargerPowerOf2(Mathf.CeilToInt(boundsSize.y * pixelsPerUnit));
            Debug.LogError($"Tilemap to texture W:{imageWidth},H:{imageHeight}");
            boundsSize = new Vector3(imageWidth * 1f / pixelsPerUnit, imageHeight * 1f / pixelsPerUnit);
            var boundsMin = boundsInWorld.center - boundsSize / 2;
            var baseColor = (Color) default;
            var image = new Texture2D(imageWidth, imageHeight, TextureFormat.ARGB32, false);
            var numPixels = imageWidth * imageHeight;
            var result = new Color[numPixels];
            Parallel.For(0, numPixels, i => result[i] = baseColor);
            var pixelsPerCell = cellScale * pixelsPerUnit;
            var pixelsPerCellHorizontal = Mathf.CeilToInt(pixelsPerCell.x);
            var pixelsPerCellVertical = Mathf.CeilToInt(pixelsPerCell.y);
            var cnt = 0;
            var size = cellBounds.size;
            for (var x = cellBounds.min.x; x <= cellBounds.max.x; x++)
            {
                for (var y = cellBounds.min.y; y <= cellBounds.max.y; y++)
                {
                    var position = new Vector3Int(x, y, 0);
                    Sprite sprite;
                    if (!(sprite = tilemap.GetSprite(position)))
                        if (tilemap.GetTile(position) is Tile tile)
                            sprite = tile.sprite;
                    if (sprite == null) continue;
                    var progress = ++cnt * 1f / (size.x * size.y);
                    if (UnityEditor.EditorUtility.DisplayCancelableProgressBar("Hold On",
                        $"Handle {position} {sprite.name}...({Mathf.FloorToInt(progress * 100)}%)",
                        0.1f + 0.9f * progress))
                        return null;
                    var scale = pixelsPerUnit / sprite.pixelsPerUnit;
                    var atlasTexture = sprite.texture;
                    var tileTexture = Math.Abs(pixelsPerUnit - sprite.pixelsPerUnit) < float.Epsilon
                        ? atlasTexture
                        : ShrinkImage(atlasTexture, scale, scale);
                    var rect = sprite.rect;
                    int tileTexWidth = (int) rect.width, tileTexHeight = (int) rect.height;
                    var spriteShrinked = false;
                    if (tilescale > 0)
                    {
                        spriteShrinked = true;
                        tileTexture = ShrinkSprite(sprite, pixelsPerCellHorizontal * 1f / tileTexWidth,
                            pixelsPerCellVertical * 1f / tileTexHeight);
                        tileTexWidth = tileTexture.width;
                        tileTexHeight = tileTexture.height;
                    }

                    var tileColors = tileTexture.GetPixels();
                    var atlasTexWidth = atlasTexture.width;
                    var tileCen = new Vector2(tileTexWidth * 0.5f, tileTexHeight * 0.5f);
                    var cellMin = tilemap.GetCellCenterWorld(position) - cellScale * 0.5f;
                    var pxOffset = Mathf.RoundToInt((cellMin.x - boundsMin.x) / boundsSize.x * imageWidth);
                    var pyOffset = Mathf.RoundToInt((cellMin.y - boundsMin.y) / boundsSize.y * imageHeight);
                    pxOffset = Mathf.Max(0, pxOffset);
                    pyOffset = Mathf.Max(0, pyOffset);
                    var cellCen = new Vector2(pixelsPerCellHorizontal * 0.5f, pixelsPerCellVertical * 0.5f);
                    Parallel.For(0, pixelsPerCellHorizontal, px =>
                    {
                        Parallel.For(0, pixelsPerCellVertical, py =>
                        {
                            var pixel = baseColor;
                            var diffX = cellCen.x - px;
                            var diffY = cellCen.y - py;
                            if (Mathf.Abs(diffX) <= tileCen.x && Mathf.Abs(diffY) <= tileCen.y)
                            {
                                var cellPx = tileCen.x - diffX;
                                var cellPy = tileCen.y - diffY;
                                if (cellPx >= 0 && cellPx < tileTexWidth && cellPy >= 0 && cellPy < tileTexHeight)
                                {
                                    int sx = (int) cellPx, sy = (int) cellPy;
                                    if (!spriteShrinked)
                                    {
                                        sx = (int) (sx + rect.x);
                                        sy = (int) (sy + rect.y);
                                        pixel = tileColors[sx + sy * atlasTexWidth];
                                    }
                                    else
                                    {
                                        pixel = tileColors[sx + sy * tileTexWidth];
                                    }

                                    if (pixel.a > 0) pixel.a = 1;
                                }
                            }

                            int ix = px + pxOffset, iy = py + pyOffset;
                            var index = ix + iy * imageWidth;
                            if (index < numPixels && index >= 0)
                            {
                                var col = result[index];
                                var alpha = pixel.a;
                                var blend = alpha * pixel + (1 - alpha) * col;
                                result[index] = blend;
                            }
                        });
                    });
                }
            }

            image.SetPixels(result);
            image.Apply();
            UnityEditor.EditorUtility.ClearProgressBar();
            return image;
        }

        public static Texture2D GenerateTexture(Tilemap tilemap, Bounds boundsInWorld, int pixelsPerUnit = 100,
            float tilescale = -1f)
        {
            var cellSize = tilemap.cellSize;
            var cellBounds = tilemap.cellBounds;
            var tilemapScale = tilemap.transform.localScale;
            var cellScale = new Vector3(cellSize.x * tilemapScale.x, cellSize.y * tilemapScale.y,
                cellSize.z * tilemapScale.z);
            if (tilescale > 0) cellScale *= tilescale;
            var boundsSize = boundsInWorld.size;
            var imageWidth = LargerPowerOf2(Mathf.CeilToInt(boundsSize.x * pixelsPerUnit));
            var imageHeight = LargerPowerOf2(Mathf.CeilToInt(boundsSize.y * pixelsPerUnit));
            boundsSize = new Vector3(imageWidth * 1f / pixelsPerUnit, imageHeight * 1f / pixelsPerUnit);
            var boundsMin = boundsInWorld.center - boundsSize / 2;
            var baseColor = (Color) default;
            var image = new Texture2D(imageWidth, imageHeight, TextureFormat.ARGB32, false);
            var numPixels = imageWidth * imageHeight;
            var result = new Color[numPixels];
            Parallel.For(0, numPixels, i => result[i] = baseColor);
            var pixelsPerCell = cellScale * pixelsPerUnit;
            var pixelsPerCellHorizontal = Mathf.CeilToInt(pixelsPerCell.x);
            var pixelsPerCellVertical = Mathf.CeilToInt(pixelsPerCell.y);
            var cnt = 0;
            var size = cellBounds.size;
            for (var x = cellBounds.min.x; x <= cellBounds.max.x; x++)
            {
                for (var y = cellBounds.min.y; y <= cellBounds.max.y; y++)
                {
                    var position = new Vector3Int(x, y, 0);
                    Sprite sprite;
                    if (!(sprite = tilemap.GetSprite(position)))
                        if (tilemap.GetTile(position) is Tile tile)
                            sprite = tile.sprite;
                    if (sprite == null) continue;
                    var progress = ++cnt * 1f / (size.x * size.y);
                    if (UnityEditor.EditorUtility.DisplayCancelableProgressBar("Hold On",
                        $"Handle {position} {sprite.name}...({Mathf.FloorToInt(progress * 100)}%)",
                        0.1f + 0.9f * progress))
                        return null;
                    var scale = pixelsPerUnit / sprite.pixelsPerUnit;
                    var atlasTexture = sprite.texture;
                    var tileTexture = Math.Abs(pixelsPerUnit - sprite.pixelsPerUnit) < float.Epsilon
                        ? atlasTexture
                        : ShrinkImage(atlasTexture, scale, scale);
                    var rect = sprite.rect;
                    var atlasTexWidth = atlasTexture.width;
                    int tileTexWidth = (int) rect.width, tileTexHeight = (int) rect.height;
                    var spriteShrinked = false;
                    if (tilescale > 0)
                    {
                        spriteShrinked = true;
                        tileTexture = ShrinkSprite(sprite, pixelsPerCellHorizontal * 1f / tileTexWidth,
                            pixelsPerCellVertical * 1f / tileTexHeight);
                        tileTexWidth = tileTexture.width;
                        tileTexHeight = tileTexture.height;
                    }

                    var tileColors = tileTexture.GetPixels();
                    var tileCen = new Vector2(tileTexWidth * 0.5f, tileTexHeight * 0.5f);
                    var cellMin = tilemap.GetCellCenterWorld(position) - cellScale * 0.5f;
                    var pxOffset = Mathf.RoundToInt((cellMin.x - boundsMin.x) / boundsSize.x * imageWidth);
                    var pyOffset = Mathf.RoundToInt((cellMin.y - boundsMin.y) / boundsSize.y * imageHeight);
                    pxOffset = Mathf.Max(0, pxOffset);
                    pyOffset = Mathf.Max(0, pyOffset);
                    var cellCen = new Vector2(pixelsPerCellHorizontal * 0.5f, pixelsPerCellVertical * 0.5f);
                    Parallel.For(0, pixelsPerCellHorizontal, px =>
                    {
                        Parallel.For(0, pixelsPerCellVertical, py =>
                        {
                            var pixel = baseColor;
                            var diffX = cellCen.x - px;
                            var diffY = cellCen.y - py;
                            if (Mathf.Abs(diffX) <= tileCen.x && Mathf.Abs(diffY) <= tileCen.y)
                            {
                                var cellPx = tileCen.x - diffX;
                                var cellPy = tileCen.y - diffY;
                                if (cellPx >= 0 && cellPx < tileTexWidth && cellPy >= 0 && cellPy < tileTexHeight)
                                {
                                    int sx = (int) cellPx, sy = (int) cellPy;
                                    if (!spriteShrinked)
                                    {
                                        sx = (int) (sx + rect.x);
                                        sy = (int) (sy + rect.y);
                                        pixel = tileColors[sx + sy * atlasTexWidth];
                                    }
                                    else
                                    {
                                        pixel = tileColors[sx + sy * tileTexWidth];
                                    }

                                    if (pixel.a > 0) pixel.a = 1;
                                }
                            }

                            int ix = px + (int) pxOffset, iy = py + (int) pyOffset;
                            var index = ix + iy * imageWidth;
                            if (index < numPixels && index >= 0)
                            {
                                var col = result[index];
                                var alpha = pixel.a;
                                var blend = alpha * pixel + (1 - alpha) * col;
                                result[index] = blend;
                            }
                        });
                    });
                }
            }

            image.SetPixels(result);
            image.Apply();
            UnityEditor.EditorUtility.ClearProgressBar();
            return image;
        }

        public static void Write2TargetPixels(Tilemap tilemap, Bounds boundsInWorld, Color[] targetPixels,
            int pixelsPerUnit = 100, float tilescale = -1f)
        {
            var cellSize = tilemap.cellSize;
            var cellBounds = tilemap.cellBounds;
            var tilemapScale = tilemap.transform.localScale;
            var cellScale = new Vector3(cellSize.x * tilemapScale.x, cellSize.y * tilemapScale.y,
                cellSize.z * tilemapScale.z);
            if (tilescale > 0) cellScale *= tilescale;
            var boundsSize = boundsInWorld.size;
            var imageWidth = LargerPowerOf2(Mathf.CeilToInt(boundsSize.x * pixelsPerUnit));
            var imageHeight = LargerPowerOf2(Mathf.CeilToInt(boundsSize.y * pixelsPerUnit));
            boundsSize = new Vector3(imageWidth * 1f / pixelsPerUnit, imageHeight * 1f / pixelsPerUnit);
            var boundsMin = boundsInWorld.center - boundsSize / 2;
            var baseColor = (Color) default;
            var numPixels = imageWidth * imageHeight;
            var pixelsPerCell = cellScale * pixelsPerUnit;
            var pixelsPerCellHorizontal = Mathf.CeilToInt(pixelsPerCell.x);
            var pixelsPerCellVertical = Mathf.CeilToInt(pixelsPerCell.y);
            var cnt = 0;
            var size = cellBounds.size;
            for (var x = cellBounds.min.x; x <= cellBounds.max.x; x++)
            {
                for (var y = cellBounds.min.y; y <= cellBounds.max.y; y++)
                {
                    var position = new Vector3Int(x, y, 0);
                    Sprite sprite;
                    if (!(sprite = tilemap.GetSprite(position)))
                        if (tilemap.GetTile(position) is Tile tile)
                            sprite = tile.sprite;
                    if (sprite == null) continue;
                    var progress = ++cnt * 1f / (size.x * size.y);
                    if (UnityEditor.EditorUtility.DisplayCancelableProgressBar("Hold On",
                        $"Handle {position} {sprite.name}...({Mathf.FloorToInt(progress * 100)}%)",
                        0.1f + 0.9f * progress))
                        return;
                    var scale = pixelsPerUnit / sprite.pixelsPerUnit;
                    var atlasTexture = sprite.texture;
                    var tileTexture = Math.Abs(pixelsPerUnit - sprite.pixelsPerUnit) < float.Epsilon
                        ? atlasTexture
                        : ShrinkImage(atlasTexture, scale, scale);
                    var rect = sprite.rect;
                    var atlasTexWidth = atlasTexture.width;
                    int tileTexWidth = (int) rect.width, tileTexHeight = (int) rect.height;
                    var spriteShrinked = false;
                    if (tilescale > 0)
                    {
                        spriteShrinked = true;
                        tileTexture = ShrinkSprite(sprite, pixelsPerCellHorizontal * 1f / tileTexWidth,
                            pixelsPerCellVertical * 1f / tileTexHeight);
                        tileTexWidth = tileTexture.width;
                        tileTexHeight = tileTexture.height;
                    }

                    var tileColors = tileTexture.GetPixels();
                    var tileCen = new Vector2(tileTexWidth * 0.5f, tileTexHeight * 0.5f);
                    var cellMin = tilemap.GetCellCenterWorld(position) - cellScale * 0.5f;
                    var pxOffset = Mathf.RoundToInt((cellMin.x - boundsMin.x) / boundsSize.x * imageWidth);
                    var pyOffset = Mathf.RoundToInt((cellMin.y - boundsMin.y) / boundsSize.y * imageHeight);
                    pxOffset = Mathf.Max(0, pxOffset);
                    pyOffset = Mathf.Max(0, pyOffset);
                    var cellCen = new Vector2(pixelsPerCellHorizontal * 0.5f, pixelsPerCellVertical * 0.5f);
                    Parallel.For(0, pixelsPerCellHorizontal, px =>
                    {
                        Parallel.For(0, pixelsPerCellVertical, py =>
                        {
                            var pixel = baseColor;
                            var diffX = cellCen.x - px;
                            var diffY = cellCen.y - py;
                            if (Mathf.Abs(diffX) <= tileCen.x && Mathf.Abs(diffY) <= tileCen.y)
                            {
                                var cellPx = tileCen.x - diffX;
                                var cellPy = tileCen.y - diffY;
                                if (cellPx >= 0 && cellPx < tileTexWidth && cellPy >= 0 && cellPy < tileTexHeight)
                                {
                                    int sx = (int) cellPx, sy = (int) cellPy;
                                    if (!spriteShrinked)
                                    {
                                        sx = (int) (sx + rect.x);
                                        sy = (int) (sy + rect.y);
                                        pixel = tileColors[sx + sy * atlasTexWidth];
                                    }
                                    else
                                    {
                                        pixel = tileColors[sx + sy * tileTexWidth];
                                    }

                                    if (pixel.a > 0) pixel.a = 1;
                                }
                            }

                            int ix = px + (int) pxOffset, iy = py + (int) pyOffset;
                            var index = ix + iy * imageWidth;
                            if (index < numPixels && index >= 0)
                            {
                                var col = targetPixels[index];
                                var alpha = pixel.a;
                                var blend = alpha * pixel + (1 - alpha) * col;
                                targetPixels[index] = blend;
                            }
                        });
                    });
                }
            }
        }

        public static Texture2D GenerateTexture(IList<Tilemap> tilemaps, int pixelsPerUnit = 100, float tilescale = -1,
            float textureExtents = 0f)
        {
            tilemaps.Sort((x, y) => x.GetComponent<Renderer>().sortingOrder - y.GetComponent<Renderer>().sortingOrder);
            var length = tilemaps.Count;
            var boundsInWorld = new Bounds(Vector3.negativeInfinity, Vector3.negativeInfinity);
            for (var i = length - 1; i >= 0; i--)
            {
                var tilemapBounds = GetTilemapBoundsInWorld(tilemaps[i], tilescale, textureExtents);
                if (Mathf.Approximately(tilemapBounds.size.x * tilemapBounds.size.y, 0))
                {
                    Debug.LogError($"Remove None Tile Tilemap {tilemaps[i].gameObject.name}");
                    tilemaps.RemoveAt(i);
                    continue;
                }

                boundsInWorld.Encapsulate(tilemapBounds);
            }

            var boundsSize = boundsInWorld.size;
            var imageWidth = LargerPowerOf2(Mathf.CeilToInt(boundsSize.x * pixelsPerUnit));
            var imageHeight = LargerPowerOf2(Mathf.CeilToInt(boundsSize.y * pixelsPerUnit));
            var image = new Texture2D(imageWidth, imageHeight, TextureFormat.ARGB32, false);
            var pixels = new Color[imageWidth * imageHeight];
            Debug.LogError($"Tilemaps Combine to texture W:{imageWidth},H:{imageHeight}");
            foreach (var tilemap in tilemaps)
                Write2TargetPixels(tilemap, boundsInWorld, pixels, pixelsPerUnit, tilescale);
            image.SetPixels(pixels);
            image.Apply();
            EditorUtility.ClearProgressBar();
            return image;
        }

        public static Bounds GetTilemapBoundsInWorld(Tilemap tilemap, float tilescale = -1f, float textureExtents = 0f)
        {
            var cellSize = tilemap.cellSize;
            var cellBounds = tilemap.cellBounds;
            var tilemapScale = tilemap.transform.localScale;
            var cellScale = new Vector3(cellSize.x * tilemapScale.x, cellSize.y * tilemapScale.y,
                cellSize.z * tilemapScale.z);
            if (tilescale > 0) cellScale *= tilescale;
            var boundsInWorld = new Bounds(Vector3.negativeInfinity, Vector3.negativeInfinity);
            for (var x = cellBounds.min.x; x < cellBounds.max.x; x++)
            {
                for (var y = cellBounds.min.y; y < cellBounds.max.y; y++)
                {
                    var cellPosition = new Vector3Int(x, y, 0);
                    if (tilemap.HasTile(cellPosition))
                    {
                        var worldPosition = tilemap.GetCellCenterWorld(cellPosition);
                        var matrix = Matrix4x4.TRS(worldPosition, Quaternion.identity, cellScale);
                        foreach (var vertex in HexagonalPointedTopVertices)
                            boundsInWorld.Encapsulate(matrix.MultiplyPoint(vertex));
                    }
                }
            }

            boundsInWorld.size *= 1 + textureExtents;
            return boundsInWorld;
        }

        private static int LargerPowerOf2(int num)
        {
            num |= num >> 1;
            num |= num >> 2;
            num |= num >> 4;
            num |= num >> 8;
            num |= num >> 16;
            return num + 1;
        }

        private static Texture2D ShrinkImage(Texture2D texture, float scaleX, float scaleY)
        {
            int width = texture.width, height = texture.height;
            float newWidth = width * scaleX, newHeight = height * scaleY;
            var image = new Texture2D((int) newWidth, (int) newHeight, TextureFormat.ARGB32, false);
            var pixels = PictureUtils.Shrink(width, height, texture.GetPixels(), scaleX, scaleY);
            image.SetPixels(pixels);
            return image;
        }

        private static Texture2D ShrinkSprite(Sprite sprite, float scaleX, float scaleY)
        {
            var rect = sprite.rect;
            int width = (int) rect.width, height = (int) rect.height;
            float newWidth = width * scaleX, newHeight = height * scaleY;
            var image = new Texture2D((int) newWidth, (int) newHeight, TextureFormat.ARGB32, false);
            var pixels = PictureUtils.Shrink(width, height,
                sprite.texture.GetPixels((int) rect.x, (int) rect.y, width, height), scaleX, scaleY);
            image.SetPixels(pixels);
            return image;
        }
    }
}
#endif