
// using ET = xml.etree.ElementTree;
using System.Xml;

using six;

using SVDDevice = cmsis_svd.model.SVDDevice;

using SVDRegisterArray = cmsis_svd.model.SVDRegisterArray;

using SVDPeripheral = cmsis_svd.model.SVDPeripheral;

using SVDInterrupt = cmsis_svd.model.SVDInterrupt;

using SVDAddressBlock = cmsis_svd.model.SVDAddressBlock;

using SVDRegister = cmsis_svd.model.SVDRegister;

using SVDField = cmsis_svd.model.SVDField;

using SVDEnumeratedValue = cmsis_svd.model.SVDEnumeratedValue;

using SVDCpu = cmsis_svd.model.SVDCpu;

using pkg_resources;

using re;

using System;

using System.Collections.Generic;

using System.Diagnostics;

namespace cmsis_svd
{
    public static class parser
    {

        //
        // Copyright 2015 Paul Osborne <osbpau@gmail.com>
        //
        // Licensed under the Apache License, Version 2.0 (the "License");
        // you may not use this file except in compliance with the License.
        // You may obtain a copy of the License at
        //
        //     http://www.apache.org/licenses/LICENSE-2.0
        //
        // Unless required by applicable law or agreed to in writing, software
        // distributed under the License is distributed on an "AS IS" BASIS,
        // WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
        // See the License for the specific language governing permissions and
        // limitations under the License.
        //
        // Get the text for the provided tag from the provided node
        public static object _get_text(object node, object tag, object @default = null)
        {
            try
            {
                return node.find(tag).text;
            }
            catch (AttributeError)
            {
                return @default;
            }
        }

        public static object _get_int(object node, object tag, object @default = null)
        {
            var text_value = _get_text(node, tag, @default);
            try
            {
                if (text_value != @default)
                {
                    text_value = text_value.strip().lower();
                    if (text_value.startswith("0x"))
                    {
                        return Convert.ToInt32(text_value[2], 16);
                    }
                    else if (text_value.startswith("#"))
                    {
                        // TODO(posborne): Deal with strange #1xx case better
                        //
                        // Freescale will sometimes provide values that look like this:
                        //   #1xx
                        // In this case, there are a number of values which all mean the
                        // same thing as the field is a "don't care".  For now, we just
                        // replace those bits with zeros.
                        text_value = text_value.replace("x", "0")[1];
                        var is_bin = all(text_value.Select(x => "01".Contains(x)));
                        return is_bin ? Convert.ToInt32(text_value, 2) : Convert.ToInt32(text_value);
                    }
                    else if (text_value.startswith("true"))
                    {
                        return 1;
                    }
                    else if (text_value.startswith("false"))
                    {
                        return 0;
                    }
                    else
                    {
                        return Convert.ToInt32(text_value);
                    }
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                return @default;
            }
            return @default;
        }

        // The SVDParser is responsible for mapping the SVD XML to Python Objects
        public class SVDParser
        {

            //[classmethod]
            public static object for_xml_file(Type cls, string path, bool remove_reserved = false)
            {
                return cls(ET.parse(path), remove_reserved);
            }

            //[classmethod]
            public static object for_packaged_svd(Type cls, object vendor, string filename, bool remove_reserved = false)
            {
                string resource = String.Format("data/{0}/{0}", vendor, filename);
                filename = pkg_resources.resource_filename("cmsis_svd", resource);
                return cls.for_xml_file(filename, remove_reserved);
            }

            //[classmethod]
            public static object for_mcu(object cls, object mcu)
            {
                object filename;
                mcu = mcu.lower();
                var vendors = pkg_resources.resource_listdir("cmsis_svd", "data");
                foreach (var vendor in vendors)
                {
                    var fnames = pkg_resources.resource_listdir("cmsis_svd", String.Format("data/%s", vendor));
                    foreach (var fname in fnames)
                    {
                        filename = fname.lower();
                        if (!filename.endswith(".svd"))
                        {
                            continue;
                        }
                        filename = filename[:: - 4];
                        if (mcu.startswith(filename))
                        {
                            return cls.for_packaged_svd(vendor, fname);
                        }
                    }
                    foreach (var fname in fnames)
                    {
                        filename = fname.lower();
                        if (!filename.endswith(".svd"))
                        {
                            continue;
                        }
                        filename = String.Format("^%s.*", filename[:: - 4].replace("x", "."));
                        if (re.match(filename, mcu))
                        {
                            return cls.for_packaged_svd(vendor, fname);
                        }
                    }
                }
                return null;
            }

            public SVDParser(object tree, bool remove_reserved = false)
            {
                this.remove_reserved = remove_reserved;
                this._tree = tree;
                this._root = this._tree.getroot();
            }

