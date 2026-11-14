using System;
using UnityEngine;

namespace GatheringSamples
{
    public static class Sim
    {
        public const float MissionDuration = 480f;
        public const float OxygenMax = 100f;
        public const float O2BaseDrain = 0.18f;
        public const float O2MoveDrain = 0.05f;
        public const float O2LopeDrain = 0.08f;
        public const float O2ToolCost = 1.5f;
        public const float O2CollectCost = 2.5f;
        public const float LowOxygen = 30f;
        public const float CriticalOxygen = 12f;
        public const float LowTime = 120f;
        public const float BoundaryRadius = 55f;
        public const float SoftBoundaryRadius = 45f;
        public const float BoundaryGrace = 8f;
        public const float InteractionRadius = 3.4f;
        public const float LanderRadius = 5f;
        public const float WalkSpeed = 2.4f;
        public const float LopeSpeed = 3f;
        public const float MoveAcceleration = 8f;
        public const float TurnRate = 7.5f;
        public const float Gravity = 1.62f;
        public const float JumpVelocity = 2.1f;
        public const float PlayerHeight = 1.82f;
        public const float CameraDistance = 6.4f;
        public const float CameraMinDistance = 3.2f;
        public const float CameraMaxDistance = 11f;
        public const float CollectDuration = 1.25f;
        public const int SampleCount = 5;
        public const float TerrainSize = 260f;
        public const int TerrainSegments = 180;
    }

    public class ToolDef
    {
        public string id;
        public string name;
        public string key;
        public string shortDesc;
        public string function;
        public string when;
        public string why;
        public string science;
    }

    public class ContainerDef
    {
        public string id;
        public string name;
        public string shortDesc;
        public string science;
    }

    [Serializable]
    public class SiteDef
    {
        public string id;
        public string code;
        public string name;
        public string type;
        public string typeLabel;
        public Vector2 position;
        public string[] requiredTools;
        public string container;
        public string brief;
        public string science;
        public string hint;
        public Color color;
    }

    public static class Content
    {
        public static readonly string[] TypeIds =
        {
            "loose_regolith",
            "individual_rock",
            "outcrop",
            "stratified_soil",
            "coarse_material"
        };

        public static readonly string[] TypeLabels =
        {
            "Loose, unconsolidated regolith",
            "Individual rock resting on the surface",
            "Consolidated bedrock / outcrop",
            "Layered soil profile (stratigraphy)",
            "Coarse gravel / pebble lag"
        };

        public static readonly ToolDef[] Tools =
        {
            new ToolDef
            {
                id = "scoop", name = "Scoop", key = "1", shortDesc = "Loose soil",
                function = "Gathers loose lunar soil and small, unconsolidated material.",
                when = "Use on loose regolith, dust, and fine surface deposits.",
                why = "A scoop collects a representative volume without pressing your glove into the sample.",
                science = "Scoops were used during Apollo to collect loose regolith and soil. A scoop is appropriate only when the material is loose enough to be gathered."
            },
            new ToolDef
            {
                id = "tongs", name = "Tongs", key = "2", shortDesc = "Individual rocks",
                function = "Picks up and handles an individual rock.",
                when = "Use on a discrete, hand-sized rock resting on the surface.",
                why = "Tongs let you lift a rock while reducing direct glove contact and contamination.",
                science = "Tongs are a sample-handling tool. They are useful for a rock that can be picked up as a discrete object, not for breaking bedrock."
            },
            new ToolDef
            {
                id = "hammer", name = "Hammer", key = "3", shortDesc = "Fracture rock",
                function = "Strikes rock to expose fresh surfaces and begin breaking a sample free.",
                when = "Use on consolidated rock or an outcrop, normally together with a chisel.",
                why = "A geological hammer delivers a controlled impact; it is not a scoop and should not be used on loose soil.",
                science = "Geological hammers are used to break or split rock and expose unweathered surfaces. Safety glasses and controlled strikes matter in real fieldwork."
            },
            new ToolDef
            {
                id = "chisel", name = "Chisel", key = "4", shortDesc = "Split rock",
                function = "Directs a controlled break to separate a portion of rock.",
                when = "Use after the rock surface has been started with a hammer.",
                why = "A chisel concentrates force along a line, helping detach a sample with less shattering.",
                science = "Chisels are used with a hammer to split rock or detach a piece along a fracture. The order matters: hammer first to start the break, then chisel to control it."
            },
            new ToolDef
            {
                id = "rake", name = "Rake", key = "5", shortDesc = "Pebbles & gravel",
                function = "Gathers small rocks, pebbles, and coarse surface material.",
                when = "Use on a coarse surface lag of small clasts.",
                why = "A rake collects many small pieces at once while leaving very fine dust behind.",
                science = "Rake samplers are designed to collect coarse fragments and small rocks from the surface. They are not the right tool for a single large rock or a soil core."
            },
            new ToolDef
            {
                id = "drive-tube", name = "Drive Tube", key = "6", shortDesc = "Soil core",
                function = "Drives a tube into soft soil to recover a cylindrical core.",
                when = "Use on soft, layered soil where the vertical sequence matters.",
                why = "The tube keeps the soil in order, preserving information about layering.",
                science = "Apollo drive tubes collected regolith cores. Preserving the order of layers (stratigraphy) gives scientists information that a loose bag of soil cannot."
            }
        };

