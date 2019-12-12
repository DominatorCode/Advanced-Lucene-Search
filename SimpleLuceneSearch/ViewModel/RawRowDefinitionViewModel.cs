using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdvancedLuceneSearch.ViewModel;

namespace AdvancedLuceneSearch
{
    public class RawRowDefinitionViewModel : INPCBase
    {
        private int lineNumber;
        private string lineText;
        private bool isSelected;
 
        public int LineNumber
        {
            get { return lineNumber; }
            set
            {
                if (this.lineNumber != value)
                {
                    this.lineNumber = value;
                    base.NotifyChanged("LineNumber");
                }
            }
        }

        public string LineText
        {
            get { return lineText; }
            set
            {
                if (this.lineText != value)
                {
                    this.lineText = value;
                    base.NotifyChanged("LineText");
                }
            }
        }

        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (this.isSelected != value)
                {
                    this.isSelected = value;
                    base.NotifyChanged("IsSelected");
                }
            }
        }
    }
}
