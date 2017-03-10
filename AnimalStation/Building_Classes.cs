using System.Collections.Generic;
using System.Text;
using Verse;
using RimWorld;
using Harmony;
using UnityEngine;
using FactoryFramework;

namespace AnimalStation
{
    class Building_Milker : Building_Storage, IInternalStorage
    {
        private int ThingCount = 0;
        public bool outputOn = true;
        //Interface-related stuff
        public ThingDef internalStoredDef
        {
            get
            {
                return ThingDef.Named("Milk");
            }
        }
        public int thingCount
        {
            get
            {
                return ThingCount;
            }
            set
            {
                ThingCount = value;
            }
        }
        public int maxCount
        {
            get
            {
                return 200;
            }
        }
        //End interface-related stuff
        public IEnumerable<IntVec3> scannerCells
        {
            get
            {
                return GenAdj.OccupiedRect(this).ExpandedBy(1).Cells;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach(Gizmo g in base.GetGizmos())
            {
                yield return g;
            }
            yield return new Command_Action
            {
                action = delegate
                {
                    outputOn = !outputOn;
                },
                defaultLabel = "Toggle output",
                defaultDesc = "Toggles if the milker will begin to output milk in the interaction slot. Milk is kept refrigerated in internal storage.",
                icon = ContentFinder<Texture2D>.Get("Things/Item/Resource/Milk"),
                hotKey = KeyBindingDefOf.Misc1
            };
        }

        public void tryModifyStorage(ref int count)
        {
            if (count + thingCount < 0) thingCount = 0;
            else if (count + thingCount > maxCount)
            {
                thingCount = maxCount;
                count -= thingCount - count;
            }
            else thingCount += count;
        }

        public override void TickRare()
        {
            base.TickRare();
            if (!GetComp<CompPowerTrader>().PowerOn) return;
            if (thingCount == maxCount) return;
            if (thingCount > 0 && outputOn)
            {
                Thing thing = ThingMaker.MakeThing(internalStoredDef, null);
                thing.stackCount = 1;
                GenPlace.TryPlaceThing(thing, InteractionCell, Map, ThingPlaceMode.Near, null);
                thingCount--;
            }
            foreach (IntVec3 c in scannerCells)
            {
                Thing p = c.GetThingList(Map).Find((Thing t) => t is Pawn && t.TryGetComp<CompMilkable>() != null);
                if (p == null || p.Faction != Faction.OfPlayer) continue;
                //BREAK POINT
                CompMilkable milkablecomp = ((Pawn)p).GetComp<CompMilkable>();
                Traverse traversable = Traverse.Create(milkablecomp); //Harmony used to access protected values
                int i = GenMath.RoundRandom(traversable.Property("ResourceAmount").GetValue<int>() * milkablecomp.Fullness);
                ThingDef resource = traversable.Property("ResourceDef").GetValue<ThingDef>();
                if (i == 0 || resource.defName != "Milk") continue;
                tryModifyStorage(ref i);
                traversable.Field("fullness").SetValue(0f);
            }
        }
        public override string GetInspectString()
        {
            StringBuilder s = new StringBuilder();
            s.AppendLine(base.GetInspectString());
            s.AppendLine("Keep milk in internal storage: " + !outputOn);
            if (thingCount > maxCount) s.AppendLine("Internal storage is full.");
            s.Append(string.Format("Internal storage: {0}/{1}", thingCount, maxCount));
            return s.ToString();
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.LookValue(ref ThingCount, "thingCount");
            Scribe_Values.LookValue(ref outputOn, "outputOn");
        }
    }
    class Building_Shearer : Building_Storage
    {
        public IEnumerable<IntVec3> scannerCells
        {
            get
            {
                return GenAdj.OccupiedRect(this).ExpandedBy(1).Cells;
            }
        }
        
        public override void TickRare()
        {
            base.TickRare();
            if (!GetComp<CompPowerTrader>().PowerOn) return;
            foreach (IntVec3 c in scannerCells)
            {
                Thing p = c.GetThingList(Map).Find((Thing t) => t is Pawn && t.TryGetComp<CompMilkable>() != null);
                if (p == null || p.Faction != Faction.OfPlayer) continue;
                //BREAK POINT
                CompShearable shearablecomp = ((Pawn)p).GetComp<CompShearable>();
                Traverse traversable = Traverse.Create(shearablecomp); //Harmony used to access protected values
                int i = GenMath.RoundRandom(traversable.Property("ResourceAmount").GetValue<int>() * shearablecomp.Fullness);
                if (i == 0) continue;
                ThingDef resource = traversable.Property("ResourceDef").GetValue<ThingDef>();
                while (i > 0)
                {
                    int num = Mathf.Clamp(i, 1, resource.stackLimit);
                    i -= num;
                    Thing thing = ThingMaker.MakeThing(resource, null);
                    thing.stackCount = num;
                    GenPlace.TryPlaceThing(thing, p.Position, p.Map, ThingPlaceMode.Near, null);
                }
                traversable.Field("fullness").SetValue(0f);
            }
        }
    }
}
