using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NetChains
{
    partial class Program
    {
        private static List<string> codeBlock = new List<string>();

        private static string PreExec(string input)
        {
            input = input.Trim('\t', ' ', '\0', '\r', '\n');

            if (input.ToLower() == "exit")
                Environment.Exit(0);
            else if (input == "")
                return null;
            else if (input.ToLower().StartsWith("load"))
            {
                string[] args = ArgSplit(input);
                try { LoadFile(args[1], args[2]); }
                catch (IndexOutOfRangeException)
                {
                    try { LoadFile(args[1], ""); }
                    catch (IndexOutOfRangeException) { throw new Exception("Load requires a filename"); }
                }
            }
            else if (input.ToLower().StartsWith("run"))
            {
                ExecFile(ArgSplit(input)[1]);
            }
            else if (input.ToLower().StartsWith("exec"))
            {
                string[] cmdString = ArgSplit(input);
                if (cmdString.Length > 2)
                    System.Diagnostics.Process.Start(cmdString[1], cmdString[2]);
                else
                    System.Diagnostics.Process.Start(cmdString[1]);
            }
            else if (input.ToLower() == "help")
            {
                Console.WriteLine("NetChains is copyrighted by Weston Sleeman, but feel free to redistribute this executable in its original form.");
                Console.WriteLine("NetChains provides direct access to the .NET framework in a scriptable and chain-y format.");
                Console.WriteLine("Phrases are formed into chains, linked by a double colon (::).");
                Console.WriteLine("\nA chain consists of two types of phrase:\n\tA selection/shift phrase starts with a bang (!) and is the name of a class (inside System) to 'select' (e.g. !Console selects System.Console).\n\tAn access/command phrase uses a member/method inside the currently selected class (e.g. !Console::WriteLine(Hello!) is equivalent to C#/VB Console.WriteLine(\"Hello!\")).");
                Console.WriteLine("\nEx. !ConsoleColor::Red::!Console::ForegroundColor = $::Write(Hello)::ResetColor");
                Console.WriteLine("(Selects to System.ConsoleColor; Accesses member Red; Shifts to System.Console; Sets ForegroundColor to the parent ($, currently Red); Writes Hello to screen; Resets colors)");
                Console.WriteLine("\nFor more complete documentation, go to https://github.com/wessupermare/NetChains/wiki");
            }
            else if (input.ToLower() == "clear")
            {
                Main(null);
            }
            else if (input.ToLower().StartsWith("$"))
            {
                string[] args = ArgSplit(input);

                if (args[1].StartsWith("!"))
                {
                    try { variables[args[0].TrimStart('$')] = PreExec(args[1]); }
                    catch (KeyNotFoundException) { variables.Add(args[0].TrimStart('$'), PreExec(args[1])); }
                }
                else
                {
                    try { variables.Add(args[0].TrimStart('$'), args[1]); }
                    catch (IndexOutOfRangeException) { variables.Add(args[0].TrimStart('$'), "TRUE"); }
                    catch (ArgumentException) { variables[args[0].TrimStart('$')] = args[1]; }
                }
            }
            else if (input.ToLower().StartsWith("#loop") || input.ToLower().StartsWith("#if"))
            {
                codeBlock = new List<string>();
                inBlock = true;
                codeBlock.Add(input);
            }
            else if (input.StartsWith("#"))
            {
                PreExecCommand(input, null);
            }
            else if (inBlock && input.ToLower() == "end")
            {
                string preCom = codeBlock[0];
                codeBlock.RemoveAt(0);
                try { PreExecCommand(preCom, codeBlock.ToArray()); }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
                codeBlock = new List<string>();
                inBlock = false;
            }
            else if (inBlock)
            {
                codeBlock.Add(input);
            }
            else
            {
                try { return Execute(ArgSplit(input, "::")); }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
            }

            return null;
        }

        private static void PreExecCommand(string preExec, string[] code)
        {
            List<string> args = new List<string>();
            args.AddRange(ArgSplit(preExec));

            string method = args[0].ToLower();
            args.RemoveAt(0);

            if (method == "#loop")
            {
                int runTo;
                try
                {
                    if (variables.ContainsKey(args[0]))
                        runTo = int.Parse(variables[args[0]]);
                    else
                        runTo = int.Parse(args[0]);
                }
                catch (FormatException) { Console.WriteLine("Loop block requires a valid number of times to run"); return; }
                
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
            else if (method == "#exec")
            {
                System.Diagnostics.Process.Start(args[0]);
            }
        }

        private static void ExecMulti(string[] code)
        {
            try
            {
                foreach (string line in code)
                {
                    if (line == "exit")
                        break;

                    Execute(ArgSplit(line, "::"));
                }
            }
            catch (Exception Ex) { Console.WriteLine(Ex.Message); }
        }

        private static string Execute(string[] function)
        {
            for (int cntr = 0; cntr < function.Length; ++cntr)
                function[cntr] = function[cntr].TrimStart('\t', ' ', '\n', '\r', '\0').TrimEnd('\t', ' ', '\n', '\r', '\0');

            Type type;
            try { type = Type.GetType("System." + function[0].TrimStart('!'), true); }
            catch { throw new SanityException($"Type name {function[0].TrimStart('!')} not found!"); }

            int colons = function.Length;
            object obj = null;
            while (colons > 1)
            {
                string curFunc = function[function.Length - (colons - 1)];
                if (curFunc.StartsWith("!"))
                {
                    type = Type.GetType("System." + curFunc.TrimStart('!'));
                }
                else
                {
                    if (curFunc.Contains("="))
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
                        catch (Exception ex) { throw new Exception($"Error in phrase {(function.Length - colons) + 1}: {ex.Message}"); }
                    }
                    else
                    {
                        try
                        {
                            obj = type.GetProperty(curFunc).GetValue(obj);
                            if (colons == 2)
                                Console.WriteLine(obj);
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
                                catch { throw new Exception($"Error in phrase {(function.Length - colons) + 1}: {ex.Message}"); }
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
                        if (parent.GetType().Name == "MdFieldInfo")
                            argTypes[cntr] = parent.ReflectedType;
                        else argTypes[cntr] = parent.GetType();

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
                                args[cntr] = ((string)args[cntr]).Replace("$", parent.ToString());
                        }
                    }
                }
            }
        }

        private static string Unescape(string input)
        {
            return Regex.Replace(input, @"\\[*]", m =>
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
    }
}