using System.Collections.Generic;
using System.Linq;

namespace Dashboards.Services
{
    public static class SupplierCatalog
    {
        public static object Build()
        {
            return new
            {
                groups = new object[]
                {
                    Group("MATERIALS", "Materials suppliers",
                        Cat("READY_MIX_CONCRETE", "Ready-mix concrete", Sub("DELIVERY", "Deliver specified mixes"), Sub("PUMP_SERVICES", "Pump services"), Sub("SLUMP_CONTROL", "Slump control"), Sub("BATCH_TICKETS", "Batch tickets")),
                        Cat("AGGREGATES_AND_SAND", "Aggregates and sand", Sub("CRUSHED_STONE", "Crushed stone"), Sub("GRAVEL", "Gravel"), Sub("BEDDING_SAND", "Bedding sand"), Sub("BACKFILL", "Backfill")),
                        Cat("CEMENT_AND_MORTAR", "Cement and mortar", Sub("BULK_CEMENT", "Bulk cement"), Sub("PREBLENDED_MORTAR", "Preblended mortars"), Sub("BAGGED_PRODUCTS", "Bagged cementitious products")),
                        Cat("MASONRY_UNITS_AND_PRECAST", "Masonry units and precast", Sub("CONCRETE_BLOCK", "Concrete block"), Sub("BRICK", "Brick"), Sub("STONE_VENEERS", "Stone veneers"), Sub("PRECAST_PANELS", "Precast panels"), Sub("LINTELS", "Lintels")),
                        Cat("STRUCTURAL_STEEL", "Structural steel", Sub("ROLLED_SECTIONS", "Rolled sections"), Sub("PLATES", "Plates"), Sub("BEAMS", "Beams"), Sub("COLUMNS", "Columns"), Sub("CONNECTION_HARDWARE", "Connection hardware"), Sub("SHOP_FABRICATION", "Shop fabrication")),
                        Cat("TIMBER_AND_LUMBER", "Timber and lumber yards", Sub("DIMENSIONAL_LUMBER", "Dimensional lumber"), Sub("PLYWOOD", "Plywood"), Sub("ENGINEERED_WOOD", "Engineered wood (LVL, glulam)"), Sub("FORMWORK_TIMBER", "Formwork timber")),
                        Cat("REBAR_AND_REINFORCEMENT", "Rebar and reinforcement", Sub("REBAR", "Bar"), Sub("WIRE_MESH", "Wire mesh"), Sub("COUPLERS", "Couplers"), Sub("CUTTING_BENDING", "Cutting/bending services")),
                        Cat("ROOFING_AND_WATERPROOFING", "Roofing and waterproofing materials", Sub("MEMBRANES", "Membranes"), Sub("SHINGLES", "Shingles"), Sub("METAL_ROOFING", "Metal roofing"), Sub("FLASHINGS", "Flashings"), Sub("LIQUID_APPLIED", "Liquid applied systems")),
                        Cat("INSULATION_AND_FACADE", "Insulation and façade materials", Sub("BATT", "Batt"), Sub("SPRAY_FOAM", "Spray foam"), Sub("RIGID_BOARD", "Rigid board"), Sub("EIFS", "EIFS"), Sub("CURTAINWALL_COMPONENTS", "Curtainwall components")),
                        Cat("FINISH_MATERIALS", "Finish materials", Sub("DRYWALL", "Drywall"), Sub("JOINT_COMPOUNDS", "Joint compounds"), Sub("PAINT", "Paint"), Sub("FLOORING", "Flooring"), Sub("TILES", "Tiles"), Sub("DOORS", "Doors"), Sub("TRIM", "Trim"), Sub("HARDWARE", "Hardware")),
                        Cat("MEP_CONSUMABLES", "Plumbing, HVAC and electrical consumables", Sub("PIPING", "Piping"), Sub("VALVES", "Valves"), Sub("DUCTWORK", "Ductwork"), Sub("WIRING", "Wiring"), Sub("FIXTURES", "Fixtures"), Sub("FITTINGS", "Fittings")),
                        CatOther()),
                    Group("EQUIPMENT_AND_HEAVY_PLANT", "Equipment and heavy plant suppliers",
                        Cat("CRANE_RENTAL", "Crane rental companies", Sub("MOBILE_CRANES", "Mobile cranes"), Sub("TOWER_CRANES", "Tower cranes"), Sub("ERECTION_CREWS", "Erection crews")),
                        Cat("EARTHMOVING_RENTAL", "Earthmoving and compact equipment rental", Sub("EXCAVATORS", "Excavators"), Sub("LOADERS", "Loaders"), Sub("BULLDOZERS", "Bulldozers"), Sub("SKID_STEERS", "Skid steers"), Sub("TELEHANDLERS", "Telehandlers")),
                        Cat("CONCRETE_EQUIPMENT_RENTAL", "Concrete equipment rental", Sub("PUMPS", "Pumps"), Sub("MIXERS", "Mixers"), Sub("PLACING_BOOMS", "Placing booms"), Sub("SCREEDS", "Screeds"), Sub("VIBRATORS", "Vibrators")),
                        Cat("AERIAL_ACCESS", "Aerial access", Sub("SCISSOR_LIFTS", "Scissor lifts"), Sub("BOOM_LIFTS", "Boom lifts"), Sub("SCAFFOLDING", "Scaffolding"), Sub("MAST_CLIMBERS", "Mast climbers")),
                        Cat("COMPACTION_AND_PAVING", "Compaction and paving equipment", Sub("ROLLERS", "Rollers"), Sub("PAVERS", "Pavers"), Sub("MILLING_MACHINES", "Milling machines")),
                        Cat("TEMP_POWER_AND_LIGHTING", "Temporary power and lighting", Sub("GENERATORS", "Generators"), Sub("DISTRIBUTION_PANELS", "Distribution panels"), Sub("LIGHTING_TOWERS", "Site lighting towers")),
                        Cat("SPECIALIZED_EQUIPMENT_RENTAL", "Specialized equipment rental", Sub("PILE_DRIVERS", "Pile drivers"), Sub("DRILLING_RIGS", "Drilling rigs"), Sub("SHOTCRETE_UNITS", "Shotcrete units"), Sub("TBM", "Tunnel boring machines")),
                        CatOther()),
                    Group("SUPPLEMENT", "Supplement suppliers",
                        Cat("MEP_CONTRACTORS", "Electrical, plumbing, HVAC contractors", Sub("LABOUR_PLUS_MATERIALS", "Supply labor plus specialty materials/fixtures")),
                        Cat("MECHANICAL_AND_RIGGING", "Mechanical and rigging contractors", Sub("HEAVY_LIFTING", "Heavy lifting"), Sub("MACHINERY_INSTALLATION", "Machinery installation")),
                        Cat("FORMWORK_AND_SHORING", "Formwork and shoring", Sub("ENGINEERED_FORM_SYSTEMS", "Engineered form systems"), Sub("SHORE_PROPS", "Shore props"), Sub("SHORES_HIRE", "Shores hire")),
                        Cat("SCAFFOLDING_PROVIDERS", "Scaffolding providers", Sub("DESIGN", "Design"), Sub("ERECTION", "Erection"), Sub("INSPECTION", "Inspection"), Sub("HIRE", "Hire")),
                        Cat("HOIST_AND_MATERIAL_LIFT", "Hoist and material lift", Sub("MAST_CLIMBERS", "Mast climbers"), Sub("PERSONNEL_MATERIAL_HOISTS", "Personnel/material hoists")),
                        Cat("TESTING_AND_INSPECTION", "Testing and inspection labs", Sub("CONCRETE_TESTING", "Concrete testing"), Sub("SOILS", "Soils"), Sub("NDT", "NDT"), Sub("MATERIAL_CERT", "Material certification")),
                        Cat("SURVEYING_AND_LAYOUT", "Surveying and layout services", Sub("MACHINE_CONTROL", "Machine control"), Sub("CONTROL_POINTS", "Control points"), Sub("AS_BUILT", "As-built surveys")),
                        CatOther()),
                    Group("TEMPORARY_SITE_SERVICES_AND_CONSUMABLES", "Temporary site services and consumables suppliers",
                        Cat("FENCING_SECURITY", "Fencing and site-security", Sub("PERIMETER_FENCING", "Perimeter fencing"), Sub("CCTV", "CCTV"), Sub("ACCESS_CONTROL", "Access control")),
                        Cat("ACCOMMODATION_WELFARE", "Site accommodation and welfare", Sub("CABINS", "Cabins"), Sub("TOILETS", "Toilets"), Sub("CANTEENS", "Canteens"), Sub("FIRST_AID", "First-aid stations")),
                        Cat("WASTE_RECYCLING", "Waste management and recycling", Sub("SKIPS", "Skips"), Sub("ROLLOFFS", "Roll-offs"), Sub("HAZARDOUS", "Hazardous waste disposal")),
                        Cat("HEALTH_SAFETY", "Health & safety supplies", Sub("PPE", "PPE"), Sub("FALL_PROTECTION", "Fall-protection equipment"), Sub("SIGNAGE", "Signage"), Sub("SAFETY_BARRIERS", "Safety barriers")),
                        Cat("FUEL_LUBRICATION", "Fuel and lubrication", Sub("DIESEL", "Diesel"), Sub("OILS", "Oils"), Sub("GREASE", "Grease"), Sub("ON_SITE_FUEL", "On-site fueling services")),
                        Cat("TEMP_TRAFFIC", "Temporary traffic management", Sub("BARRIERS", "Barriers"), Sub("CONES", "Cones"), Sub("SIGNAGE", "Signage"), Sub("MARSHALS", "Traffic marshals")),
                        CatOther()),
                    Group("SPECIALTY_AND_MANUFACTURER_DIRECT", "Specialty and manufacturer-direct suppliers",
                        Cat("GLASS_GLAZING", "Glass and glazing fabricators", Sub("CURTAINWALL_UNITS", "Curtainwall units"), Sub("INSULATED_GLASS", "Insulated glass"), Sub("SPANDREL_PANELS", "Spandrel panels")),
                        Cat("ELEVATOR_ESCALATOR", "Elevator and escalator manufacturers", Sub("SUPPLY_AND_COORDINATE", "Supply and coordinate installation")),
                        Cat("SPECIALTY_FACADE", "Specialty façade and cladding", Sub("METAL_PANELS", "Metal panels"), Sub("TERRACOTTA", "Terracotta"), Sub("STONE_CLADDING", "Stone cladding")),
                        Cat("FIRE_PROTECTION", "Fire protection systems", Sub("SPRINKLERS", "Sprinklers"), Sub("DETECTION", "Detection"), Sub("SUPPRESSION", "Suppression equipment")),
                        Cat("RENEWABLE_ENERGY", "Renewable-energy", Sub("SOLAR_PANELS", "Solar panels"), Sub("INVERTERS", "Inverters"), Sub("BATTERY_SYSTEMS", "Battery systems")),
                        CatOther()),
                    Group("LOGISTICS_AND_PROCUREMENT", "Logistics and procurement suppliers",
                        Cat("FREIGHT_HAULAGE", "Freight and haulage", Sub("LONG_HAUL", "Long-haul delivery"), Sub("LAST_MILE", "Last-mile delivery"), Sub("HEAVY_LIFT_TRANSPORT", "Heavy lift transport")),
                        Cat("JUST_IN_TIME", "Just-in-time delivery", Sub("SEQUENCED_DELIVERIES", "Sequenced deliveries")),
                        Cat("PREFAB_MODULAR", "Prefabrication and modular", Sub("BATHROOM_PODS", "Bathroom pods"), Sub("PREFAB_MEP_RACKS", "Prefabricated MEP racks"), Sub("TIMBER_PANELS", "Timber panels")),
                        CatOther()),
                    Group("OTHER", "Other", CatOther())
                }
            };
        }

        static object Group(string id, string title, params object[] categories)
        {
            return new { id, title, categories };
        }

        static object Cat(string id, string title, params object[] subcategories)
        {
            var list = subcategories.ToList();
            list.Add(Sub("OTHER", "Other"));
            return new { id, title, subcategories = list };
        }

        static object CatOther()
        {
            return new { id = "OTHER", title = "Other", subcategories = new[] { Sub("OTHER", "Other") } };
        }

        static object Sub(string id, string title)
        {
            return new { id, title };
        }
    }
}
