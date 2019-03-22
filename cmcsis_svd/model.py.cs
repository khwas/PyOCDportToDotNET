
using json;

using six;

using System.Collections;

using System.Collections.Generic;

using System;

namespace cmsis_svd
{
    public static class model
    {

        public static object NOT_PRESENT = new object();

        public static HashSet<string> TO_DICT_SKIP_KEYS = new HashSet<string> {
        {
            "_register_arrays"},
        {
            "parent"}};

        public static HashSet<string> REGISTER_PROPERTY_KEYS = new HashSet<string> {
        {
            "size"},
        {
            "access"},
        {
            "protection"},
        {
            "reset_value"},
        {
            "reset_mask"}};

        public static HashSet<string> LIST_TYPE_KEYS = new HashSet<string> {
        {
            "register_arrays"},
        {
            "registers"},
        {
            "fields"},
        {
            "peripherals"},
        {
            "interrupts"}};

        // Perform type checking on the provided value
        // 
        //     This is a helper that will raise ``TypeError`` if the provided value is
        //     not an instance of the provided type.  This method should be used sparingly
        //     but can be good for preventing problems earlier when you want to restrict
        //     duck typing to make the types of fields more obvious.
        // 
        //     If the value passed the type check it will be returned from the call.
        //     
        public static object _check_type(object value, object expected_type)
        {
            if (!(value is expected_type))
            {
                throw TypeError("Value {value!r} has unexpected type {actual_type!r}, expected {expected_type!r}".format(value: value, expected_type: expected_type, actual_type: type(value)));
            }
            return value;
        }

        public static object _none_as_empty(object v)
        {
            if (v != null)
            {
                foreach (var e in v)
                {
                    yield return e;
                }
            }
        }

        public class SVDJSONEncoder
            : json.JSONEncoder
        {

            public virtual object @default(object obj)
            {
                if (obj is SVDElement)
                {
                    var eldict = new Dictionary<object, object>
                    {
                    };
                    foreach (var _tup_1 in six)
                    {
                        var k = _tup_1.Item1;
                        var v = _tup_1.Item2;
                        if (TO_DICT_SKIP_KEYS.Contains(k))
                        {
                            continue;
                        }
                        if (k.startswith("_"))
                        {
                            var pubkey = k[1];
                            eldict[pubkey] = getattr(obj, pubkey);
                        }
                        else
                        {
                            eldict[k] = v;
                        }
                    }
                    return eldict;
                }
                else
                {
                    return json.JSONEncoder.@default(this, obj);
                }
            }
        }

        // Base class for all SVD Elements
        public class SVDElement
        {

            public SVDElement()
            {
                this.parent = null;
            }

            public virtual object _lookup_possibly_derived_attribute(object attr)
            {
                object value;
                object value_self;
                var derived_from = this.get_derived_from();
                // see if there is an attribute with the same name and leading underscore
                try
                {
                    value_self = object.@__getattribute__(this, "_{}".format(attr));
                }
                catch (AttributeError)
                {
                    value_self = NOT_PRESENT;
                }
                if (object.ReferenceEquals(value_self, NOT_PRESENT))
                {
                    throw AttributeError("Requested missing key");
                }
                else if (value_self != null)
                {
                    // if there is a non-None value, that is what we want to use
                    return value_self;
                }
                else if (derived_from != null)
                {
                    // if there is a derivedFrom, check there first
                    var derived_value = getattr(derived_from, attr, NOT_PRESENT);
                    if (derived_value != NOT_PRESENT)
                    {
                        return derived_value;
                    }
                }
                // for some attributes, try to grab from parent
                if (REGISTER_PROPERTY_KEYS.Contains(attr))
                {
                    value = getattr(this.parent, attr, value_self);
                }
                else
                {
                    value = value_self;
                }
                // if value is None and this is a list type, transform to empty list
                if (value == null && LIST_TYPE_KEYS.Contains(attr))
                {
                    value = new List<object>();
                }
                return value;
            }

            public virtual object get_derived_from()
            {
            }

            public virtual object to_dict()
            {
                // This is a little convoluted but it works and ensures a
                // json-compatible dictionary representation (at the cost of
                // some computational overhead)
                var encoder = SVDJSONEncoder();
                return json.loads(encoder.encode(this));
            }
        }

        public class SVDEnumeratedValue
            : SVDElement
        {

            public SVDEnumeratedValue(object name, object description, object value, object is_default)
            {
                this.name = name;
                this.description = description;
                this.value = value;
                this.is_default = is_default;
            }
        }

