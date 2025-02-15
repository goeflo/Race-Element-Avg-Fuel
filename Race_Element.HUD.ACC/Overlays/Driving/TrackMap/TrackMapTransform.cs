﻿
using System;
using System.Collections.Generic;

namespace RaceElement.HUD.ACC.Overlays.Driving.TrackMap;

public static class TrackMapTransform
{
    public static BoundingBox GetBoundingBox(List<TrackPoint> positions)
    {
        BoundingBox result = new();

        if (positions.Count > 0)
        {
            result.Left = positions[0].X;
            result.Right = positions[0].X;

            result.Top = positions[0].Y;
            result.Bottom = positions[0].Y;
        }

        foreach (var it in positions)
        {
            result.Left = Math.Min(result.Left, it.X);
            result.Right = Math.Max(result.Right, it.X);

            result.Top = Math.Max(result.Top, it.Y);
            result.Bottom = Math.Min(result.Bottom, it.Y);
        }

        return result;
    }

    public static TrackPoint ScaleAndRotate(TrackPoint point, BoundingBox boundaries, float scale, float rotation)
    {
        TrackPoint pos = new(point);
        var rot = Double.DegreesToRadians(rotation);

        var centerX = (boundaries.Right + boundaries.Left) * 0.5f;
        var centerY = (boundaries.Top + boundaries.Bottom) * 0.5f;

        var xScale = (point.X - centerX) * scale;
        var yScale = (point.Y - centerY) * scale;

        var xScaleAndRot = xScale * Math.Cos(rot) - yScale * Math.Sin(rot);
        var yScaleAndRot = xScale * Math.Sin(rot) + yScale * Math.Cos(rot);

        pos.X = (float)xScaleAndRot;
        pos.Y = (float)yScaleAndRot;

        return pos;
    }

    public static List<TrackPoint> ScaleAndRotate(List<TrackPoint> positions, BoundingBox boundaries, float scale, float rotation)
    {
        List<TrackPoint> result = new();

        foreach (var it in positions)
        {
            var pos = ScaleAndRotate(it, boundaries, scale, rotation);
            result.Add(pos);
        }

        return result;
    }
}
