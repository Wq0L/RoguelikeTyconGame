using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Pure geometry regression; never opens or changes a scene or save.
public static class AntennaAlignmentVerification
{
    public static void RunBatch()
    {
        int checks = 0;
        try
        {
            var cells = new List<GridPosition>();
            for (int side = 0; side < 4; side++)
            for (int lane = 4; lane <= 6; lane++)
            {
                int first = AntennaSlots.Id(side, lane);
                int second = AntennaSlots.Opposite(first);
                Require(AntennaSlots.Opposite(second) == first, "opposite symmetry");
                for (int size = 3; size <= 11; size += 2)
                {
                    int min = 5 - size / 2, max = 5 + size / 2;
                    var bounds = new FieldBounds(min, max, min, max);
                    var a = AntennaSlots.Anchor(bounds, first);
                    var b = AntennaSlots.Anchor(bounds, second);
                    Require(AntennaSlots.IsValid(bounds, first) && AntennaSlots.IsValid(bounds, second), "valid endpoints");
                    Require(side < 2 ? a.z == lane && b.z == lane : a.x == lane && b.x == lane, "selected alignment survives growth");
                    Require(side < 2 ? Math.Min(a.x,b.x) == min && Math.Max(a.x,b.x) == max : Math.Min(a.z,b.z) == min && Math.Max(a.z,b.z) == max, "endpoints track outer edges");
                    var facing = AntennaSlots.Facing(first);
                    var reverse = AntennaSlots.Facing(second);
                    Require(facing.x == -reverse.x && facing.y == -reverse.y, "antennas face each other");
                    AntennaSlots.Line(a,b,cells);
                    Require(cells.Count == size && cells.TrueForAll(c => side < 2 ? c.z == lane : c.x == lane), "line grows along selected alignment");
                    checks += 5;
                }
                checks++;
            }
            var config = ScriptableObject.CreateInstance<ConductiveLineEffectSO>();
            Require(!config.NeedsPlacement, "second antenna is automatic");
            UnityEngine.Object.DestroyImmediate(config);
            File.WriteAllText("Logs/AntennaAlignmentVerification.txt", "PASS " + (checks + 1) + " checks");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText("Logs/AntennaAlignmentVerification.txt", "FAIL " + e);
            EditorApplication.Exit(1);
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