        public class SVDField
            : SVDElement
        {

            public SVDField(
                object name,
                object derived_from,
                object description,
                object bit_offset,
                object bit_width,
                object access,
                object enumerated_values,
                object modified_write_values,
                object read_action)
            {
                this.name = name;
                this.derived_from = derived_from;
                this.description = description;
                this.bit_offset = bit_offset;
                this.bit_width = bit_width;
                this.access = access;
                this.enumerated_values = enumerated_values;
                this.modified_write_values = modified_write_values;
                this.read_action = read_action;
            }

            public virtual object @__getattr__(object attr)
            {
                return this._lookup_possibly_derived_attribute(attr);
            }

            public virtual object get_derived_from()
            {
                // TODO: add support for dot notation derivedFrom
                if (this.derived_from == null)
                {
                    return null;
                }
                foreach (var field in this.parent.fields)
                {
                    if (field.name == this.derived_from)
                    {
                        return field;
                    }
                }
                throw KeyError(String.Format("Unable to find derived_from: %r", this.derived_from));
            }

            // Return True if the field is an enumerated type
            public object is_enumerated_type
            {
                get
                {
                    return this.enumerated_values != null;
                }
            }

            public object is_reserved
            {
                get
                {
                    return this.name.lower() == "reserved";
                }
            }
        }

        // Represent a register array in the tree
        public class SVDRegisterArray
            : SVDElement
        {

            public SVDRegisterArray(
                object name,
                object derived_from,
                object description,
                object address_offset,
                object size,
                object access,
                object protection,
                object reset_value,
                object reset_mask,
                object fields,
                object display_name,
                object alternate_group,
                object modified_write_values,
                object read_action,
                object dim,
                object dim_indices,
                object dim_increment)
            {
                // When deriving a register, it is mandatory to specify at least the name, the description,
                // and the addressOffset
                this.derived_from = derived_from;
                this.name = name;
                this.description = description;
                this.address_offset = address_offset;
                this.dim = dim;
                this.dim_indices = dim_indices;
                this.dim_increment = dim_increment;
                this._read_action = read_action;
                this._modified_write_values = modified_write_values;
                this._display_name = display_name;
                this._alternate_group = alternate_group;
                this._size = size;
                this._access = access;
                this._protection = protection;
                this._reset_value = reset_value;
                this._reset_mask = reset_mask;
                this._fields = fields;
                // make parent association
                foreach (var field in this._fields)
                {
                    field.parent = this;
                }
            }

            public virtual object @__getattr__(object attr)
            {
                return this._lookup_possibly_derived_attribute(attr);
            }

            public object registers
            {
                get
                {
                    foreach (var i in six.moves.range(this.dim))
                    {
                        var reg = SVDRegister(name: this.name % this.dim_indices[i], fields: this._fields, derived_from: this.derived_from, description: this.description, address_offset: this.address_offset + this.dim_increment * i, size: this._size, access: this._access, protection: this._protection, reset_value: this._reset_value, reset_mask: this._reset_mask, display_name: this._display_name, alternate_group: this._alternate_group, modified_write_values: this._modified_write_values, read_action: this._read_action);
                        reg.parent = this.parent;
                        yield return reg;
                    }
                }
            }

            public virtual object get_derived_from()
            {
                // TODO: add support for dot notation derivedFrom
                if (this.derived_from == null)
                {
                    return null;
                }
                foreach (var register in this.parent.registers)
                {
                    if (register.name == this.derived_from)
                    {
                        return register;
                    }
                }
                throw KeyError(String.Format("Unable to find derived_from: %r", this.derived_from));
            }

            public virtual object is_reserved()
            {
                return this.name.lower().Contains("reserved");
            }
        }

        public class SVDRegister
            : SVDElement
        {

            public SVDRegister(
                object name,
                object derived_from,
                object description,
                object address_offset,
                object size,
                object access,
                object protection,
                object reset_value,
                object reset_mask,
                object fields,
                object display_name,
                object alternate_group,
                object modified_write_values,
                object read_action)
            {
                // When deriving a register, it is mandatory to specify at least the name, the description,
                // and the addressOffset
                this.derived_from = derived_from;
                this.name = name;
                this.description = description;
                this.address_offset = address_offset;
                this._read_action = read_action;
                this._modified_write_values = modified_write_values;
                this._display_name = display_name;
                this._alternate_group = alternate_group;
                this._size = size;
                this._access = access;
                this._protection = protection;
                this._reset_value = reset_value;
                this._reset_mask = reset_mask;
                this._fields = fields;
                // make parent association
                foreach (var field in this._fields)
                {
                    field.parent = this;
                }
            }

            public virtual object @__getattr__(object attr)
            {
                return this._lookup_possibly_derived_attribute(attr);
            }

            public virtual object get_derived_from()
            {
                // TODO: add support for dot notation derivedFrom
                if (this.derived_from == null)
                {
                    return null;
                }
                foreach (var register in this.parent.registers)
                {
                    if (register.name == this.derived_from)
                    {
                        return register;
                    }
                }
                throw KeyError(String.Format("Unable to find derived_from: %r", this.derived_from));
            }

            public virtual object is_reserved()
            {
                return this.name.lower().Contains("reserved");
            }
        }

