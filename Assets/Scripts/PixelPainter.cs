using System.Collections.Generic;
using UnityEngine;

namespace TankBattle
{
    // 以画布坐标作画的像素画笔：原点在纹理中心，y 轴向下（与网页版 Canvas 一致）
    public class PixelPainter
    {
        public readonly int W;
        public readonly int H;
        public readonly Color32[] Buf;

        public PixelPainter(int w, int h)
        {
            W = w;
            H = h;
            Buf = new Color32[w * h];
        }

        public void SetPixel(int ix, int iy, Color c)
        {
            if (ix < 0 || iy < 0 || ix >= W || iy >= H) return;
            int idx = iy * W + ix;

            if (c.a <= 0.0001f)
            {
                Buf[idx] = new Color32(0, 0, 0, 0);
                return;
            }

            Color32 d = Buf[idx];
            float da = d.a / 255f;
            float sa = c.a;
            float oa = sa + da * (1f - sa);
            if (oa <= 0.0001f)
            {
                Buf[idx] = new Color32(0, 0, 0, 0);
                return;
            }

            float r = (c.r * sa + (d.r / 255f) * da * (1f - sa)) / oa;
            float g = (c.g * sa + (d.g / 255f) * da * (1f - sa)) / oa;
            float b = (c.b * sa + (d.b / 255f) * da * (1f - sa)) / oa;

            Buf[idx] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(b * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(oa * 255f), 0, 255));
        }

        // 画布坐标（中心原点，y 向下）→ 纹理像素
        public void Set(float x, float y, Color c)
        {
            int ix = Mathf.FloorToInt(W * 0.5f + x);
            int iy = Mathf.FloorToInt(H * 0.5f - y);
            SetPixel(ix, iy, c);
        }

        public void FillRect(float x, float y, float w, float h, Color c)
        {
            int x0 = Mathf.FloorToInt(W * 0.5f + x);
            int x1 = Mathf.CeilToInt(W * 0.5f + x + w) - 1;
            int y0 = Mathf.FloorToInt(H * 0.5f - (y + h));
            int y1 = Mathf.CeilToInt(H * 0.5f - y) - 1;
            for (int iy = y0; iy <= y1; iy++)
            {
                for (int ix = x0; ix <= x1; ix++)
                {
                    SetPixel(ix, iy, c);
                }
            }
        }

        public void FillCircle(float cx, float cy, float r, Color c)
        {
            int x0 = Mathf.FloorToInt(W * 0.5f + cx - r) - 1;
            int x1 = Mathf.CeilToInt(W * 0.5f + cx + r) + 1;
            int y0 = Mathf.FloorToInt(H * 0.5f - cy - r) - 1;
            int y1 = Mathf.CeilToInt(H * 0.5f - cy + r) + 1;
            float r2 = r * r;

            for (int iy = y0; iy <= y1; iy++)
            {
                float py = H * 0.5f - (iy + 0.5f);
                float dy = py - cy;
                for (int ix = x0; ix <= x1; ix++)
                {
                    float px = ix + 0.5f - W * 0.5f;
                    float dx = px - cx;
                    if (dx * dx + dy * dy <= r2) SetPixel(ix, iy, c);
                }
            }
        }

        public void StrokeCircle(float cx, float cy, float r, float thick, Color c)
        {
            float outer = r + thick * 0.5f;
            float inner = r - thick * 0.5f;
            int x0 = Mathf.FloorToInt(W * 0.5f + cx - outer) - 1;
            int x1 = Mathf.CeilToInt(W * 0.5f + cx + outer) + 1;
            int y0 = Mathf.FloorToInt(H * 0.5f - cy - outer) - 1;
            int y1 = Mathf.CeilToInt(H * 0.5f - cy + outer) + 1;

            for (int iy = y0; iy <= y1; iy++)
            {
                float py = H * 0.5f - (iy + 0.5f);
                float dy = py - cy;
                for (int ix = x0; ix <= x1; ix++)
                {
                    float px = ix + 0.5f - W * 0.5f;
                    float dx = px - cx;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d >= inner && d <= outer) SetPixel(ix, iy, c);
                }
            }
        }

        public void FillEllipse(float cx, float cy, float rx, float ry, Color c)
        {
            int x0 = Mathf.FloorToInt(W * 0.5f + cx - rx) - 1;
            int x1 = Mathf.CeilToInt(W * 0.5f + cx + rx) + 1;
            int y0 = Mathf.FloorToInt(H * 0.5f - cy - ry) - 1;
            int y1 = Mathf.CeilToInt(H * 0.5f - cy + ry) + 1;

            for (int iy = y0; iy <= y1; iy++)
            {
                float py = H * 0.5f - (iy + 0.5f);
                float dy = (py - cy) / ry;
                for (int ix = x0; ix <= x1; ix++)
                {
                    float px = ix + 0.5f - W * 0.5f;
                    float dx = (px - cx) / rx;
                    if (dx * dx + dy * dy <= 1f) SetPixel(ix, iy, c);
                }
            }
        }

        public void Line(float x0, float y0, float x1, float y1, float thick, Color c)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 2f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                FillCircle(x0 + dx * t, y0 + dy * t, Mathf.Max(0.6f, thick * 0.5f), c);
            }
        }

        // 扫描线填充（支持凹多边形，如五角星）
        public void FillPoly(Vector2[] pts, Color c)
        {
            if (pts == null || pts.Length < 3) return;

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < pts.Length; i++)
            {
                if (pts[i].y < minY) minY = pts[i].y;
                if (pts[i].y > maxY) maxY = pts[i].y;
            }

            var xs = new List<float>();
            int yStart = Mathf.FloorToInt(minY);
            int yEnd = Mathf.CeilToInt(maxY);

            for (int y = yStart; y <= yEnd; y++)
            {
                float sy = y + 0.5f;
                xs.Clear();
                for (int i = 0; i < pts.Length; i++)
                {
                    Vector2 a = pts[i];
                    Vector2 b = pts[(i + 1) % pts.Length];
                    if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy))
                    {
                        float t = (sy - a.y) / (b.y - a.y);
                        xs.Add(a.x + (b.x - a.x) * t);
                    }
                }
                if (xs.Count < 2) continue;
                xs.Sort();
                for (int i = 0; i + 1 < xs.Count; i += 2)
                {
                    float xa = xs[i];
                    float xb = xs[i + 1];
                    int ix0 = Mathf.FloorToInt(W * 0.5f + xa);
                    int ix1 = Mathf.CeilToInt(W * 0.5f + xb);
                    for (int ix = ix0; ix <= ix1; ix++)
                    {
                        int iy = Mathf.FloorToInt(H * 0.5f - sy);
                        SetPixel(ix, iy, c);
                    }
                }
            }
        }

        public void RadialGlow(float cx, float cy, float radius, Color center, Color edge)
        {
            int x0 = Mathf.FloorToInt(W * 0.5f + cx - radius) - 1;
            int x1 = Mathf.CeilToInt(W * 0.5f + cx + radius) + 1;
            int y0 = Mathf.FloorToInt(H * 0.5f - cy - radius) - 1;
            int y1 = Mathf.CeilToInt(H * 0.5f - cy + radius) + 1;

            for (int iy = y0; iy <= y1; iy++)
            {
                float py = H * 0.5f - (iy + 0.5f);
                for (int ix = x0; ix <= x1; ix++)
                {
                    float px = ix + 0.5f - W * 0.5f;
                    float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    if (d > radius) continue;
                    float t = d / radius;
                    SetPixel(ix, iy, Color.Lerp(center, edge, t));
                }
            }
        }
    }
}
