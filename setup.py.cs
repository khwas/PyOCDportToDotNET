// 
//  mbed CMSIS-DAP debugger
//  Copyright (c) 2012-2015 ARM Limited
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 
namespace pyOCD_master {
    
    using setup = setuptools.setup;
    
    using find_packages = setuptools.find_packages;
    
    using sys;
    
    using System.Collections.Generic;
    
    public static class setup {
        
        public static object install_requires = new List<object> {
            "intelhex",
            "six",
            "enum34",
            "future",
            "websocket-client",
            "intervaltree"
        };
        
        static setup() {
            install_requires.extend(new List<object> {
                "pyusb>=1.0.0b2"
            });
            install_requires.extend(new List<object> {
                "pywinusb>=0.4.0"
            });
            install_requires.extend(new List<object> {
                "hidapi"
            });
            setup(name: "pyOCD", use_scm_version: new Dictionary<object, object> {
                {
                    "local_scheme",
                    "dirty-tag"},
                {
                    "write_to",
                    "pyOCD/_version.py"}}, setup_requires: new List<object> {
                "setuptools_scm!=1.5.3,!=1.5.4"
            }, description: "CMSIS-DAP debugger for Python", long_description: open("README.rst", "Ur").read(), author: "Martin Kojtal, Russ Butler", author_email: "martin.kojtal@arm.com, russ.butler@arm.com", url: "https://github.com/mbedmicro/pyOCD", license: "Apache 2.0", install_requires: install_requires, classifiers: new List<object> {
                "Development Status :: 4 - Beta",
                "License :: OSI Approved :: Apache Software License",
                "Programming Language :: Python"
            }, extras_require: new Dictionary<object, object> {
                {
                    "dissassembler",
                    new List<object> {
                        "capstone"
                    }}}, entry_points: new Dictionary<object, object> {
                {
                    "console_scripts",
                    new List<object> {
                        "pyocd-gdbserver = pyOCD.tools.gdb_server:main",
                        "pyocd-flashtool = pyOCD.tools.flash_tool:main",
                        "pyocd-tool = pyOCD.tools.pyocd:main"
                    }}}, use_2to3: true, packages: find_packages(), include_package_data: true);
        }
    }
}
