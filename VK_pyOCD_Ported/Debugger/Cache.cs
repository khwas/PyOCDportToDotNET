namespace openocd.Debugger
{
    using System.Collections.Generic;
    using System;
    using System.Linq;
    using System.Collections;
    using System.Diagnostics;
    using openocd.CoreSight;
    using RangeTree;
    using openocd.Utility;

    public class MemoryAccessError
        : Exception
    {
    }

    /// <summary>
    /// A simple example cass, which contains an integer range and a text property.
    /// </summary>
    public class RangeItem : IRangeProvider<UInt32>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RangeItem"/> class.
        /// </summary>
        /// <param name="a">a.</param>
        /// <param name="b">The b.</param>
        public RangeItem(UInt32 a, UInt32 b, List<byte> data = null)
        {
            Range = new Range<UInt32>(a, b);
        }

        /// <summary>
        /// Gets or sets the range.
        /// </summary>
        /// <value>
        /// The range.
        /// </value>
        public Range<UInt32> Range
        {
            get;
            set;
        }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("({0} .. {1})", Range.From, Range.To);
        }

        /// <summary>
        /// Determines whether the specified <see cref="RangeItem" />, is equal to this instance.
        /// </summary>
        /// <param name="other">The <see cref="RangeItem" /> to compare with this instance.</param>
        /// <returns>
        ///     <c>true</c> if the specified <see cref="RangeItem" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected bool Equals(RangeItem other)
        {
            return Range.Equals(other.Range);
        }

        /// <summary>
        /// Determines whether the specified <see cref="object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((RangeItem)obj);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((0) * 397) ^ Range.GetHashCode();
            }
        }

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator ==(RangeItem left, RangeItem right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=(RangeItem left, RangeItem right)
        {
            return !Equals(left, right);
        }
    }

    /// <summary>
    /// Compares two range items by comparing their ranges.
    /// </summary>
    public class RangeItemComparer : IComparer<RangeItem>
    {
        /// <summary>
        /// Compares two objects and returns a value indicating whether one is less than, equal to, or greater than the other.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        /// <returns>
        /// A signed integer that indicates the relative values of <paramref name="x" /> and <paramref name="y" />, as shown in the following table.Value Meaning Less than zero<paramref name="x" /> is less than <paramref name="y" />.Zero<paramref name="x" /> equals <paramref name="y" />.Greater than zero<paramref name="x" /> is greater than <paramref name="y" />.
        /// </returns>
        public int Compare(RangeItem x, RangeItem y)
        {
            return x.Range.CompareTo(y.Range);
        }
    }
    public class CacheMetrics
    {
        internal UInt32 hits;
        internal UInt32 misses;
        internal UInt32 reads;
        internal UInt32 writes;

        public CacheMetrics()
        {
            this.hits = 0;
            this.misses = 0;
            this.reads = 0;
            this.writes = 0;
        }

        public UInt32 total
        {
            get
            {
                return this.hits + this.misses;
            }
        }

        public double percent_hit
        {
            get
            {
                if (this.total > 0)
                {
                    return this.hits * 100.0 / this.total;
                }
                else
                {
                    return 0;
                }
            }
        }

        public object percent_miss
        {
            get
            {
                if (this.total > 0)
                {
                    return this.misses * 100.0 / this.total;
                }
                else
                {
                    return 0;
                }
            }
        }
    }

    public class RegisterCache
    {
        internal Dictionary<object, object> _cache;
        internal CacheMetrics _metrics;
        internal int _run_token;
        internal DebugContext _context;

        public List<sbyte> CFBP_REGS = new List<sbyte>
            {
                CortexM.CORE_REGISTER["cfbp"],
                CortexM.CORE_REGISTER["control"],
                CortexM.CORE_REGISTER["faultmask"],
                CortexM.CORE_REGISTER["basepri"],
                CortexM.CORE_REGISTER["primask"]
            };

        public RegisterCache(DebugContext parentContext)
        {
            this._context = parentContext;
            this._run_token = -1;
            //this._log = logging.getLogger("regcache");
            this._reset_cache();
        }

        public virtual void _reset_cache()
        {
            this._cache = new Dictionary<object, object>
            {
            };
            this._metrics = new CacheMetrics();
        }

        public virtual void _dump_metrics()
        {
            if (this._metrics.total > 0)
            {
                Trace.TraceInformation("%d reads [%d%% hits, %d regs]", this._metrics.total, this._metrics.percent_hit, this._metrics.hits);
            }
            else
            {
                Trace.TraceInformation("no accesses");
            }
        }

        public virtual void _check_cache()
        {
            if (this._context.core.isRunning())
            {
                Trace.TraceInformation("core is running; invalidating cache");
                this._reset_cache();
            }
            else if (this._run_token != this._context.core.run_token)
            {
                this._dump_metrics();
                Trace.TraceInformation("out of date run token; invalidating cache");
                this._reset_cache();
                this._run_token = this._context.core.run_token;
            }
        }

        public virtual List<sbyte> _convert_and_check_registers(List<string> reg_list_s)
        {
            // convert to index only
            List<sbyte> reg_list = reg_list_s.Select(reg => CortexM.register_name_to_index(reg)).ToList();
            // Sanity check register values
            foreach (var reg in reg_list)
            {
                if (!CortexM.CORE_REGISTER.Values.Contains(reg))
                {
                    throw new ArgumentOutOfRangeException(String.Format("unknown reg: %d", reg));
                }
                else if ((reg >= 64 || reg == 33) && !this._context.core.has_fpu)
                {
                    throw new ArgumentOutOfRangeException("attempt to read FPU register without FPU");
                }
            }
            return reg_list;
        }

        public virtual List<UInt32> readCoreRegistersRaw(List<string> reg_list_s)
        {
            object v;
            this._check_cache();
            List<sbyte> reg_list = this._convert_and_check_registers(reg_list_s);
            var reg_set = new HashSet<sbyte>(reg_list);
            // Get list of values we have cached.
            HashSet<sbyte> cached_set = new HashSet<sbyte>(reg_list.Where(r => this._cache.ContainsKey(r)).Select(r => r));
            this._metrics.hits += (UInt32)cached_set.Count;
            // Read uncached registers from the target.
            List<sbyte> read_list = reg_set.Except(cached_set).ToList();
            bool reading_cfbp = read_list.Where(r => this.CFBP_REGS.Contains(r)).Any();
            if (reading_cfbp)
            {
                if (!read_list.Contains(CortexM.CORE_REGISTER["cfbp"]))
                {
                    read_list.Add(CortexM.CORE_REGISTER["cfbp"]);
                }
                var cfbp_index = read_list.IndexOf(CortexM.CORE_REGISTER["cfbp"]);
            }
            this._metrics.misses += (UInt32)read_list.Count;
            var values = this._context.readCoreRegistersRaw(read_list);
            // Update all CFBP based registers.
            if (reading_cfbp)
            {
                v = values[cfbp_index];
                this._cache[CortexM.CORE_REGISTER["cfbp"]] = v;
                foreach (var r in this.CFBP_REGS)
                {
                    if (r == CortexM.CORE_REGISTER["cfbp"])
                    {
                        continue;
                    }
                    this._cache[r] = v >> (-r - 1) * 8 & 0xFF;
                }
            }
            // Build the results list in the same order as requested registers.
            List<UInt32> results = new List<UInt32>();
            foreach (var r in reg_list)
            {
                if (cached_set.Contains(r))
                {
                    results.Add(this._cache[r]);
                }
                else
                {
                    var i = read_list.IndexOf(r);
                    v = values[i];
                    results.Add(v);
                    this._cache[r] = v;
                }
            }
            return results;
        }

        // TODO only write dirty registers to target right before running.
        public virtual void writeCoreRegistersRaw(List<string> reg_list_s, List<UInt32> data_list)
        {
            this._check_cache();
            List<sbyte> reg_list = this._convert_and_check_registers(reg_list_s);
            this._metrics.writes += (UInt32)reg_list.Count;
            bool writing_cfbp = reg_list.Where(r => this.CFBP_REGS.Contains(r)).Any();
            // Update cached register values.
            foreach (var _tup_1 in reg_list.Select((_p_1, _p_2) => Tuple.Create(_p_2, _p_1)))
            {
                var i = _tup_1.Item1;
                var r = _tup_1.Item2;
                var v = data_list[i];
                this._cache[r] = v;
            }
            // Just remove all cached CFBP based register values.
            if (writing_cfbp)
            {
                foreach (var r in this.CFBP_REGS)
                {
                    try
                    {
                        this._cache.Remove(r);
                    }
                    catch (KeyNotFoundException)
                    {
                    }
                }
            }
            // Write new register values to target.
            this._context.writeCoreRegistersRaw(reg_list, data_list);
        }

        public virtual void invalidate()
        {
            this._reset_cache();
        }
    }

    public class MemoryCache
    {
        internal DebugContext _context;
        internal int _run_token;
        internal RangeTree.RangeTree<UInt32, RangeItem> _cache;
        internal CacheMetrics _metrics;

        public MemoryCache(DebugContext context)
        {
            this._context = context;
            this._run_token = -1;
            //this._log = logging.getLogger("memcache");
            this._reset_cache();
        }

        public virtual void _reset_cache()
        {
            this._cache = new RangeTree.RangeTree<UInt32, RangeItem>(new RangeItemComparer());
            this._metrics = new CacheMetrics();
        }

        //#
        // @brief Invalidates the cache if appropriate.
        public virtual void _check_cache()
        {
            if (this._context.core.isRunning())
            {
                Trace.TraceInformation("core is running; invalidating cache");
                this._reset_cache();
            }
            else if (this._run_token != this._context.core.run_token)
            {
                this._dump_metrics();
                Trace.TraceInformation("out of date run token; invalidating cache");
                this._reset_cache();
                this._run_token = this._context.core.run_token;
            }
        }

        //#
        // @brief Splits a memory address range into cached and uncached subranges.
        // @return Returns a 2-tuple with the first element being a set of Interval objects for each
        //   of the cached subranges. The second element is a set of Interval objects for each of the
        //   non-cached subranges.
        public virtual Tuple<HashSet<RangeItem>, HashSet<RangeItem>> _get_ranges(UInt32 addr, UInt32 count)
        {
            HashSet<RangeItem> cached = new HashSet<RangeItem>(this._cache.Query(new RangeItem(addr, addr + count).Range));
            HashSet<RangeItem> uncached = new HashSet<RangeItem>() {
                    {
                        new RangeItem(addr, addr + count)}
                    };
            foreach (RangeItem cachedIv in cached)
            {
                var newUncachedSet = new HashSet<RangeItem> ();
                foreach (var uncachedIv in uncached)
                {
                    // No overlap.
                    if (cachedIv.Range.To < uncachedIv.Range.From || cachedIv.Range.From > uncachedIv.Range.To)
                    {
                        newUncachedSet.Add(uncachedIv);
                        continue;
                    }
                    // Begin segment.
                    if (cachedIv.Range.From - uncachedIv.Range.From > 0)
                    {
                        newUncachedSet.Add(new RangeItem(uncachedIv.Range.From, cachedIv.Range.From));
                    }
                    // End segment.
                    if (uncachedIv.Range.To - cachedIv.Range.To > 0)
                    {
                        newUncachedSet.Add(new RangeItem(cachedIv.Range.To, uncachedIv.Range.To));
                    }
                }
                uncached = newUncachedSet;
            }
            return Tuple.Create(cached, uncached);
        }

        //#
        // @brief Reads uncached memory ranges and updates the cache.
        // @return A list of Interval objects is returned. Each Interval has its @a data attribute set
        //   to a bytearray of the data read from target memory.
        public virtual object _read_uncached(HashSet<RangeItem> uncached)
        {
            var uncachedData = new List<object>();
            foreach (var uncachedIv in uncached)
            {
                List<byte> data = this._context.readBlockMemoryUnaligned8(uncachedIv.Range.From, uncachedIv.Range.To - uncachedIv.Range.From);
                var iv = new RangeItem(uncachedIv.Range.From, uncachedIv.Range.To, data);
                this._cache.Add(iv);
                uncachedData.Add(iv);
            }
            return uncachedData;
        }

        public virtual void _update_metrics(HashSet<RangeItem> cached, HashSet<RangeItem> uncached, UInt32 addr, UInt32 size)
        {
            var cachedSize = 0;
            foreach (var iv in cached)
            {
                var begin = iv.From;
                var end = iv.To;
                if (iv.From < addr)
                {
                    begin = addr;
                }
                if (iv.To > addr + size)
                {
                    end = addr + size;
                }
                cachedSize += end - begin;
            }
            var uncachedSize = uncached.Select(iv => iv.To - iv.From).Sum();
            this._metrics.reads += 1;
            this._metrics.hits += cachedSize;
            this._metrics.misses += uncachedSize;
        }

        public virtual void _dump_metrics()
        {
            if (this._metrics.total > 0)
            {
                Trace.TraceInformation("%d reads, %d bytes [%d%% hits, %d bytes]; %d bytes written", this._metrics.reads, this._metrics.total, this._metrics.percent_hit, this._metrics.hits, this._metrics.writes);
            }
            else
            {
                Trace.TraceInformation("no reads");
            }
        }

        //#
        // @brief Performs a cached read operation of an address range.
        // @return A list of Interval objects sorted by address.
        public virtual object _read(UInt32 addr, UInt32 size)
        {
            // Get the cached and uncached subranges of the requested read.
            var _tup_1 = this._get_ranges(addr, size);
            var cached = _tup_1.Item1;
            var uncached = _tup_1.Item2;
            this._update_metrics(cached, uncached, addr, size);
            // Read any uncached ranges.
            var uncachedData = this._read_uncached(uncached);
            // Merged cached with data we just read
            var combined = cached.ToList() + uncachedData;
            combined.sort(key: x => x.begin);
            return combined;
        }

        //#
        // @brief Extracts data from the intersection of an address range across a list of interval objects.
        //
        // The range represented by @a addr and @a size are assumed to overlap the intervals. The first
        // and last interval in the list may have ragged edges not fully contained in the address range, in
        // which case the correct slice of those intervals is extracted.
        //
        // @param self
        // @param combined List of Interval objects forming a contiguous range. The @a data attribute of
        //   each interval must be a bytearray.
        // @param addr Start address. Must be within the range of the first interval.
        // @param size Number of bytes. (@a addr + @a size) must be within the range of the last interval.
        // @return A single bytearray object with all data from the intervals that intersects the address
        //   range.
        public virtual List<byte> _merge_data(object combined, object addr, object size)
        {
            object offset;
            var result = bytearray();
            var resultAppend = bytearray();
            // Take slice of leading ragged edge.
            if (combined.Count && combined[0].begin < addr)
            {
                offset = addr - combined[0].begin;
                result += combined[0].data[offset];
                combined = combined[1];
            }
            // Take slice of trailing ragged edge.
            if (combined.Count && combined[-1].end > addr + size)
            {
                offset = addr + size - combined[-1].begin;
                resultAppend = combined[-1].data[::offset];
                combined = combined[:: - 1];
            }
            // Merge.
            foreach (var iv in combined)
            {
                result += iv.data;
            }
            result += resultAppend;
            return result;
        }

        //#
        // @brief
        public virtual void _update_contiguous(object cached, UInt32 addr, List<object> value)
        {
            object offset;
            var size = value.Count;
            var end = addr + size;
            var leadBegin = addr;
            var leadData = bytearray();
            var trailData = bytearray();
            var trailEnd = end;
            if (cached[0].begin < addr && cached[0].end > addr)
            {
                offset = addr - cached[0].begin;
                leadData = cached[0].data[::offset];
                leadBegin = cached[0].begin;
            }
            if (cached[-1].begin < end && cached[-1].end > end)
            {
                offset = end - cached[-1].begin;
                trailData = cached[-1].data[offset];
                trailEnd = cached[-1].end;
            }
            this._cache.remove_overlap(addr, end);
            var data = leadData + value + trailData;
            this._cache.addi(leadBegin, trailEnd, data);
        }

        //#
        // @return A bool indicating whether the given address range is fully contained within
        //       one known memory region, and that region is cacheable.
        // @exception MemoryAccessError Raised if the access is not entirely contained within a single region.
        public virtual bool _check_regions(UInt32 addr, UInt32 count)
        {
            var regions = this._context.core.memory_map.getIntersectingRegions(addr, length: count);
            // If no regions matched, then allow an uncached operation.
            if (regions.Count == 0)
            {
                return false;
            }
            // Raise if not fully contained within one region.
            if (regions.Count > 1 || !regions[0].containsRange(addr, length: count))
            {
                throw new Exception("individual memory accesses must not cross memory region boundaries"); // MemoryAccessError
            }
            // Otherwise return whether the region is cacheable.
            return regions[0].isCacheable;
        }

        public virtual object readMemory(UInt32 addr, byte transfer_size = 32, bool now = true)
        {
            // TODO use more optimal underlying readMemory call
            object data;
            if (transfer_size == 8)
            {
                data = this.readBlockMemoryUnaligned8(addr, 1)[0];
            }
            else if (transfer_size == 16)
            {
                data = Conversion.byteListToU16leList(this.readBlockMemoryUnaligned8(addr, 2))[0];
            }
            else if (transfer_size == 32)
            {
                data = Conversion.byteListToU32leList(this.readBlockMemoryUnaligned8(addr, 4))[0];
            }
            else
            {
                throw new Exception();
            }
            if (now)
            {
                return data;
            }
            else
            {
                Func<object> read_cb = () =>
                {
                    return data;
                };
                return read_cb;
            }
        }

        public virtual List<byte> readBlockMemoryUnaligned8(UInt32 addr, UInt32 size)
        {
            if (size <= 0)
            {
                return new List<byte>();
            }
            this._check_cache();
            // Validate memory regions.
            if (!this._check_regions(addr, size))
            {
                Trace.TraceInformation("range [%x:%x] is not cacheable", addr, addr + size);
                return this._context.readBlockMemoryUnaligned8(addr, size);
            }
            // Get the cached and uncached subranges of the requested read.
            var combined = this._read(addr, size);
            // Extract data out of combined intervals.
            List<byte> result = this._merge_data(combined, addr, size).ToList();
            return result;
        }

        public virtual List<UInt32> readBlockMemoryAligned32(UInt32 addr, UInt32 size)
        {
            return Conversion.byteListToU32leList(this.readBlockMemoryUnaligned8(addr, size * 4));
        }

        public virtual void writeMemory(UInt32 addr, UInt32 value, byte transfer_size = 32)
        {
            if (transfer_size == 8)
            {
                this.writeBlockMemoryUnaligned8(addr, new List<byte> {
                        (byte)value
                    });
            }
            else if (transfer_size == 16)
            {
                this.writeBlockMemoryUnaligned8(addr, Conversion.u16leListToByteList(new List<UInt16> {
                        (UInt16)value
                    }));
            }
            else if (transfer_size == 32)
            {
                this.writeBlockMemoryUnaligned8(addr, Conversion.u32leListToByteList(new List<UInt32> {
                        value
                    }));
            }
            else
            {
                throw new Exception();
            }
        }

        public virtual void writeBlockMemoryUnaligned8(UInt32 addr, List<byte> value)
        {
            if (value.Count <= 0)
            {
                return;
            }
            this._check_cache();
            // Validate memory regions.
            var cacheable = this._check_regions(addr, (UInt32)value.Count);
            // Write to the target first, so if it fails we don't update the cache.
            this._context.writeBlockMemoryUnaligned8(addr, value);
            if (cacheable)
            {
                var size = value.Count;
                var end = addr + size;
                var cached = this._cache.search(addr, end).OrderBy(x => x.begin).ToList();
                this._metrics.writes += (UInt32)size;
                if (cached.Any())
                {
                    // Write data is entirely within cached data.
                    if (addr >= cached[0].begin && end <= cached[0].end)
                    {
                        var beginOffset = addr - cached[0].begin;
                        var endOffset = end - cached[0].end;
                        cached[0].data.GetRange(beginOffset, endOffset - beginOffset) = value;
                    }
                    else
                    {
                        this._update_contiguous(cached, addr, value);
                    }
                }
                else
                {
                    // No cached data in this range, so just add the entire interval.
                    this._cache.addi(addr, end, value);
                }
            }
        }

        public virtual void writeBlockMemoryAligned32(UInt32 addr, List<UInt32> data)
        {
            this.writeBlockMemoryUnaligned8(addr, Conversion.u32leListToByteList(data));
        }

        public virtual void invalidate()
        {
            this._reset_cache();
        }
    }

    public class CachingDebugContext
        : DebugContext
    {
        internal MemoryCache _memcache;
        internal RegisterCache _regcache;

        public CachingDebugContext(Core.Target core, DebugContext parentContext) : base(core)
        {
            this._regcache = new RegisterCache(parentContext);
            this._memcache = new MemoryCache(parentContext);
        }

        public override void writeMemory(UInt32 addr, UInt32 value, byte transfer_size = 32)
        {
            this._memcache.writeMemory(addr, value, transfer_size);
        }

        public override object readMemory(UInt32 addr, byte transfer_size = 32, bool now = true)
        {
            return this._memcache.readMemory(addr, transfer_size, now);
        }

        public override void writeBlockMemoryUnaligned8(UInt32 addr, List<byte> value)
        {
            this._memcache.writeBlockMemoryUnaligned8(addr, value);
        }

        public override void writeBlockMemoryAligned32(UInt32 addr, List<UInt32> data)
        {
            this._memcache.writeBlockMemoryAligned32(addr, data);
        }

        public override List<byte> readBlockMemoryUnaligned8(UInt32 addr, UInt32 size)
        {
            return this._memcache.readBlockMemoryUnaligned8(addr, size);
        }

        public override List<UInt32> readBlockMemoryAligned32(UInt32 addr, UInt32 size)
        {
            return this._memcache.readBlockMemoryAligned32(addr, size);
        }

        public override List<UInt32> readCoreRegistersRaw(List<string> reg_list)
        {
            return this._regcache.readCoreRegistersRaw(reg_list);
        }

        public virtual void writeCoreRegistersRaw(List<string> reg_list, List<UInt32> data_list)
        {
            this._regcache.writeCoreRegistersRaw(reg_list, data_list);
        }

        public virtual void invalidate()
        {
            this._regcache.invalidate();
            this._memcache.invalidate();
        }
    }
}
