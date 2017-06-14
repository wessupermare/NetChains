using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NetChains
{
    class Program
    {
        const string VERSIONSTRING = "1.1.0";
        static void Main(string[] args)
        {
            Console.WriteLine($"Welcome to the NetChains interpreter!\n(c) 2017 Weston Sleeman, version {VERSIONSTRING}");
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
                        ExecFile(input.Substring((input.Length > 4)?5:4));

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
                Main(null);
            }
        }

        private static string Execute(string[] function)
        {
            Type type = Type.GetType("System." + function[0].TrimStart('!'));

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
                                if (args == null) { obj = type.GetMethod(curFunc).Invoke(obj, null); }
                                else { obj = type.GetMethod(curFunc, argTypes).Invoke(obj, args.ToArray()); }
                            }
                            catch
                            {
                                try { obj = type.GetMember(curFunc)[0]; }
                                catch (Exception ex) { throw new Exception($"Error in phrase {(function.Length - colons) + 1}: {ex.Message}"); }
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
                        argTypes[cntr] = parent.ReflectedType;
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
