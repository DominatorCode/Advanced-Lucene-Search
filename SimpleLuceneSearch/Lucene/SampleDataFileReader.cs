using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AdvancedLuceneSearch.Lucene
{
    public interface ISampleDataFileReader
    {
        IEnumerable<SampleDataFileRow> ReadAllRows(string pathFile);
    }

    public class SampleDataFileReader : ISampleDataFileReader
    {
        public IEnumerable<SampleDataFileRow> ReadAllRows(string pathFile)
        {


            //FileInfo assFile = new FileInfo(Assembly.GetExecutingAssembly().Location);
            //string file = string.Format(pathFile, assFile.DirectoryFS.FullName);

            // ДОРАБОТКА нормализация текста (знаки пунктуации, -, пробелы в единицах измерения и т.д.)
            string[] lines = WriteSafeReadAllLines(pathFile);
            for (int i = 0; i < lines.Length; i++)
            {
                yield return new SampleDataFileRow
                {
                    LineNumber = i + 1,
                    LineText = lines[i]
                };
            }

            /*string[] lines = WriteSafeReadAllLines(pathFile);
            List<SampleDataFileRow> test = new List<SampleDataFileRow>();
            for (int i = 0; i < lines.Length; i++)
            {
                test.Add(new SampleDataFileRow { LineNumber = i + 1, LineText = lines[i] });
            }          

             return test;*/
        }

        public string[] WriteSafeReadAllLines(String path)
        {
            using (var csv = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(csv, Encoding.UTF8))
            {
                List<string> file = new List<string>();
                while (!sr.EndOfStream)
                {
                    file.Add(sr.ReadLine());
                }

                return file.ToArray();
            }
        }
    }
}