        public static readonly ContainerDef[] Containers =
        {
            new ContainerDef
            {
                id = "soil-bag", name = "Sterile Soil Bag",
                shortDesc = "Sealed bag for loose regolith and soil.",
                science = "Loose soil is bagged and sealed to limit contamination and keep the sample together."
            },
            new ContainerDef
            {
                id = "rock-bag", name = "Rock Sample Bag",
                shortDesc = "Padded bag for individual rocks and rock fragments.",
                science = "Discrete rocks are individually bagged so their identity and context are not mixed with other material."
            },
            new ContainerDef
            {
                id = "drive-tube", name = "Sealed Drive Tube",
                shortDesc = "Capped tube that preserves a soil core in order.",
                science = "A core must stay in its tube so the vertical layering is not disturbed."
            },
            new ContainerDef
            {
                id = "coarse-bag", name = "Coarse Sample Bag",
                shortDesc = "Bag for pebbles, gravel, and coarse surface material.",
                science = "A coarse sample is kept separate from fine soil so the size fraction can be studied."
            }
        };

        public static readonly SiteDef[] Sites =
        {
            new SiteDef
            {
                id = "REG-01", code = "01", name = "Loose Regolith Patch", type = "loose_regolith",
                typeLabel = "Loose, unconsolidated regolith", position = new Vector2(-13f, -11f),
                requiredTools = new[] { "scoop" }, container = "soil-bag",
                brief = "A soft patch of fine, loose regolith has collected in a shallow hollow. The grains are not cemented together.",
                science = "Loose regolith is best sampled as bulk material. A scoop can gather it without destroying the surrounding surface.",
                hint = "The material is loose and unconsolidated. Reach for the tool with a bowl, not a hammer.",
                color = new Color32(0x8d, 0x8b, 0x86, 0xff)
            },
            new SiteDef
            {
                id = "ROC-02", code = "02", name = "Float Rock", type = "individual_rock",
                typeLabel = "Individual rock resting on the surface", position = new Vector2(16f, -18f),
                requiredTools = new[] { "tongs" }, container = "rock-bag",
                brief = "A single, hand-sized rock sits on the surface. It is not attached to bedrock and can be lifted as one object.",
                science = "A float rock is a discrete sample. Handling it with tongs reduces glove contact and keeps it identifiable.",
                hint = "This is one discrete object, not soil and not bedrock. Pick it up rather than breaking it.",
                color = new Color32(0x7b, 0x78, 0x72, 0xff)
            },
            new SiteDef
            {
                id = "OUT-03", code = "03", name = "Consolidated Outcrop", type = "outcrop",
                typeLabel = "Consolidated bedrock / outcrop", position = new Vector2(-27f, 18f),
                requiredTools = new[] { "hammer", "chisel" }, container = "rock-bag",
                brief = "A resistant outcrop rises above the regolith. Its surface is cemented and cannot be scooped or picked up by hand.",
                science = "Consolidated rock must be fractured. Start the break with a hammer, then use a chisel to control the split and free a sample.",
                hint = "This rock is cemented in place. Start with the hammer, then control the break with the chisel.",
                color = new Color32(0x6f, 0x6c, 0x67, 0xff)
            },
            new SiteDef
            {
                id = "STR-04", code = "04", name = "Stratified Soil Exposure", type = "stratified_soil",
                typeLabel = "Layered soil profile (stratigraphy)", position = new Vector2(28f, 20f),
                requiredTools = new[] { "drive-tube" }, container = "drive-tube",
                brief = "A small exposure shows distinct layers of soil. The vertical order of the layers is the important information.",
                science = "A drive tube is pushed into soft, layered soil to recover a core. Keeping the layers in order preserves stratigraphy.",
                hint = "You need to preserve the vertical sequence. A bag would mix the layers together.",
                color = new Color32(0x8a, 0x82, 0x77, 0xff)
            },
            new SiteDef
            {
                id = "PEB-05", code = "05", name = "Coarse Surface Lag", type = "coarse_material",
                typeLabel = "Coarse gravel / pebble lag", position = new Vector2(6f, -38f),
                requiredTools = new[] { "rake" }, container = "coarse-bag",
                brief = "A concentration of small pebbles and gravel rests on the surface, with finer dust between the clasts.",
                science = "A rake is suited to collecting many small coarse fragments at once. The size fraction is kept separate from fine soil.",
                hint = "Many small clasts are spread across the surface. Use the tool with tines to gather them.",
                color = new Color32(0x85, 0x82, 0x7c, 0xff)
            }
        };

