using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Configuration;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

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
        var boundsInWorld = GetTilemapBoundsInWorld(tilemap, tilescale, textureExtents);
        var boundsSize = boundsInWorld.size;
        var width = LargerPowerOf2(Mathf.CeilToInt(boundsSize.x * pixelsPerUnit));
        var height = LargerPowerOf2(Mathf.CeilToInt(boundsSize.y * pixelsPerUnit));
        var image = new Texture2D(width, height, TextureFormat.ARGB32, false);
        var pixels = new Color[width * height];
        boundsInWorld.size = new Vector3(width * 1f / pixelsPerUnit, height * 1f / pixelsPerUnit, 0);
        Write2TargetPixels(tilemap, boundsInWorld, pixels, pixelsPerUnit, tilescale);
        image.SetPixels(pixels);
        image.Apply();
        return image;
    }

    public static Texture2D GenerateTexture(Tilemap tilemap, Bounds boundsInWorld, int pixelsPerUnit = 100,
        float tilescale = -1f)
    {
        var boundsSize = boundsInWorld.size;
        var width = LargerPowerOf2(Mathf.CeilToInt(boundsSize.x * pixelsPerUnit));
        var height = LargerPowerOf2(Mathf.CeilToInt(boundsSize.y * pixelsPerUnit));
        var image = new Texture2D(width, height, TextureFormat.ARGB32, false);
        var pixels = new Color[width * height];
        boundsInWorld.size = new Vector3(width * 1f / pixelsPerUnit, height * 1f / pixelsPerUnit, 0);
        Write2TargetPixels(tilemap, boundsInWorld, pixels, pixelsPerUnit, tilescale);
        image.SetPixels(pixels);
        image.Apply();
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
        var imageWidth = Mathf.CeilToInt(boundsSize.x * pixelsPerUnit);
        var imageHeight = Mathf.CeilToInt(boundsSize.y * pixelsPerUnit);
        var boundsMin = boundsInWorld.min;
        var baseColor = (Color) default;
        var numPixels = imageWidth * imageHeight;
        var pixelsPerCell = cellScale * pixelsPerUnit;
        var pixelsPerCellHorizontal = Mathf.CeilToInt(pixelsPerCell.x);
        var pixelsPerCellVertical = Mathf.CeilToInt(pixelsPerCell.y);
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
                            // var col = targetPixels[index];
                            // var alpha = pixel.a;
                            // var blend = alpha * pixel + (1 - alpha) * col;
                            // targetPixels[index] = blend;
                            
                            var col = targetPixels[index];
                            // Blend SrcAlpha OneMinusSrcAlpha
                            // var blend = pixel.a * pixel + (1 - pixel.a) * col;
                            var oneMinusSrcAlpha = 1 - pixel.a;
                            col.r = pixel.a * pixel.r + oneMinusSrcAlpha * col.r;
                            col.g = pixel.a * pixel.g + oneMinusSrcAlpha * col.g;
                            col.b = pixel.a * pixel.b + oneMinusSrcAlpha * col.b;
                            col.a = Mathf.Max(col.a, pixel.a);
                            targetPixels[index] = col;
                        }
                    });
                });
            }
        }
    }

    public static void Write2TargetPixels(SpriteRenderer renderer, Bounds boundsInWorld, Color[] targetPixels,
        int pixelsPerUnit = 100)
    {
        if (renderer == null) return;
        var sprite = renderer.sprite;
        var spriteTexture = sprite.texture;
        var spriteWidth = spriteTexture.width;
        var spriteHeight = spriteTexture.height;
        var spriteObject = renderer.gameObject;
        var transform = spriteObject.transform;
        var spriteBoundsInWorld = renderer.bounds;
        var scale = transform.localScale;
        var angle = transform.eulerAngles.z;
        var spriteRect = sprite.rect;
        int scaleSignX = scale.x >= 0 ? 1 : -1, scaleSignY = scale.y >= 0 ? 1 : -1;
        var shrink = Math.Abs(scale.x - 1) > float.Epsilon ||
                     Math.Abs(scale.y - 1) > float.Epsilon;
        var srcPixels = spriteTexture.GetPixels((int) spriteRect.x, (int) spriteRect.y, (int) spriteRect.width,
            (int) spriteRect.height);
        if (scaleSignX < 0 || scaleSignY < 0 || shrink)
        {
            var pixels = (Color[]) default;
            if (shrink)
            {
                var newWidth = Mathf.FloorToInt((float) spriteWidth * scale.x * scaleSignX);
                var newHeight = Mathf.FloorToInt((float) spriteHeight * scale.y * scaleSignY);
                pixels = PictureUtils.Shrink(spriteWidth, spriteHeight, srcPixels, scale.x, scale.y);
                spriteTexture = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
                spriteWidth = newWidth;
                spriteHeight = newHeight;
            }
            else
            {
                pixels = srcPixels;
                spriteTexture = new Texture2D(spriteWidth, spriteHeight, TextureFormat.RGBA32, false);
            }

            for (int px = 0; px < spriteWidth; px++)
            {
                for (int py = 0; py < spriteHeight; py++)
                {
                    spriteTexture.SetPixel(scaleSignX < 0 ? spriteWidth - px : px,
                        scaleSignY < 0 ? spriteHeight - py : py, pixels[px + py * spriteWidth]);
                }
            }

            spriteTexture.Apply();
        }

        var width = Mathf.CeilToInt(boundsInWorld.size.x * pixelsPerUnit);
        var height = Mathf.CeilToInt(boundsInWorld.size.y * pixelsPerUnit);
        var offsetX =
            Mathf.FloorToInt(
                (spriteBoundsInWorld.min.x - boundsInWorld.min.x) / boundsInWorld.size.x * width);
        var offsetY =
            Mathf.FloorToInt(
                (spriteBoundsInWorld.min.y - boundsInWorld.min.y) / boundsInWorld.size.y * height);

        var spritePixels = (Color[]) default;
        if (Mathf.Abs(angle) > float.Epsilon)
        {
            spriteTexture = PictureUtils.RotateTexture(spriteTexture, angle);
            spriteWidth = spriteTexture.width;
            spriteHeight = spriteTexture.height;
            spritePixels = spriteTexture.GetPixels();
        }
        else
        {
            spritePixels = shrink
                ? spriteTexture.GetPixels()
                : srcPixels;
        }

        Parallel.For(0, spriteWidth, x =>
        {
            Parallel.For(0, spriteHeight, y =>
            {
                var pixel = spritePixels[x + y * spriteWidth];
                var px = x + offsetX;
                var py = y + offsetY;
                var index = px + py * width;
                if (index >= 0 && index < targetPixels.Length)
                {
                    var col = targetPixels[index];
                    // Blend SrcAlpha OneMinusSrcAlpha
                    // var blend = pixel.a * pixel + (1 - pixel.a) * col;
                    var oneMinusSrcAlpha = 1 - pixel.a;
                    col.r = pixel.a * pixel.r + oneMinusSrcAlpha * col.r;
                    col.g = pixel.a * pixel.g + oneMinusSrcAlpha * col.g;
                    col.b = pixel.a * pixel.b + oneMinusSrcAlpha * col.b;
                    col.a = Mathf.Max(col.a, pixel.a);
                    targetPixels[index] = col;
                }
            });
        });
    }

    public static Texture2D GenerateTexture(List<Tilemap> tilemaps, int pixelsPerUnit = 100, float tilescale = -1,
        float textureExtents = 0f)
    {
        int CompareSortingOrder(Renderer x, Renderer y) => x.sortingOrder - y.sortingOrder;
        tilemaps.Sort((x, y) => CompareSortingOrder(x.GetComponent<Renderer>(), y.GetComponent<Renderer>()));
        var length = tilemaps.Count;

        var boundsInWorld = new Bounds(Vector3.negativeInfinity, Vector3.negativeInfinity);
        for (var i = length - 1; i >= 0; i--)
        {
            var tilemapBounds = GetTilemapBoundsInWorld(tilemaps[i], tilescale, textureExtents);
            if (Mathf.Approximately(tilemapBounds.size.x * tilemapBounds.size.y, 0))
            {
                tilemaps.RemoveAt(i);
                continue;
            }

            boundsInWorld.Encapsulate(tilemapBounds);
        }

        var spriteRenderers = Object.FindObjectsOfType<SpriteRenderer>().ToList();
        spriteRenderers.Sort(CompareSortingOrder);
        foreach (var spriteRenderer in spriteRenderers)
            boundsInWorld.Encapsulate(spriteRenderer.bounds);
        var boundsSize = boundsInWorld.size;
        var width = Mathf.CeilToInt(boundsSize.x * pixelsPerUnit);
        var height = Mathf.CeilToInt(boundsSize.y * pixelsPerUnit);
        var image = new Texture2D(width, height, TextureFormat.ARGB32, false);
        var pixels = new Color[width * height];
        boundsInWorld.size = new Vector3(width * 1f / pixelsPerUnit, height * 1f / pixelsPerUnit, 0);
        Debug.LogError($"Tilemaps Combine to texture W:{width},H:{height}");
        foreach (var tilemap in tilemaps)
            Write2TargetPixels(tilemap, boundsInWorld, pixels, pixelsPerUnit, tilescale);
        foreach (var spriteRenderer in spriteRenderers)
            Write2TargetPixels(spriteRenderer, boundsInWorld, pixels, pixelsPerUnit);
        image.SetPixels(pixels);
        image.Apply();
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

        var boundsInWorld = new Bounds(Vector3.negativeInfinity, Vector3.zero);
        for (var x = cellBounds.min.x;
            x < cellBounds.max.x;
            x++)
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