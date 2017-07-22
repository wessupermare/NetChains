using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NetChains
{
    partial class Program
    {
        private static List<string> codeBlock = new List<string>();

        private static void PreExec(string input)
        {
            input = input.Trim('\t', ' ', '\0', '\r', '\n');

            if (input == "exit")
                Environment.Exit(0);
            else if (input == "")
                return;
            else if (input.StartsWith("load"))
            {
                ExecFile(input.Substring((input.Length > 4) ? 5 : 4));
                Main(null);
            }
            else if (input == "help")
            {
                Console.WriteLine("NetChains is copyrighted by Weston Sleeman, but feel free to redistribute this executable in its original form.");
                Console.WriteLine("NetChains provides direct access to the .NET framework in a scriptable and chain-y format.");
                Console.WriteLine("Phrases are formed into chains, linked by a double colon (::).");
                Console.WriteLine("\nA chain consists of two types of phrase:\n\tA selection/shift phrase starts with a bang (!) and is the name of a class (inside System) to 'select' (e.g. !Console selects System.Console).\n\tAn access/command phrase uses a member/method inside the currently selected class (e.g. !Console::WriteLine(Hello!) is equivalent to C#/VB Console.WriteLine(\"Hello!\")).");
                Console.WriteLine("\nEx. !ConsoleColor::Red::!Console::ForegroundColor = $::Write(Hello)::ResetColor");
                Console.WriteLine("(Selects to System.ConsoleColor; Accesses member Red; Shifts to System.Console; Sets ForegroundColor to the parent ($, currently Red); Writes Hello to screen; Resets colors)");
            }
            else if (input == "clear")
            {
                Main(null);
            }
            else if (input.StartsWith("$"))
            {
                try { variables.Add(input.Split(' ')[0].TrimStart('$'), input.Split(' ')[1]); }
                catch (IndexOutOfRangeException) { variables.Add(input.Split(' ')[0].TrimStart('$'), "TRUE"); }
                catch (ArgumentException) { variables[input.Split(' ')[0].TrimStart('$')] = input.Split(' ')[1]; }
            }
            else if (input.StartsWith("#"))
            {
                codeBlock = new List<string>();
                inBlock = true;
                codeBlock.Add(input);
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
                try { Execute(input.Split(new string[] { "::" }, StringSplitOptions.RemoveEmptyEntries)); }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
            }
        }

        private static void PreExecCommand(string preExec, string[] code)
        {
            List<string> args = new List<string>();
            args.AddRange(preExec.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

            string method = args[0].ToLower();
            args.RemoveAt(0);

            if (method == "#run")
            {
                int runTo;
                try
                {
                    if (variables.ContainsKey(args[0]))
                        runTo = int.Parse(variables[args[0]]);
                    else
                        runTo = int.Parse(args[0]);
                }
                catch (FormatException) { Console.WriteLine("Run block requires a valid number of times to run"); return; }
                
                for (int cntr = 0; cntr < runTo; cntr++)
                {
                    ExecMulti(code);
                }
            }

            if (method == "#if")
            {
                bool condition = false;

                if (args.Count == 1)
                {
                    try { condition = bool.Parse(variables[args[0]]); }
                    catch (FormatException) { condition = bool.Parse(args[0]); }
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
        }

        private static void ExecMulti(string[] code)
        {
            try
            {
                foreach (string line in code)
                {
                    if (line == "exit")
                        break;

                    Execute(line.Split(new string[] { "::" }, StringSplitOptions.RemoveEmptyEntries));
                }
            }
            catch (Exception Ex) { Console.WriteLine(Ex.Message); }
        }

        private static void Execute(string[] function)
        {
            for (int cntr = 0; cntr < function.Length; ++cntr)
                function[cntr] = function[cntr].TrimStart('\t', ' ', '\n', '\r', '\0').TrimEnd('\t', ' ', '\n', '\r', '\0');

            Type type;
            try { type = Type.GetType("System." + function[0].TrimStart('!')); }
            catch { type = Type.GetType("System"); }

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
        }

        private static void ParseArgs(ref string input, ref List<object> args, ref Type[] argTypes, dynamic parent)
        {
            if (input.Contains("("))
            {
                args = new List<object>();
                args.AddRange(input.Substring(input.IndexOf('(') + 1, input.IndexOf(')') - (input.IndexOf('(') + 1)).Replace(", ", ",").Split(','));
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
                        else argTypes[cntr] = typeof(string);
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