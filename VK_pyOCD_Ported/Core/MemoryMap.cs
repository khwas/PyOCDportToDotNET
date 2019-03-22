using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace openocd.Core
{
    public static class Memory
    {
        public static object @__all__ = new List<object> {
            "MemoryRange",
            "MemoryRegion",
            "MemoryMap",
            "RamRegion",
            "RomRegion",
            "FlashRegion",
            "DeviceRegion",
            "AliasRegion"
        };

        public static string MAP_XML_HEADER = 
@"<?xml version=""1.0""?>
<!DOCTYPE memory-map PUBLIC ""+//IDN gnu.org//DTD GDB Memory Map V1.0//EN"" ""http://sourceware.org/gdb/gdb-memory-map.dtd"">
";
        public static Tuple<UInt32, UInt32> check_range(object start, UInt32? end = null, UInt32? length = null, MemoryRange range = null)
        {
            Debug.Assert(start != null && (start is MemoryRange || range != null || end != null ^ length != null));
            if (start is MemoryRange)
            {
                range = (MemoryRange)start;
            }
            if (range != null)
            {
                start = range.start;
                end = range.end;
            }
            else if (end == null)
            {
                end = (UInt32)start + length - 1;
            }
            return Tuple.Create((UInt32)start, (UInt32)end);
        }

        public class MemoryRangeBase
        {
            internal MemoryRegion _region;

            public MemoryRangeBase(UInt32 start = 0, UInt32 end = 0, UInt32 length = 0, MemoryRegion region = null)
            {
                this.start = start;
                if (length != 0)
                {
                    this.end = this.start + length - 1;
                }
                else
                {
                    this.end = end;
                }
                this._region = region;
            }

            public UInt32 start { get; }

            public UInt32 end { get; }

            public UInt32 length
            {
                get
                {
                    return this.end - this.start + 1;
                }
            }

            public virtual MemoryRegion region
            {
                get
                {
                    return this._region;
                }
            }

            public virtual bool containsAddress(UInt32 address)
            {
                return address >= this.start && address <= this.end;
            }

            //#
            // @return Whether the given range is fully contained by the region.
            public virtual bool containsRange(UInt32 start, UInt32? end = null, UInt32? length = null, MemoryRange range = null)
            {
                var _tup_1 = check_range(start, end, length, range);
                start = _tup_1.Item1;
                end = _tup_1.Item2;
                return this.containsAddress(start) && this.containsAddress((UInt32)end);
            }

            //#
            // @return Whether the region is fully within the bounds of the given range.
            public virtual bool containedByRange(object start, UInt32? end = null, UInt32? length = null, MemoryRange range = null)
            {
                var _tup_1 = check_range(start, end, length, range);
                start = _tup_1.Item1;
                end = _tup_1.Item2;
                return (UInt32)start <= this.start && end >= this.end;
            }

            //#
            // @return Whether the region and the given range intersect at any point.
            public virtual bool intersectsRange(object start, UInt32? end = null, UInt32? length = null, MemoryRange range = null)
            {
                var _tup_1 = check_range(start, end, length, range);
                start = _tup_1.Item1;
                end = _tup_1.Item2;
                return (UInt32)start <= this.start && end >= this.start || (UInt32)start <= this.end && end >= this.end || (UInt32)start >= this.start && end <= this.end;
            }
        }

        public class MemoryRange : MemoryRangeBase
        {

            public MemoryRange(UInt32 start = 0, UInt32 end = 0, UInt32 length = 0, MemoryRegion region = null)
                : base(end: end, length: length)
            {
                this._region = region;
            }

            public override MemoryRegion region
            {
                get
                {
                    return this._region;
                }
            }

            public virtual object @__repr__()
            {
                return String.Format("<{0}@0{1:X} start=0x{2:X} end=0x{3:X} length=0x{4:X} region={5}>", this.GetType().Name, "?", //id(this), 
                    this.start, this.end, this.length, this.region);
            }
        }

        public class MemoryRegion
            : MemoryRangeBase
        {

            private UInt32 _blocksize;
            private string _name;
            private bool _is_boot_mem;
            private bool _isPoweredOnBoot;
            private bool _isCacheable;
            private bool _invalidateCacheOnRun;

            public MemoryRegion(
                UInt32 start = 0,
                UInt32 end = 0,
                UInt32 length = 0,
                UInt32 blocksize = 0,
                string name = "",
                bool isBootMemory = false,
                bool isPoweredOnBoot = true,
                bool isCacheable = true,
                bool invalidateCacheOnRun = true)
                : base(end: end, length: length)
            {
                this._blocksize = blocksize;
                if (String.IsNullOrWhiteSpace(name))
                {
                    this._name = this.GetType().Name;
                }
                else
                {
                    this._name = name;
                }
                this._is_boot_mem = isBootMemory;
                this._isPoweredOnBoot = isPoweredOnBoot;
                this._isCacheable = isCacheable;
                this._invalidateCacheOnRun = invalidateCacheOnRun;
            }

            public UInt32 blocksize
            {
                get
                {
                    return this._blocksize;
                }
            }

            public object name
            {
                get
                {
                    return this._name;
                }
            }

            public bool isFlash
            {
                get
                {
                    return typeof(FlashRegion).Equals(this.GetType());
                }
            }

            public bool isRam
            {
                get
                {
                    return typeof(RamRegion).Equals(this.GetType());
                }
            }

            public bool isRom
            {
                get
                {
                    return typeof(RomRegion).Equals(this.GetType());
                }
            }

            public bool isDevice
            {
                get
                {
                    return typeof(DeviceRegion).Equals(this.GetType());
                }
            }

            public bool isAlias
            {
                get
                {
                    return typeof(AliasRegion).Equals(this.GetType());
                }
            }

            public bool isBootMemory
            {
                get
                {
                    return this._is_boot_mem;
                }
            }

            public bool isPoweredOnBoot
            {
                get
                {
                    return this._isPoweredOnBoot;
                }
            }

            public bool isCacheable
            {
                get
                {
                    return this._isCacheable;
                }
            }

            public object invalidateCacheOnRun
            {
                get
                {
                    return this._invalidateCacheOnRun;
                }
            }

            public virtual string @__repr__()
            {
                return String.Format("<{0}@0x{1:X} name={2} type={3} start=0x{4:X} end=0x{5:X} length=0x{6:X} blocksize=0x{7:X}>",
                    this.GetType().Name,
                    "?", //id(this), 
                    this.name, this.GetType().Name, this.start, this.end, this.length, this.blocksize);
            }
        }

        public class RamRegion
            : MemoryRegion
        {

            public RamRegion(
                UInt32 start = 0,
                UInt32 end = 0,
                UInt32 length = 0,
                string name = "",
                bool isBootMemory = false,
                bool isPoweredOnBoot = true,
                bool isCacheable = true,
                bool invalidateCacheOnRun = true)
                : base(start: start, end: end, length: length, name: name, isBootMemory: isBootMemory, isPoweredOnBoot: isPoweredOnBoot, isCacheable: isCacheable, invalidateCacheOnRun: invalidateCacheOnRun)
            {
            }
        }

        public class RomRegion
            : MemoryRegion
        {

            public RomRegion(
                UInt32 start = 0,
                UInt32 end = 0,
                UInt32 length = 0,
                string name = "",
                bool isBootMemory = false,
                bool isPoweredOnBoot = true,
                bool isCacheable = true,
                bool invalidateCacheOnRun = false)
                : base(start: start, end: end, length: length, name: name, isBootMemory: isBootMemory, isPoweredOnBoot: isPoweredOnBoot, isCacheable: isCacheable, invalidateCacheOnRun: invalidateCacheOnRun)
            {
            }
        }

        public class FlashRegion
            : MemoryRegion
        {

            public FlashRegion(
                UInt32 start = 0,
                UInt32 end = 0,
                UInt32 length = 0,
                UInt32 blocksize = 0,
                string name = "",
                bool isBootMemory = false,
                bool isPoweredOnBoot = true,
                bool isCacheable = true,
                bool invalidateCacheOnRun = true)
                : base(start: start, end: end, length: length, blocksize: blocksize, name: name, isBootMemory: isBootMemory, isPoweredOnBoot: isPoweredOnBoot, isCacheable: isCacheable, invalidateCacheOnRun: invalidateCacheOnRun)
            {
            }
        }

        public class DeviceRegion
            : MemoryRegion
        {

            public DeviceRegion(
                UInt32 start = 0,
                UInt32 end = 0,
                UInt32 length = 0,
                string name = "",
                bool isPoweredOnBoot = true)
                : base(start: start, end: end, length: length, name: name, isBootMemory: false, isPoweredOnBoot: isPoweredOnBoot, isCacheable: false, invalidateCacheOnRun: true)
            {
            }
        }

        public class AliasRegion
            : MemoryRegion
        {
            private object _alias_reference;
            public AliasRegion(
                UInt32 start = 0,
                UInt32 end = 0,
                UInt32 length = 0,
                UInt32 blocksize = 0,
                string name = "",
                object aliasOf = null,
                bool isBootMemory = false,
                bool isPoweredOnBoot = true,
                bool isCacheable = true,
                bool invalidateCacheOnRun = true)
                : base(start: start, end: end, length: length, name: name, isBootMemory: isBootMemory, isPoweredOnBoot: isPoweredOnBoot, isCacheable: isCacheable, invalidateCacheOnRun: invalidateCacheOnRun)
            {
                this._alias_reference = aliasOf;
            }

            public object aliased_region
            {
                get
                {
                    return this._alias_reference;
                }
            }
        }

        public class MemoryMap
        {

            List<MemoryRegion> _regions;

            public MemoryMap(params object[] moreRegions)
            {
                this._regions = new List<MemoryRegion>();
                foreach (object mR in moreRegions)
                {
                    if (mR is List<MemoryRegion>)
                    {
                        this._regions = (List<MemoryRegion>)mR;
                    }
                    else
                    {
                        this._regions.Add((MemoryRegion)mR);
                    }
                }
                this._regions = this._regions.OrderBy(x => x.start).ToList();
            }

            public object regions
            {
                get
                {
                    return this._regions;
                }
            }

            public object regionCount
            {
                get
                {
                    return this._regions.Count;
                }
            }

            public virtual void addRegion(MemoryRegion newRegion)
            {
                this._regions.Add(newRegion);
                this._regions = this._regions.OrderBy(x => x.start).ToList(); // Sort
            }

            public virtual MemoryRegion getBootMemory()
            {
                return this._regions.FirstOrDefault(r => r.isBootMemory);
            }

            public virtual MemoryRegion getRegionForAddress(UInt32 address)
            {
                foreach (var r in this._regions)
                {
                    if (r.containsAddress(address))
                    {
                        return r;
                    }
                }
                return null;
            }

            public virtual object getRegionByName(object name)
            {
                foreach (var r in this._regions)
                {
                    if (r.name == name)
                    {
                        return r;
                    }
                }
                return null;
            }

            public virtual bool isValidAddress(UInt32 address)
            {
                return this.getRegionForAddress(address) != null;
            }

            public virtual List<MemoryRegion> getContainedRegions(object start, UInt32? end = null, UInt32? length = null, MemoryRange range = null)
            {
                var _tup_1 = check_range(start, end, length, range);
                start = _tup_1.Item1;
                end = _tup_1.Item2;
                return this._regions.Where(r => r.containedByRange(start, end)).ToList();
            }

            public virtual List<MemoryRegion> getIntersectingRegions(object start, UInt32? end = null, UInt32? length = null, MemoryRange range = null)
            {
                var _tup_1 = check_range(start, end, length, range);
                start = _tup_1.Item1;
                end = _tup_1.Item2;
                return this._regions.Where(r => r.intersectsRange(start, end)).ToList();
            }

            // Generate GDB memory map XML.
            public virtual object getXML()
            {
                throw new NotImplementedException();
                // var root = ElementTree.Element("memory-map");
                // foreach (var r in this._regions)
                // {
                //     var mem = ElementTree.SubElement(root, "memory", type: r.type, start: hex(r.start).rstrip("L"), length: hex(r.length).rstrip("L"));
                //     if (r.isFlash)
                //     {
                //         var prop = ElementTree.SubElement(mem, "property", name: "blocksize");
                //         prop.text = hex(r.blocksize).rstrip("L");
                //     }
                // }
                // return MAP_XML_HEADER + ElementTree.tostring(root);
            }

            // Enable iteration over the memory map.
            public virtual object @__iter__()
            {
                throw new NotImplementedException();
                //return iter(this._regions);
            }

            public virtual string @__repr__()
            {
                throw new NotImplementedException();
                //return String.Format("<MemoryMap@0x{0:X8} regions={1}>", "?", //id(this), 
                //    repr(this._regions));
            }
        }
    }

}
