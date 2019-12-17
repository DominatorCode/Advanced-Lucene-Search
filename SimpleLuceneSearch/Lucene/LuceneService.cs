using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Version = Lucene.Net.Util.Version;

namespace AdvancedLuceneSearch.Lucene
{
    public interface ILuceneService
    {
        void BuildIndex(IEnumerable<SampleDataFileRow> dataToIndex, bool newIndex);
        int GetIndexDocCount();

        IEnumerable<SampleDataFileRow> SearchMultipleSteps(string searchTerm, int precision);
        IEnumerable<SampleDataFileRow> SearchAdvShort(string input, int precision);
        void BuildRamIndex(IEnumerable<SampleDataFileRow> dataToIndex);

        void SetPath(string path);

        void ShowExplanationResult();
    }

    public class LuceneService : ILuceneService
    {
        // Note there are many different types of Analyzer that may be used with Lucene, the exact one you use
        // will depend on your requirements

        //private DirectoryFS luceneIndexDirectory;       
        private string _indexPath = AppDomain.CurrentDomain.BaseDirectory + @"\Lucene\Index";

        private static string resource1 = "AdvancedLuceneSearch.Lucene.Net.dll";
        private static string resource2 = "AdvancedLuceneSearch.ICSharpCode.SharpZipLib.dll";

        private static bool _condShouldResearch = true; // стоит ли делать пере-поиск с учетом знака препинания

        private static readonly HashSet<string> ListStopWords = new HashSet<string>(new[] {
            "в", "без", "до", "из", "к", "на", "не", "по", "о", "от", "перед", "при", "через", "с", "у",
            "за", "над", "об", "под", "про", "для", "%", "№", "шт", "упак", "арт", "штука", "упаковок", "штук", "рул", "кг", "см", "м2", "т",
            "литров", "диаметр", "зао", "ооо", "оао", "упаковка", "рулон", "литр"});

        private static readonly HashSet<string> ListUnitsExclude = new HashSet<string>(new[] { "производитель", "страна", "китай", "россия", "казахстан", "неизвестен" });

        private static readonly HashSet<string> ListAdjectives = new HashSet<string>(new[] {
            "ее","ые","ое","ей","ий","ый","ой","ем","им","ым","ом","их","ых","ую","юю","ая","яя","ою","ею", "ими", "ыми", "его", "ого", "ему", "ому" });

        private FSDirectory _directoryTemp;
        private RAMDirectory _RAMdirectory = new RAMDirectory();
        public int CountSearchResults { get; } = 1000;
        private readonly int _lengthStopPunctuationMark = 10; // количество знаков после которого можно искать стоп символы

        public FSDirectory DirectoryFs
        {
            get
            {
                if (_directoryTemp == null) _directoryTemp = FSDirectory.Open(new DirectoryInfo(_indexPath));
                if (IndexWriter.IsLocked(_directoryTemp)) IndexWriter.Unlock(_directoryTemp);
                var lockFilePath = Path.Combine(_indexPath, "write.lock");
                if (File.Exists(lockFilePath)) File.Delete(lockFilePath);
                return _directoryTemp;
            }

        }

        private string _explanationResult = "";
        private string _similarityResult = "";

        public LuceneService()
        {
            EmbeddedAssembly.Load(resource1, "Lucene.Net.dll");
            EmbeddedAssembly.Load(resource2, "ICSharpCode.SharpZipLib.dll");

            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

            InitialiseLucene();
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return EmbeddedAssembly.Get(args.Name);
        }

        private void InitialiseLucene()
        {
            if (System.IO.Directory.Exists(_indexPath)) System.IO.Directory.Delete(_indexPath, true);

            // luceneIndexDirectory = FSDirectory.Open(indexPath); 
            //writer = new IndexWriter(_directory, analyzer, true, IndexWriter.MaxFieldLength.UNLIMITED); 

        }

