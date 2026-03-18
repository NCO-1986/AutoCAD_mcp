using System;
using Newtonsoft.Json.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AutoCADMCPPlugin.Models;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using static AutoCADMCPPlugin.Commands.EntityHelper;

namespace AutoCADMCPPlugin.Commands
{
    // ========================================================================
    // Entity Query commands
    // ========================================================================

    public class ListEntitiesCommand : ICommand
    {
        public string MethodName => "list_entities";

        public CommandResult Execute(JObject parameters)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return CommandResult.Fail("No active document");

            string filterLayer = parameters?["layer"]?.ToString();
            string filterType = parameters?["type"]?.ToString();
            int limit = parameters?["limit"]?.Value<int>() ?? 500;

            Database db = doc.Database;
            JArray entities = new JArray();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                int count = 0;
                foreach (ObjectId id in modelSpace)
                {
                    if (count >= limit) break;

                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;

                    // Apply filters
                    if (!string.IsNullOrEmpty(filterLayer) &&
                        !ent.Layer.Equals(filterLayer, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string typeName = ent.GetType().Name;
                    if (!string.IsNullOrEmpty(filterType) &&
                        !typeName.Equals(filterType, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var entJson = new JObject
                    {
                        ["handle"] = id.Handle.Value.ToString(),
                        ["type"] = typeName,
                        ["layer"] = ent.Layer,
                        ["color"] = ent.ColorIndex
                    };

                    // Add geometry info for common types
                    if (ent is Line line)
                    {
                        entJson["start"] = new JArray(line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z);
                        entJson["end"] = new JArray(line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z);
                    }
                    else if (ent is Circle circle)
                    {
                        entJson["center"] = new JArray(circle.Center.X, circle.Center.Y, circle.Center.Z);
                        entJson["radius"] = circle.Radius;
                    }
                    else if (ent is DBText text)
                    {
                        entJson["text"] = text.TextString;
                        entJson["position"] = new JArray(text.Position.X, text.Position.Y, text.Position.Z);
                    }

                    entities.Add(entJson);
                    count++;
                }

                tr.Commit();
            }

            return CommandResult.Ok(new JObject
            {
                ["entities"] = entities,
                ["count"] = entities.Count
            });
        }
    }

    public class GetEntityCommand : ICommand
    {
        public string MethodName => "get_entity";

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
                ObjectId id;
                try
                {
                    Handle h = new Handle(Convert.ToInt64(handle));
                    if (!db.TryGetObjectId(h, out id))
                        return CommandResult.Fail($"Entity not found with handle: {handle}");
                }
                catch
                {
                    return CommandResult.Fail($"Invalid handle format: {handle}");
                }

                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null)
                    return CommandResult.Fail($"Object with handle {handle} is not an entity");

                var result = new JObject
                {
                    ["handle"] = handle,
                    ["type"] = ent.GetType().Name,
                    ["layer"] = ent.Layer,
                    ["color"] = ent.ColorIndex,
                    ["linetype"] = ent.Linetype,
                    ["visible"] = ent.Visible
                };

                // Detailed geometry by type
                if (ent is Line line)
                {
                    result["start"] = new JArray(line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z);
                    result["end"] = new JArray(line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z);
                    result["length"] = line.Length;
                }
                else if (ent is Circle circle)
                {
                    result["center"] = new JArray(circle.Center.X, circle.Center.Y, circle.Center.Z);
                    result["radius"] = circle.Radius;
                    result["area"] = circle.Area;
                }
                else if (ent is Arc arc)
                {
                    result["center"] = new JArray(arc.Center.X, arc.Center.Y, arc.Center.Z);
                    result["radius"] = arc.Radius;
                    result["start_angle"] = arc.StartAngle * 180.0 / Math.PI;
                    result["end_angle"] = arc.EndAngle * 180.0 / Math.PI;
                }
                else if (ent is Polyline pline)
                {
                    result["vertex_count"] = pline.NumberOfVertices;
                    result["closed"] = pline.Closed;
                    result["length"] = pline.Length;
                    var verts = new JArray();
                    for (int i = 0; i < pline.NumberOfVertices; i++)
                    {
                        Point2d pt = pline.GetPoint2dAt(i);
                        verts.Add(new JArray(pt.X, pt.Y));
                    }
                    result["vertices"] = verts;
                }
                else if (ent is DBText text)
                {
                    result["text"] = text.TextString;
                    result["position"] = new JArray(text.Position.X, text.Position.Y, text.Position.Z);
                    result["height"] = text.Height;
                    result["rotation"] = text.Rotation * 180.0 / Math.PI;
                }
                else if (ent is MText mtext)
                {
                    result["text"] = mtext.Contents;
                    result["position"] = new JArray(mtext.Location.X, mtext.Location.Y, mtext.Location.Z);
                    result["height"] = mtext.TextHeight;
                }
                else if (ent is BlockReference blkRef)
                {
                    result["block_name"] = blkRef.Name;
                    result["position"] = new JArray(blkRef.Position.X, blkRef.Position.Y, blkRef.Position.Z);
                    result["rotation"] = blkRef.Rotation * 180.0 / Math.PI;
                    result["scale_x"] = blkRef.ScaleFactors.X;
                    result["scale_y"] = blkRef.ScaleFactors.Y;
                }

                tr.Commit();
                return CommandResult.Ok(result);
            }
        }
    }

    // ========================================================================
    // Entity Modification commands
    // ========================================================================

    public class EraseEntityCommand : ICommand
    {
        public string MethodName => "erase_entity";

        public CommandResult Execute(JObject parameters)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return CommandResult.Fail("No active document");

            string handle = parameters["handle"]?.ToString();
            if (string.IsNullOrEmpty(handle))
                return CommandResult.Fail("Parameter 'handle' is required");

            Database db = doc.Database;

            using (LockDoc())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Handle h = new Handle(Convert.ToInt64(handle));
                if (!db.TryGetObjectId(h, out ObjectId id))
                    return CommandResult.Fail($"Entity not found: {handle}");

                Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                if (ent == null)
                    return CommandResult.Fail($"Object {handle} is not an entity");

                ent.Erase();
                tr.Commit();
            }

            return CommandResult.Ok($"Entity {handle} erased");
        }
    }

    public class MoveEntityCommand : ICommand
    {
        public string MethodName => "move_entity";

        public CommandResult Execute(JObject parameters)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return CommandResult.Fail("No active document");

            string handle = parameters["handle"]?.ToString();
            if (string.IsNullOrEmpty(handle))
                return CommandResult.Fail("Parameter 'handle' is required");

            Point3d from = EntityHelper.ParsePoint(parameters["from"], "from");
            Point3d to = EntityHelper.ParsePoint(parameters["to"], "to");
            Vector3d displacement = to - from;

            Database db = doc.Database;
            using (LockDoc())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Handle h = new Handle(Convert.ToInt64(handle));
                if (!db.TryGetObjectId(h, out ObjectId id))
                    return CommandResult.Fail($"Entity not found: {handle}");

                Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                if (ent == null)
                    return CommandResult.Fail($"Object {handle} is not an entity");

                ent.TransformBy(Matrix3d.Displacement(displacement));
                tr.Commit();
            }

            return CommandResult.Ok($"Entity {handle} moved");
        }
    }

    public class CopyEntityCommand : ICommand
    {
        public string MethodName => "copy_entity";

        public CommandResult Execute(JObject parameters)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return CommandResult.Fail("No active document");

            string handle = parameters["handle"]?.ToString();
            if (string.IsNullOrEmpty(handle))
                return CommandResult.Fail("Parameter 'handle' is required");

            Point3d from = EntityHelper.ParsePoint(parameters["from"], "from");
            Point3d to = EntityHelper.ParsePoint(parameters["to"], "to");
            Vector3d displacement = to - from;

            Database db = doc.Database;
            using (LockDoc())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Handle h = new Handle(Convert.ToInt64(handle));
                if (!db.TryGetObjectId(h, out ObjectId id))
                    return CommandResult.Fail($"Entity not found: {handle}");

                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null)
                    return CommandResult.Fail($"Object {handle} is not an entity");

                Entity clone = ent.Clone() as Entity;
                clone.TransformBy(Matrix3d.Displacement(displacement));

                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                ObjectId newId = modelSpace.AppendEntity(clone);
                tr.AddNewlyCreatedDBObject(clone, true);
                tr.Commit();

                var result = EntityHelper.EntityToJson(newId);
                result["type"] = clone.GetType().Name;
                result["message"] = $"Entity {handle} copied";
                return CommandResult.Ok(result);
            }
        }
    }

    public class RotateEntityCommand : ICommand
    {
        public string MethodName => "rotate_entity";

        public CommandResult Execute(JObject parameters)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return CommandResult.Fail("No active document");

            string handle = parameters["handle"]?.ToString();
            if (string.IsNullOrEmpty(handle))
                return CommandResult.Fail("Parameter 'handle' is required");

            Point3d basePoint = EntityHelper.ParsePoint(parameters["base_point"], "base_point");
            double angle = parameters["angle"]?.Value<double>() ?? 0;
            double radians = angle * Math.PI / 180.0;

            Database db = doc.Database;
            using (LockDoc())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Handle h = new Handle(Convert.ToInt64(handle));
                if (!db.TryGetObjectId(h, out ObjectId id))
                    return CommandResult.Fail($"Entity not found: {handle}");

                Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                if (ent == null)
                    return CommandResult.Fail($"Object {handle} is not an entity");

                ent.TransformBy(Matrix3d.Rotation(radians, Vector3d.ZAxis, basePoint));
                tr.Commit();
            }

            return CommandResult.Ok($"Entity {handle} rotated {angle} degrees");
        }
    }

    public class ScaleEntityCommand : ICommand
    {
        public string MethodName => "scale_entity";

        public CommandResult Execute(JObject parameters)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return CommandResult.Fail("No active document");

            string handle = parameters["handle"]?.ToString();
            if (string.IsNullOrEmpty(handle))
                return CommandResult.Fail("Parameter 'handle' is required");

            Point3d basePoint = EntityHelper.ParsePoint(parameters["base_point"], "base_point");
            double factor = parameters["factor"]?.Value<double>() ?? 1.0;

            if (factor <= 0)
                return CommandResult.Fail("Parameter 'factor' must be positive");

            Database db = doc.Database;
            using (LockDoc())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Handle h = new Handle(Convert.ToInt64(handle));
                if (!db.TryGetObjectId(h, out ObjectId id))
                    return CommandResult.Fail($"Entity not found: {handle}");

                Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                if (ent == null)
                    return CommandResult.Fail($"Object {handle} is not an entity");

                ent.TransformBy(Matrix3d.Scaling(factor, basePoint));
                tr.Commit();
            }

            return CommandResult.Ok($"Entity {handle} scaled by {factor}");
        }
    }

    public class MirrorEntityCommand : ICommand
    {
        public string MethodName => "mirror_entity";

        public CommandResult Execute(JObject parameters)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return CommandResult.Fail("No active document");

            string handle = parameters["handle"]?.ToString();
            if (string.IsNullOrEmpty(handle))
                return CommandResult.Fail("Parameter 'handle' is required");

            Point3d mirrorPt1 = EntityHelper.ParsePoint(parameters["mirror_line_start"], "mirror_line_start");
            Point3d mirrorPt2 = EntityHelper.ParsePoint(parameters["mirror_line_end"], "mirror_line_end");
            bool eraseSource = parameters["erase_source"]?.Value<bool>() ?? false;

            Database db = doc.Database;
            using (LockDoc())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Handle h = new Handle(Convert.ToInt64(handle));
                if (!db.TryGetObjectId(h, out ObjectId id))
                    return CommandResult.Fail($"Entity not found: {handle}");

                Entity ent = tr.GetObject(id, eraseSource ? OpenMode.ForWrite : OpenMode.ForRead) as Entity;
                if (ent == null)
                    return CommandResult.Fail($"Object {handle} is not an entity");

                // Create mirrored copy
                Line3d mirrorLine = new Line3d(mirrorPt1, mirrorPt2);
                Matrix3d mirrorMatrix = Matrix3d.Mirroring(mirrorLine);

                Entity mirrored = ent.Clone() as Entity;
                mirrored.TransformBy(mirrorMatrix);

                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                ObjectId newId = modelSpace.AppendEntity(mirrored);
                tr.AddNewlyCreatedDBObject(mirrored, true);

                if (eraseSource)
                    ent.Erase();

                tr.Commit();

                var result = EntityHelper.EntityToJson(newId);
                result["type"] = mirrored.GetType().Name;
                result["source_erased"] = eraseSource;
                return CommandResult.Ok(result);
            }
        }
    }
}
