using System;
using System.Collections.Generic;

namespace NetChains
{
    class Program
    {
        const string VERSIONSTRING = "1.0.0";
        static void Main(string[] args)
        {
            Console.WriteLine($"Welcome to the NetChains interpreter!\n© 2017 Weston Sleeman, version {VERSIONSTRING}");

            while (true)
            {
                Console.Write("\n>>>");
                string input = Console.ReadLine();
                Console.WriteLine(Execute(input.Split(new string[] { "::" }, StringSplitOptions.RemoveEmptyEntries)));
            }
        }

        private static string Execute(string[] function)
        {
            try
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
                        try
                        {
                            obj = type.GetProperty(curFunc).GetValue(obj);
                        }
                        catch
                        {
                            List<object> args = null;
                            Type[] argTypes = null;

                            if (curFunc.Contains("("))
                            {
                                args = new List<object>();
                                args.AddRange(curFunc.Substring(curFunc.IndexOf('(') + 1, curFunc.IndexOf(')') - (curFunc.IndexOf('(') + 1)).Replace(", ", ",").Split(','));
                                curFunc = curFunc.Substring(0, curFunc.IndexOf('('));
                                argTypes = new Type[args.Count];

                                for (ushort cntr = 0; cntr < args.Count; ++cntr)
                                {
                                    if (int.TryParse((string)args[cntr], out int catchval))
                                    {
                                        argTypes[cntr] = typeof(int);
                                        args[cntr] = catchval;
                                    }
                                    else argTypes[cntr] = typeof(string);
                                }
                            }

                            obj = type.GetMethod(curFunc, argTypes).Invoke(obj, args.ToArray());
                        }
                    }
                    --colons;
                }
                return obj?.ToString();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
