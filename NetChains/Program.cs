using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace NetChains
{
    class Program
    {
        const string VERSIONSTRING = "1.2.3";
        static void Main(string[] args)
        {
            Console.WriteLine($"Welcome to the NetChains interpreter!\n(c) 2017 Weston Sleeman, version {VERSIONSTRING}\nType \"help\" for a brief tutorial or \"exit\" to return to the shell.");
            if (args == null) args = new string[0]{ };

            if (args.Length == 0)
            {
                while (true)
                {
                    Console.Write("\n>>>");
                    string input = Console.ReadLine();
                    if (input == "exit")
                        break;
                    else if (input.StartsWith("load"))
                    {
                        ExecFile(input.Substring((input.Length > 4) ? 5 : 4));
                        Main(null);
                        break;
                    }
                    else if (input == "help")
                    {
                        Console.WriteLine("NetChains is copyrighted by Weston Sleeman, but feel free to redistribute this executable in its original form.");
                        Console.WriteLine("NetChains provides direct access to the .NET framework in a scriptable and chain-y format.");
                        Console.WriteLine("Commands are formed in chains, linked by a double colon (::). Types are specified with a bang (!).");
                        Console.WriteLine("\nEx. !ConsoleColor::Red::!Console::ForegroundColor = $::Write(Hello)::ResetColor");
                        Console.WriteLine("(Shifts to System.ConsoleColor; Selects Red; Shifts to System.Console; Sets ForegroundColor to the parent ($, currently Red); Writes Hello to screen; Resets colors)");
                    }

                    try { Console.WriteLine(Execute(input.Split(new string[] { "::" }, StringSplitOptions.RemoveEmptyEntries))); }
                    catch (Exception ex) { Console.WriteLine(ex.Message); }
                }
            }
            else
            {
                foreach (string arg in args)
                {
                    ExecFile(arg);
                }
            }
        }

        private static void ExecFile(string path)
        {
            try
            {
                string[] code = File.ReadAllLines(path);

                foreach (string line in code) Console.WriteLine(Execute(line.Split(new string[] { "::" }, StringSplitOptions.RemoveEmptyEntries)));
            }
            catch (Exception Ex) { Console.WriteLine(Ex.Message); }
            finally
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
                Console.Clear();
            }
        }

        private static string Execute(string[] function)
        {
            Type type;
            try { type = Type.GetType("System." + function[0].TrimStart('!')); }
            catch { throw new Exception("Commands must start with a type."); }

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
                        try { obj = type.GetProperty(curFunc).GetValue(obj); }
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
                args.AddRange(input.Substring(input.IndexOf('(') + 1, input.IndexOf(')') - (input.IndexOf('(') + 1)).Replace(", ", ",").Split(','));
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
    }
}