        public class SVDAddressBlock
            : SVDElement
        {

            public SVDAddressBlock(object offset, object size, object usage)
            {
                this.offset = offset;
                this.size = size;
                this.usage = usage;
            }
        }

        public class SVDInterrupt
            : SVDElement
        {

            public SVDInterrupt(object name, object value)
            {
                this.name = name;
                this.value = _check_type(value, six.integer_types);
            }
        }

        public class SVDPeripheral
            : SVDElement
        {

            public SVDPeripheral(
                object name,
                object version,
                object derived_from,
                object description,
                object prepend_to_name,
                object base_address,
                object address_block,
                object interrupts,
                object registers,
                object register_arrays,
                object size,
                object access,
                object protection,
                object reset_value,
                object reset_mask,
                object group_name,
                object append_to_name,
                object disable_condition)
            {
                // items with underscore are potentially derived
                this.name = name;
                this._version = version;
                this._derived_from = derived_from;
                this._description = description;
                this._prepend_to_name = prepend_to_name;
                this._base_address = base_address;
                this._address_block = address_block;
                this._interrupts = interrupts;
                this._registers = registers;
                this._register_arrays = register_arrays;
                this._size = size;
                this._access = access;
                this._protection = protection;
                this._reset_value = reset_value;
                this._reset_mask = reset_mask;
                this._group_name = group_name;
                this._append_to_name = append_to_name;
                this._disable_condition = disable_condition;
                // make parent association for complex node types
                foreach (var i in _none_as_empty(this._interrupts))
                {
                    i.parent = this;
                }
                foreach (var r in _none_as_empty(this._registers))
                {
                    r.parent = this;
                }
            }

            public virtual object @__getattr__(object attr)
            {
                return this._lookup_possibly_derived_attribute(attr);
            }

            public object registers
            {
                get
                {
                    var regs = new List<object>();
                    foreach (var reg in this._lookup_possibly_derived_attribute("registers"))
                    {
                        regs.append(reg);
                    }
                    foreach (var arr in this._lookup_possibly_derived_attribute("register_arrays"))
                    {
                        regs.extend(arr.registers);
                    }
                    return regs;
                }
            }

            public virtual object get_derived_from()
            {
                if (this._derived_from == null)
                {
                    return null;
                }
                // find the peripheral with this name in the tree
                try
                {
                    return this.parent.peripherals.Where(p => p.name == this._derived_from)[0];
                }
                catch (IndexError)
                {
                    return null;
                }
            }
        }

        public class SVDCpu
            : SVDElement
        {

            public SVDCpu(
                object name,
                object revision,
                object endian,
                object mpu_present,
                object fpu_present,
                object fpu_dp,
                object icache_present,
                object dcache_present,
                object itcm_present,
                object dtcm_present,
                object vtor_present,
                object nvic_prio_bits,
                object vendor_systick_config,
                object device_num_interrupts,
                object sau_num_regions,
                object sau_regions_config)
            {
                this.name = name;
                this.revision = revision;
                this.endian = endian;
                this.mpu_present = mpu_present;
                this.fpu_present = fpu_present;
                this.fpu_dp = fpu_dp;
                this.icache_present = icache_present;
                this.dcache_present = dcache_present;
                this.itcm_present = itcm_present;
                this.dtcm_present = dtcm_present;
                this.vtor_present = vtor_present;
                this.nvic_prio_bits = nvic_prio_bits;
                this.vendor_systick_config = vendor_systick_config;
                this.device_num_interrupts = device_num_interrupts;
                this.sau_num_regions = sau_num_regions;
                this.sau_regions_config = sau_regions_config;
            }
        }

        public class SVDDevice
            : SVDElement
        {

            public SVDDevice(
                object vendor,
                object vendor_id,
                object name,
                object version,
                object description,
                object cpu,
                object address_unit_bits,
                object width,
                object peripherals,
                object size,
                object access,
                object protection,
                object reset_value,
                object reset_mask)
            {
                this.vendor = vendor;
                this.vendor_id = vendor_id;
                this.name = name;
                this.version = version;
                this.description = description;
                this.cpu = cpu;
                this.address_unit_bits = _check_type(address_unit_bits, six.integer_types);
                this.width = _check_type(width, six.integer_types);
                this.peripherals = peripherals;
                this.size = size;
                this.access = access;
                this.protection = protection;
                this.reset_value = reset_value;
                this.reset_mask = reset_mask;
                // set up parent relationship
                if (this.cpu)
                {
                    this.cpu.parent = this;
                }
                foreach (var p in _none_as_empty(this.peripherals))
                {
                    p.parent = this;
                }
            }
        }
    }
}