            public virtual SVDEnumeratedValue _parse_enumerated_value(object enumerated_value_node)
            {
                return new SVDEnumeratedValue(name: _get_text(enumerated_value_node, "name"), description: _get_text(enumerated_value_node, "description"), value: _get_int(enumerated_value_node, "value"), is_default: _get_int(enumerated_value_node, "isDefault"));
            }

            public virtual SVDField _parse_field(object field_node)
            {
                var enumerated_values = new List<object>();
                foreach (var enumerated_value_node in field_node.findall("./enumeratedValues/enumeratedValue"))
                {
                    enumerated_values.append(this._parse_enumerated_value(enumerated_value_node));
                }
                var modified_write_values = _get_text(field_node, "modifiedWriteValues");
                var read_action = _get_text(field_node, "readAction");
                var bit_range = _get_text(field_node, "bitRange");
                var bit_offset = _get_int(field_node, "bitOffset");
                var bit_width = _get_int(field_node, "bitWidth");
                var msb = _get_int(field_node, "msb");
                var lsb = _get_int(field_node, "lsb");
                if (bit_range != null)
                {
                    var m = re.search("\[([0-9]+):([0-9]+)\]", bit_range);
                    bit_offset = Convert.ToInt32(m.group(2));
                    bit_width = 1 + (Convert.ToInt32(m.group(1)) - Convert.ToInt32(m.group(2)));
                }
                else if (msb != null)
                {
                    bit_offset = lsb;
                    bit_width = 1 + (msb - lsb);
                }
                return new SVDField(name: _get_text(field_node, "name"), derived_from: _get_text(field_node, "derivedFrom"), description: _get_text(field_node, "description"), bit_offset: bit_offset, bit_width: bit_width, access: _get_text(field_node, "access"), enumerated_values: enumerated_values || null, modified_write_values: modified_write_values, read_action: read_action);
            }

            public virtual SVDRegister _parse_registers(object register_node)
            {
                var fields = new List<object>();
                foreach (var field_node in register_node.findall(".//field"))
                {
                    var node = this._parse_field(field_node);
                    if (this.remove_reserved || !node.name.lower().Contains("reserved"))
                    {
                        fields.Add(node);
                    }
                }
                var dim = _get_int(register_node, "dim");
                var name = _get_text(register_node, "name");
                var derived_from = _get_text(register_node, "derivedFrom");
                var description = _get_text(register_node, "description");
                var address_offset = _get_int(register_node, "addressOffset");
                var size = _get_int(register_node, "size");
                var access = _get_text(register_node, "access");
                var protection = _get_text(register_node, "protection");
                var reset_value = _get_int(register_node, "resetValue");
                var reset_mask = _get_int(register_node, "resetMask");
                var dim_increment = _get_int(register_node, "dimIncrement");
                var dim_index_text = _get_text(register_node, "dimIndex");
                var display_name = _get_text(register_node, "displayName");
                var alternate_group = _get_text(register_node, "alternateGroup");
                var modified_write_values = _get_text(register_node, "modifiedWriteValues");
                var read_action = _get_text(register_node, "readAction");
                if (dim == null)
                {
                    return new SVDRegister(name: name, fields: fields, derived_from: derived_from, description: description, address_offset: address_offset, size: size, access: access, protection: protection, reset_value: reset_value, reset_mask: reset_mask, display_name: display_name, alternate_group: alternate_group, modified_write_values: modified_write_values, read_action: read_action);
                }
                else
                {
                    // the node represents a register array
                    if (dim_index_text == null)
                    {
                        var dim_indices = range(0, dim);
                    }
                    else if (dim_index_text.Contains(","))
                    {
                        dim_indices = dim_index_text.split(",");
                    }
                    else if (dim_index_text.Contains("-"))
                    {
                        // some files use <dimIndex>0-3</dimIndex> as an inclusive inclusive range
                        var m = re.search(@"([0-9]+)-([0-9]+)", dim_index_text);
                        dim_indices = range(Convert.ToInt32(m.group(1)), Convert.ToInt32(m.group(2)) + 1);
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(String.Format("Unexpected dim_index_text: %r", dim_index_text));
                    }
                    // yield `SVDRegisterArray` (caller will differentiate on type)
                    return new SVDRegisterArray(name: name, fields: fields, derived_from: derived_from, description: description, address_offset: address_offset, size: size, access: access, protection: protection, reset_value: reset_value, reset_mask: reset_mask, display_name: display_name, alternate_group: alternate_group, modified_write_values: modified_write_values, read_action: read_action, dim: dim, dim_indices: dim_indices, dim_increment: dim_increment);
                }
            }

