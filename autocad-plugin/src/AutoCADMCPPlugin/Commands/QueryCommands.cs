using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AutoCADMCPPlugin.Models;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using static AutoCADMCPPlugin.Commands.EntityHelper;

namespace AutoCADMCPPlugin.Commands
{
    public class MeasureDistanceCommand : ICommand
    {
        public string MethodName => "measure_distance";

        public CommandResult Execute(JObject parameters)
        {
            Point3d pt1 = ParsePoint(parameters["point1"], "point1");
            Point3d pt2 = ParsePoint(parameters["point2"], "point2");

            double dx = pt2.X - pt1.X;
            double dy = pt2.Y - pt1.Y;
            double distance = pt1.DistanceTo(pt2);
            double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

            return CommandResult.Ok(new JObject
            {
                ["distance"] = distance,
                ["dx"] = dx,
                ["dy"] = dy,
                ["angle"] = angle
            });
        }
    }

    public class MeasureAreaCommand : ICommand
    {
        public string MethodName => "measure_area";

        public CommandResult Execute(JObject parameters)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return CommandResult.Fail("No active document");

            string handle = parameters["handle"]?.ToString();
            if (string.IsNullOrEmpty(handle))
                return CommandResult.Fail("Parameter 'handle' is required");

            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Handle h = new Handle(Convert.ToInt64(handle));
                if (!db.TryGetObjectId(h, out ObjectId id))
                    return CommandResult.Fail($"Entity not found: {handle}");

                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null)
                    return CommandResult.Fail($"Object {handle} is not an entity");

                double area = 0, perimeter = 0;
                string typeName = ent.GetType().Name;

                if (ent is Polyline pline)
                {
                    if (!pline.Closed)
                        return CommandResult.Fail("Polyline must be closed to measure area");
                    area = pline.Area;
                    perimeter = pline.Length;
                }
                else if (ent is Circle circle)
                {
                    area = circle.Area;
                    perimeter = 2 * Math.PI * circle.Radius;
                }
                else if (ent is Ellipse ellipse)
                {
                    area = ellipse.Area;
                    // Approximate perimeter using Ramanujan's formula
                    double a = ellipse.MajorRadius, b = ellipse.MinorRadius;
                    perimeter = Math.PI * (3 * (a + b) - Math.Sqrt((3 * a + b) * (a + 3 * b)));
                }
                else if (ent is Hatch hatch)
                {
                    area = hatch.Area;
                }
                else
                {
                    return CommandResult.Fail($"Cannot measure area of {typeName}. Use closed polyline, circle, ellipse, or hatch.");
                }