        ~LuceneService()
        {
            DirectoryFs.Dispose();
        }

        public void BuildIndex(IEnumerable<SampleDataFileRow> dataToIndex, bool newIndex = true)
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30, ListStopWords);
            using (var writer = new IndexWriter(DirectoryFs, analyzer, newIndex, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var sampleDataFileRow in dataToIndex)
                {
                    Document doc = new Document();
                    doc.Add(new Field("LineNumber", sampleDataFileRow.LineNumber.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO));
                    doc.Add(new Field("LineText", sampleDataFileRow.LineText, Field.Store.YES, Field.Index.ANALYZED));
                    writer.AddDocument(doc);
                }
                analyzer.Close();
                writer.Optimize();
                //writer.Flush(true, false, true);
                writer.Dispose();
            }
        }

        public void BuildRamIndex(IEnumerable<SampleDataFileRow> dataToIndex)
        {

            var analyzer = new StandardAnalyzer(Version.LUCENE_30, ListStopWords);
            using (var writer = new IndexWriter(_RAMdirectory, analyzer, true, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var sampleDataFileRow in dataToIndex)
                {
                    Document doc = new Document();
                    doc.Add(new Field("LineNumber", sampleDataFileRow.LineNumber.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO));
                    doc.Add(new Field("LineText", sampleDataFileRow.LineText, Field.Store.YES, Field.Index.ANALYZED));
                    writer.AddDocument(doc);
                }
                analyzer.Close();
                writer.Optimize();
                //writer.Flush(true, false, true);
                writer.Dispose();
            }
        }

        private static List<string> RemovePartFromEnd(List<string> listInput, string tailToRemove)
        {
            Debug.Assert(tailToRemove != null);

            for (int i = 0; i < listInput.Count(); i++)
                if (listInput[i].EndsWith(tailToRemove))
                    listInput[i] = listInput[i].Remove(listInput[i].Length - 1, 1);

            return listInput;
        }

        private static List<string> RemovePartFromStart(List<string> listInput, string frontToRemove)
        {

            for (var i = 0; i < listInput.Count(); i++)
                if (listInput[i].StartsWith(frontToRemove))
                    listInput[i] = listInput[i].Remove(0, 1);

            return listInput;
        }

        private static List<string> RemoveTrashEntries(List<string> listInput)
        {
            for (var i = listInput.Count - 1; i >= 0; --i)
            {
                if (listInput[i].Length == 0)
                    listInput.RemoveAt(i);
                if (listInput[i].Length == 1 & !Char.IsLetterOrDigit(listInput[i].First()))
                    listInput.RemoveAt(i);
            }

            return listInput;

        }

        private static List<string> RemoveDashes(List<string> listInput)
        {
            for (var i = listInput.Count - 1; i >= 0; --i)
            {
                var indexDash = listInput[i].IndexOf('-');

                if (indexDash > 4 & listInput[i].Length > 9 & indexDash < listInput[i].Length - 4)
                    if (char.IsLetter(listInput[i][indexDash - 2]) & char.IsLetter(listInput[i][indexDash + 2]))
                    {
                        listInput[i] = listInput[i].Replace('-', ' ');
                    }
            }

            return listInput;

        }

        public IEnumerable<SampleDataFileRow> SearchMultipleSteps(string searchTerm, int precision)
        {
            if (string.IsNullOrEmpty(searchTerm)) return new List<SampleDataFileRow>();

            const int countWordsLimitSecondStep = 3; // количество слов при первичном поиске (альтернативный поиск)

            // полный список отформатированных слов из запроса
            List<string> terms;

            // список слов до первого знака препинания
            // для первого цикла поиска
            var termsInitial = new List<string>();

            // остальная часть слов

            // ищем поисковую фразу до первого знака препинания
            int indexStopPunctuationMark = 0;
            char[] arrayCharsStop = { ',', '.', ':', '(' };
            if (searchTerm.Length > 6)
                indexStopPunctuationMark = searchTerm.IndexOfAny(arrayCharsStop, 0, searchTerm.Length - 3);
            bool condHasInitialTerms = false;

            if (indexStopPunctuationMark > _lengthStopPunctuationMark)
            {
                condHasInitialTerms = true;
                var termInitial = searchTerm.Substring(0, indexStopPunctuationMark);
                termsInitial = GetFormattedTerms(termInitial, 1);

                // вторая часть, все что за знаком препинания
                var termLast = searchTerm.Substring(indexStopPunctuationMark);
                var termsLast = GetFormattedTerms(termLast);

                // соединяем полученные подстроки вместе           
                terms = termsInitial.Concat(termsLast).ToList();
            }
            else
            {
                terms = GetFormattedTerms(searchTerm, 1);
            }

            // вспомогательные переменные для циклов поиска
            HashSet<string> finalTerm = new HashSet<string>();
            IEnumerable<SampleDataFileRow> listTempSearchResults;

            var firstMain = false;
            string nameStringForSearch;

            // выбираем первое слово для поиска           
            var firstTerm = terms.First();
            var condFound = false;

            // если первое слово существительное с заглавной буквой или цифровым кодом
            if (Char.IsUpper(firstTerm, 0) & !Char.IsNumber(firstTerm, 0) & Char.IsLower(firstTerm, firstTerm.Length - 1) &
                !Char.IsNumber(firstTerm, firstTerm.Length - 1) & !ListAdjectives.Any(x => firstTerm.EndsWith(x)))
                condFound = true;
            else if (firstTerm.All(char.IsDigit) & firstTerm.Length > 4)
                condFound = true;


            // если самое первое слово не подходит по критерию поиска, 
            // делаем обход по первым трем словам запроса
            var countWordsLimitFirstStep = 3;
            if (countWordsLimitFirstStep > terms.Count - 1)
                countWordsLimitFirstStep = terms.Count - 1;

            // берем первое слово в кавычках
            if (!condFound)
                for (var i = 0; i <= countWordsLimitFirstStep; i++)
                {
                    if (!(terms[i].First() == '"' & terms[i].Last() == '"')) continue;
                    firstTerm = terms[i].Replace("\"", "");
                    condFound = true;
                    break;
                }

            terms = RemovePartFromStart(terms, "\"");
            terms = RemovePartFromEnd(terms, "\"");

            // если такого нет,
            // берем любое слово существительное
            if (!condFound)
                for (int i = 0; i <= countWordsLimitFirstStep; i++)
                {
                    //string tempTerm = new String(firstTerm.Where(ch => Char.IsLetterOrDigit(ch)).ToArray());
                    if (terms[i].All(Char.IsLetter) & !ListAdjectives.Any(x => terms[i].EndsWith(x)) & terms[i].Length > 2)
                    {
                        firstTerm = terms[i];
                        condFound = true;
                        break;
                    }
                }

            // если его нет, то берем код
            if (!condFound)
                for (int i = 0; i <= countWordsLimitFirstStep; i++)
                    if (terms[i].All(Char.IsUpper) & terms[i].Length > 1)
                    {
                        firstTerm = terms[i];
                        condFound = true;
                        break;
                    }

            // если найдено первое подходящее слово
            if (condFound)
            {
                // первый самый грубый поиск только по первому слову
                listTempSearchResults = _search(firstTerm + "~", precision, DirectoryFs);

                Debug.Assert(listTempSearchResults != null);

                // второй поиск по трем ключевым словам среди найденного
                var sampleDataFileRows = listTempSearchResults as SampleDataFileRow[] ?? listTempSearchResults.ToArray();
                if (sampleDataFileRows.Count() > 1 & terms.Count() > 1)
                {
                    foreach (var term in terms)
                    {
                        if (finalTerm.Count > countWordsLimitSecondStep)
                            break;

                        if (termsInitial.Count > 0)
                            if (terms.IndexOf(term) > termsInitial.Count - 1)
                                break;

                        if (ListAdjectives.Any(x => term.EndsWith(x)))
                        {
                            finalTerm.Add(term + "^1.2~");
                        }
                        else if (term.All(Char.IsUpper) & term.All(Char.IsLetterOrDigit))
                        {
                            finalTerm.Add(term + "^1.4~");
                        }
                        else if (Char.IsUpper(term, 0) & !Char.IsNumber(term, 0) & Char.IsLower(term, term.Length - 1) & !Char.IsNumber(term, term.Length - 1) & !firstMain)
                        {
                            finalTerm.Add(term + "^2~");
                            firstMain = true;
                        }
                        else if (term.Length > 3 & (term.First() != '(' & term.Last() != ')'))
                        {
                            finalTerm.Add(term + "~");
                        }
                    }

                    nameStringForSearch = string.Join(" ", finalTerm.ToArray());

                    // пропускаем слово из первого этапа поиска
                    if (string.CompareOrdinal(nameStringForSearch, firstTerm) != 0)
                    {
                        BuildRamIndex(sampleDataFileRows);

                        listTempSearchResults = _search(nameStringForSearch, precision, _RAMdirectory);

                        // если есть результаты и поиск был до ключевого знака препинания
                        // исключаем варианты, где нет ни одного найденного слова до ключевого знака препинания
                        var listResultsToCheck = listTempSearchResults as SampleDataFileRow[] ?? listTempSearchResults.ToArray();
                        if (listResultsToCheck.Any())
                            sampleDataFileRows = (SampleDataFileRow[])ExcludeBadResults(listResultsToCheck, nameStringForSearch, arrayCharsStop, precision);

                    }

                    // третий полный поиск по всем словам в оставшемся                
                    if (sampleDataFileRows.Count() > 1 & terms.Count() > finalTerm.Count)
                    {
                        finalTerm.Clear();

                        // убираем скобки в начале и конце слов, иначе поиск будет некорректным
                        terms = RemovePartFromEnd(terms, ")");
                        terms = RemovePartFromStart(terms, "(");

                        foreach (var term in terms)
                        {

                            if (ListAdjectives.Any(x => term.EndsWith(x)))
                            {
                                finalTerm.Add(term + " ^1.2~");
                            }
                            //else if (int.TryParse(term, out digitTerm) || double.TryParse(term, out doubleTerm))
                            // finalTerm.Add(term + "^0.7");
                            else if (Char.IsUpper(term, 0) & !Char.IsNumber(term, 0) & Char.IsLower(term, term.Length - 1) & !Char.IsNumber(term, term.Length - 1) & !firstMain)
                            {
                                finalTerm.Add(term + "^2~");
                                firstMain = true;
                            }
                            else if (term.Length > 3)
                            {
                                finalTerm.Add(term + "^1.1~");
                            }
                            else
                                finalTerm.Add(term);
                        }


                        nameStringForSearch = string.Join(" ", finalTerm.ToArray());
                        finalTerm.Clear();

                        BuildRamIndex(sampleDataFileRows);

                        return _search(nameStringForSearch, precision, _RAMdirectory);
                    }
                    else
                        return sampleDataFileRows;

                }
                else if (terms.Count == 1 & sampleDataFileRows.Any())
                    return sampleDataFileRows;
                else if (sampleDataFileRows.Count() == 1)
                    return sampleDataFileRows;
                else if (!sampleDataFileRows.Any() & terms.Count == 1)
                    return sampleDataFileRows;

            }

            //--------- альтернативный поиск, если не удалось ничего найти с помощью первого шага            

            // лимит искомых слов в строке
            int countLimitWordsForSearch = 5;

            if (condHasInitialTerms)
                if (countLimitWordsForSearch > termsInitial.Count - 1)
                    countLimitWordsForSearch = termsInitial.Count - 1;

            // счетчик количества прилагательных
            int countAdjectiveLimit = 0;

            // расставляем приоритеты для слов
            finalTerm.Clear();
            foreach (var term in terms)
            {
                if (finalTerm.Count > countWordsLimitSecondStep || terms.IndexOf(term) > countLimitWordsForSearch)
                    break;

                if (ListAdjectives.Any(x => term.EndsWith(x)) & countAdjectiveLimit < 3)
                {
                    finalTerm.Add(term + "^1.2~");
                    countAdjectiveLimit++;
                }
                else if (term.Length > 2 & term.All(c => char.IsUpper(c)))
                {
                    finalTerm.Add(term + "^1.4~");
                }
                else if (term.Length > 3 & term.First() != '(' & term.Last() != ')')
                {
                    finalTerm.Add(term + "~");
                }
            }


            // если вообще ничего не найдено, то рассматриваем самые простые слова
            if (finalTerm.Count == 0)
            {
                countLimitWordsForSearch = 2;
                foreach (var term in terms)
                {
                    if (finalTerm.Count > countLimitWordsForSearch)
                        break;

                    if (condHasInitialTerms)
                        if (terms.IndexOf(term) > termsInitial.Count - 1)
                            break;

                    if (term.Length > 2 & term.All(r => char.IsLetter(r)))
                    {
                        finalTerm.Add(term);
                    }
                    else if (term.Length > 4 & term.All(r => char.IsLetterOrDigit(r)))
                    {
                        finalTerm.Add(term);
                    }
                }

            }


            if (finalTerm.Count > 0)
                nameStringForSearch = string.Join(" ", finalTerm.ToArray());
            else
                return new List<SampleDataFileRow>();

            listTempSearchResults = _search(nameStringForSearch, precision, DirectoryFs);

            // если есть результаты и поиск был до ключевого знака препинания
            // исключаем варианты, где нет ни одного найденного слова до ключевого знака препинания
            var searchMultipleSteps = listTempSearchResults as SampleDataFileRow[] ?? listTempSearchResults.ToArray();
            if (searchMultipleSteps.Any())
            {
                BuildRamIndex(new List<SampleDataFileRow> { searchMultipleSteps.First() });
                searchMultipleSteps = (SampleDataFileRow[])ExcludeBadResults(searchMultipleSteps, nameStringForSearch, arrayCharsStop, precision);
            }

            // убираем скобки в начале и конце слов, иначе поиск будет некорректным
            terms = RemovePartFromEnd(terms, ")");
            terms = RemovePartFromStart(terms, "(");

            // ДОРАБОТАТЬ отфильтровать найденные результаты по соотношению найденных слов в фразе

            // теперь среди найденного ищем по всем словам            
            if (searchMultipleSteps.Count() > 1 & terms.Count() > finalTerm.Count)
            {
                finalTerm.Clear();
                // расставляем приоритеты для слов
                foreach (var term in terms)
                {
                    if (ListAdjectives.Any(x => term.EndsWith(x)))
                    {
                        finalTerm.Add(term + "^1.2~");
                    }
                    else if (term.Length > 3 & term.All(c => char.IsUpper(c)))
                    {
                        finalTerm.Add(term + "^1.4~");
                    }

                    else if (term.Length > 4)
                    {
                        finalTerm.Add(term + "~");
                    }
                    else
                        finalTerm.Add(term);
                }

                nameStringForSearch = string.Join(" ", finalTerm.ToArray());

                BuildRamIndex(searchMultipleSteps);

                return _search(nameStringForSearch, precision, _RAMdirectory);
            }
            else
                return searchMultipleSteps;

        }

        public IEnumerable<SampleDataFileRow> SearchAdvShort(string input, int precision)
        {
            Debug.Assert(!string.IsNullOrEmpty(input));
            /*var terms = input.Trim().Replace("-", " ").Split(' ')
                .Where(x => !string.IsNullOrEmpty(x)).Select(x => x.Trim() + "*");*/

            var terms = input.Trim().Replace("-", " ").Split(' ')
                 .Where(x => !string.IsNullOrEmpty(x)).Select(x => x.Trim());

            HashSet<string> finalTerm = new HashSet<string>();

            var countAdjectiveLimit = 0;

            const int countLimitWord = 4;

            foreach (var term in terms)
            {
                if (finalTerm.Count > countLimitWord)
                    break;

                if (ListAdjectives.Any(x => term.EndsWith(x)) & countAdjectiveLimit < 3)
                {
                    finalTerm.Add(term + " ^0.8~");
                    countAdjectiveLimit++;
                }
                else if (term.Length > 3 & term.All(c => char.IsUpper(c)))
                    finalTerm.Add(term + " ^1.2~");
                else if (term.Length > 3) // условие все заглавные
                {
                    finalTerm.Add(term + "^~");
                }
            }

            string exportString = string.Join(" ", finalTerm.ToArray());

            return _search(exportString, precision, DirectoryFs);
        }

        private FuzzyQuery FuzzyQueryParseQuery(string searchQuery)
        {
            var query = new FuzzyQuery(new Term("LineText", searchQuery), 0.7f);
            return query;
        }


        private static Query ParseQuery(string searchQuery, QueryParser parser)
        {
            Query query;
            try
            {
                query = parser.Parse(searchQuery.Trim());
            }
            catch (ParseException)
            {
                query = parser.Parse(QueryParser.Escape(searchQuery.Trim()));
            }
            return query;
        }

        public IEnumerable<SampleDataFileRow> _search(string searchTerm, int precision, global::Lucene.Net.Store.Directory indexDirectory)
        {
            Debug.Assert(!String.IsNullOrEmpty(searchTerm));

            List<SampleDataFileRow> results = new List<SampleDataFileRow>();

            if (String.IsNullOrEmpty(searchTerm))
                return results;

            using (IndexSearcher searcher = new IndexSearcher(indexDirectory))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30, ListStopWords);

                QueryParser parser = new QueryParser(Version.LUCENE_30, "LineText", analyzer);

                if (precision > 0 && precision < 100)
                    parser.FuzzyMinSim = ((float)precision) * 0.01f;
                else if (precision == 100)
                    parser.FuzzyMinSim = 0.99f;
                else
                    parser.FuzzyMinSim = 0.8f;

                //parser.PhraseSlop = 5;

                var query = ParseQuery(searchTerm, parser);

                //var query = fparseQuery(searchTerm);
                ScoreDoc[] hitsFound = searcher.Search(query, null, CountSearchResults).ScoreDocs;

                foreach (var t in hitsFound)
                {
                    var sampleDataFileRow = new SampleDataFileRow();
                    int docId = t.Doc;
                    float score = t.Score;
                    Explanation explanation = searcher.Explain(query, t.Doc);
                    Document doc = searcher.Doc(docId);

                    sampleDataFileRow.LineNumber = int.Parse(doc.Get("LineNumber"));
                    sampleDataFileRow.LineText = doc.Get("LineText");
                    sampleDataFileRow.Score = score;

                    _explanationResult = explanation.ToString();


                    results.Add(sampleDataFileRow);
                }

                analyzer.Close();
                searcher.Dispose();
            }
            return results.OrderByDescending(x => x.Score).ToList();
        }

        public int GetIndexDocCount()
        {
            var reader = IndexReader.Open(DirectoryFs, true);
            int docCount = reader.NumDocs();
            reader.Dispose();
            return docCount;
        }

        public HashSet<String> GetListOfItemsNameFromIndex()
        {
            IndexReader reader = IndexReader.Open(DirectoryFs, true);
            TermEnum terms = reader.Terms();
            HashSet<String> uniqueTerms = new HashSet<String>();
            while (terms.Next())
            {
                Term term = terms.Term;
                if (term.Field.Equals("LineText"))
                {
                    uniqueTerms.Add(term.Text);
                }
            }

            return uniqueTerms;
        }

        // ДОРАБОТКА использовать данную функцию для фильтрации результатов поиска
        public int GetMatchWordCount(IEnumerable<SampleDataFileRow> listFoundDocs, string searchTerm)
        {

            int totalFreq = 0;
            IndexReader reader = IndexReader.Open(DirectoryFs, true);

            TermDocs termDocs = reader.TermDocs();
            termDocs.Seek(new Term("LineText", searchTerm));
            foreach (SampleDataFileRow singleRow in listFoundDocs)
            {
                termDocs.SkipTo(singleRow.LineNumber);
                totalFreq += termDocs.Freq;
            }

            return totalFreq;
        }

        // поиск с указанием найденной позиции в тексте
        public void DoSearch(String db, String querystr, global::Lucene.Net.Store.Directory indexDirectory)
        {
            // 1. Specify the analyzer for tokenizing text.  
            //    The same analyzer should be used as was used for indexing  
            StandardAnalyzer analyzer = new StandardAnalyzer(Version.LUCENE_30, ListStopWords);


            // 2. query  
            Query q = new QueryParser(Version.LUCENE_30, "LineText", analyzer).Parse(querystr);

            // 3. search  
            int hitsPerPage = 10;
            IndexSearcher searcher = new IndexSearcher(indexDirectory, true);
            IndexReader reader = IndexReader.Open(indexDirectory, true);
            searcher.SetDefaultFieldSortScoring(true, false);
            TopScoreDocCollector collector = TopScoreDocCollector.Create(hitsPerPage, true);
            searcher.Search(q, collector);
            ScoreDoc[] hits = collector.TopDocs().ScoreDocs;

            // 4. display term positions, and term indexes   
            MessageBox.Show("Found " + hits.Length + " hits.");

            for (int i = 0; i < hits.Length; ++i)
            {

                int docId = hits[i].Doc;
                ITermFreqVector tfvector = reader.GetTermFreqVector(docId, "LineText");
                TermPositionVector tpvector = (TermPositionVector)tfvector;
                // this part works only if there is one term in the query string,  
                // otherwise you will have to iterate this section over the query terms.  
                int termidx = tfvector.IndexOf(querystr);
                int[] termposx = tpvector.GetTermPositions(termidx);
                TermVectorOffsetInfo[] tvoffsetinfo = tpvector.GetOffsets(termidx);

                for (int j = 0; j < termposx.Length; j++)
                {
                    MessageBox.Show("termpos : " + termposx[j]);
                }
                for (int j = 0; j < tvoffsetinfo.Length; j++)
                {
                    int offsetStart = tvoffsetinfo[j].StartOffset;
                    int offsetEnd = tvoffsetinfo[j].EndOffset;
                    MessageBox.Show("offsets : " + offsetStart + " " + offsetEnd);
                }

                // print some info about where the hit was found...  
                Document d = searcher.Doc(docId);
                MessageBox.Show((i + 1) + ". " + d.Get("path"));
            }

            // searcher can only be closed when there  
            // is no need to access the documents any more.   
            searcher.Dispose();
        }

        public void SetPath(string path)
        {
            _indexPath = path;
            _directoryTemp = null;
        }

        public void ShowExplanationResult()
        {
            MessageBox.Show(_explanationResult);
        }

        private static List<string> GetFormattedTerms(string termRaw, int indexStop = 0)
        {
            termRaw = termRaw.Trim().Replace("*", " ").Replace("?", "").Replace("!", "").Replace("+", " ").Replace("&", "").Replace("^", "").Replace("~", "");
            termRaw = termRaw.Replace(":", " ").Replace(";", "").Replace("{", "").Replace("}", "").Replace("[", "").Replace("]", "").Replace("|", "").Replace("\\", "");
            termRaw = Regex.Replace(termRaw, @"\s+", " ");

            List<string> terms = termRaw.Split(' ')
                 .Where(x => !string.IsNullOrEmpty(x)).Select(x => x.Trim()).ToList();


            // удаляем лишние символы в конце слова
            terms = RemovePartFromEnd(terms, ",");
            terms = RemovePartFromEnd(terms, ".");
            terms = RemoveTrashEntries(terms);

            // убираем "-" в словах существительных
            terms = RemoveDashes(terms);

            // исключаем из поиска пользовательские слова
            for (var i = terms.Count - 1; i >= indexStop; i--)
            {
                if (ListUnitsExclude.Contains(terms[i].ToLower()))
                    terms.Remove(terms[i]);
            }

            return terms;
        }

        // исключает из входящего списка
        // некорректные результаты 
        private IEnumerable<SampleDataFileRow> ExcludeBadResults(SampleDataFileRow[] listResultsToCheck, string termSearch, char[] stopPunctuationMark, int searchPrecision)
        {
            var listFinal = new List<SampleDataFileRow>();

            // делаем проход по всем результатам поиска
            // и исключаем варианты, где найденные слова из запроса стоят после знака препинания
            for (var i = 0; i < listResultsToCheck.Count(); i++)
            {
                // создаем индекс для поиска с урезанным вариантом
                var listClipped = new List<SampleDataFileRow>();
                var dataRow = new SampleDataFileRow
                {
                    Score = listResultsToCheck.ElementAt(i).Score,
                    LineText = listResultsToCheck.ElementAt(i).LineText,
                    LineNumber = listResultsToCheck.ElementAt(i).LineNumber
                };
                listClipped.Add(dataRow);
                // обрезать строку при необходимости
                listClipped[0] = CutDataIndex(listClipped[0], stopPunctuationMark);

                if (_condShouldResearch) // если было обрезание
                {
                    // выполнить перестройку индекса
                    // для обрезанного значения
                    RebuildRamIndex(listClipped);
                    // и выполнить поиск заново
                    var listTempSearchResults = _search(termSearch, searchPrecision, _RAMdirectory);

                    // если значение было найдено, добавляем в финальный результат
                    if (!listTempSearchResults.Any()) continue;
                    listFinal.Add(listResultsToCheck.ElementAt(i));
                }
                else
                    listFinal.Add(listResultsToCheck.ElementAt(i));
            }

            return listFinal;
        }

        // обрезаем строки у входных данных
        // до первого знака препинания
        private SampleDataFileRow CutDataIndex(SampleDataFileRow dataInput, char[] stopPunctuationMark)
        {
            _condShouldResearch = false;

            var textCut = dataInput.LineText;

            int positionPMark = -1;
            positionPMark = textCut.IndexOfAny(stopPunctuationMark);

            // если есть знаки препинания,
            // обрезаем строку до первого знака
            if (positionPMark > _lengthStopPunctuationMark)
            {
                dataInput.LineText = textCut.Substring(0, positionPMark);
                _condShouldResearch = true;
            }

            return dataInput;
        }

        public void RebuildRamIndex(IEnumerable<SampleDataFileRow> dataToIndex)
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30, ListStopWords);
            using (var writer = new IndexWriter(_RAMdirectory, analyzer, false, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                writer.DeleteAll();
                writer.Commit();
                foreach (var sampleDataFileRow in dataToIndex)
                {
                    Document doc = new Document();
                    doc.Add(new Field("LineNumber", sampleDataFileRow.LineNumber.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO));
                    doc.Add(new Field("LineText", sampleDataFileRow.LineText, Field.Store.YES, Field.Index.ANALYZED));
                    writer.AddDocument(doc);
                }
                analyzer.Close();
                writer.Optimize();
                //writer.Flush(true, false, true);
                writer.Dispose();
            }
        }
    }
}