            public virtual SVDAddressBlock _parse_address_block(object address_block_node)
            {
                return new SVDAddressBlock(_get_int(address_block_node, "offset"), _get_int(address_block_node, "size"), _get_text(address_block_node, "usage"));
            }

            public virtual SVDInterrupt _parse_interrupt(object interrupt_node)
            {
                return new SVDInterrupt(name: _get_text(interrupt_node, "name"), value: _get_int(interrupt_node, "value"));
            }

            public virtual SVDPeripheral _parse_peripheral(object peripheral_node)
            {
                object address_block;
                // parse registers
                var registers = peripheral_node.find("registers") == null ? null : new List<object>();
                var register_arrays = peripheral_node.find("registers") == null ? null : new List<object>();
                foreach (var register_node in peripheral_node.findall("./registers/register"))
                {
                    var reg = this._parse_registers(register_node);
                    if (reg is SVDRegisterArray)
                    {
                        register_arrays.Add(reg);
                    }
                    else
                    {
                        registers.Add(reg);
                    }
                }
                // parse all interrupts for the peripheral
                var interrupts = new List<object>();
                foreach (var interrupt_node in peripheral_node.findall("./interrupt"))
                {
                    interrupts.Add(this._parse_interrupt(interrupt_node));
                }
                interrupts = interrupts ?? null;
                // parse address block if any
                var address_block_nodes = peripheral_node.findall("./addressBlock");
                if (address_block_nodes)
                {
                    address_block = this._parse_address_block(address_block_nodes[0]);
                }
                else
                {
                    address_block = null;
                }
                return new SVDPeripheral(name: _get_text(peripheral_node, "name"), version: _get_text(peripheral_node, "version"), derived_from: peripheral_node.get("derivedFrom"), description: _get_text(peripheral_node, "description"), group_name: _get_text(peripheral_node, "groupName"), prepend_to_name: _get_text(peripheral_node, "prependToName"), append_to_name: _get_text(peripheral_node, "appendToName"), disable_condition: _get_text(peripheral_node, "disableCondition"), base_address: _get_int(peripheral_node, "baseAddress"), size: _get_int(peripheral_node, "size"), access: _get_text(peripheral_node, "access"), reset_value: _get_int(peripheral_node, "resetValue"), reset_mask: _get_int(peripheral_node, "resetMask"), address_block: address_block, interrupts: interrupts, register_arrays: register_arrays, registers: registers, protection: _get_text(peripheral_node, "protection"));
            }

            public virtual SVDDevice _parse_device(object device_node)
            {
                var peripherals = new List<object>();
                foreach (var peripheral_node in device_node.findall(".//peripheral"))
                {
                    peripherals.append(this._parse_peripheral(peripheral_node));
                }
                var cpu_node = device_node.find("./cpu");
                var cpu = new SVDCpu(name: _get_text(cpu_node, "name"), revision: _get_text(cpu_node, "revision"), endian: _get_text(cpu_node, "endian"), mpu_present: _get_int(cpu_node, "mpuPresent"), fpu_present: _get_int(cpu_node, "fpuPresent"), fpu_dp: _get_int(cpu_node, "fpuDP"), icache_present: _get_int(cpu_node, "icachePresent"), dcache_present: _get_int(cpu_node, "dcachePresent"), itcm_present: _get_int(cpu_node, "itcmPresent"), dtcm_present: _get_int(cpu_node, "dtcmPresent"), vtor_present: _get_int(cpu_node, "vtorPresent"), nvic_prio_bits: _get_int(cpu_node, "nvicPrioBits"), vendor_systick_config: _get_int(cpu_node, "vendorSystickConfig"), device_num_interrupts: _get_int(cpu_node, "vendorSystickConfig"), sau_num_regions: _get_int(cpu_node, "vendorSystickConfig"), sau_regions_config: _get_text(cpu_node, "sauRegionsConfig"));
                return new SVDDevice(vendor: _get_text(device_node, "vendor"), vendor_id: _get_text(device_node, "vendorID"), name: _get_text(device_node, "name"), version: _get_text(device_node, "version"), description: _get_text(device_node, "description"), cpu: cpu, address_unit_bits: _get_int(device_node, "addressUnitBits"), width: _get_int(device_node, "width"), peripherals: peripherals, size: _get_int(device_node, "size"), access: _get_text(device_node, "access"), protection: _get_text(device_node, "protection"), reset_value: _get_int(device_node, "resetValue"), reset_mask: _get_int(device_node, "resetMask"));
            }

            // Get the device described by this SVD
            public virtual object get_device()
            {
                return this._parse_device(this._root);
            }
        }

        public static object duplicate_array_of_registers(object svdreg)
        {
            // expects a SVDRegister which is an array of registers
            Debug.Assert(svdreg.dim == svdreg.dim_index.Count);
        }
    }
}
