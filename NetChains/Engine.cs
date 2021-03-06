﻿using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace NetChains
{
    public static class Engine
    {
        public static bool inBlock = false;
        private static List<string> codeBlock = new List<string>();
        static private Dictionary<string, string> variables = new Dictionary<string, string>();
        static private Dictionary<string, string> scriptCache = new Dictionary<string, string>();


        /// <summary>
        /// Executes a provided NetChain
        /// </summary>
        /// <param name="command">The command to execute</param>
        /// <returns>Any indirectly generated output or errors</returns>
        public static IEnumerable<string> Execute(string[] commands)
        {
            foreach (string command in commands)
            {
                yield return Execute(command);
            }
        }

        /// <summary>
        /// Executes a provided NetChain
        /// </summary>
        /// <param name="command">The command to execute</param>
        /// <returns>Any indirectly generated output or errors</returns>
        public static string Execute(string command)
        {
            string retVal = "";
            command = command.Trim('\t', ' ', '\0', '\r', '\n');

            if (command.ToLower() == "exit")
                Environment.Exit(0);
            else if (command == "")
                return null;
            else if (command.ToLower().StartsWith("load"))
            {
                string[] args = ArgSplit(command, " ", true);
                try
                {
                    try { LoadFile(args[1], args[2]); }
                    catch (IndexOutOfRangeException)
                    {
                        try { LoadFile(args[1], ""); }
                        catch (IndexOutOfRangeException) { throw new Exception("Load requires a filename"); }
                    }
                }
                catch (ArgumentException)
                {
                    try
                    {
                        try { LoadFile(args[1] + ".net", args[2]); }
                        catch (IndexOutOfRangeException)
                        {
                            try { LoadFile(args[1] + ".net", ""); }
                            catch (IndexOutOfRangeException) { throw new Exception("Load requires a filename"); }
                        }
                    }
                    catch (ArgumentException) { retVal += ($"Cannot find file {args[1]}\n"); }
                }
            }
            else if (command.ToLower().StartsWith("run"))
            {
                ExecFile(ArgSplit(command, " ", true)[1]);
            }
            else if (command.ToLower().StartsWith("exec"))
            {
                string[] cmdString = ArgSplit(command, " ", true);

                Process process = new Process();
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.FileName = cmdString[1];
                if (cmdString.Length > 2)
                {
                    string argstring = "";
                    for (int i = 2; i < cmdString.Length; ++i)
                        argstring += cmdString[i];
                    process.StartInfo.Arguments = argstring;
                }
                process.Start();
            }
            else if (command.ToLower() == "help")
            {
                retVal += ("NetChains is copyrighted by Weston Sleeman, but feel free to redistribute this executable in its original form.\n");
                retVal += ("NetChains provides direct access to the .NET framework in a scriptable and chain-y format.\n");
                retVal += ("Phrases are formed into chains, linked by a double colon (::).\n");
                retVal += ("\nA chain consists of two types of phrase:\n\tA selection/shift phrase starts with a bang (!) and is the name of a class (inside System) to 'select' (e.g. !Console selects System.Console).\n\tAn access/command phrase uses a member/method inside the currently selected class (e.g. !Console::WriteLine(Hello!) is equivalent to C#/VB retVal += (\"Hello!\")).\n");
                retVal += ("\nEx. !ConsoleColor::Red::!Console::ForegroundColor = $::Write(Hello)::ResetColor\n");
                retVal += ("(Selects to System.ConsoleColor; Accesses member Red; Shifts to System.Console; Sets ForegroundColor to the parent ($, currently Red); Writes Hello to screen; Resets colors)\n");
                retVal += ("\nFor more complete documentation, go to https://github.com/wessupermare/NetChains/wiki\n");
            }
            else if (command.ToLower().StartsWith("$"))
            {
                string[] args = ArgSplit(command, " ", true);

                if (args[1].StartsWith("!"))
                {
                    try { variables[args[0].TrimStart('$')] = Execute(args[1]); }
                    catch (KeyNotFoundException) { variables.Add(args[0].TrimStart('$'), Execute(args[1])); }
                }
                else
                {
                    try { variables.Add(args[0].TrimStart('$'), args[1]); }
                    catch (IndexOutOfRangeException) { variables.Add(args[0].TrimStart('$'), "TRUE"); }
                    catch (ArgumentException) { variables[args[0].TrimStart('$')] = args[1]; }
                }
            }
            else if (command.ToLower().StartsWith("#loop") || command.ToLower().StartsWith("#if") || command.ToLower().StartsWith("#use"))
            {
                codeBlock = new List<string>();
                inBlock = true;
                codeBlock.Add(command);
            }
            else if (command.StartsWith("#"))
            {
                PreExecCommand(command, null);
            }
            else if (inBlock && command.ToLower() == "end")
            {
                string preCom = codeBlock[0];
                codeBlock.RemoveAt(0);
                try { PreExecCommand(preCom, codeBlock.ToArray()); }
                catch (Exception ex) { retVal += (ex.Message + "\n"); }
                codeBlock = new List<string>();
                inBlock = false;
            }
            else if (inBlock)
            {
                codeBlock.Add(command);
            }
            else
            {
                try { return ExecuteIn(ArgSplit(command, "::", true)); }
                catch (Exception ex) { retVal += (ex.Message + "\n"); }
            }

            return retVal.Trim('\n', '\r');
        }

        /// <summary>
        /// Handles NetChains pre-execution directives
        /// </summary>
        /// <param name="preExec">A directive to enact</param>
        /// <param name="code">The block of code from if/loop/etc statements</param>
        /// <returns>Any iderectly generated results or errors</returns>
        private static string PreExecCommand(string preExec, string[] code)
        {
            List<string> args = new List<string>();
            args.AddRange(ArgSplit(preExec, " ", true));

            string method = args[0].ToLower();
            args.RemoveAt(0);

            if (method == "#loop")
            {
                int runTo;
                try
                {
                    runTo = int.Parse(variables.ContainsKey(args[0]) ? variables[args[0]] : args[0]);
                }
                catch (FormatException) { return ("Loop block requires a valid number of times to run"); }

                for (int cntr = 0; cntr < runTo; cntr++)
                {
                    ExecMulti(code);
                }
            }
            else if (method == "#load")
            {
                try { LoadFile(args[0], args[1]); }
                catch (ArgumentOutOfRangeException)
                {
                    try { LoadFile(args[0], ""); }
                    catch (ArgumentOutOfRangeException) { throw new Exception("Load requires a filename"); }
                }
            }
            else if (method == "#run")
            {
                ExecFile(args[0]);
            }
            else if (method == "#if")
            {
                bool condition = false;

                if (args.Count == 1)
                {
                    try { condition = bool.Parse(variables[args[0]]); }
                    catch (KeyNotFoundException)
                    {
                        try { condition = bool.Parse(args[0]); }
                        catch (FormatException) { condition = false; }
                    }
                }
                else
                {
                    throw new NotImplementedException("Only boolean if statements are currently supported.");
                }

                if (condition)
                {
                    ExecMulti(code);
                }
            }
            else if (method == "#use")
            {
                List<object> argList = new List<object>();
                string argCache = args[0];
                Type[] typeList = new Type[0];

                ParseArgs(ref argCache, ref argList, ref typeList, null);
                Assembly assembly = Assembly.Load(GetAssemblyNameContainingType("System." + argCache.TrimStart('!', '\'', '"', '`')));
                Type type = assembly.GetType("System." + argCache.TrimStart('!', '\'', '"', '`'), true);
                using (dynamic used = Activator.CreateInstance(type))
                {
                    return ExecMulti(code, used);
                }
            }
            else if (method == "#exec")
            {
                System.Diagnostics.Process.Start(args[0]);
            }

            return null;
        }

        public static string GetAssemblyNameContainingType(string typeName)
        {
            foreach (Assembly currentassembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = currentassembly.GetType(typeName, false, true);
                if (t != null) { return currentassembly.FullName; }
            }

            throw new ArgumentException("Type not found in any loaded assemblies");
        }

        /// <summary>
        /// Executes multiple NetChains
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private static string ExecMulti(string[] code)
        {
            return ExecMulti(code, null);
        }

        public static string ExecMulti(string[] code, object parent)
        {
            List<string> retList = new List<string>();
            try
            {
                foreach (string line in code)
                {
                    if (line == "exit")
                        break;

                    retList.Add(ExecuteIn(ArgSplit(line, "::"), parent));
                }
            }
            catch (Exception Ex) { return (Ex.Message); }

            string retVal = "";
            foreach (string retitem in retList)
                retVal += retitem + '\n';

            return retVal.Trim('\n');
        }

        private static string ExecuteIn(string[] function)
        {
            return ExecuteIn(function, null);
        }

        private static string ExecuteIn(string[] functionIn, object obj)
        {
            for (int cntr = 0; cntr < functionIn.Length; ++cntr)
                functionIn[cntr] = functionIn[cntr].TrimStart('\t', ' ', '\n', '\r', '\0').TrimEnd('\t', ' ', '\n', '\r', '\0');

            List<string> function = new List<string>();
            function.AddRange(functionIn);

            Type type;
            if (obj == null)
            {
                try
                {
                    Assembly assembly = Assembly.Load(GetAssemblyNameContainingType("System." + function[0].TrimStart('!', '\'', '"', '`')));
                    type = assembly.GetType("System." + function[0].TrimStart('!', '\'', '"', '`'), true);
                    function.RemoveAt(0);
                }
                catch { throw new SanityException($"Type name {function[0].TrimStart('!')} not found!"); }
            }
            else
            {
                type = obj.GetType();
            }

            int colons = function.Count;
            while (colons > 0)
            {
                string curFunc = function[function.Count - (colons)];
                if (curFunc.StartsWith("!"))
                {
                    Assembly assembly = Assembly.Load(GetAssemblyNameContainingType("System." + curFunc.TrimStart('!', '\'', '"', '`')));
                    type = assembly.GetType("System." + curFunc.TrimStart('!', '\'', '"', '`'), true);
                }
                else
                {
                    if (curFunc.Contains("=") && !curFunc.Contains("("))
                    {
                        List<object> args = null;
                        Type[] argTypes = null;

                        curFunc = curFunc.Replace('=', '(');
                        curFunc = curFunc.Replace(" ", "");
                        curFunc = curFunc + ')';

                        ParseArgs(ref curFunc, ref args, ref argTypes, obj);

                        object castedVal = new object();
                        try { Convert.ChangeType(args[0], argTypes[0]); }
                        catch
                        {
                            if (argTypes[0] == null || String.IsNullOrEmpty(args[0].ToString()))
                                castedVal = null;
                            if (argTypes[0].IsEnum)
                            {
                                Type enumType = argTypes[0];
                                dynamic argCast = args[0];
                                castedVal = Enum.Parse(enumType, argCast.Name.ToString());
                            }

                        }

                        try { type.GetProperty(curFunc).SetValue(null, castedVal); }
                        catch (Exception ex) { throw new Exception($"Error in phrase {(function.Count - colons) + 1}: {ex.Message}"); }
                    }
                    else
                    {
                        try
                        {
                            obj = type.GetProperty(curFunc).GetValue(obj);
                            if (colons == 1)
                                return (obj.ToString());
                        }
                        catch
                        {
                            List<object> args = null;
                            Type[] argTypes = null;

                            ParseArgs(ref curFunc, ref args, ref argTypes, obj);

                            try
                            {
                                if (args == null)
                                {
                                    try
                                    {
                                        try
                                        {
                                            object tmp = obj.GetType().InvokeMember(curFunc, BindingFlags.InvokeMethod, null, obj, null);
                                            if (tmp != null) obj = tmp;
                                        }
                                        catch
                                        {
                                            object tmp = type.GetMethod(curFunc).Invoke(obj, null);
                                            if (tmp != null) obj = tmp;
                                        }
                                    }
                                    catch { throw new Exception($"Function {curFunc} not found."); }
                                }
                                else
                                {
                                    try
                                    {
                                        object tmp = obj.GetType().InvokeMember(curFunc, BindingFlags.InvokeMethod, null, obj, args.ToArray());
                                        if (tmp != null) obj = tmp;
                                    }
                                    catch
                                    {
                                        object tmp = type.GetMethod(curFunc, argTypes).Invoke(obj, args.ToArray());
                                        if (tmp != null) obj = tmp;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                try { obj = type.GetMember(curFunc)[0]; }
                                catch { throw new Exception($"Error in phrase {(function.Count - colons) + 1}: {ex.Message}"); }
                            }
                        }
                    }
                }
                --colons;
            }

            return obj?.ToString();
        }

        private static void ParseArgs(ref string input, ref List<object> args, ref Type[] argTypes, dynamic parent)
        {
            if (input.Contains("("))
            {
                args = new List<object>();
                args.AddRange(ArgSplit(input.Substring(input.IndexOf('(') + 1, input.IndexOf(')') - (input.IndexOf('(') + 1)), ","));
                for (ushort cntr = 0; cntr < args.Count; ++cntr)
                    args[cntr] = Unescape(args[cntr].ToString());

                input = input.Substring(0, input.IndexOf('('));
                argTypes = new Type[args.Count];

                for (ushort cntr = 0; cntr < args.Count; ++cntr)
                {
                    if ((string)args[cntr] == "$")
                    {
                        argTypes[cntr] = parent.GetType().Name == "MdFieldInfo" ? (Type)parent.ReflectedType : (Type)parent.GetType();

                        args[cntr] = parent;
                    }
                    else
                    {
                        if (int.TryParse((string)args[cntr], out int catchval))
                        {
                            argTypes[cntr] = typeof(int);
                            args[cntr] = catchval;
                        }
                        else
                        {
                            argTypes[cntr] = typeof(string);
                            if (((string)args[cntr]).Contains("$"))
                                try { args[cntr] = ((string)args[cntr]).Replace("$", parent.ToString()); }
                                catch (RuntimeBinderException) { }//throw new Exception("Are you sure you meant to use the parent var ($) here?"); }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Unescapes any \\'s
        /// </summary>
        /// <param name="input">An escaped string</param>
        /// <returns>The unescaped equivalent of input</returns>
        private static string Unescape(string input)
        {
            return System.Text.RegularExpressions.Regex.Replace(input, @"\\[*]", m =>
            {
                switch (m.Value)
                {
                    case @"\r": return "\r";
                    case @"\n": return "\n";
                    case @"\t": return "\t";
                    case @"\\": return "\\";
                    default: return m.Value;
                }
            });
        }

        /// <summary>
        /// Parses arguments from a string, dealing with quotes and variables
        /// </summary>
        /// <param name="input">The string of arguments</param>
        /// <param name="splitStr">The delimiter</param>
        /// <returns>The split array of string arguments</returns>
        public static string[] ArgSplit(string input, string splitStr)
        {
            return ArgSplit(input, splitStr, false);
        }

        /// <summary>
        /// Parses arguments from a string, dealing with quotes and variables
        /// </summary>
        /// <param name="input">The string of arguments</param>
        /// <param name="splitStr">The delimiter</param>
        /// <param name="leaveQuotes">Should quotes be demoted?</param>
        /// <returns>The split array of string arguments</returns>
        public static string[] ArgSplit(string input, string splitStr, bool leaveQuotes)
        {
            List<string> retval = new List<string>();

            string arg = "";
            bool isInQuote = false;
            char quoteChar = '\0';

            for (ushort outCntr = 0; outCntr < input.Length; ++outCntr)
            {
                char c = input[outCntr];
                bool isSplit = true;

                for (ushort cntr = 0; cntr < splitStr.Length; ++cntr)
                {
                    if (isSplit && input[outCntr + cntr] != splitStr[cntr])
                        isSplit = false;
                }

                if (!isInQuote && isSplit)
                {
                    if (arg != "")
                        retval.Add(arg);
                    arg = "";
                    outCntr += (ushort)(splitStr.Length - 1);
                    continue;
                }

                if (c == '\'')
                {
                    if (isInQuote && c == quoteChar)
                    {
                        isInQuote = false;
                        if (leaveQuotes) arg += '\'';
                    }
                    else if (isInQuote)
                        arg += c;
                    else if (!isInQuote)
                    {
                        isInQuote = true;
                        quoteChar = c;
                        if (leaveQuotes) arg += '\'';
                    }
                    else throw new SanityException($"{c} is {(isInQuote ? " " : "not ")}in quotes");
                    continue;
                }

                if (c == '"')
                {
                    if (isInQuote && c == quoteChar)
                    {
                        isInQuote = false;
                        arg += leaveQuotes ? '"' : '\'';
                    }
                    else if (isInQuote)
                        arg += c;
                    else if (!isInQuote)
                    {
                        isInQuote = true;
                        quoteChar = c;
                        arg += leaveQuotes ? '"' : '\'';
                    }
                    else throw new SanityException($"{c} is {(isInQuote ? " " : "not ")}in quotes");
                    continue;
                }

                if (c == '`')
                {
                    if (isInQuote && c == quoteChar)
                    {
                        isInQuote = false;
                        arg += '`';
                    }
                    else if (isInQuote)
                        arg += c;
                    else if (!isInQuote)
                    {
                        isInQuote = true;
                        quoteChar = c;
                        arg += '`';
                    }
                    else throw new SanityException($"{c} is {(isInQuote ? " " : "not ")}in quotes");
                    continue;
                }

                arg += c;
            }

            retval.Add(arg);
            string[] outval = retval.ToArray();

            foreach (KeyValuePair<string, string> var in variables)
                foreach (string argSearch in retval)
                    if (argSearch.ToLower().Contains(var.Key.ToLower()))
                        outval[retval.FindIndex(ind => ind.Equals(argSearch))] = argSearch.Replace(var.Key, var.Value).Trim();

            return outval;
        }

        /// <summary>
        /// Loads a script to prepare it for use
        /// </summary>
        /// <param name="path">The relative path of the file to use</param>
        /// <param name="name">The name with which to assign it if no #NAME directive is provided</param>
        /// <returns>The actual script name</returns>
        public static string LoadFile(string path, string name)
        {
            try
            {
                string[] code = File.ReadAllLines(path);

                if (code[0].ToLower().StartsWith("#name") && name == "")
                    name = ArgSplit(code[0], " ", true)[1];
                else if (name == "")
                    name = path.Substring(path.LastIndexOf('\\') + 1);

                if (!scriptCache.ContainsKey(name.ToLower()))
                    scriptCache.Add(name.ToLower(), path);

                return name;
            }
            catch { throw new ArgumentException($"Loading script at {path} failed."); }
        }

        /// <summary>
        /// Executes an already loaded script
        /// </summary>
        /// <param name="name">The name of the script to execute</param>
        /// <returns>Any indirectly generated output or errors</returns>
        public static string ExecFile(string name)
        {
            List<string> outputList = new List<string>();
            try
            {
                string[] code;
                try { code = File.ReadAllLines(scriptCache[name.ToLower()]); }
                catch (KeyNotFoundException) { code = File.ReadAllLines(name); }

                foreach (string line in code)
                {
                    string output = Execute(line);
                    if (output != null && output != "") outputList.Add(output);
                }
            }
            catch (KeyNotFoundException) { return ($"Can't find script {name}"); }
            catch (Exception Ex) { return (Ex.Message); }

            string retVal = "";
            foreach (string op in outputList)
                retVal += op + "\n";

            return retVal.Trim('\n', '\r', '\0');
        }
    }

    public class SanityException : Exception
    {
        public SanityException() { }

        public SanityException(string message) : base(message) { }

        public SanityException(string message, Exception inner) : base(message, inner) { }
    }
}
