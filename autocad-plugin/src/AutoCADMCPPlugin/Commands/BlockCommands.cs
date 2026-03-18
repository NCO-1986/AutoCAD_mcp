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
    public class ListBlocksCommand : ICommand
    {
        public string MethodName => "list_blocks";

        public CommandResult Execute(JObject parameters)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return CommandResult.Fail("No active document");

            Database db = doc.Database;
            JArray blocks = new JArray();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                foreach (ObjectId blockId in bt)
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);

                    // Skip model/paper space and anonymous blocks
                    if (btr.IsLayout || btr.IsAnonymous) continue;

                    var blockInfo = new JObject
                    {
                        ["name"] = btr.Name,
                        ["is_dynamic"] = btr.IsDynamicBlock,
                        ["has_attributes"] = btr.HasAttributeDefinitions
                    };

                    // Count entities in the block
                    int entityCount = 0;
                    foreach (ObjectId id in btr)
                        entityCount++;
                    blockInfo["entity_count"] = entityCount;

                    // List attribute definitions
                    if (btr.HasAttributeDefinitions)
                    {
                        JArray attrs = new JArray();
                        foreach (ObjectId id in btr)
                        {
                            DBObject obj = tr.GetObject(id, OpenMode.ForRead);
                            if (obj is AttributeDefinition attDef)
                            {
                                attrs.Add(new JObject
                                {
                                    ["tag"] = attDef.Tag,
                                    ["prompt"] = attDef.Prompt,
                                    ["default_value"] = attDef.TextString
                                });
                            }
                        }
                        blockInfo["attributes"] = attrs;
                    }

                    blocks.Add(blockInfo);
                }

                tr.Commit();
            }

            return CommandResult.Ok(new JObject
            {
                ["blocks"] = blocks,
                ["count"] = blocks.Count
            });
        }
    }

    public class InsertBlockCommand : ICommand
    {
        public string MethodName => "insert_block";

        public CommandResult Execute(JObject parameters)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return CommandResult.Fail("No active document");

            string blockName = parameters["name"]?.ToString();
            if (string.IsNullOrEmpty(blockName))
                return CommandResult.Fail("Parameter 'name' is required");

            Point3d position = EntityHelper.ParsePoint(parameters["position"], "position");
            double rotation = (parameters["rotation"]?.Value<double>() ?? 0) * Math.PI / 180.0;
            double scaleX = parameters["scale_x"]?.Value<double>() ?? 1.0;
            double scaleY = parameters["scale_y"]?.Value<double>() ?? 1.0;
            double scaleZ = parameters["scale_z"]?.Value<double>() ?? 1.0;

            Database db = doc.Database;

            using (LockDoc())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                if (!bt.Has(blockName))
                    return CommandResult.Fail($"Block '{blockName}' not found in drawing");

                ObjectId blockDefId = bt[blockName];
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                using (BlockReference blkRef = new BlockReference(position, blockDefId))
                {
                    blkRef.Rotation = rotation;
                    blkRef.ScaleFactors = new Scale3d(scaleX, scaleY, scaleZ);

                    // Set layer if specified
                    string layer = parameters["layer"]?.ToString();
                    if (!string.IsNullOrEmpty(layer))
                    {
                        LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                        if (lt.Has(layer))
                            blkRef.Layer = layer;
                    }

                    ObjectId refId = modelSpace.AppendEntity(blkRef);
                    tr.AddNewlyCreatedDBObject(blkRef, true);

                    // Set attribute values if provided
                    JObject attrValues = parameters["attributes"] as JObject;
                    if (attrValues != null)
                    {
                        BlockTableRecord blockDef = (BlockTableRecord)tr.GetObject(blockDefId, OpenMode.ForRead);
                        foreach (ObjectId attId in blockDef)
                        {
                            DBObject obj = tr.GetObject(attId, OpenMode.ForRead);
                            if (obj is AttributeDefinition attDef && !attDef.Constant)
                            {
                                AttributeReference attRef = new AttributeReference();
                                attRef.SetAttributeFromBlock(attDef, blkRef.BlockTransform);

                                string val = attrValues[attDef.Tag]?.ToString();
                                if (val != null)
                                    attRef.TextString = val;

                                blkRef.AttributeCollection.AppendAttribute(attRef);
                                tr.AddNewlyCreatedDBObject(attRef, true);
                            }
                        }
                    }

                    tr.Commit();

                    var result = EntityHelper.EntityToJson(refId);
                    result["type"] = "BlockReference";
                    result["block_name"] = blockName;
                    return CommandResult.Ok(result);
                }
            }
        }
    }
}
