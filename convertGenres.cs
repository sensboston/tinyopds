using System;
using System.IO;

namespace convertGenres
{
    // Simple program to convert genres from text file to xml
    public class Program
    {
		static bool firstTime = true;

        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: convertGenres file");
                return;
            }

            if (File.Exists(args[0]))
            {
                using (StreamReader sr = new StreamReader(args[0]))
                {
                    using (StreamWriter sw = new StreamWriter(Path.GetFileNameWithoutExtension(args[0]) + ".xml"))
                    {
						sw.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>");

                        while (!sr.EndOfStream)
                        {
                            string s = sr.ReadLine().Replace("&", "&amp;");

                            if (!s.Contains("codepage"))
                            {
                                // Is it multilingual file (en+ru)?
                                int lang_div = s.IndexOf((char)65533);
                                //Console.WriteLine("lang_div = {0}", lang_div);

                                if (s[0] != ' ')
                                {
                                    if (lang_div > 0)
                                    {
                                        string ru_name = s.Substring(0, lang_div - 1).Trim();
                                        string en_name = s.Substring(lang_div + 1).Trim();
                                        sw.WriteLine( (firstTime ? "" :"\t</genre>\n") + "\t<genre ru=\"" + ru_name + "\" name=\"" + en_name + "\">");
                                    }
                                    else sw.WriteLine("\t<genre name=\"" + s + "\">");
                                    firstTime = false;
                                }
                                else
                                {
                                    int name_idx = s.IndexOf(' ', 2);
                                    string subgenre = s.Substring(1, name_idx - 1);

                                    if (lang_div > 0)
                                    {
                                        string ru_name = s.Substring(name_idx + 1, lang_div - name_idx - 2).Trim();
                                        string en_name = s.Substring(lang_div + 1).Trim();
                                        sw.WriteLine("\t\t<subgenre tag=\"" + subgenre + "\" ru=\"" + ru_name + "\">" + en_name + "</subgenre>");
                                    }
                                    else
                                    {
                                        string name = s.Substring(name_idx + 1);
                                        sw.WriteLine("\t\t<subgenre tag=\"" + subgenre + "\">" + name + "</subgenre>");
                                    }
                                }
                            }
                        }

						sw.WriteLine("\t</genre>\n</root>");
                    }
                }
            }
        }
    }
}