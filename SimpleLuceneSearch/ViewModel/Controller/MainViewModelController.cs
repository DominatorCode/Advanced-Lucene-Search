using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AdvancedLuceneSearch.Lucene;

namespace AdvancedLuceneSearch.ViewModel.Controller
{
    public class MainViewModelController
    {
        private MainWindowViewModel viewModel;
        private ILuceneService luceneService;
        private ISampleDataFileReader sampleDataFileReader;
        private IEnumerable<SampleDataFileRow> sampleDataFileRows;
        public int CountSearchResult { get; set; } = 10;
        private string _errorText = "";
        private List<string> _listMultipleSearchResult;

        public MainViewModelController(MainWindowViewModel viewModel)
        {
            this.viewModel = viewModel;
            sampleDataFileReader = new SampleDataFileReader();
            this.luceneService = new LuceneService();
        }

        // устанавливает количество искомых результатов 
        // (при достижении n-го количества найденных результатов поиск прекращается)
        public void SetCountSearchResult(int countSearchResults)
        {
            if (countSearchResults > 0 & countSearchResults < 1000000)
                CountSearchResult = countSearchResults;
        }

        public string GetSearchResults()
        {
            if (_listMultipleSearchResult.Count > 0)
            {
                var lastResult = _listMultipleSearchResult.Last();
                _listMultipleSearchResult.Remove(lastResult);
                return lastResult;
            }
            else
                return "";
        }

        public void Start()
        {
            try
            {
                sampleDataFileRows = sampleDataFileReader.ReadAllRows(AppDomain.CurrentDomain.BaseDirectory + @"\Lucene\SampleDataFile.txt");
                
                var rawRows = sampleDataFileRows.Select(x => new RawRowDefinitionViewModel()
                {
                    LineNumber = x.LineNumber,
                    LineText = x.LineText
                });

                foreach (var rawRow in rawRows)
                {
                    this.viewModel.RawRows.Add(rawRow);
                }

                WireupViewModelStreams();

                BuildLuceneIndex();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                throw;
            }
            
        }



        private void WireupViewModelStreams()
        {
            viewModel.AddDisposable(viewModel.SearchCommand.CommandExecutedStream.Subscribe(_ => DoLuceneSearch()));
        }

        private void DoLuceneSearch()
        {
            int prec = viewModel.PrecisionSearch;
            this.viewModel.IsBusy = true;

            if (string.IsNullOrEmpty(viewModel.SearchTerm))
                return;

            var results = this.luceneService.SearchMultipleSteps(viewModel.SearchTerm, prec);

            var dataFileRows = results as SampleDataFileRow[] ?? results.ToArray();
            if (dataFileRows.Any())
            {

                foreach (var rawrow in this.viewModel.RawRows)
                {
                    rawrow.IsSelected = false;
                }

                var rawRowsToSelect = from result in dataFileRows
                                      let lineNum = result.LineNumber
                                      from rawRow in this.viewModel.RawRows
                                      where rawRow.LineNumber == lineNum
                                      select rawRow;

                foreach (var rawrow in rawRowsToSelect)
                {
                    rawrow.IsSelected = true;

                }
                _listMultipleSearchResult = dataFileRows.Select(b => b.LineText).Take(CountSearchResult).ToList();


                var singleResult = "123";

                while (singleResult != "")
                    singleResult = GetSearchResults();

                MessageBox.Show(dataFileRows.First().LineText);
                luceneService.ShowExplanationResult();
            }
            else
                MessageBox.Show("Nothing was found");

            this.viewModel.IsBusy = false;
        }

        private void BuildLuceneIndex()
        {
            sampleDataFileRows = sampleDataFileReader.ReadAllRows(AppDomain.CurrentDomain.BaseDirectory + @"\Lucene\SampleDataFile.txt");
            this.viewModel.IsBusy = true;
            this.luceneService.BuildIndex(sampleDataFileRows, true);
            this.viewModel.IsBusy = false;
        }


        public bool UpdateIndexFromFile(string updateFile, string indexPath)
        {
            // validate update file
            if (!System.IO.File.Exists(updateFile))
            {
                _errorText = "Невозможно обновить индекс. Файл для обновлений не найден";
                return false;
            }


            // validate search index
            if (!System.IO.Directory.EnumerateFiles(indexPath).Any())
            {
                _errorText = "Невозможно обновить индекс. Индекс еще не построен";
                return false;
            }

            
            luceneService.SetPath(indexPath);

            var docCount = luceneService.GetIndexDocCount();

            sampleDataFileRows = sampleDataFileReader.ReadAllRows(updateFile);
            List<SampleDataFileRow> updateList = new List<SampleDataFileRow>();

            foreach (var fileRow in sampleDataFileRows)
            {
                docCount++;
                fileRow.LineNumber = docCount;
                updateList.Add(fileRow);
            }

            luceneService.BuildIndex(updateList, false);

            return true;
        }

        public bool UpdateIndexByTerm(string additingTerm, string indexPath)
        {
            if (string.IsNullOrEmpty(indexPath))
            {
                _errorText = "Пустая строка additingTerm";
                return false;
            }
                

            // validate search index
            if (!System.IO.Directory.EnumerateFiles(indexPath).Any())
            {
                _errorText = "Невозможно обновить индекс. Индекс еще не построен";
                return false;
            }

            luceneService.SetPath(indexPath);

            var docCount = luceneService.GetIndexDocCount() + 1;

            List<SampleDataFileRow> updateList = new List<SampleDataFileRow>();

            SampleDataFileRow additingRow = new SampleDataFileRow();
            additingRow.LineNumber = docCount;
            additingRow.LineText = additingTerm;

            updateList.Add(additingRow);

            luceneService.BuildIndex(updateList, false);

            return true;
        }
    }
}
