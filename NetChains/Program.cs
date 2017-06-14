using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetChains
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine();

            while (true)
            {
                Console.Write("\n>>>");
                string input = Console.ReadLine();
                Console.WriteLine(Execute(input.Split(new string[] { "::" }, StringSplitOptions.RemoveEmptyEntries)));
            }
        }

        private static string Execute(string[] function)
        {
            Type type = Type.GetType("System." + function[0]);

            try
            {
                int colons = function.Length;
                object obj = null;
                while (colons > 2)
                {
                    string curFunc = function[function.Length - colons];
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
                            string[] args = null;

                            if (curFunc.Contains("("))
                            {
                                args = curFunc.Substring(curFunc.IndexOf('(') + 1, curFunc.IndexOf(')') - (curFunc.IndexOf('(') + 1)).Replace(", ", ",").Split(',');
                                curFunc = curFunc.Substring(0, curFunc.IndexOf('('));
                            }

                            obj = type.GetMethod(curFunc).Invoke(obj, args);
                        }
                    }
                    --colons;
                }
                return type.GetProperty(function[function.Length - 1]).GetValue(obj).ToString();
            }
            catch
            {
                string retval = type.GetMethod(function[1]).Invoke(null, null).ToString();
                return retval;
            }
        }
    }
}