                tr.Commit();
                return CommandResult.Ok(new JObject
                {
                    ["area"] = area,
                    ["perimeter"] = perimeter,
                    ["type"] = typeName
                });
            }
        }
    }

    public class GetBoundingBoxCommand : ICommand
    {
        public string MethodName => "get_bounding_box";

        public CommandResult Execute(JObject parameters)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return CommandResult.Fail("No active document");

            string handle = parameters["handle"]?.ToString();
            if (string.IsNullOrEmpty(handle))
                return CommandResult.Fail("Parameter 'handle' is required");

            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Handle h = new Handle(Convert.ToInt64(handle));
                if (!db.TryGetObjectId(h, out ObjectId id))
                    return CommandResult.Fail($"Entity not found: {handle}");

                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null)
                    return CommandResult.Fail($"Object {handle} is not an entity");

                Extents3d ext = ent.GeometricExtents;
                tr.Commit();

                return CommandResult.Ok(new JObject
                {
                    ["min_point"] = new JArray(ext.MinPoint.X, ext.MinPoint.Y, ext.MinPoint.Z),
                    ["max_point"] = new JArray(ext.MaxPoint.X, ext.MaxPoint.Y, ext.MaxPoint.Z),
                    ["width"] = ext.MaxPoint.X - ext.MinPoint.X,
                    ["height"] = ext.MaxPoint.Y - ext.MinPoint.Y
                });
            }
        }
    }

    public class SelectByWindowCommand : ICommand
    {
        public string MethodName => "select_by_window";

        public CommandResult Execute(JObject parameters)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return CommandResult.Fail("No active document");

            Point3d minPt = ParsePoint(parameters["min_point"], "min_point");
            Point3d maxPt = ParsePoint(parameters["max_point"], "max_point");
            int limit = parameters["limit"]?.Value<int>() ?? 500;

            double winMinX = Math.Min(minPt.X, maxPt.X);
            double winMinY = Math.Min(minPt.Y, maxPt.Y);
            double winMaxX = Math.Max(minPt.X, maxPt.X);
            double winMaxY = Math.Max(minPt.Y, maxPt.Y);

            Database db = doc.Database;
            JArray matches = new JArray();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                int count = 0;
                foreach (ObjectId id in ms)
                {
                    if (count >= limit) break;
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;

                    try
                    {
                        Extents3d ext = ent.GeometricExtents;
                        if (ext.MinPoint.X >= winMinX && ext.MinPoint.Y >= winMinY &&
                            ext.MaxPoint.X <= winMaxX && ext.MaxPoint.Y <= winMaxY)
                        {
                            matches.Add(new JObject
                            {
                                ["handle"] = id.Handle.Value.ToString(),
                                ["type"] = ent.GetType().Name,
                                ["layer"] = ent.Layer
                            });
                            count++;
                        }
                    }
                    catch { /* entities without extents */ }
                }
                tr.Commit();
            }

            return CommandResult.Ok(new JObject { ["entities"] = matches, ["count"] = matches.Count });
        }
    }

    public class SelectByPropertiesCommand : ICommand
    {
        public string MethodName => "select_by_properties";

        public CommandResult Execute(JObject parameters)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return CommandResult.Fail("No active document");

            string filterLayer = parameters["layer"]?.ToString();
            string filterType = parameters["type"]?.ToString();
            int? filterColor = parameters["color"]?.Value<int>();
            string filterLinetype = parameters["linetype"]?.ToString();
            int limit = parameters["limit"]?.Value<int>() ?? 500;

            Database db = doc.Database;
            JArray matches = new JArray();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                int count = 0;
                foreach (ObjectId id in ms)
                {
                    if (count >= limit) break;
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;

                    if (!string.IsNullOrEmpty(filterLayer) && !ent.Layer.Equals(filterLayer, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.IsNullOrEmpty(filterType) && !ent.GetType().Name.Equals(filterType, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (filterColor.HasValue && ent.ColorIndex != filterColor.Value)
                        continue;
                    if (!string.IsNullOrEmpty(filterLinetype) && !ent.Linetype.Equals(filterLinetype, StringComparison.OrdinalIgnoreCase))
                        continue;

                    matches.Add(new JObject
                    {
                        ["handle"] = id.Handle.Value.ToString(),
                        ["type"] = ent.GetType().Name,
                        ["layer"] = ent.Layer,
                        ["color"] = ent.ColorIndex
                    });
                    count++;
                }
                tr.Commit();
            }

            return CommandResult.Ok(new JObject { ["entities"] = matches, ["count"] = matches.Count });
        }
    }

    public class FindIntersectionsCommand : ICommand
    {
        public string MethodName => "find_intersections";

        public CommandResult Execute(JObject parameters)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return CommandResult.Fail("No active document");

            string handle1 = parameters["handle1"]?.ToString();
            string handle2 = parameters["handle2"]?.ToString();
            if (string.IsNullOrEmpty(handle1) || string.IsNullOrEmpty(handle2))
                return CommandResult.Fail("Parameters 'handle1' and 'handle2' are required");

            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Handle h1 = new Handle(Convert.ToInt64(handle1));
                Handle h2 = new Handle(Convert.ToInt64(handle2));
                if (!db.TryGetObjectId(h1, out ObjectId id1)) return CommandResult.Fail($"Entity not found: {handle1}");
                if (!db.TryGetObjectId(h2, out ObjectId id2)) return CommandResult.Fail($"Entity not found: {handle2}");

                Entity ent1 = tr.GetObject(id1, OpenMode.ForRead) as Entity;
                Entity ent2 = tr.GetObject(id2, OpenMode.ForRead) as Entity;

                if (!(ent1 is Curve) || !(ent2 is Curve))
                    return CommandResult.Fail("Both entities must be curve-type (line, arc, circle, polyline, etc.)");

                Point3dCollection points = new Point3dCollection();
                ent1.IntersectWith(ent2, Intersect.OnBothOperands, points, IntPtr.Zero, IntPtr.Zero);

                JArray pts = new JArray();
                foreach (Point3d pt in points)
                    pts.Add(new JArray(pt.X, pt.Y, pt.Z));

                tr.Commit();
                return CommandResult.Ok(new JObject { ["points"] = pts, ["count"] = pts.Count });
            }
        }
    }
}
