﻿//  Copyright 2016 Google Inc. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace NtApiDotNet.Win32
{
    /// <summary>
    /// Utilities for Win32 APIs.
    /// </summary>
    public static class Win32Utils
    {
        static bool IsValidMask(uint mask, uint valid_mask)
        {
            if (mask == 0)
            {
                return false;
            }

            // Filter out generic access etc.
            if ((mask & ~valid_mask) != 0)
            {
                return false;
            }

            // Check if the mask only has a single bit set.
            if ((mask & (mask - 1)) != 0)
            {
                return false;
            }

            return true;
        }

        static void AddEnumToDictionary(Dictionary<uint, String> access, Type enumType, uint valid_mask)
        {
            Regex re = new Regex("([A-Z])");

            foreach(uint mask in Enum.GetValues(enumType))
            {
                if (IsValidMask(mask, valid_mask))
                {
                    access.Add(mask, re.Replace(Enum.GetName(enumType, mask), " $1").Trim());
                }
            }
        }

        internal static Dictionary<uint, String> GetMaskDictionary(Type access_type, AccessMask valid_access)
        {
            Dictionary<uint, String> access = new Dictionary<uint, String>();
            AddEnumToDictionary(access, access_type, valid_access.Access);

            return access;
        }

        /// <summary>
        /// Display the edit security dialog.
        /// </summary>
        /// <param name="hwnd">Parent window handle.</param>
        /// <param name="handle">NT object to display the security.</param>
        /// <param name="object_name">The name of the object to display.</param>
        /// <param name="read_only">True to force the UI to read only.</param>
        public static void EditSecurity(IntPtr hwnd, NtObject handle, string object_name, bool read_only)
        {
            Dictionary<uint, String> access = GetMaskDictionary(handle.NtType.AccessRightsType, handle.NtType.ValidAccess);

            using (SecurityInformationImpl impl = new SecurityInformationImpl(object_name, handle, access,
               handle.NtType.GenericMapping, read_only))
            {
                Win32NativeMethods.EditSecurity(hwnd, impl);
            }
        }

        /// <summary>
        /// Display the edit security dialog.
        /// </summary>
        /// <param name="hwnd">Parent window handle.</param>
        /// <param name="name">The name of the object to display.</param>
        /// <param name="sd">The security descriptor to display.</param>
        /// <param name="type">The NT type of the object.</param>
        public static void EditSecurity(IntPtr hwnd, string name, SecurityDescriptor sd, NtType type) 
        {
            EditSecurity(hwnd, name, sd, type.AccessRightsType, type.ValidAccess, type.GenericMapping);
        }

        /// <summary>
        /// Display the edit security dialog.
        /// </summary>
        /// <param name="hwnd">Parent window handle.</param>
        /// <param name="name">The name of the object to display.</param>
        /// <param name="sd">The security descriptor to display.</param>
        /// <param name="access_type">An enumerated type for the access mask.</param>
        /// <param name="generic_mapping">Generic mapping for the access rights.</param>
        /// <param name="valid_access">Valid access mask for the access rights.</param>
        public static void EditSecurity(IntPtr hwnd, string name, SecurityDescriptor sd, 
            Type access_type, AccessMask valid_access, GenericMapping generic_mapping)
        {
            Dictionary<uint, String> access = GetMaskDictionary(access_type, valid_access);
            using (var impl = new SecurityInformationImpl(name, sd, access, generic_mapping))
            {
                Win32NativeMethods.EditSecurity(hwnd, impl);
            }
        }

        /// <summary>
        /// Define a new DOS device.
        /// </summary>
        /// <param name="flags">The dos device flags.</param>
        /// <param name="device_name">The device name to define.</param>
        /// <param name="target_path">The target path.</param>
        public static void DefineDosDevice(DefineDosDeviceFlags flags, string device_name, string target_path)
        {
            if (!Win32NativeMethods.DefineDosDevice(flags, device_name, target_path))
            {
                throw new SafeWin32Exception();
            }
        }

        /// <summary>
        /// Get Windows INVALID_HANDLE_VALUE.
        /// </summary>
        public static IntPtr InvalidHandle { get => new IntPtr(-1); }

        /// <summary>
        /// Parse a command line into arguments.
        /// </summary>
        /// <param name="command_line">The parsed command line.</param>
        /// <returns>The list of arguments.</returns>
        public static string[] ParseCommandLine(string command_line)
        {
            using (var argv = Win32NativeMethods.CommandLineToArgvW(command_line, out int argc))
            {
                if (argv.IsInvalid)
                {
                    throw new SafeWin32Exception();
                }

                string[] ret = new string[argc];
                for (int i = 0; i < argc; ++i)
                {
                    ret[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv.DangerousGetHandle() + IntPtr.Size * i));
                }
                return ret;
            }
        }

        /// <summary>
        /// Get the image path from a command line.
        /// </summary>
        /// <param name="command_line">The command line to parse.</param>
        /// <returns>The image path, returns the original command line if can't find a valid image path.</returns>
        public static string GetImagePathFromCommandLine(string command_line)
        {
            command_line = command_line.Trim();
            if (File.Exists(command_line))
            {
                return command_line;
            }

            string[] args = ParseCommandLine(command_line);
            if (args.Length == 0)
            {
                return command_line;
            }

            if (command_line.StartsWith("\""))
            {
                return args[0];
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < args.Length; ++i)
            {
                string file = string.Join(" ", args.Take(i + 1));
                if (File.Exists(file))
                {
                    return file;
                }
            }

            return command_line;
        }
    }
}