        public static readonly string Gravity =
            "The Moon's surface gravity is about 1.62 m/s², roughly one-sixth of Earth's. This is why the astronaut's steps and hops feel floaty.";

        public static readonly string Atmosphere =
            "The Moon has no breathable atmosphere — only an extremely tenuous exosphere. A pressurized spacesuit and portable life-support system are mandatory outside a spacecraft.";

        public static readonly string Regolith =
            "Lunar regolith is a layer of loose, fragmented rock and dust. It is abrasive, can cling to suits and equipment, and is scientifically valuable because it records surface processes.";

        public static readonly string Temperature =
            "Without a substantial atmosphere, the lunar surface experiences extreme temperature swings between sunlit and shadowed areas. Spacesuits provide thermal control; the game does not simulate a precise temperature at every spot.";

        public static readonly string Mobility =
            "Pressurized EVA suits are stiff and heavy in Earth terms, and thick gloves reduce dexterity. Real astronauts train extensively for bending, reaching, and tool use in a suit. The movement limits here are simulation choices, not measured suit specifications.";

        public static readonly string Samples =
            "Lunar samples are carefully documented, bagged, and curated so that their original context — location, orientation, and associations — is preserved for research. Contamination control and sample numbering are part of real sample handling practice.";

        public static readonly string DriveTubes =
            "Drive tubes are pushed into soft soil or regolith to recover a cylindrical core. Because the material is retained in order, the tube preserves information about layering (stratigraphy).";

        public static readonly string ToolsScience =
            "Geological field tools such as scoops, tongs, hammers, chisels, rakes, and drive tubes are selected for the material being sampled. Using the wrong tool can damage a sample or lose context.";

        public static readonly string[] ReferenceLabels =
        {
            "NASA — Moon Facts",
            "NASA — Lunar Regolith",
            "NASA — Spacesuits",
            "NASA — Apollo Sample Curation"
        };

        public static readonly string[] ReferenceUrls =
        {
            "https://science.nasa.gov/moon/facts/",
            "https://science.nasa.gov/resource/lunar-regolith/",
            "https://www.nasa.gov/humans-in-space/spacesuits/",
            "https://curator.jsc.nasa.gov/lunar/"
        };

        public static ToolDef Tool(string id)
        {
            return Array.Find(Tools, tool => tool.id == id);
        }

        public static ContainerDef Container(string id)
        {
            return Array.Find(Containers, container => container.id == id);
        }

        public static SiteDef Site(string id)
        {
            return Array.Find(Sites, site => site.id == id);
        }

        public static string TypeLabel(string id)
        {
            int index = Array.IndexOf(TypeIds, id);
            return index >= 0 ? TypeLabels[index] : id;
        }
    }
}
